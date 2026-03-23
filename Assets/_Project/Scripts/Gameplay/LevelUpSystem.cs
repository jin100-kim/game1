using System;
using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class LevelUpSystem
    {
        private static readonly WeaponUpgradeId[] AllWeaponIds =
        {
            WeaponUpgradeId.Rifle,
            WeaponUpgradeId.Smg,
            WeaponUpgradeId.SniperRifle,
            WeaponUpgradeId.Shotgun,
            WeaponUpgradeId.Katana,
            WeaponUpgradeId.BfSword,
            WeaponUpgradeId.ChainAttack,
            WeaponUpgradeId.SatelliteBeam,
            WeaponUpgradeId.RifleTurret,
            WeaponUpgradeId.Aura,
        };

        private static readonly StatUpgradeId[] AllGlobalStatIds =
        {
            StatUpgradeId.AttackPower,
            StatUpgradeId.AttackSpeed,
            StatUpgradeId.MaxHealth,
            StatUpgradeId.HealthRegen,
            StatUpgradeId.MoveSpeed,
            StatUpgradeId.AttackRange,
            StatUpgradeId.Luck,
        };

        private readonly List<LevelUpOption> _workingOptions = new(3);
        private readonly List<LevelUpOption> _candidates = new(24);
        private int _pendingChoices;
        private bool _awaitingChoice;
        private PlayerBuildRuntime _build;
        private LevelUpBalanceConfig _balanceConfig;
        private Func<WeaponUpgradeId, bool> _weaponUnlockPredicate;

        public int Level { get; private set; } = 1;
        public int CurrentExperience { get; private set; }
        public int RequiredExperience { get; private set; } = ProgressionMath.RequiredExperienceForLevel(1);
        public bool IsAwaitingChoice => _awaitingChoice;
        public bool HasPendingChoices => _pendingChoices > 0;

        public event Action<int, int, int> ExperienceChanged;
        public event Action<LevelUpOption[]> OptionsGenerated;

        public void Initialize(PlayerBuildRuntime build, LevelUpBalanceConfig balanceConfig = null, Func<WeaponUpgradeId, bool> weaponUnlockPredicate = null)
        {
            _build = build;
            _balanceConfig = balanceConfig ?? LevelUpBalanceConfig.CreateRuntimeDefault();
            _weaponUnlockPredicate = weaponUnlockPredicate ?? (_ => true);
        }

        public void AddExperience(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            CurrentExperience += amount;
            while (CurrentExperience >= RequiredExperience)
            {
                CurrentExperience -= RequiredExperience;
                Level++;
                RequiredExperience = ProgressionMath.RequiredExperienceForLevel(Level);
                _pendingChoices++;
            }

            ExperienceChanged?.Invoke(CurrentExperience, RequiredExperience, Level);
            TryOpenNextChoice();
        }

        public void ApplyOption(int optionIndex, IReadOnlyList<LevelUpOption> options)
        {
            if (!_awaitingChoice || _build == null || options == null || options.Count <= 0)
            {
                return;
            }

            optionIndex = Mathf.Clamp(optionIndex, 0, options.Count - 1);
            _build.Apply(options[optionIndex]);
            _pendingChoices = Mathf.Max(0, _pendingChoices - 1);
            _awaitingChoice = false;

            TryOpenNextChoice();
            ExperienceChanged?.Invoke(CurrentExperience, RequiredExperience, Level);
        }

        public bool RerollCurrentChoice()
        {
            if (!_awaitingChoice || _build == null)
            {
                return false;
            }

            var nextOptions = GenerateOptions(Level);
            if (nextOptions.Length <= 0)
            {
                return false;
            }

            OptionsGenerated?.Invoke(nextOptions);
            return true;
        }

        private void TryOpenNextChoice()
        {
            if (_awaitingChoice || _build == null)
            {
                return;
            }

            while (_pendingChoices > 0)
            {
                var options = GenerateOptions(Level);
                if (options.Length > 0)
                {
                    _awaitingChoice = true;
                    OptionsGenerated?.Invoke(options);
                    return;
                }

                _pendingChoices--;
            }

            _awaitingChoice = false;
        }

        private LevelUpOption[] GenerateOptions(int playerLevel)
        {
            _candidates.Clear();
            _workingOptions.Clear();

            if (_build == null)
            {
                return Array.Empty<LevelUpOption>();
            }

            for (var i = 0; i < _build.OwnedWeapons.Count; i++)
            {
                var weaponId = _build.OwnedWeapons[i];
                if (!_build.CanLevelWeapon(weaponId))
                {
                    continue;
                }

                var currentLevel = _build.GetWeaponLevel(weaponId);
                var nextLevel = currentLevel + 1;
                _candidates.Add(nextLevel == 5 || nextLevel == 10
                    ? CreateWeaponMilestoneOption(weaponId, currentLevel, nextLevel)
                    : CreateWeaponRollOption(weaponId, currentLevel, nextLevel));
            }

            for (var i = 0; i < AllWeaponIds.Length; i++)
            {
                var weaponId = AllWeaponIds[i];
                if ((_weaponUnlockPredicate?.Invoke(weaponId) ?? true) && _build.CanAcquireWeapon(weaponId, playerLevel))
                {
                    _candidates.Add(CreateWeaponAcquireOption(weaponId));
                }
            }

            for (var i = 0; i < AllGlobalStatIds.Length; i++)
            {
                _candidates.Add(CreateGlobalStatRollOption(AllGlobalStatIds[i]));
            }

            if (_candidates.Count <= 0)
            {
                return Array.Empty<LevelUpOption>();
            }

            ShuffleCandidates(_candidates);
            var optionCount = Mathf.Min(3, _candidates.Count);
            for (var i = 0; i < optionCount; i++)
            {
                _workingOptions.Add(_candidates[i]);
            }

            return _workingOptions.ToArray();
        }

        private LevelUpOption CreateWeaponAcquireOption(WeaponUpgradeId weaponId)
        {
            var title = $"New {SharedGameCatalog.GetWeaponDisplayName(weaponId)} Lv1";
            var description = "Acquire weapon";
            return LevelUpOption.CreateWeaponAcquire(weaponId, title, description, ComposeLabel(title, description, OptionRarity.Common, hideRarity: true));
        }

        private LevelUpOption CreateWeaponRollOption(WeaponUpgradeId weaponId, int currentLevel, int nextLevel)
        {
            var rarity = _balanceConfig.RollRarity(_build != null ? _build.GlobalLuckTotal : 0f);
            var rollKind = (WeaponRollKind)UnityEngine.Random.Range(0, 3);
            var value = _balanceConfig.GetWeaponRollValue(rollKind, rarity);
            var title = $"Upgrade {SharedGameCatalog.GetWeaponDisplayName(weaponId)} Lv{nextLevel}";
            var description = BuildWeaponRollDescription(rollKind, value);
            return LevelUpOption.CreateWeaponRoll(
                weaponId,
                rollKind,
                rarity,
                value,
                currentLevel,
                nextLevel,
                title,
                description,
                ComposeLabel(title, description, rarity));
        }

        private LevelUpOption CreateWeaponMilestoneOption(WeaponUpgradeId weaponId, int currentLevel, int nextLevel)
        {
            var title = $"{SharedGameCatalog.GetWeaponDisplayName(weaponId)} Lv{nextLevel}";
            var description = GetWeaponMilestoneDescription(weaponId, nextLevel);
            return LevelUpOption.CreateWeaponMilestone(
                weaponId,
                GetWeaponMilestoneKind(weaponId, nextLevel),
                GetWeaponMilestoneValue(weaponId, nextLevel),
                currentLevel,
                nextLevel,
                title,
                description,
                ComposeSpecialLabel(title, description));
        }

        private LevelUpOption CreateGlobalStatRollOption(StatUpgradeId statId)
        {
            var rarity = _balanceConfig.RollRarity(_build != null ? _build.GlobalLuckTotal : 0f);
            var value = _balanceConfig.GetGlobalRollValue(statId, rarity);
            var title = $"Global {SharedGameCatalog.GetStatDisplayName(statId)}";
            var description = BuildGlobalStatDescription(statId, value);
            return LevelUpOption.CreateGlobalStatRoll(
                statId,
                rarity,
                value,
                title,
                description,
                ComposeLabel(title, description, rarity));
        }

        private static void ShuffleCandidates(List<LevelUpOption> items)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                var swapIndex = UnityEngine.Random.Range(0, i + 1);
                (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
            }
        }

        private static string ComposeLabel(string title, string description, OptionRarity rarity, bool hideRarity = false)
        {
            return hideRarity
                ? $"{title}\n{description}"
                : $"{title}\n{GetRarityRichText(rarity)}\n{description}";
        }

        private static string ComposeSpecialLabel(string title, string description)
        {
            return $"{title}\n{GetRarityRichText(OptionRarity.Special)}\n{description}";
        }

        private static string GetRarityRichText(OptionRarity rarity)
        {
            var color = rarity switch
            {
                OptionRarity.Rare => "#66A8FF",
                OptionRarity.Epic => "#B781FF",
                OptionRarity.Legendary => "#FFB14A",
                OptionRarity.Special => "#FFD64D",
                _ => "#C8C8C8",
            };

            var text = rarity switch
            {
                OptionRarity.Rare => "Rare",
                OptionRarity.Epic => "Epic",
                OptionRarity.Legendary => "Legendary",
                OptionRarity.Special => "SPECIAL",
                _ => "Common",
            };

            return $"<color={color}>{text}</color>";
        }

        private static string BuildWeaponRollDescription(WeaponRollKind rollKind, float value)
        {
            return rollKind switch
            {
                WeaponRollKind.AttackSpeedPercent => $"Attack Speed +{value:0.#}%",
                WeaponRollKind.RangePercent => $"Range +{value:0.#}%",
                _ => $"Damage +{value:0.#}%",
            };
        }

        private static string BuildGlobalStatDescription(StatUpgradeId statId, float value)
        {
            return statId switch
            {
                StatUpgradeId.MaxHealth => $"Max Health +{value:0}",
                StatUpgradeId.HealthRegen => $"Health Regen +{value:0.##}/s",
                StatUpgradeId.Luck => $"Luck +{value:0.##}",
                StatUpgradeId.AttackSpeed => $"Attack Speed +{value:0.#}%",
                StatUpgradeId.MoveSpeed => $"Move Speed +{value:0.#}%",
                StatUpgradeId.AttackRange => $"Attack Range +{value:0.#}%",
                _ => $"Attack Power +{value:0.#}%",
            };
        }

        private static WeaponMilestoneKind GetWeaponMilestoneKind(WeaponUpgradeId weaponId, int nextLevel)
        {
            return weaponId switch
            {
                WeaponUpgradeId.Rifle => WeaponMilestoneKind.ExtraProjectile,
                WeaponUpgradeId.Smg => WeaponMilestoneKind.ExtraProjectile,
                WeaponUpgradeId.SniperRifle => WeaponMilestoneKind.ExtraProjectile,
                WeaponUpgradeId.Shotgun => WeaponMilestoneKind.ExtraPellets,
                WeaponUpgradeId.Katana => WeaponMilestoneKind.ExtraSlashes,
                WeaponUpgradeId.BfSword when nextLevel == 5 => WeaponMilestoneKind.BfSwordWidth,
                WeaponUpgradeId.BfSword => WeaponMilestoneKind.BfSwordLength,
                WeaponUpgradeId.ChainAttack => WeaponMilestoneKind.ExtraChains,
                WeaponUpgradeId.SatelliteBeam => WeaponMilestoneKind.ExtraSlashes,
                WeaponUpgradeId.RifleTurret => WeaponMilestoneKind.ExtraTurrets,
                WeaponUpgradeId.Aura => WeaponMilestoneKind.AuraRadius,
                _ => WeaponMilestoneKind.ExtraProjectile,
            };
        }

        private static float GetWeaponMilestoneValue(WeaponUpgradeId weaponId, int nextLevel)
        {
            return weaponId switch
            {
                WeaponUpgradeId.Smg => 1f,
                WeaponUpgradeId.SniperRifle => 1f,
                WeaponUpgradeId.Shotgun => 2f,
                WeaponUpgradeId.ChainAttack => 2f,
                WeaponUpgradeId.BfSword when nextLevel == 5 => 20f,
                WeaponUpgradeId.BfSword => 25f,
                WeaponUpgradeId.SatelliteBeam => 25f,
                WeaponUpgradeId.Aura => 20f,
                _ => 1f,
            };
        }

        private static string GetWeaponMilestoneDescription(WeaponUpgradeId weaponId, int nextLevel)
        {
            return weaponId switch
            {
                WeaponUpgradeId.Rifle => "Extra Projectile +1",
                WeaponUpgradeId.Smg => "Fireball +1",
                WeaponUpgradeId.SniperRifle => "Bat Count +1",
                WeaponUpgradeId.Shotgun => "Pellets +2",
                WeaponUpgradeId.Katana => "Extra Slash +1",
                WeaponUpgradeId.BfSword when nextLevel == 5 => "Blade Width +20%",
                WeaponUpgradeId.BfSword => "Blade Length +25%",
                WeaponUpgradeId.ChainAttack => "Chain Count +2",
                WeaponUpgradeId.SatelliteBeam => "Stun Power +25%",
                WeaponUpgradeId.RifleTurret => "Turret Count +1",
                WeaponUpgradeId.Aura => "Aura Radius +20%",
                _ => "Special Upgrade",
            };
        }

    }
}

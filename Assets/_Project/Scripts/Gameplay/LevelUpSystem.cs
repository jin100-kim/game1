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
            WeaponUpgradeId.Fireball,
            WeaponUpgradeId.Slash,
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

        private static readonly WeaponRollKind[] DefaultWeaponRollKinds =
        {
            WeaponRollKind.DamagePercent,
            WeaponRollKind.AttackSpeedPercent,
            WeaponRollKind.RangePercent,
        };

        private readonly List<LevelUpOption> _workingOptions = new(3);
        private readonly List<LevelUpOption> _weaponCandidates = new(24);
        private readonly List<LevelUpOption> _globalCandidates = new(12);
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
            if (amount <= 0) return;

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
            if (!_awaitingChoice || _build == null || options == null || options.Count <= 0) return;

            optionIndex = Mathf.Clamp(optionIndex, 0, options.Count - 1);
            _build.Apply(options[optionIndex]);
            _pendingChoices = Mathf.Max(0, _pendingChoices - 1);
            _awaitingChoice = false;

            TryOpenNextChoice();
            ExperienceChanged?.Invoke(CurrentExperience, RequiredExperience, Level);
        }

        public bool TryRerollCurrentChoice(out LevelUpOption[] nextOptions)
        {
            nextOptions = Array.Empty<LevelUpOption>();
            if (!_awaitingChoice || _build == null) return false;

            nextOptions = GenerateOptions(Level);
            return nextOptions.Length > 0;
        }

        private void TryOpenNextChoice()
        {
            if (_awaitingChoice || _build == null) return;

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
            _weaponCandidates.Clear();
            _globalCandidates.Clear();
            _workingOptions.Clear();

            if (_build == null) return Array.Empty<LevelUpOption>();

            for (var i = 0; i < _build.OwnedWeapons.Count; i++)
            {
                var weaponId = _build.OwnedWeapons[i];
                if (!_build.CanLevelWeapon(weaponId)) continue;

                var currentLevel = _build.GetWeaponLevel(weaponId);
                var nextLevel = currentLevel + 1;
                if (nextLevel == 5 || nextLevel == 10)
                {
                    _weaponCandidates.Add(CreateWeaponMilestoneOption(weaponId, currentLevel, nextLevel));
                }
                else
                {
                    AppendWeaponRollOptions(weaponId, currentLevel, nextLevel);
                }
            }

            for (var i = 0; i < AllWeaponIds.Length; i++)
            {
                var weaponId = AllWeaponIds[i];
                if ((_weaponUnlockPredicate?.Invoke(weaponId) ?? true) && _build.CanAcquireWeapon(weaponId, playerLevel))
                {
                    _weaponCandidates.Add(CreateWeaponAcquireOption(weaponId));
                }
            }

            for (var i = 0; i < AllGlobalStatIds.Length; i++)
            {
                _globalCandidates.Add(CreateGlobalStatRollOption(AllGlobalStatIds[i]));
            }

            var totalCandidateCount = _weaponCandidates.Count + _globalCandidates.Count;
            if (totalCandidateCount <= 0) return Array.Empty<LevelUpOption>();

            ShuffleCandidates(_weaponCandidates);
            ShuffleCandidates(_globalCandidates);

            var optionCount = Mathf.Min(3, totalCandidateCount);
            for (var slotIndex = 0; slotIndex < optionCount; slotIndex++)
            {
                var useWeaponBucket = ShouldUseWeaponBucketForSlot();
                var selected = TakeNextCandidate(useWeaponBucket ? _weaponCandidates : _globalCandidates);
                if (!selected.HasValue) selected = TakeNextCandidate(useWeaponBucket ? _globalCandidates : _weaponCandidates);

                if (selected.HasValue) _workingOptions.Add(selected.Value);
            }

            return _workingOptions.ToArray();
        }

        private LevelUpOption CreateWeaponAcquireOption(WeaponUpgradeId weaponId)
        {
            var title = $"새 무기: {SharedGameCatalog.GetWeaponDisplayName(weaponId)} 레벨 1";
            var description = "무기 획득";
            return LevelUpOption.CreateWeaponAcquire(weaponId, title, description, ComposeLabel(title, description, OptionRarity.Common, hideRarity: true));
        }

        private LevelUpOption CreateWeaponRollOption(WeaponUpgradeId weaponId, int currentLevel, int nextLevel, WeaponRollKind rollKind, OptionRarity rarity)
        {
            var value = _balanceConfig.GetWeaponRollValue(rollKind, rarity);
            var title = $"{SharedGameCatalog.GetWeaponDisplayName(weaponId)} 강화 레벨 {nextLevel}";
            var description = BuildWeaponRollDescription(rollKind, value);
            return LevelUpOption.CreateWeaponRoll(weaponId, rollKind, rarity, value, currentLevel, nextLevel, title, description, ComposeLabel(title, description, rarity));
        }

        private LevelUpOption CreateWeaponMilestoneOption(WeaponUpgradeId weaponId, int currentLevel, int nextLevel)
        {
            var title = $"{SharedGameCatalog.GetWeaponDisplayName(weaponId)} 레벨 {nextLevel}";
            var description = GetWeaponMilestoneDescription(weaponId, nextLevel);
            return LevelUpOption.CreateWeaponMilestone(weaponId, GetWeaponMilestoneKind(weaponId, nextLevel), GetWeaponMilestoneValue(weaponId, nextLevel), currentLevel, nextLevel, title, description, ComposeSpecialLabel(title, description));
        }

        private LevelUpOption CreateGlobalStatRollOption(StatUpgradeId statId)
        {
            var rarity = _balanceConfig.RollRarity(_build != null ? _build.GlobalLuckTotal : 0f);
            var value = _balanceConfig.GetGlobalRollValue(statId, rarity);
            var title = $"전역 {SharedGameCatalog.GetStatDisplayName(statId)}";
            var description = BuildGlobalStatDescription(statId, value);
            return LevelUpOption.CreateGlobalStatRoll(statId, rarity, value, title, description, ComposeLabel(title, description, rarity));
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
            return hideRarity ? $"{title}\n{description}" : $"{title}\n{GetRarityRichText(rarity)}\n{description}";
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
                OptionRarity.Rare => "희귀",
                OptionRarity.Epic => "영웅",
                OptionRarity.Legendary => "전설",
                OptionRarity.Special => "특수",
                _ => "일반",
            };
            return $"<color={color}>{text}</color>";
        }

        private static string BuildWeaponRollDescription(WeaponRollKind rollKind, float value)
        {
            return rollKind switch
            {
                WeaponRollKind.AttackSpeedPercent => $"공격 속도 +{value:0.#}%",
                WeaponRollKind.RangePercent => $"사거리 +{value:0.#}%",
                _ => $"피해량 +{value:0.#}%",
            };
        }

        private static string BuildGlobalStatDescription(StatUpgradeId statId, float value)
        {
            return statId switch
            {
                StatUpgradeId.MaxHealth => $"최대 체력 +{value:0}",
                StatUpgradeId.HealthRegen => $"체력 재생 +{value:0.##}/초",
                StatUpgradeId.Luck => $"행운 +{value:0}",
                StatUpgradeId.AttackSpeed => $"공격 속도 +{value:0.#}%",
                StatUpgradeId.MoveSpeed => $"이동 속도 +{value:0.#}%",
                StatUpgradeId.AttackRange => $"공격 범위 +{value:0.#}%",
                _ => $"피해량 +{value:0.#}%",
            };
        }

        private static WeaponMilestoneKind GetWeaponMilestoneKind(WeaponUpgradeId weaponId, int nextLevel)
        {
            return weaponId switch
            {
                WeaponUpgradeId.Fireball => WeaponMilestoneKind.ExtraProjectile,
                WeaponUpgradeId.Slash => WeaponMilestoneKind.ExtraSlashes,
                _ => WeaponMilestoneKind.ExtraProjectile,
            };
        }

        private static float GetWeaponMilestoneValue(WeaponUpgradeId weaponId, int nextLevel)
        {
            return 1f;
        }

        private static string GetWeaponMilestoneDescription(WeaponUpgradeId weaponId, int nextLevel)
        {
            return weaponId switch
            {
                WeaponUpgradeId.Fireball => "화부 +1",
                WeaponUpgradeId.Slash => "추가 베기 +1",
                _ => "특수 강화",
            };
        }

        private void AppendWeaponRollOptions(WeaponUpgradeId weaponId, int currentLevel, int nextLevel)
        {
            for (var i = 0; i < DefaultWeaponRollKinds.Length; i++)
            {
                var rarity = _balanceConfig.RollRarity(_build != null ? _build.GlobalLuckTotal : 0f);
                _weaponCandidates.Add(CreateWeaponRollOption(weaponId, currentLevel, nextLevel, DefaultWeaponRollKinds[i], rarity));
            }
        }

        private bool ShouldUseWeaponBucketForSlot()
        {
            if (_weaponCandidates.Count <= 0) return false;
            if (_globalCandidates.Count <= 0) return true;
            return UnityEngine.Random.value < 0.5f;
        }

        private static LevelUpOption? TakeNextCandidate(List<LevelUpOption> source)
        {
            if (source.Count <= 0) return null;
            var next = source[^1];
            source.RemoveAt(source.Count - 1);
            return next;
        }
    }
}

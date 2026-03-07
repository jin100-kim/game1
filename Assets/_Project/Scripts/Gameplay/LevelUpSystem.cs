using System;
using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class LevelUpSystem
    {
        private readonly struct PendingCoreChoice
        {
            public PendingCoreChoice(WeaponUpgradeId weaponId, int targetCoreLevel, WeaponCoreElement lockedElement)
            {
                WeaponId = weaponId;
                TargetCoreLevel = targetCoreLevel;
                LockedElement = lockedElement;
            }

            public WeaponUpgradeId WeaponId { get; }
            public int TargetCoreLevel { get; }
            public WeaponCoreElement LockedElement { get; }
        }

        private static readonly WeaponUpgradeId[] AllWeaponIds =
        {
            WeaponUpgradeId.Rifle,
            WeaponUpgradeId.Smg,
            WeaponUpgradeId.SniperRifle,
            WeaponUpgradeId.Shotgun,
            WeaponUpgradeId.Katana,
        };

        private static readonly StatUpgradeId[] AllStatIds =
        {
            StatUpgradeId.AttackPower,
            StatUpgradeId.AttackSpeed,
            StatUpgradeId.MaxHealth,
            StatUpgradeId.HealthRegen,
            StatUpgradeId.MoveSpeed,
            StatUpgradeId.AttackRange,
        };

        private readonly List<LevelUpOption> _workingOptions = new(3);
        private readonly List<LevelUpOption> _candidates = new(24);
        private readonly Queue<PendingCoreChoice> _pendingCoreChoices = new();
        private int _pendingChoices;
        private bool _awaitingChoice;
        private bool _awaitingCoreChoice;
        private PlayerBuildRuntime _build;

        public int Level { get; private set; } = 1;
        public int CurrentExperience { get; private set; }
        public int RequiredExperience { get; private set; } = ProgressionMath.RequiredExperienceForLevel(1);
        public bool IsAwaitingChoice => _awaitingChoice;
        public bool HasPendingChoices => _pendingChoices > 0 || _pendingCoreChoices.Count > 0;

        public event Action<int, int, int> ExperienceChanged;
        public event Action<LevelUpOption[]> OptionsGenerated;

        public void Initialize(PlayerBuildRuntime build)
        {
            _build = build;
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
            if (!_awaitingChoice || _build == null || options == null || options.Count == 0)
            {
                return;
            }

            optionIndex = Mathf.Clamp(optionIndex, 0, options.Count - 1);
            var selectedOption = options[optionIndex];
            _build.Apply(selectedOption);

            if (_awaitingCoreChoice)
            {
                if (_pendingCoreChoices.Count > 0)
                {
                    _pendingCoreChoices.Dequeue();
                }
            }
            else
            {
                _pendingChoices = Mathf.Max(0, _pendingChoices - 1);
                TryQueueCoreChoiceAfterWeaponUpgrade(selectedOption);
            }

            _awaitingChoice = false;
            _awaitingCoreChoice = false;

            TryOpenNextChoice();
            ExperienceChanged?.Invoke(CurrentExperience, RequiredExperience, Level);
        }

        private void TryOpenNextChoice()
        {
            if (_awaitingChoice || _build == null)
            {
                return;
            }

            while (true)
            {
                if (_pendingCoreChoices.Count > 0)
                {
                    var coreOptions = GenerateCoreOptions(_pendingCoreChoices.Peek());
                    if (coreOptions.Length > 0)
                    {
                        _awaitingChoice = true;
                        _awaitingCoreChoice = true;
                        OptionsGenerated?.Invoke(coreOptions);
                        return;
                    }

                    _pendingCoreChoices.Dequeue();
                    continue;
                }

                if (_pendingChoices > 0)
                {
                    var options = GenerateOptions(Level);
                    if (options.Length > 0)
                    {
                        _awaitingChoice = true;
                        _awaitingCoreChoice = false;
                        OptionsGenerated?.Invoke(options);
                        return;
                    }

                    _pendingChoices--;
                    continue;
                }

                break;
            }

            _awaitingChoice = false;
            _awaitingCoreChoice = false;
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
                var currentLevel = _build.GetWeaponLevel(weaponId);
                if (currentLevel >= PlayerBuildRuntime.MaxUpgradeLevel)
                {
                    continue;
                }

                var nextLevel = currentLevel + 1;
                _candidates.Add(new LevelUpOption(
                    UpgradeCategory.Weapon,
                    weaponId,
                    default,
                    currentLevel,
                    nextLevel,
                    isNewAcquire: false,
                    isLockedBySlot: false,
                    label: $"UP Weapon: {GetWeaponName(weaponId)} Lv{nextLevel}"));
            }

            for (var i = 0; i < _build.OwnedStats.Count; i++)
            {
                var statId = _build.OwnedStats[i];
                var currentLevel = _build.GetStatLevel(statId);
                if (currentLevel >= PlayerBuildRuntime.MaxUpgradeLevel)
                {
                    continue;
                }

                var nextLevel = currentLevel + 1;
                _candidates.Add(new LevelUpOption(
                    UpgradeCategory.Stat,
                    default,
                    statId,
                    currentLevel,
                    nextLevel,
                    isNewAcquire: false,
                    isLockedBySlot: false,
                    label: $"UP Stat: {GetStatName(statId)} Lv{nextLevel}"));
            }

            for (var i = 0; i < AllWeaponIds.Length; i++)
            {
                var weaponId = AllWeaponIds[i];
                if (_build.CanAcquireWeapon(weaponId, playerLevel))
                {
                    _candidates.Add(new LevelUpOption(
                        UpgradeCategory.Weapon,
                        weaponId,
                        default,
                        0,
                        1,
                        isNewAcquire: true,
                        isLockedBySlot: false,
                        label: $"NEW Weapon: {GetWeaponName(weaponId)} Lv1"));
                }
            }

            if (_build.OwnedStats.Count < PlayerBuildRuntime.MaxStatSlots)
            {
                for (var i = 0; i < AllStatIds.Length; i++)
                {
                    var statId = AllStatIds[i];
                    if (_build.HasStat(statId))
                    {
                        continue;
                    }

                    _candidates.Add(new LevelUpOption(
                        UpgradeCategory.Stat,
                        default,
                        statId,
                        0,
                        1,
                        isNewAcquire: true,
                        isLockedBySlot: false,
                        label: $"NEW Stat: {GetStatName(statId)} Lv1"));
                }
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

        private LevelUpOption[] GenerateCoreOptions(PendingCoreChoice coreChoice)
        {
            _workingOptions.Clear();
            if (_build == null || !_build.HasWeapon(coreChoice.WeaponId))
            {
                return Array.Empty<LevelUpOption>();
            }

            var weaponName = GetWeaponName(coreChoice.WeaponId);
            if (coreChoice.TargetCoreLevel <= 1)
            {
                _workingOptions.Add(new LevelUpOption(
                    UpgradeCategory.WeaponCore,
                    coreChoice.WeaponId,
                    default,
                    0,
                    1,
                    isNewAcquire: true,
                    isLockedBySlot: false,
                    label: $"CORE {weaponName}: Fire Lv1",
                    coreElement: WeaponCoreElement.Fire));

                _workingOptions.Add(new LevelUpOption(
                    UpgradeCategory.WeaponCore,
                    coreChoice.WeaponId,
                    default,
                    0,
                    1,
                    isNewAcquire: true,
                    isLockedBySlot: false,
                    label: $"CORE {weaponName}: Wind Lv1",
                    coreElement: WeaponCoreElement.Wind));

                _workingOptions.Add(new LevelUpOption(
                    UpgradeCategory.WeaponCore,
                    coreChoice.WeaponId,
                    default,
                    0,
                    1,
                    isNewAcquire: true,
                    isLockedBySlot: false,
                    label: $"CORE {weaponName}: Light Lv1",
                    coreElement: WeaponCoreElement.Light));
                return _workingOptions.ToArray();
            }

            var lockedElement = coreChoice.LockedElement;
            if (lockedElement == WeaponCoreElement.None)
            {
                lockedElement = _build.GetWeaponCoreElement(coreChoice.WeaponId);
            }

            if (lockedElement == WeaponCoreElement.None)
            {
                return Array.Empty<LevelUpOption>();
            }

            var currentCoreLevel = Mathf.Max(1, _build.GetWeaponCoreLevel(coreChoice.WeaponId));
            var nextCoreLevel = Mathf.Clamp(currentCoreLevel + 1, 1, PlayerBuildRuntime.MaxCoreLevel);
            _workingOptions.Add(new LevelUpOption(
                UpgradeCategory.WeaponCore,
                coreChoice.WeaponId,
                default,
                currentCoreLevel,
                nextCoreLevel,
                isNewAcquire: false,
                isLockedBySlot: false,
                label: $"CORE {weaponName}: {GetCoreName(lockedElement)} Lv{nextCoreLevel}",
                coreElement: lockedElement));

            return _workingOptions.ToArray();
        }

        private void TryQueueCoreChoiceAfterWeaponUpgrade(LevelUpOption selectedOption)
        {
            if (_build == null || selectedOption.Category != UpgradeCategory.Weapon)
            {
                return;
            }

            var weaponId = selectedOption.WeaponId;
            var weaponLevel = _build.GetWeaponLevel(weaponId);
            if (weaponLevel <= 0)
            {
                return;
            }

            if (weaponLevel == 3 && _build.CanChooseInitialCore(weaponId))
            {
                EnqueueCoreChoice(weaponId, targetCoreLevel: 1, WeaponCoreElement.None);
                return;
            }

            var coreLevel = _build.GetWeaponCoreLevel(weaponId);
            if (weaponLevel == 6 && coreLevel == 1)
            {
                EnqueueCoreChoice(weaponId, targetCoreLevel: 2, _build.GetWeaponCoreElement(weaponId));
                return;
            }

            if (weaponLevel == 10 && coreLevel == 2)
            {
                EnqueueCoreChoice(weaponId, targetCoreLevel: 3, _build.GetWeaponCoreElement(weaponId));
            }
        }

        private void EnqueueCoreChoice(WeaponUpgradeId weaponId, int targetCoreLevel, WeaponCoreElement lockedElement)
        {
            foreach (var pending in _pendingCoreChoices)
            {
                if (pending.WeaponId == weaponId && pending.TargetCoreLevel == targetCoreLevel)
                {
                    return;
                }
            }

            _pendingCoreChoices.Enqueue(new PendingCoreChoice(weaponId, targetCoreLevel, lockedElement));
        }

        private static void ShuffleCandidates(List<LevelUpOption> items)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                var swapIndex = UnityEngine.Random.Range(0, i + 1);
                (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
            }
        }

        private static string GetWeaponName(WeaponUpgradeId weaponId)
        {
            return weaponId switch
            {
                WeaponUpgradeId.Smg => "SMG",
                WeaponUpgradeId.SniperRifle => "Sniper",
                WeaponUpgradeId.Shotgun => "Shotgun",
                WeaponUpgradeId.Katana => "Katana",
                _ => "Rifle",
            };
        }

        private static string GetStatName(StatUpgradeId statId)
        {
            return statId switch
            {
                StatUpgradeId.AttackPower => "Attack Power",
                StatUpgradeId.AttackSpeed => "Attack Speed",
                StatUpgradeId.MaxHealth => "Max Health",
                StatUpgradeId.HealthRegen => "Health Regen",
                StatUpgradeId.MoveSpeed => "Move Speed",
                StatUpgradeId.AttackRange => "Attack Range",
                _ => statId.ToString(),
            };
        }

        private static string GetCoreName(WeaponCoreElement coreElement)
        {
            return coreElement switch
            {
                WeaponCoreElement.Fire => "Fire",
                WeaponCoreElement.Wind => "Wind",
                WeaponCoreElement.Light => "Light",
                _ => "Core",
            };
        }
    }
}

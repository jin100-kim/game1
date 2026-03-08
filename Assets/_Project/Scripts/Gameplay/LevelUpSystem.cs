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
            WeaponUpgradeId.ChainAttack,
            WeaponUpgradeId.SatelliteBeam,
            WeaponUpgradeId.Drone,
            WeaponUpgradeId.RifleTurret,
            WeaponUpgradeId.Aura,
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

        public bool RerollCurrentChoice()
        {
            if (!_awaitingChoice || _build == null)
            {
                return false;
            }

            LevelUpOption[] nextOptions;
            if (_awaitingCoreChoice && _pendingCoreChoices.Count > 0)
            {
                nextOptions = GenerateCoreOptions(_pendingCoreChoices.Peek());
            }
            else
            {
                nextOptions = GenerateOptions(Level);
            }

            if (nextOptions == null || nextOptions.Length == 0)
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
                if (currentLevel >= PlayerBuildRuntime.MaxWeaponLevel)
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
                    label: BuildWeaponUpgradeLabel(weaponId, currentLevel, nextLevel)));
            }

            for (var i = 0; i < _build.OwnedStats.Count; i++)
            {
                var statId = _build.OwnedStats[i];
                var currentLevel = _build.GetStatLevel(statId);
                if (currentLevel >= PlayerBuildRuntime.MaxStatLevel)
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
                    label: BuildStatUpgradeLabel(statId, currentLevel, nextLevel)));
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
                        label: BuildNewWeaponLabel(weaponId)));
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
                        label: BuildNewStatLabel(statId)));
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
                _candidates.Clear();
                _candidates.Add(new LevelUpOption(
                    UpgradeCategory.WeaponCore,
                    coreChoice.WeaponId,
                    default,
                    0,
                    1,
                    isNewAcquire: true,
                    isLockedBySlot: false,
                    label: BuildCoreLabel(weaponName, WeaponCoreElement.Fire, 0, 1),
                    coreElement: WeaponCoreElement.Fire));

                _candidates.Add(new LevelUpOption(
                    UpgradeCategory.WeaponCore,
                    coreChoice.WeaponId,
                    default,
                    0,
                    1,
                    isNewAcquire: true,
                    isLockedBySlot: false,
                    label: BuildCoreLabel(weaponName, WeaponCoreElement.Wind, 0, 1),
                    coreElement: WeaponCoreElement.Wind));

                _candidates.Add(new LevelUpOption(
                    UpgradeCategory.WeaponCore,
                    coreChoice.WeaponId,
                    default,
                    0,
                    1,
                    isNewAcquire: true,
                    isLockedBySlot: false,
                    label: BuildCoreLabel(weaponName, WeaponCoreElement.Light, 0, 1),
                    coreElement: WeaponCoreElement.Light));

                _candidates.Add(new LevelUpOption(
                    UpgradeCategory.WeaponCore,
                    coreChoice.WeaponId,
                    default,
                    0,
                    1,
                    isNewAcquire: true,
                    isLockedBySlot: false,
                    label: BuildCoreLabel(weaponName, WeaponCoreElement.Water, 0, 1),
                    coreElement: WeaponCoreElement.Water));

                ShuffleCandidates(_candidates);
                var optionCount = Mathf.Min(3, _candidates.Count);
                for (var i = 0; i < optionCount; i++)
                {
                    _workingOptions.Add(_candidates[i]);
                }

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
                label: BuildCoreLabel(weaponName, lockedElement, currentCoreLevel, nextCoreLevel),
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
                WeaponUpgradeId.Smg => "\uAE30\uAD00\uB2E8\uCD1D",
                WeaponUpgradeId.SniperRifle => "\uC800\uACA9\uC18C\uCD1D",
                WeaponUpgradeId.Shotgun => "\uC0B0\uD0C4\uCD1D",
                WeaponUpgradeId.Katana => "\uCE74\uD0C0\uB098",
                WeaponUpgradeId.ChainAttack => "\uCCB4\uC778\uC5B4\uD0DD",
                WeaponUpgradeId.SatelliteBeam => "\uC704\uC131\uBE54",
                WeaponUpgradeId.Drone => "\uB4DC\uB860",
                WeaponUpgradeId.RifleTurret => "\uB77C\uC774\uD50C\uD3EC\uD0D1",
                WeaponUpgradeId.Aura => "\uC624\uB77C",
                _ => "\uB77C\uC774\uD50C",
            };
        }

        private static string GetStatName(StatUpgradeId statId)
        {
            return statId switch
            {
                StatUpgradeId.AttackPower => "\uACF5\uACA9\uB825",
                StatUpgradeId.AttackSpeed => "\uACF5\uACA9\uC18D\uB3C4",
                StatUpgradeId.MaxHealth => "\uCD5C\uB300\uCCB4\uB825",
                StatUpgradeId.HealthRegen => "\uCCB4\uB825\uC7AC\uC0DD",
                StatUpgradeId.MoveSpeed => "\uC774\uB3D9\uC18D\uB3C4",
                StatUpgradeId.AttackRange => "\uC0AC\uAC70\uB9AC",
                _ => statId.ToString(),
            };
        }

        private static string GetCoreName(WeaponCoreElement coreElement)
        {
            return coreElement switch
            {
                WeaponCoreElement.Fire => "\uBD88",
                WeaponCoreElement.Wind => "\uBC14\uB78C",
                WeaponCoreElement.Light => "\uBE5B",
                WeaponCoreElement.Water => "\uBB3C",
                _ => "\uCF54\uC5B4",
            };
        }

                private static string BuildWeaponUpgradeLabel(WeaponUpgradeId weaponId, int currentLevel, int nextLevel)
        {
            var weaponName = GetWeaponName(weaponId);
            var currentDamage = GetWeaponDamageMultiplier(weaponId, currentLevel);
            var nextDamage = GetWeaponDamageMultiplier(weaponId, nextLevel);
            var currentCooldown = GetWeaponCooldownMultiplier(weaponId, currentLevel);
            var nextCooldown = GetWeaponCooldownMultiplier(weaponId, nextLevel);
            var currentRange = GetWeaponRangeMultiplier(weaponId, currentLevel);
            var nextRange = GetWeaponRangeMultiplier(weaponId, nextLevel);

            var header = $"강화 {weaponName} LV{nextLevel}";
            var details = new List<string>(4);

            if (!Mathf.Approximately(currentDamage, nextDamage))
            {
                var deltaDamagePercent = (nextDamage - currentDamage) * 100f;
                details.Add($"피해 {FormatSignedPercent(deltaDamagePercent)}");
            }

            if (!Mathf.Approximately(currentCooldown, nextCooldown))
            {
                var currentAttackSpeedPercent = CooldownMultiplierToAttackSpeedPercent(currentCooldown);
                var nextAttackSpeedPercent = CooldownMultiplierToAttackSpeedPercent(nextCooldown);
                var deltaAttackSpeedPercent = nextAttackSpeedPercent - currentAttackSpeedPercent;
                details.Add($"공속 {FormatSignedPercent(deltaAttackSpeedPercent)}");
            }

            if (!Mathf.Approximately(currentRange, nextRange))
            {
                var deltaRangePercent = (nextRange - currentRange) * 100f;
                details.Add($"사거리 {FormatSignedPercent(deltaRangePercent)}");
            }

            var extra = GetWeaponLevelBonusDeltaText(weaponId, currentLevel, nextLevel);
            if (!string.IsNullOrEmpty(extra))
            {
                details.Add(extra);
            }

            if (details.Count <= 0)
            {
                return header;
            }

            return $"{header}\n{string.Join(" | ", details)}";
        }
                private static string BuildNewWeaponLabel(WeaponUpgradeId weaponId)
        {
            var weaponName = GetWeaponName(weaponId);
            return $"신규 {weaponName} LV1\n피해 +0% | 공속 +0% | 사거리 +0%";
        }
                private static string BuildStatUpgradeLabel(StatUpgradeId statId, int currentLevel, int nextLevel)
        {
            var statName = GetStatName(statId);
            var detail = GetStatUpgradeDetailText(statId, currentLevel, nextLevel);
            return $"강화 {statName} LV{nextLevel}\n{detail}";
        }
                private static string BuildNewStatLabel(StatUpgradeId statId)
        {
            var statName = GetStatName(statId);
            var detail = GetStatUpgradeDetailText(statId, 0, 1);
            return $"신규 {statName} LV1\n{detail}";
        }
        private static float GetWeaponDamageMultiplier(WeaponUpgradeId weaponId, int weaponLevel)
        {
            return GetValueFromLevelCurve(GetWeaponDamageCurve(weaponId), weaponLevel, 1f);
        }

        private static float GetWeaponCooldownMultiplier(WeaponUpgradeId weaponId, int weaponLevel)
        {
            var attackSpeedBonus = GetValueFromLevelCurve(GetWeaponAttackSpeedBonusCurve(weaponId), weaponLevel, 0f);
            return 1f / (1f + Mathf.Max(0f, attackSpeedBonus));
        }

        private static float GetWeaponRangeMultiplier(WeaponUpgradeId weaponId, int weaponLevel)
        {
            return GetValueFromLevelCurve(GetWeaponRangeCurve(weaponId), weaponLevel, 1f);
        }

                private static string GetWeaponLevelBonusDeltaText(WeaponUpgradeId weaponId, int currentLevel, int nextLevel)
        {
            var currentExtra = GetWeaponExtraCount(weaponId, currentLevel);
            var nextExtra = GetWeaponExtraCount(weaponId, nextLevel);
            var delta = nextExtra - currentExtra;
            if (delta <= 0)
            {
                return string.Empty;
            }

            return weaponId switch
            {
                WeaponUpgradeId.Rifle => $"추가 탄환 +{delta}",
                WeaponUpgradeId.Smg => $"연사 수 +{delta}",
                WeaponUpgradeId.SniperRifle => $"추가 관통 +{delta}",
                WeaponUpgradeId.Shotgun => $"추가 탄환 +{delta}",
                WeaponUpgradeId.Katana => $"추가 공격 +{delta}",
                WeaponUpgradeId.ChainAttack => $"추가 연쇄 +{delta}",
                WeaponUpgradeId.SatelliteBeam => $"추가 타겟 +{delta}",
                WeaponUpgradeId.Drone => $"추가 드론 +{delta}",
                WeaponUpgradeId.RifleTurret => $"추가 포탑 +{delta}",
                _ => string.Empty,
            };
        }
                private static string GetStatUpgradeDetailText(StatUpgradeId statId, int currentLevel, int nextLevel)
        {
            switch (statId)
            {
                case StatUpgradeId.AttackPower:
                {
                    var deltaPercent = (nextLevel - currentLevel) * 10f;
                    return $"피해 {FormatSignedPercent(deltaPercent)}";
                }
                case StatUpgradeId.AttackSpeed:
                {
                    var deltaPercent = (nextLevel - currentLevel) * 5f;
                    return $"공속 {FormatSignedPercent(deltaPercent)}";
                }
                case StatUpgradeId.MaxHealth:
                {
                    var delta = (nextLevel - currentLevel) * 20;
                    return $"최대체력 +{delta:0}";
                }
                case StatUpgradeId.HealthRegen:
                {
                    var delta = (nextLevel - currentLevel) * 0.5f;
                    return $"체력재생 +{delta:0.0}/초";
                }
                case StatUpgradeId.MoveSpeed:
                {
                    var deltaPercent = (nextLevel - currentLevel) * 6f;
                    return $"이동속도 {FormatSignedPercent(deltaPercent)}";
                }
                case StatUpgradeId.AttackRange:
                {
                    var deltaPercent = (nextLevel - currentLevel) * 10f;
                    return $"사거리 {FormatSignedPercent(deltaPercent)}";
                }
                default:
                    return "수치 증가";
            }
        }
        private static float MultiplierToPercent(float multiplier)
        {
            return (multiplier - 1f) * 100f;
        }

        private static float CooldownMultiplierToAttackSpeedPercent(float cooldownMultiplier)
        {
            return ((1f / Mathf.Max(0.0001f, cooldownMultiplier)) - 1f) * 100f;
        }

        private static string FormatSignedPercent(float value)
        {
            return value >= 0f ? $"+{value:0.#}%" : $"{value:0.#}%";
        }

        private static float GetValueFromLevelCurve(float[] curve, int weaponLevel, float fallback)
        {
            if (curve == null || curve.Length <= 0)
            {
                return fallback;
            }

            var index = Mathf.Clamp(weaponLevel, 1, 10) - 1;
            return curve[index];
        }

        private static float[] GetWeaponDamageCurve(WeaponUpgradeId weaponId)
        {
            return weaponId switch
            {
                WeaponUpgradeId.Rifle => new[] { 1f, 1.15f, 1.15f, 1.15f, 1.1f, 1.35f, 1.35f, 1.35f, 1.5f, 1.5f },
                WeaponUpgradeId.Smg => new[] { 1f, 1.15f, 1.15f, 1.15f, 1.45f, 1.45f, 1.45f, 1.45f, 1.6f, 1.6f },
                WeaponUpgradeId.SniperRifle => new[] { 1f, 1.15f, 1.15f, 1.15f, 1.5f, 1.65f, 1.65f, 1.65f, 1.8f, 2f },
                WeaponUpgradeId.Shotgun => new[] { 1f, 1.15f, 1.15f, 1.15f, 1.45f, 1.45f, 1.45f, 1.45f, 1.6f, 1.6f },
                WeaponUpgradeId.Katana => new[] { 1f, 1.15f, 1.15f, 1.15f, 1.1f, 1.35f, 1.35f, 1.35f, 1.5f, 1.5f },
                WeaponUpgradeId.ChainAttack => new[] { 1f, 1.15f, 1.15f, 1.15f, 1.45f, 1.45f, 1.45f, 1.45f, 1.6f, 1.6f },
                WeaponUpgradeId.SatelliteBeam => new[] { 1f, 1.15f, 1.15f, 1.15f, 1.1f, 1.35f, 1.35f, 1.35f, 1.5f, 1.5f },
                WeaponUpgradeId.Drone => new[] { 1f, 1.15f, 1.15f, 1.15f, 1.45f, 1.45f, 1.45f, 1.45f, 1.6f, 1.6f },
                WeaponUpgradeId.RifleTurret => new[] { 1f, 1.15f, 1.15f, 1.15f, 1.45f, 1.45f, 1.45f, 1.45f, 1.6f, 1.6f },
                WeaponUpgradeId.Aura => new[] { 1f, 1.15f, 1.15f, 1.15f, 1.45f, 1.45f, 1.45f, 1.45f, 1.6f, 1.6f },
                _ => new[] { 1f, 1.15f, 1.15f, 1.15f, 1.1f, 1.35f, 1.35f, 1.35f, 1.5f, 1.5f },
            };
        }

        private static float[] GetWeaponAttackSpeedBonusCurve(WeaponUpgradeId _)
        {
            return new[] { 0f, 0f, 0.15f, 0.15f, 0.15f, 0.15f, 0.30f, 0.30f, 0.30f, 0.30f };
        }

        private static float[] GetWeaponRangeCurve(WeaponUpgradeId weaponId)
        {
            if (weaponId == WeaponUpgradeId.Aura)
            {
                return new[] { 1f, 1f, 1f, 1.15f, 1.45f, 1.45f, 1.45f, 1.6f, 1.6f, 1.9f };
            }

            return new[] { 1f, 1f, 1f, 1.15f, 1.15f, 1.15f, 1.15f, 1.3f, 1.3f, 1.3f };
        }

        private static int GetWeaponExtraCount(WeaponUpgradeId weaponId, int weaponLevel)
        {
            var curve = weaponId switch
            {
                WeaponUpgradeId.Rifle => new[] { 0, 0, 0, 0, 1, 1, 1, 1, 1, 2 },
                WeaponUpgradeId.Smg => new[] { 0, 0, 0, 0, 2, 2, 2, 2, 2, 4 },
                WeaponUpgradeId.SniperRifle => new[] { 0, 0, 0, 0, 1, 1, 1, 1, 1, 2 },
                WeaponUpgradeId.Shotgun => new[] { 0, 0, 0, 0, 2, 2, 2, 2, 2, 4 },
                WeaponUpgradeId.Katana => new[] { 0, 0, 0, 0, 1, 1, 1, 1, 1, 2 },
                WeaponUpgradeId.ChainAttack => new[] { 0, 0, 0, 0, 2, 2, 2, 2, 2, 4 },
                WeaponUpgradeId.SatelliteBeam => new[] { 0, 0, 0, 0, 1, 1, 1, 1, 1, 2 },
                WeaponUpgradeId.Drone => new[] { 0, 0, 0, 0, 1, 1, 1, 1, 1, 2 },
                WeaponUpgradeId.RifleTurret => new[] { 0, 0, 0, 0, 1, 1, 1, 1, 1, 2 },
                _ => null,
            };

            if (curve == null || curve.Length <= 0)
            {
                return 0;
            }

            var index = Mathf.Clamp(weaponLevel, 1, 10) - 1;
            return curve[index];
        }

        private static string BuildCoreLabel(string weaponName, WeaponCoreElement coreElement, int currentLevel, int nextLevel)
        {
            var coreName = GetCoreName(coreElement);
            return $"肄붿뼱 {weaponName} {coreName} LV{nextLevel}\n{GetCoreLevelDetailText(coreElement, nextLevel)}";
        }

        private static string GetCoreLevelDetailText(WeaponCoreElement coreElement, int level)
        {
            var clampedLevel = Mathf.Clamp(level, 1, PlayerBuildRuntime.MaxCoreLevel);
            switch (coreElement)
            {
                case WeaponCoreElement.Fire:
                    return clampedLevel switch
                    {
                        1 => "??컻 ?꾩쟻 10%, 5? ?곸쨷 ????컻",
                        2 => "??컻 ?꾩쟻 20%, 4? ?곸쨷 ????컻",
                        _ => "??컻 ?꾩쟻 30%, 2? ?곸쨷 ????컻",
                    };
                case WeaponCoreElement.Wind:
                    return clampedLevel switch
                    {
                        1 => "?됰갚 0.1, ?쇳빐 -12%, 怨듭냽 +25%",
                        2 => "?됰갚 0.2, ?쇳빐 -18%, 怨듭냽 +47.1%",
                        _ => "?됰갚 0.3, ?쇳빐 -24%, 怨듭냽 +72.4%",
                    };
                case WeaponCoreElement.Water:
                    return clampedLevel switch
                    {
                        1 => "?뷀솕 30%(1.0珥?, ?쇳빐 +10%",
                        2 => "?뷀솕 50%(1.0珥?, ?쇳빐 +20%",
                        _ => "?뷀솕 80%(1.0珥?, ?쇳빐 +30%",
                    };
                case WeaponCoreElement.Light:
                    return clampedLevel switch
                    {
                        1 => "異붽? ?쇳빐 10%(1.0珥?",
                        2 => "異붽? ?쇳빐 20%(2.0珥?",
                        _ => "異붽? ?쇳빐 30%(5.0珥?",
                    };
                default:
                    return "?④낵 ?놁쓬";
            }
        }

        private static string GetCoreDirectionHint(WeaponCoreElement coreElement)
        {
            return coreElement switch
            {
                WeaponCoreElement.Fire => "愿묒뿭 ?쇳빐",
                WeaponCoreElement.Water => "?뷀솕, ?쇳빐 利앷?",
                WeaponCoreElement.Wind => "?됰갚, 怨듦꺽 ?띾룄 利앷?",
                WeaponCoreElement.Light => "異붽? ?쇳빐",
                _ => "?④낵 ?놁쓬",
            };
        }
    }
}


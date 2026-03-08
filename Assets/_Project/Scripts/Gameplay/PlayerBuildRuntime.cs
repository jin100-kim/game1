using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    [System.Serializable]
    public sealed class PlayerBuildRuntime
    {
        public const int MaxWeaponSlotsAbsolute = 3;
        public const int MaxStatSlots = 5;
        public const int MaxWeaponLevel = 10;
        public const int MaxStatLevel = 5;
        public const int MaxCoreLevel = 3;
        public const int SecondWeaponUnlockLevel = 5;
        public const int ThirdWeaponUnlockLevel = 10;

        private readonly List<WeaponUpgradeId> _weaponOrder = new(3);
        private readonly List<StatUpgradeId> _statOrder = new(5);
        private readonly Dictionary<WeaponUpgradeId, int> _weaponLevels = new();
        private readonly Dictionary<StatUpgradeId, int> _statLevels = new();
        private readonly Dictionary<WeaponUpgradeId, WeaponCoreElement> _weaponCoreElements = new();
        private readonly Dictionary<WeaponUpgradeId, int> _weaponCoreLevels = new();

        public IReadOnlyList<WeaponUpgradeId> OwnedWeapons => _weaponOrder;
        public IReadOnlyList<StatUpgradeId> OwnedStats => _statOrder;

        public void InitializeDefaults(bool grantStarterRifle = true)
        {
            _weaponOrder.Clear();
            _statOrder.Clear();
            _weaponLevels.Clear();
            _statLevels.Clear();
            _weaponCoreElements.Clear();
            _weaponCoreLevels.Clear();
            if (grantStarterRifle)
            {
                _weaponOrder.Add(WeaponUpgradeId.Rifle);
                _weaponLevels[WeaponUpgradeId.Rifle] = 1;
            }
        }

        public int GetUnlockedWeaponSlots(int playerLevel)
        {
            if (playerLevel >= ThirdWeaponUnlockLevel)
            {
                return 3;
            }

            if (playerLevel >= SecondWeaponUnlockLevel)
            {
                return 2;
            }

            return 1;
        }

        public bool HasWeapon(WeaponUpgradeId id)
        {
            return _weaponLevels.ContainsKey(id);
        }

        public bool HasStat(StatUpgradeId id)
        {
            return _statLevels.ContainsKey(id);
        }

        public int GetWeaponLevel(WeaponUpgradeId id)
        {
            return _weaponLevels.TryGetValue(id, out var level) ? level : 0;
        }

        public int GetStatLevel(StatUpgradeId id)
        {
            return _statLevels.TryGetValue(id, out var level) ? level : 0;
        }

        public bool HasWeaponCore(WeaponUpgradeId id)
        {
            return _weaponCoreElements.ContainsKey(id) && GetWeaponCoreLevel(id) > 0;
        }

        public WeaponCoreElement GetWeaponCoreElement(WeaponUpgradeId id)
        {
            return _weaponCoreElements.TryGetValue(id, out var element) ? element : WeaponCoreElement.None;
        }

        public int GetWeaponCoreLevel(WeaponUpgradeId id)
        {
            return _weaponCoreLevels.TryGetValue(id, out var level) ? level : 0;
        }

        public bool CanChooseInitialCore(WeaponUpgradeId id)
        {
            var weaponLevel = GetWeaponLevel(id);
            if (weaponLevel < 3)
            {
                return false;
            }

            return !HasWeaponCore(id);
        }

        public bool CanUpgradeCore(WeaponUpgradeId id)
        {
            var coreLevel = GetWeaponCoreLevel(id);
            if (coreLevel <= 0 || coreLevel >= MaxCoreLevel)
            {
                return false;
            }

            var requiredWeaponLevel = GetRequiredWeaponLevelForCoreLevel(coreLevel + 1);
            return GetWeaponLevel(id) >= requiredWeaponLevel;
        }

        public static int GetRequiredWeaponLevelForCoreLevel(int coreLevel)
        {
            return coreLevel switch
            {
                1 => 3,
                2 => 6,
                _ => 10,
            };
        }

        public bool CanAcquireWeapon(WeaponUpgradeId id, int playerLevel)
        {
            if (HasWeapon(id))
            {
                return false;
            }

            var unlocked = Mathf.Clamp(GetUnlockedWeaponSlots(playerLevel), 1, MaxWeaponSlotsAbsolute);
            return _weaponOrder.Count < unlocked;
        }

        public bool CanAcquireStat(StatUpgradeId id)
        {
            if (HasStat(id))
            {
                return false;
            }

            return _statOrder.Count < MaxStatSlots;
        }

        public bool CanLevelWeapon(WeaponUpgradeId id)
        {
            var level = GetWeaponLevel(id);
            return level > 0 && level < MaxWeaponLevel;
        }

        public bool CanLevelStat(StatUpgradeId id)
        {
            var level = GetStatLevel(id);
            return level > 0 && level < MaxStatLevel;
        }

        public void Apply(LevelUpOption option)
        {
            switch (option.Category)
            {
                case UpgradeCategory.Weapon:
                    ApplyWeapon(option.WeaponId);
                    break;
                case UpgradeCategory.Stat:
                    ApplyStat(option.StatId);
                    break;
                case UpgradeCategory.WeaponCore:
                    ApplyWeaponCore(option.WeaponId, option.CoreElement);
                    break;
            }
        }

        private void ApplyWeapon(WeaponUpgradeId id)
        {
            if (!_weaponLevels.TryGetValue(id, out var level))
            {
                if (_weaponOrder.Count >= MaxWeaponSlotsAbsolute)
                {
                    return;
                }

                _weaponOrder.Add(id);
                _weaponLevels[id] = 1;
                return;
            }

            if (level >= MaxWeaponLevel)
            {
                return;
            }

            _weaponLevels[id] = Mathf.Clamp(level + 1, 1, MaxWeaponLevel);
        }

        private void ApplyStat(StatUpgradeId id)
        {
            if (!_statLevels.TryGetValue(id, out var level))
            {
                if (_statOrder.Count >= MaxStatSlots)
                {
                    return;
                }

                _statOrder.Add(id);
                _statLevels[id] = 1;
                return;
            }

            if (level >= MaxStatLevel)
            {
                return;
            }

            _statLevels[id] = Mathf.Clamp(level + 1, 1, MaxStatLevel);
        }

        private void ApplyWeaponCore(WeaponUpgradeId weaponId, WeaponCoreElement coreElement)
        {
            if (!HasWeapon(weaponId))
            {
                return;
            }

            var weaponLevel = GetWeaponLevel(weaponId);
            var currentCoreLevel = GetWeaponCoreLevel(weaponId);
            if (currentCoreLevel <= 0)
            {
                if (weaponLevel < GetRequiredWeaponLevelForCoreLevel(1))
                {
                    return;
                }

                if (coreElement == WeaponCoreElement.None)
                {
                    return;
                }

                _weaponCoreElements[weaponId] = coreElement;
                _weaponCoreLevels[weaponId] = 1;
                return;
            }

            if (currentCoreLevel >= MaxCoreLevel)
            {
                return;
            }

            var lockedElement = GetWeaponCoreElement(weaponId);
            if (lockedElement == WeaponCoreElement.None)
            {
                return;
            }

            var requestedElement = coreElement == WeaponCoreElement.None ? lockedElement : coreElement;
            if (requestedElement != lockedElement)
            {
                // Multi-core mixing is intentionally disabled for now.
                return;
            }

            var nextCoreLevel = currentCoreLevel + 1;
            var requiredWeaponLevel = GetRequiredWeaponLevelForCoreLevel(nextCoreLevel);
            if (weaponLevel < requiredWeaponLevel)
            {
                return;
            }

            _weaponCoreElements[weaponId] = lockedElement;
            _weaponCoreLevels[weaponId] = Mathf.Clamp(nextCoreLevel, 1, MaxCoreLevel);
        }
    }
}

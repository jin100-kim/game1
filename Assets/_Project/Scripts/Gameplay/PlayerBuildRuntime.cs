using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    [System.Serializable]
    public sealed class PlayerBuildRuntime
    {
        public const int MaxWeaponSlotsAbsolute = 3;
        public const int MaxStatSlots = 3;
        public const int MaxUpgradeLevel = 10;
        public const int SecondWeaponUnlockLevel = 10;
        public const int ThirdWeaponUnlockLevel = 20;

        private readonly List<WeaponUpgradeId> _weaponOrder = new(3);
        private readonly List<StatUpgradeId> _statOrder = new(3);
        private readonly Dictionary<WeaponUpgradeId, int> _weaponLevels = new();
        private readonly Dictionary<StatUpgradeId, int> _statLevels = new();

        public IReadOnlyList<WeaponUpgradeId> OwnedWeapons => _weaponOrder;
        public IReadOnlyList<StatUpgradeId> OwnedStats => _statOrder;

        public void InitializeDefaults()
        {
            _weaponOrder.Clear();
            _statOrder.Clear();
            _weaponLevels.Clear();
            _statLevels.Clear();
            _weaponOrder.Add(WeaponUpgradeId.Rifle);
            _weaponLevels[WeaponUpgradeId.Rifle] = 1;
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
            return level > 0 && level < MaxUpgradeLevel;
        }

        public bool CanLevelStat(StatUpgradeId id)
        {
            var level = GetStatLevel(id);
            return level > 0 && level < MaxUpgradeLevel;
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

            if (level >= MaxUpgradeLevel)
            {
                return;
            }

            _weaponLevels[id] = Mathf.Clamp(level + 1, 1, MaxUpgradeLevel);
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

            if (level >= MaxUpgradeLevel)
            {
                return;
            }

            _statLevels[id] = Mathf.Clamp(level + 1, 1, MaxUpgradeLevel);
        }
    }
}

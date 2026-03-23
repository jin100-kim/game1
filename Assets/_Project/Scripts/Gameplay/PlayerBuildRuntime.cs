using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    [System.Serializable]
    public sealed class PlayerBuildRuntime
    {
        public const int MaxWeaponSlotsAbsolute = 3;
        public const int MaxWeaponLevel = 10;
        public const int SecondWeaponUnlockLevel = 5;
        public const int ThirdWeaponUnlockLevel = 10;

        private readonly struct WeaponBonusTotals
        {
            public WeaponBonusTotals(
                float damageBonusPercent,
                float attackSpeedBonusPercent,
                float rangeBonusPercent,
                bool milestone5Taken,
                bool milestone10Taken)
            {
                DamageBonusPercent = damageBonusPercent;
                AttackSpeedBonusPercent = attackSpeedBonusPercent;
                RangeBonusPercent = rangeBonusPercent;
                Milestone5Taken = milestone5Taken;
                Milestone10Taken = milestone10Taken;
            }

            public float DamageBonusPercent { get; }
            public float AttackSpeedBonusPercent { get; }
            public float RangeBonusPercent { get; }
            public bool Milestone5Taken { get; }
            public bool Milestone10Taken { get; }
        }

        private readonly List<WeaponUpgradeId> _weaponOrder = new(3);
        private readonly Dictionary<WeaponUpgradeId, int> _weaponLevels = new();
        private readonly Dictionary<WeaponUpgradeId, WeaponBonusTotals> _weaponBonuses = new();

        private float _metaAttackPowerPercentTotal;
        private float _metaAttackSpeedPercentTotal;
        private float _metaMaxHealthFlatTotal;
        private float _metaHealthRegenPerSecondTotal;
        private float _metaMoveSpeedPercentTotal;
        private float _metaAttackRangePercentTotal;
        private float _metaLuckTotal;

        private float _runAttackPowerPercentTotal;
        private float _runAttackSpeedPercentTotal;
        private float _runMaxHealthFlatTotal;
        private float _runHealthRegenPerSecondTotal;
        private float _runMoveSpeedPercentTotal;
        private float _runAttackRangePercentTotal;
        private float _runLuckTotal;

        public IReadOnlyList<WeaponUpgradeId> OwnedWeapons => _weaponOrder;

        public float GlobalAttackPowerPercentTotal => _metaAttackPowerPercentTotal + _runAttackPowerPercentTotal;
        public float GlobalAttackSpeedPercentTotal => _metaAttackSpeedPercentTotal + _runAttackSpeedPercentTotal;
        public float GlobalMaxHealthFlatTotal => _metaMaxHealthFlatTotal + _runMaxHealthFlatTotal;
        public float GlobalHealthRegenPerSecondTotal => _metaHealthRegenPerSecondTotal + _runHealthRegenPerSecondTotal;
        public float GlobalMoveSpeedPercentTotal => _metaMoveSpeedPercentTotal + _runMoveSpeedPercentTotal;
        public float GlobalAttackRangePercentTotal => _metaAttackRangePercentTotal + _runAttackRangePercentTotal;
        public float GlobalLuckTotal => _metaLuckTotal + _runLuckTotal;

        public void InitializeDefaults(bool grantStarterRifle = true)
        {
            _weaponOrder.Clear();
            _weaponLevels.Clear();
            _weaponBonuses.Clear();

            _metaAttackPowerPercentTotal = 0f;
            _metaAttackSpeedPercentTotal = 0f;
            _metaMaxHealthFlatTotal = 0f;
            _metaHealthRegenPerSecondTotal = 0f;
            _metaMoveSpeedPercentTotal = 0f;
            _metaAttackRangePercentTotal = 0f;
            _metaLuckTotal = 0f;

            _runAttackPowerPercentTotal = 0f;
            _runAttackSpeedPercentTotal = 0f;
            _runMaxHealthFlatTotal = 0f;
            _runHealthRegenPerSecondTotal = 0f;
            _runMoveSpeedPercentTotal = 0f;
            _runAttackRangePercentTotal = 0f;
            _runLuckTotal = 0f;

            if (grantStarterRifle)
            {
                AcquireWeaponInternal(WeaponUpgradeId.Rifle);
            }
        }

        public void ApplyMetaBonuses(MetaBonusValues bonuses)
        {
            _metaAttackPowerPercentTotal = Mathf.Max(0f, bonuses.attackPowerPercent);
            _metaAttackSpeedPercentTotal = Mathf.Max(0f, bonuses.attackSpeedPercent);
            _metaMaxHealthFlatTotal = Mathf.Max(0f, bonuses.maxHealthFlat);
            _metaHealthRegenPerSecondTotal = Mathf.Max(0f, bonuses.healthRegenPerSecond);
            _metaMoveSpeedPercentTotal = Mathf.Max(0f, bonuses.moveSpeedPercent);
            _metaAttackRangePercentTotal = Mathf.Max(0f, bonuses.attackRangePercent);
            _metaLuckTotal = Mathf.Max(0f, bonuses.luck);
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

        public int GetWeaponLevel(WeaponUpgradeId id)
        {
            return _weaponLevels.TryGetValue(id, out var level) ? level : 0;
        }

        public bool CanAcquireWeapon(WeaponUpgradeId id, int playerLevel)
        {
            if (HasWeapon(id))
            {
                return false;
            }

            var unlockedSlots = Mathf.Clamp(GetUnlockedWeaponSlots(playerLevel), 1, MaxWeaponSlotsAbsolute);
            return _weaponOrder.Count < unlockedSlots;
        }

        public bool CanLevelWeapon(WeaponUpgradeId id)
        {
            var level = GetWeaponLevel(id);
            return level > 0 && level < MaxWeaponLevel;
        }

        public float GetWeaponDamageBonusPercentTotal(WeaponUpgradeId id)
        {
            return GetWeaponBonuses(id).DamageBonusPercent;
        }

        public float GetWeaponAttackSpeedBonusPercentTotal(WeaponUpgradeId id)
        {
            return GetWeaponBonuses(id).AttackSpeedBonusPercent;
        }

        public float GetWeaponRangeBonusPercentTotal(WeaponUpgradeId id)
        {
            return GetWeaponBonuses(id).RangeBonusPercent;
        }

        public bool HasWeaponMilestone5(WeaponUpgradeId id)
        {
            return GetWeaponBonuses(id).Milestone5Taken;
        }

        public bool HasWeaponMilestone10(WeaponUpgradeId id)
        {
            return GetWeaponBonuses(id).Milestone10Taken;
        }

        public int GetWeaponMilestoneCount(WeaponUpgradeId id)
        {
            var bonuses = GetWeaponBonuses(id);
            var count = 0;
            if (bonuses.Milestone5Taken)
            {
                count++;
            }

            if (bonuses.Milestone10Taken)
            {
                count++;
            }

            return count;
        }

        public int GetWeaponExtraCountBonus(WeaponUpgradeId id)
        {
            var milestones = GetWeaponMilestoneCount(id);
            return id switch
            {
                WeaponUpgradeId.Rifle => milestones,
                WeaponUpgradeId.Smg => milestones,
                WeaponUpgradeId.SniperRifle => milestones,
                WeaponUpgradeId.Shotgun => milestones * 2,
                WeaponUpgradeId.Katana => milestones,
                WeaponUpgradeId.ChainAttack => milestones * 2,
                WeaponUpgradeId.RifleTurret => milestones,
                _ => 0,
            };
        }

        public float GetBfSwordWidthMultiplier()
        {
            return 1f + (HasWeaponMilestone5(WeaponUpgradeId.BfSword) ? 0.20f : 0f);
        }

        public float GetBfSwordLengthMultiplier()
        {
            return 1f + (HasWeaponMilestone10(WeaponUpgradeId.BfSword) ? 0.25f : 0f);
        }

        public float GetAuraMilestoneRangeMultiplier()
        {
            return 1f + (GetWeaponMilestoneCount(WeaponUpgradeId.Aura) * 0.20f);
        }

        public float GetGlobalStatTotal(StatUpgradeId statId)
        {
            return statId switch
            {
                StatUpgradeId.AttackPower => GlobalAttackPowerPercentTotal,
                StatUpgradeId.AttackSpeed => GlobalAttackSpeedPercentTotal,
                StatUpgradeId.MaxHealth => GlobalMaxHealthFlatTotal,
                StatUpgradeId.HealthRegen => GlobalHealthRegenPerSecondTotal,
                StatUpgradeId.MoveSpeed => GlobalMoveSpeedPercentTotal,
                StatUpgradeId.AttackRange => GlobalAttackRangePercentTotal,
                StatUpgradeId.Luck => GlobalLuckTotal,
                _ => 0f,
            };
        }

        public void Apply(LevelUpOption option)
        {
            switch (option.Domain)
            {
                case LevelUpOptionDomain.WeaponAcquire:
                    AcquireWeaponInternal(option.WeaponId);
                    break;
                case LevelUpOptionDomain.WeaponLevelRoll:
                    ApplyWeaponLevelRoll(option);
                    break;
                case LevelUpOptionDomain.WeaponMilestone:
                    ApplyWeaponMilestone(option);
                    break;
                case LevelUpOptionDomain.GlobalStatRoll:
                    ApplyGlobalStatRoll(option);
                    break;
            }
        }

        private void AcquireWeaponInternal(WeaponUpgradeId weaponId)
        {
            if (_weaponLevels.ContainsKey(weaponId))
            {
                return;
            }

            if (_weaponOrder.Count >= MaxWeaponSlotsAbsolute)
            {
                return;
            }

            _weaponOrder.Add(weaponId);
            _weaponLevels[weaponId] = 1;
            _weaponBonuses[weaponId] = default;
        }

        private void ApplyWeaponLevelRoll(LevelUpOption option)
        {
            if (!HasWeapon(option.WeaponId))
            {
                return;
            }

            var currentLevel = GetWeaponLevel(option.WeaponId);
            if (currentLevel <= 0 || currentLevel >= MaxWeaponLevel)
            {
                return;
            }

            _weaponLevels[option.WeaponId] = Mathf.Clamp(currentLevel + 1, 1, MaxWeaponLevel);

            var bonuses = GetWeaponBonuses(option.WeaponId);
            var damageBonus = bonuses.DamageBonusPercent;
            var attackSpeedBonus = bonuses.AttackSpeedBonusPercent;
            var rangeBonus = bonuses.RangeBonusPercent;

            switch (option.WeaponRollKind)
            {
                case WeaponRollKind.AttackSpeedPercent:
                    attackSpeedBonus += option.PrimaryValue;
                    break;
                case WeaponRollKind.RangePercent:
                    rangeBonus += option.PrimaryValue;
                    break;
                default:
                    damageBonus += option.PrimaryValue;
                    break;
            }

            _weaponBonuses[option.WeaponId] = new WeaponBonusTotals(
                damageBonus,
                attackSpeedBonus,
                rangeBonus,
                bonuses.Milestone5Taken,
                bonuses.Milestone10Taken);
        }

        private void ApplyWeaponMilestone(LevelUpOption option)
        {
            if (!HasWeapon(option.WeaponId))
            {
                return;
            }

            var currentLevel = GetWeaponLevel(option.WeaponId);
            if (currentLevel <= 0 || currentLevel >= MaxWeaponLevel)
            {
                return;
            }

            var nextLevel = Mathf.Clamp(currentLevel + 1, 1, MaxWeaponLevel);
            _weaponLevels[option.WeaponId] = nextLevel;

            var bonuses = GetWeaponBonuses(option.WeaponId);
            _weaponBonuses[option.WeaponId] = new WeaponBonusTotals(
                bonuses.DamageBonusPercent,
                bonuses.AttackSpeedBonusPercent,
                bonuses.RangeBonusPercent,
                bonuses.Milestone5Taken || nextLevel == 5,
                bonuses.Milestone10Taken || nextLevel == 10);
        }

        private void ApplyGlobalStatRoll(LevelUpOption option)
        {
            switch (option.StatId)
            {
                case StatUpgradeId.AttackPower:
                    _runAttackPowerPercentTotal += option.PrimaryValue;
                    break;
                case StatUpgradeId.AttackSpeed:
                    _runAttackSpeedPercentTotal += option.PrimaryValue;
                    break;
                case StatUpgradeId.MaxHealth:
                    _runMaxHealthFlatTotal += option.PrimaryValue;
                    break;
                case StatUpgradeId.HealthRegen:
                    _runHealthRegenPerSecondTotal += option.PrimaryValue;
                    break;
                case StatUpgradeId.MoveSpeed:
                    _runMoveSpeedPercentTotal += option.PrimaryValue;
                    break;
                case StatUpgradeId.AttackRange:
                    _runAttackRangePercentTotal += option.PrimaryValue;
                    break;
                case StatUpgradeId.Luck:
                    _runLuckTotal += option.PrimaryValue;
                    break;
            }
        }

        private WeaponBonusTotals GetWeaponBonuses(WeaponUpgradeId id)
        {
            return _weaponBonuses.TryGetValue(id, out var bonuses)
                ? bonuses
                : default;
        }
    }
}

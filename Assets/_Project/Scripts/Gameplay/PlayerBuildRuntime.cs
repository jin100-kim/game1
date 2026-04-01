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
        private readonly HashSet<RunAugmentId> _runAugments = new();

        private MetaBonusValues _metaBonuses;
        private MetaBonusValues _characterBaseBonuses;
        private MetaBonusValues _characterDynamicBonuses;
        private MetaBonusValues _runBonuses;
        private bool _chainAttackIgnoresDecay;
        private int _chainAttackBonusJumps;

        public IReadOnlyList<WeaponUpgradeId> OwnedWeapons => _weaponOrder;
        public IReadOnlyCollection<RunAugmentId> ActiveAugments => _runAugments;

        public float GlobalAttackPowerPercentTotal => _metaBonuses.attackPowerPercent + _characterBaseBonuses.attackPowerPercent + _characterDynamicBonuses.attackPowerPercent + _runBonuses.attackPowerPercent;
        public float GlobalAttackSpeedPercentTotal => _metaBonuses.attackSpeedPercent + _characterBaseBonuses.attackSpeedPercent + _characterDynamicBonuses.attackSpeedPercent + _runBonuses.attackSpeedPercent;
        public float GlobalMaxHealthFlatTotal => _metaBonuses.maxHealthFlat + _characterBaseBonuses.maxHealthFlat + _characterDynamicBonuses.maxHealthFlat + _runBonuses.maxHealthFlat;
        public float GlobalHealthRegenPerSecondTotal => _metaBonuses.healthRegenPerSecond + _characterBaseBonuses.healthRegenPerSecond + _characterDynamicBonuses.healthRegenPerSecond + _runBonuses.healthRegenPerSecond;
        public float GlobalMoveSpeedPercentTotal => _metaBonuses.moveSpeedPercent + _characterBaseBonuses.moveSpeedPercent + _characterDynamicBonuses.moveSpeedPercent + _runBonuses.moveSpeedPercent;
        public float GlobalAttackRangePercentTotal => _metaBonuses.attackRangePercent + _characterBaseBonuses.attackRangePercent + _characterDynamicBonuses.attackRangePercent + _runBonuses.attackRangePercent;
        public float GlobalLuckTotal => _metaBonuses.luck + _characterBaseBonuses.luck + _characterDynamicBonuses.luck + _runBonuses.luck;
        public float GlobalExperienceGainPercentTotal => _metaBonuses.experienceGainPercent + _characterBaseBonuses.experienceGainPercent + _characterDynamicBonuses.experienceGainPercent + _runBonuses.experienceGainPercent;
        public float GlobalCreditGainPercentTotal => _metaBonuses.creditGainPercent + _characterBaseBonuses.creditGainPercent + _characterDynamicBonuses.creditGainPercent + _runBonuses.creditGainPercent;

        public void InitializeDefaults(bool grantStarterRifle = true)
        {
            _weaponOrder.Clear();
            _weaponLevels.Clear();
            _weaponBonuses.Clear();
            _runAugments.Clear();
            _metaBonuses = default;
            _characterBaseBonuses = default;
            _characterDynamicBonuses = default;
            _runBonuses = default;
            _chainAttackIgnoresDecay = false;
            _chainAttackBonusJumps = 0;

            if (grantStarterRifle)
            {
                AcquireWeaponInternal(WeaponUpgradeId.Rifle);
            }
        }

        public void ApplyMetaBonuses(MetaBonusValues bonuses)
        {
            _metaBonuses = SanitizeBonuses(bonuses);
        }

        public void ApplyCharacterBaseBonuses(MetaBonusValues bonuses)
        {
            _characterBaseBonuses = SanitizeBonuses(bonuses);
        }

        public void ApplyCharacterDynamicBonuses(MetaBonusValues bonuses)
        {
            _characterDynamicBonuses = SanitizeBonuses(bonuses);
        }

        public void SetChainAttackModifiers(bool ignoreDecay, int bonusJumps)
        {
            _chainAttackIgnoresDecay = ignoreDecay;
            _chainAttackBonusJumps = Mathf.Max(0, bonusJumps);
        }

        public void AddRuntimeMaxHealthFlat(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _runBonuses.maxHealthFlat += amount;
        }

        public bool DoesChainAttackIgnoreDecay()
        {
            return _chainAttackIgnoresDecay;
        }

        public int GetChainAttackBonusJumps()
        {
            return _chainAttackBonusJumps;
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
                WeaponUpgradeId.ChainAttack => (milestones * 2) + _chainAttackBonusJumps,
                WeaponUpgradeId.RifleTurret => milestones,
                _ => 0,
            };
        }

        public float GetBfSwordWidthMultiplier()
        {
            return 1f;
        }

        public float GetBfSwordLengthMultiplier()
        {
            return 1f;
        }

        public int GetBfSwordAfterimageCount()
        {
            return GetWeaponMilestoneCount(WeaponUpgradeId.BfSword);
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
                case LevelUpOptionDomain.Augment:
                    ApplyAugment(option);
                    break;
            }
        }

        public bool HasAugment(RunAugmentId augmentId)
        {
            return _runAugments.Contains(augmentId);
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
                    _runBonuses.attackPowerPercent += option.PrimaryValue;
                    break;
                case StatUpgradeId.AttackSpeed:
                    _runBonuses.attackSpeedPercent += option.PrimaryValue;
                    break;
                case StatUpgradeId.MaxHealth:
                    _runBonuses.maxHealthFlat += option.PrimaryValue;
                    break;
                case StatUpgradeId.HealthRegen:
                    _runBonuses.healthRegenPerSecond += option.PrimaryValue;
                    break;
                case StatUpgradeId.MoveSpeed:
                    _runBonuses.moveSpeedPercent += option.PrimaryValue;
                    break;
                case StatUpgradeId.AttackRange:
                    _runBonuses.attackRangePercent += option.PrimaryValue;
                    break;
                case StatUpgradeId.Luck:
                    _runBonuses.luck += option.PrimaryValue;
                    break;
            }
        }

        private void ApplyAugment(LevelUpOption option)
        {
            var augmentId = SharedAugmentCatalog.NormalizeAugmentId(option.AugmentId);
            if (_runAugments.Contains(augmentId))
            {
                return;
            }

            var definition = SharedAugmentCatalog.GetDefinition(augmentId);
            _runAugments.Add(definition.Id);
            _runBonuses += definition.Bonuses;
        }

        private WeaponBonusTotals GetWeaponBonuses(WeaponUpgradeId id)
        {
            return _weaponBonuses.TryGetValue(id, out var bonuses)
                ? bonuses
                : default;
        }

        private static MetaBonusValues SanitizeBonuses(MetaBonusValues bonuses)
        {
            bonuses.attackPowerPercent = Mathf.Max(0f, bonuses.attackPowerPercent);
            bonuses.attackSpeedPercent = Mathf.Max(0f, bonuses.attackSpeedPercent);
            bonuses.maxHealthFlat = Mathf.Max(0f, bonuses.maxHealthFlat);
            bonuses.healthRegenPerSecond = Mathf.Max(0f, bonuses.healthRegenPerSecond);
            bonuses.moveSpeedPercent = Mathf.Max(0f, bonuses.moveSpeedPercent);
            bonuses.attackRangePercent = Mathf.Max(0f, bonuses.attackRangePercent);
            bonuses.luck = Mathf.Max(0f, bonuses.luck);
            bonuses.experienceGainPercent = Mathf.Max(0f, bonuses.experienceGainPercent);
            bonuses.creditGainPercent = Mathf.Max(0f, bonuses.creditGainPercent);
            return bonuses;
        }
    }
}

using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    [System.Serializable]
    public sealed class PlayerBuildRuntime
    {
        public const int MaxWeaponSlotsAbsolute = 4;
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

        private readonly List<WeaponUpgradeId> _weaponOrder = new(MaxWeaponSlotsAbsolute);
        private readonly Dictionary<WeaponUpgradeId, int> _weaponLevels = new();
        private readonly Dictionary<WeaponUpgradeId, WeaponBonusTotals> _weaponBonuses = new();
        private readonly HashSet<RunAugmentId> _runAugments = new();

        private MetaBonusValues _metaBonuses;
        private MetaBonusValues _characterBaseBonuses;
        private MetaBonusValues _characterDynamicBonuses;
        private MetaBonusValues _runBonuses;
        private int _extraWeaponSlots;
        private float _maxHealthScale = 1f;
        private float _damageTakenScale = 1f;
        private bool _suppressPassiveRegen;
        private int _lifestealHealPerHit;
        private float _lifestealDamageRatio;
        private float _lifestealMaxHealPerHit;
        private float _lifestealBossMultiplier = 1f;
        private float _lifestealInternalCooldown;
        private float _lowHealthDamagePercentMax;
        private float _lowHealthMoveSpeedPercentMax;
        private float _lowHealthMaxThreshold;
        private float _lowEnemyHealthDamagePercent;
        private float _lowEnemyHealthThreshold;
        private bool _chainLightningIgnoresDecay;
        private int _chainLightningBonusJumps;
        private bool _hasCharacterWeaponBonuses;
        private WeaponUpgradeId _characterBonusWeaponId;
        private float _characterWeaponDamageBonusPercent;
        private float _characterWeaponAttackSpeedBonusPercent;
        private float _characterWeaponRangeBonusPercent;

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
        public int ExtraWeaponSlots => _extraWeaponSlots;
        public float GlobalMaxHealthScale => _maxHealthScale;
        public float GlobalDamageTakenScale => _damageTakenScale;
        public bool SuppressesPassiveRegen => _suppressPassiveRegen;
        public int LifestealHealPerHit => _lifestealHealPerHit;
        public float LifestealDamageRatio => _lifestealDamageRatio;
        public float LifestealMaxHealPerHit => _lifestealMaxHealPerHit;
        public float LifestealBossMultiplier => _lifestealBossMultiplier;
        public float LifestealInternalCooldown => _lifestealInternalCooldown;
        public bool HasLowHealthBonuses => _lowHealthDamagePercentMax > 0f || _lowHealthMoveSpeedPercentMax > 0f;

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
            _extraWeaponSlots = 0;
            _maxHealthScale = 1f;
            _damageTakenScale = 1f;
            _suppressPassiveRegen = false;
            _lifestealHealPerHit = 0;
            _lifestealDamageRatio = 0f;
            _lifestealMaxHealPerHit = 0f;
            _lifestealBossMultiplier = 1f;
            _lifestealInternalCooldown = 0f;
            _lowHealthDamagePercentMax = 0f;
            _lowHealthMoveSpeedPercentMax = 0f;
            _lowHealthMaxThreshold = 0f;
            _lowEnemyHealthDamagePercent = 0f;
            _lowEnemyHealthThreshold = 0f;
            _chainLightningIgnoresDecay = false;
            _chainLightningBonusJumps = 0;
            _hasCharacterWeaponBonuses = false;
            _characterBonusWeaponId = WeaponUpgradeId.Fireball;
            _characterWeaponDamageBonusPercent = 0f;
            _characterWeaponAttackSpeedBonusPercent = 0f;
            _characterWeaponRangeBonusPercent = 0f;

            if (grantStarterRifle)
            {
                AcquireWeaponInternal(WeaponUpgradeId.Fireball);
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
            _chainLightningIgnoresDecay = ignoreDecay;
            _chainLightningBonusJumps = Mathf.Max(0, bonusJumps);
        }

        public void ClearCharacterWeaponBonuses()
        {
            _hasCharacterWeaponBonuses = false;
            _characterBonusWeaponId = WeaponUpgradeId.Fireball;
            _characterWeaponDamageBonusPercent = 0f;
            _characterWeaponAttackSpeedBonusPercent = 0f;
            _characterWeaponRangeBonusPercent = 0f;
        }

        public void ApplyCharacterWeaponBonuses(
            WeaponUpgradeId weaponId,
            float damageBonusPercent,
            float attackSpeedBonusPercent,
            float rangeBonusPercent)
        {
            _hasCharacterWeaponBonuses = true;
            _characterBonusWeaponId = weaponId;
            _characterWeaponDamageBonusPercent = Mathf.Max(0f, damageBonusPercent);
            _characterWeaponAttackSpeedBonusPercent = attackSpeedBonusPercent;
            _characterWeaponRangeBonusPercent = rangeBonusPercent;
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
            return _chainLightningIgnoresDecay;
        }

        public int GetChainAttackBonusJumps()
        {
            return _chainLightningBonusJumps;
        }

        public int GetUnlockedWeaponSlots(int playerLevel)
        {
            var baseUnlockedSlots = 1;
            if (playerLevel >= ThirdWeaponUnlockLevel)
            {
                baseUnlockedSlots = 3;
            }
            else if (playerLevel >= SecondWeaponUnlockLevel)
            {
                baseUnlockedSlots = 2;
            }

            return Mathf.Clamp(baseUnlockedSlots + _extraWeaponSlots, 1, MaxWeaponSlotsAbsolute);
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
            var total = GetWeaponBonuses(id).DamageBonusPercent;
            if (_hasCharacterWeaponBonuses && id == _characterBonusWeaponId)
            {
                total += _characterWeaponDamageBonusPercent;
            }

            return total;
        }

        public float GetWeaponAttackSpeedBonusPercentTotal(WeaponUpgradeId id)
        {
            var total = GetWeaponBonuses(id).AttackSpeedBonusPercent;
            if (_hasCharacterWeaponBonuses && id == _characterBonusWeaponId)
            {
                total += _characterWeaponAttackSpeedBonusPercent;
            }

            return total;
        }

        public float GetWeaponRangeBonusPercentTotal(WeaponUpgradeId id)
        {
            var total = GetWeaponBonuses(id).RangeBonusPercent;
            if (_hasCharacterWeaponBonuses && id == _characterBonusWeaponId)
            {
                total += _characterWeaponRangeBonusPercent;
            }

            return total;
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
                WeaponUpgradeId.Fireball => milestones,
                WeaponUpgradeId.Slash => milestones,
                WeaponUpgradeId.LightningBolt => milestones,
                WeaponUpgradeId.IceSpike => milestones, // 0->0, 1->1, 2->2 (will use as factor for fragments)
                WeaponUpgradeId.WindBlade => milestones + (milestones >= 2 ? 1 : 0), // 5lv: +1, 10lv: +3 (total pierce bonus)
                WeaponUpgradeId.ChaosBurst => milestones, // 5lv: +1, 10lv: +2
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

        public MetaBonusValues GetLowHealthDynamicBonuses(float healthRatio)
        {
            if (!HasLowHealthBonuses)
            {
                return default;
            }

            var threshold = Mathf.Clamp(_lowHealthMaxThreshold, 0.01f, 1f);
            var clampedRatio = Mathf.Clamp01(healthRatio);
            var normalized = Mathf.InverseLerp(1f, threshold, clampedRatio);
            return new MetaBonusValues
            {
                attackPowerPercent = _lowHealthDamagePercentMax * normalized,
                moveSpeedPercent = _lowHealthMoveSpeedPercentMax * normalized,
            };
        }

        public float GetContextualDamageMultiplier(EnemyController enemy, Vector3 attackerPosition)
        {
            if (enemy == null)
            {
                return 1f;
            }

            var bonusPercent = 0f;

            if (_lowEnemyHealthDamagePercent > 0f && _lowEnemyHealthThreshold > 0f && enemy.MaxHealth > 0.0001f)
            {
                var enemyHealthRatio = enemy.CurrentHealth / enemy.MaxHealth;
                if (enemyHealthRatio <= _lowEnemyHealthThreshold)
                {
                    bonusPercent += _lowEnemyHealthDamagePercent;
                }
            }

            return 1f + (Mathf.Max(0f, bonusPercent) / 100f);
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
            _extraWeaponSlots += definition.ExtraWeaponSlots;
            _maxHealthScale *= definition.MaxHealthScale;
            _damageTakenScale *= definition.DamageTakenScale;
            _suppressPassiveRegen |= definition.SuppressPassiveRegen;
            _lifestealHealPerHit += definition.LifestealHealPerHit;
            _lifestealDamageRatio += definition.LifestealDamageRatio;
            _lifestealMaxHealPerHit = Mathf.Max(_lifestealMaxHealPerHit, definition.LifestealMaxHealPerHit);
            _lifestealBossMultiplier = Mathf.Min(_lifestealBossMultiplier, definition.LifestealBossMultiplier);
            _lifestealInternalCooldown = Mathf.Max(_lifestealInternalCooldown, definition.LifestealInternalCooldown);
            _lowHealthDamagePercentMax += definition.LowHealthDamagePercentMax;
            _lowHealthMoveSpeedPercentMax += definition.LowHealthMoveSpeedPercentMax;
            _lowHealthMaxThreshold = Mathf.Max(_lowHealthMaxThreshold, definition.LowHealthMaxThreshold);
            _lowEnemyHealthDamagePercent += definition.LowEnemyHealthDamagePercent;
            _lowEnemyHealthThreshold = Mathf.Max(_lowEnemyHealthThreshold, definition.LowEnemyHealthThreshold);
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
            bonuses.luck = Mathf.Max(0f, bonuses.luck);
            bonuses.experienceGainPercent = Mathf.Max(0f, bonuses.experienceGainPercent);
            bonuses.creditGainPercent = Mathf.Max(0f, bonuses.creditGainPercent);
            return bonuses;
        }
    }
}

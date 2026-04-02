using System;
using System.Collections.Generic;

namespace EJR.Game.Core
{
    public enum CharacterPassiveId
    {
        None = 0,
        ArcherLevelAttackSpeed = 1,
        VampireMaxHealthDamage = 2,
        SwordsmanLevelMoveSpeed = 3,
        TaoistLevelDamage = 4,
        ExorcistLevelRange = 5,
        ThunderMageChainMastery = 6,
    }

    public enum MetaUpgradeId
    {
        DamagePercent = 0,
        AttackSpeedPercent = 1,
        MaxHealthFlat = 2,
        HealthRegenPerSecond = 3,
        MoveSpeedPercent = 4,
        RangePercent = 5,
        Luck = 6,
        ExperienceGainPercent = 7,
        CreditGainPercent = 8,
    }

    [Serializable]
    public sealed class MetaUpgradeProgressEntry
    {
        public int id;
        public int level;
    }

    [Serializable]
    public sealed class RunWeaponDamageEntry
    {
        public int weaponId;
        public float damage;
    }

    [Serializable]
    public sealed class RunCombatStats
    {
        public float totalDamageDealt;
        public float damageTaken;
        public float healingDone;
        public List<RunWeaponDamageEntry> weaponDamages = new();
    }

    [Serializable]
    public sealed class RunCreditBreakdown
    {
        public string mapId = string.Empty;
        public int killCredits;
        public int timeCredits;
        public int bossDamageCredits;
        public int firstClearCredits;
        public int repeatCreditsBase;
        public int creditBonusPercent;
        public int creditBonusApplied;
        public int bossThresholdsReached;
        public int totalCredits;
    }
}

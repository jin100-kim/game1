namespace EJR.Game.Core
{
    public enum WeaponUpgradeId
    {
        Rifle = 0,
        Fireball = 1,
        Bat = 2,
        Shotgun = 3,
        Slash = 4,
        BfSword = 5,
        ChainLightning = 6,
        SwingMace = 7,
        OrbitWeapon = 8,
        Turret = 9,
        Aura = 10,
        LightningBolt = 11,
        IceSpike = 12,
        WindBlade = 13,
    }

    public enum StatUpgradeId
    {
        AttackPower = 0,
        AttackSpeed = 1,
        MaxHealth = 2,
        HealthRegen = 3,
        MoveSpeed = 4,
        AttackRange = 5,
        Luck = 6,
    }

    public enum LevelUpOptionDomain
    {
        WeaponAcquire = 0,
        WeaponLevelRoll = 1,
        WeaponMilestone = 2,
        GlobalStatRoll = 3,
        Augment = 4,
    }

    public enum OptionRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3,
        Special = 4,
    }

    public enum WeaponRollKind
    {
        DamagePercent = 0,
        AttackSpeedPercent = 1,
        RangePercent = 2,
    }

    public enum WeaponMilestoneKind
    {
        ExtraProjectile = 0,
        ExtraBurstShots = 1,
        ExtraPierce = 2,
        ExtraPellets = 3,
        ExtraSlashes = 4,
        BfSwordWidth = 5,
        ExtraChains = 6,
        ExtraTargets = 7,
        ExtraDrones = 8,
        ExtraTurrets = 9,
        AuraRadius = 10,
        BfSwordLength = 11,
    }

    public readonly struct LevelUpOption
    {
        public LevelUpOption(
            LevelUpOptionDomain domain,
            OptionRarity rarity,
            WeaponUpgradeId weaponId,
            StatUpgradeId statId,
            RunAugmentId augmentId,
            WeaponRollKind weaponRollKind,
            WeaponMilestoneKind milestoneKind,
            float primaryValue,
            float secondaryValue,
            int currentLevel,
            int nextLevel,
            bool isNewAcquire,
            bool isSpecialMilestone,
            string title,
            string description,
            string label)
        {
            Domain = domain;
            Rarity = rarity;
            WeaponId = weaponId;
            StatId = statId;
            AugmentId = augmentId;
            WeaponRollKind = weaponRollKind;
            MilestoneKind = milestoneKind;
            PrimaryValue = primaryValue;
            SecondaryValue = secondaryValue;
            CurrentLevel = currentLevel;
            NextLevel = nextLevel;
            IsNewAcquire = isNewAcquire;
            IsSpecialMilestone = isSpecialMilestone;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Label = label ?? string.Empty;
        }

        public LevelUpOptionDomain Domain { get; }
        public OptionRarity Rarity { get; }
        public WeaponUpgradeId WeaponId { get; }
        public StatUpgradeId StatId { get; }
        public RunAugmentId AugmentId { get; }
        public WeaponRollKind WeaponRollKind { get; }
        public WeaponMilestoneKind MilestoneKind { get; }
        public float PrimaryValue { get; }
        public float SecondaryValue { get; }
        public int CurrentLevel { get; }
        public int NextLevel { get; }
        public bool IsNewAcquire { get; }
        public bool IsSpecialMilestone { get; }
        public string Title { get; }
        public string Description { get; }
        public string Label { get; }

        public static LevelUpOption CreateWeaponAcquire(
            WeaponUpgradeId weaponId,
            string title,
            string description,
            string label)
        {
            return new LevelUpOption(
                LevelUpOptionDomain.WeaponAcquire,
                OptionRarity.Common,
                weaponId,
                default,
                default,
                default,
                default,
                0f,
                0f,
                0,
                1,
                isNewAcquire: true,
                isSpecialMilestone: false,
                title,
                description,
                label);
        }

        public static LevelUpOption CreateWeaponRoll(
            WeaponUpgradeId weaponId,
            WeaponRollKind rollKind,
            OptionRarity rarity,
            float primaryValue,
            int currentLevel,
            int nextLevel,
            string title,
            string description,
            string label)
        {
            return new LevelUpOption(
                LevelUpOptionDomain.WeaponLevelRoll,
                rarity,
                weaponId,
                default,
                default,
                rollKind,
                default,
                primaryValue,
                0f,
                currentLevel,
                nextLevel,
                isNewAcquire: false,
                isSpecialMilestone: false,
                title,
                description,
                label);
        }

        public static LevelUpOption CreateWeaponMilestone(
            WeaponUpgradeId weaponId,
            WeaponMilestoneKind milestoneKind,
            float primaryValue,
            int currentLevel,
            int nextLevel,
            string title,
            string description,
            string label)
        {
            return new LevelUpOption(
                LevelUpOptionDomain.WeaponMilestone,
                OptionRarity.Special,
                weaponId,
                default,
                default,
                default,
                milestoneKind,
                primaryValue,
                0f,
                currentLevel,
                nextLevel,
                isNewAcquire: false,
                isSpecialMilestone: true,
                title,
                description,
                label);
        }

        public static LevelUpOption CreateGlobalStatRoll(
            StatUpgradeId statId,
            OptionRarity rarity,
            float primaryValue,
            string title,
            string description,
            string label)
        {
            return new LevelUpOption(
                LevelUpOptionDomain.GlobalStatRoll,
                rarity,
                default,
                statId,
                default,
                default,
                default,
                primaryValue,
                0f,
                0,
                0,
                isNewAcquire: false,
                isSpecialMilestone: false,
                title,
                description,
                label);
        }

        public static LevelUpOption CreateAugment(
            RunAugmentId augmentId,
            string title,
            string description,
            string label)
        {
            return new LevelUpOption(
                LevelUpOptionDomain.Augment,
                OptionRarity.Special,
                default,
                default,
                augmentId,
                default,
                default,
                0f,
                0f,
                0,
                0,
                isNewAcquire: false,
                isSpecialMilestone: true,
                title,
                description,
                label);
        }
    }
}

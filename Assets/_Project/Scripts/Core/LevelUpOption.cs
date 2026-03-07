namespace EJR.Game.Core
{
    public enum UpgradeCategory
    {
        Weapon = 0,
        Stat = 1,
        WeaponCore = 2,
    }

    public enum WeaponUpgradeId
    {
        Rifle = 0,
        Smg = 1,
        SniperRifle = 2,
        Shotgun = 3,
        Katana = 4,
    }

    public enum StatUpgradeId
    {
        AttackPower = 0,
        AttackSpeed = 1,
        MaxHealth = 2,
        HealthRegen = 3,
        MoveSpeed = 4,
        AttackRange = 5,
    }

    public enum WeaponCoreElement
    {
        None = 0,
        Fire = 1,
        Wind = 2,
        Light = 3,
    }

    public readonly struct LevelUpOption
    {
        public LevelUpOption(
            UpgradeCategory category,
            WeaponUpgradeId weaponId,
            StatUpgradeId statId,
            int currentLevel,
            int nextLevel,
            bool isNewAcquire,
            bool isLockedBySlot,
            string label,
            WeaponCoreElement coreElement = WeaponCoreElement.None)
        {
            Category = category;
            WeaponId = weaponId;
            StatId = statId;
            CurrentLevel = currentLevel;
            NextLevel = nextLevel;
            IsNewAcquire = isNewAcquire;
            IsLockedBySlot = isLockedBySlot;
            Label = label;
            CoreElement = coreElement;
        }

        public UpgradeCategory Category { get; }
        public WeaponUpgradeId WeaponId { get; }
        public StatUpgradeId StatId { get; }
        public int CurrentLevel { get; }
        public int NextLevel { get; }
        public bool IsNewAcquire { get; }
        public bool IsLockedBySlot { get; }
        public string Label { get; }
        public WeaponCoreElement CoreElement { get; }
    }
}

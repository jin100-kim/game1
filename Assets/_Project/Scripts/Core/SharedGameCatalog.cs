using UnityEngine;

namespace EJR.Game.Core
{
    public readonly struct SharedCharacterDefinition
    {
        public SharedCharacterDefinition(
            int id,
            string displayName,
            Color color,
            int unlockCost,
            bool defaultUnlocked,
            CharacterUnlockSource unlockSource,
            WeaponUpgradeId starterWeaponId,
            MetaBonusValues baseBonuses,
            CharacterPassiveId passiveId,
            string passiveDescription,
            string requiredAchievementId = null)
        {
            Id = id;
            DisplayName = displayName;
            Color = color;
            UnlockCost = unlockCost;
            DefaultUnlocked = defaultUnlocked;
            UnlockSource = unlockSource;
            StarterWeaponId = starterWeaponId;
            BaseBonuses = baseBonuses;
            PassiveId = passiveId;
            PassiveDescription = passiveDescription ?? string.Empty;
            RequiredAchievementId = requiredAchievementId ?? string.Empty;
        }

        public int Id { get; }
        public string DisplayName { get; }
        public Color Color { get; }
        public int UnlockCost { get; }
        public bool DefaultUnlocked { get; }
        public CharacterUnlockSource UnlockSource { get; }
        public WeaponUpgradeId StarterWeaponId { get; }
        public MetaBonusValues BaseBonuses { get; }
        public MetaBonusValues TraitBonuses => BaseBonuses;
        public CharacterPassiveId PassiveId { get; }
        public string PassiveDescription { get; }
        public string RequiredAchievementId { get; }
    }

    public readonly struct SharedWeaponDefinition
    {
        public SharedWeaponDefinition(WeaponUpgradeId id, string displayName, bool isSelectable = true)
        {
            Id = id;
            DisplayName = displayName;
            IsSelectable = isSelectable;
        }

        public WeaponUpgradeId Id { get; }
        public string DisplayName { get; }
        public bool IsSelectable { get; }
        public int UnlockCost => 0;
        public bool DefaultUnlocked => true;
    }

    public static class SharedGameCatalog
    {
        private static readonly SharedCharacterDefinition[] Characters =
        {
            new(
                0,
                "연발 투사체 캐릭터",
                new Color(0.92f, 0.92f, 0.82f, 1f),
                0,
                true,
                CharacterUnlockSource.Default,
                WeaponUpgradeId.Rifle,
                new MetaBonusValues { attackSpeedPercent = 10f },
                CharacterPassiveId.ArcherLevelAttackSpeed,
                "현재 레벨만큼 공속 +1%"),
            new(
                1,
                "흡혈 박쥐 캐릭터",
                new Color(0.94f, 0.42f, 0.56f, 1f),
                100,
                false,
                CharacterUnlockSource.Shop,
                WeaponUpgradeId.Bat,
                new MetaBonusValues { healthRegenPerSecond = 1f },
                CharacterPassiveId.VampireMaxHealthDamage,
                "추가 체력 3당 피해 +1%"),
            new(
                2,
                "부채꼴 연속베기 캐릭터",
                new Color(0.76f, 0.90f, 1f, 1f),
                100,
                false,
                CharacterUnlockSource.Shop,
                WeaponUpgradeId.Slash,
                new MetaBonusValues { moveSpeedPercent = 10f },
                CharacterPassiveId.SwordsmanLevelMoveSpeed,
                "현재 레벨만큼 이동속도 +1%"),
            new(
                3,
                "폭발 화염탄 캐릭터",
                new Color(0.96f, 0.74f, 0.38f, 1f),
                100,
                false,
                CharacterUnlockSource.Shop,
                WeaponUpgradeId.Fireball,
                new MetaBonusValues { attackPowerPercent = 10f },
                CharacterPassiveId.TaoistLevelDamage,
                "현재 레벨만큼 피해 +1%"),
            new(
                4,
                "근접 장판 캐릭터",
                new Color(0.72f, 1f, 0.84f, 1f),
                100,
                false,
                CharacterUnlockSource.Shop,
                WeaponUpgradeId.Aura,
                new MetaBonusValues { attackRangePercent = 10f },
                CharacterPassiveId.ExorcistLevelRange,
                "현재 레벨만큼 범위 +1%"),
            new(
                5,
                "연쇄 번개 캐릭터",
                new Color(1f, 0.94f, 0.42f, 1f),
                100,
                false,
                CharacterUnlockSource.Shop,
                WeaponUpgradeId.ChainLightning,
                default,
                CharacterPassiveId.ThunderMageChainMastery,
                "연쇄 감쇠 제거, 연쇄 수 +2"),
            new(
                6,
                "근거리 산탄 캐릭터",
                new Color(0.98f, 0.72f, 0.42f, 1f),
                100,
                false,
                CharacterUnlockSource.Shop,
                WeaponUpgradeId.Shotgun,
                new MetaBonusValues { maxHealthFlat = 20f },
                CharacterPassiveId.StarterWeaponSpecialist,
                "근거리 산탄 피해량 +10%, 근거리 산탄 범위 +10%"),
            new(
                7,
                "방향 대검 캐릭터",
                new Color(0.82f, 0.87f, 0.96f, 1f),
                100,
                false,
                CharacterUnlockSource.Shop,
                WeaponUpgradeId.BfSword,
                new MetaBonusValues { maxHealthFlat = 20f },
                CharacterPassiveId.StarterWeaponSpecialist,
                "방향 대검 피해량 +10%, 방향 대검 범위 +10%"),
            new(
                8,
                "철퇴 캐릭터",
                new Color(0.94f, 0.82f, 0.50f, 1f),
                100,
                false,
                CharacterUnlockSource.Shop,
                WeaponUpgradeId.SwingMace,
                new MetaBonusValues { maxHealthFlat = 20f },
                CharacterPassiveId.StarterWeaponSpecialist,
                "철퇴 피해량 +10%, 철퇴 범위 +10%"),
            new(
                9,
                "회전 위성 캐릭터",
                new Color(0.70f, 0.96f, 0.90f, 1f),
                100,
                false,
                CharacterUnlockSource.Shop,
                WeaponUpgradeId.OrbitWeapon,
                new MetaBonusValues { maxHealthFlat = 20f },
                CharacterPassiveId.StarterWeaponSpecialist,
                "회전 위성 피해량 +10%, 회전 위성 범위 +10%"),
            new(
                10,
                "설치 포탑 캐릭터",
                new Color(0.84f, 0.74f, 0.60f, 1f),
                100,
                false,
                CharacterUnlockSource.Shop,
                WeaponUpgradeId.Turret,
                new MetaBonusValues { maxHealthFlat = 20f },
                CharacterPassiveId.StarterWeaponSpecialist,
                "설치 포탑 피해량 +10%, 설치 포탑 범위 +10%"),
        };

        private static readonly SharedWeaponDefinition[] StarterWeapons =
        {
            new(WeaponUpgradeId.Rifle, "연발 투사체(Rifle)"),
            new(WeaponUpgradeId.Fireball, "폭발 화염탄(Fireball)"),
            new(WeaponUpgradeId.Bat, "흡혈 박쥐(Bat)"),
            new(WeaponUpgradeId.Shotgun, "근거리 산탄(Shotgun)"),
            new(WeaponUpgradeId.BfSword, "방향 대검(BfSword)"),
            new(WeaponUpgradeId.Slash, "부채꼴 연속베기(Slash)"),
            new(WeaponUpgradeId.ChainLightning, "연쇄 번개(ChainLightning)"),
            new(WeaponUpgradeId.SwingMace, "철퇴(SwingMace)"),
            new(WeaponUpgradeId.OrbitWeapon, "회전 위성(OrbitWeapon)"),
            new(WeaponUpgradeId.Turret, "설치 포탑(Turret)"),
            new(WeaponUpgradeId.Aura, "근접 장판(Aura)"),
        };

        public static int CharacterCount => Characters.Length;
        public static int StarterWeaponCount => StarterWeapons.Length;
        public static System.Collections.Generic.IReadOnlyList<SharedCharacterDefinition> CharacterDefinitions => Characters;
        public static System.Collections.Generic.IReadOnlyList<SharedWeaponDefinition> StarterWeaponDefinitions => StarterWeapons;

        public static int NormalizeCharacterId(int characterId)
        {
            if (Characters.Length <= 0)
            {
                return 0;
            }

            var normalized = characterId % Characters.Length;
            return normalized < 0 ? normalized + Characters.Length : normalized;
        }

        public static SharedCharacterDefinition GetCharacter(int characterId)
        {
            return Characters[NormalizeCharacterId(characterId)];
        }

        public static int GetDefaultUnlockedCharacterId()
        {
            for (var i = 0; i < Characters.Length; i++)
            {
                if (Characters[i].DefaultUnlocked)
                {
                    return Characters[i].Id;
                }
            }

            return 0;
        }

        public static WeaponUpgradeId GetStarterWeaponForCharacter(int characterId)
        {
            return GetCharacter(characterId).StarterWeaponId;
        }

        public static int NormalizeStarterWeaponIndex(int index)
        {
            if (StarterWeapons.Length <= 0)
            {
                return 0;
            }

            var normalized = index % StarterWeapons.Length;
            return normalized < 0 ? normalized + StarterWeapons.Length : normalized;
        }

        public static SharedWeaponDefinition GetStarterWeaponDefinition(int index)
        {
            return StarterWeapons[NormalizeStarterWeaponIndex(index)];
        }

        public static SharedWeaponDefinition GetStarterWeaponDefinition(WeaponUpgradeId weaponId)
        {
            return GetStarterWeaponDefinition(GetStarterWeaponIndex(weaponId));
        }

        public static WeaponUpgradeId GetStarterWeaponByIndex(int index)
        {
            return GetStarterWeaponDefinition(index).Id;
        }

        public static int GetStarterWeaponIndex(WeaponUpgradeId weaponId)
        {
            for (var i = 0; i < StarterWeapons.Length; i++)
            {
                if (StarterWeapons[i].Id == weaponId)
                {
                    return i;
                }
            }

            return 0;
        }

        public static WeaponUpgradeId GetDefaultUnlockedStarterWeapon()
        {
            return GetStarterWeaponForCharacter(GetDefaultUnlockedCharacterId());
        }

        public static bool IsStarterWeaponSelectable(WeaponUpgradeId weaponId)
        {
            for (var i = 0; i < StarterWeapons.Length; i++)
            {
                if (StarterWeapons[i].Id == weaponId)
                {
                    return StarterWeapons[i].IsSelectable;
                }
            }

            return false;
        }

        public static string GetWeaponDisplayName(WeaponUpgradeId weaponId)
        {
            for (var i = 0; i < StarterWeapons.Length; i++)
            {
                if (StarterWeapons[i].Id == weaponId)
                {
                    return StarterWeapons[i].DisplayName;
                }
            }

            return "연발 투사체(Rifle)";
        }

        public static string GetStatDisplayName(StatUpgradeId statId)
        {
            return statId switch
            {
                StatUpgradeId.AttackPower => "피해량",
                StatUpgradeId.AttackSpeed => "공격 속도",
                StatUpgradeId.MaxHealth => "최대 체력",
                StatUpgradeId.HealthRegen => "체력 재생",
                StatUpgradeId.MoveSpeed => "이동 속도",
                StatUpgradeId.AttackRange => "범위",
                StatUpgradeId.Luck => "행운",
                _ => statId.ToString(),
            };
        }

        public static string GetMetaUpgradeDisplayName(MetaUpgradeId upgradeId)
        {
            return upgradeId switch
            {
                MetaUpgradeId.DamagePercent => "기본 피해",
                MetaUpgradeId.AttackSpeedPercent => "기본 공속",
                MetaUpgradeId.MaxHealthFlat => "기본 체력",
                MetaUpgradeId.HealthRegenPerSecond => "기본 재생",
                MetaUpgradeId.MoveSpeedPercent => "기본 이속",
                MetaUpgradeId.RangePercent => "기본 범위",
                MetaUpgradeId.Luck => "행운",
                MetaUpgradeId.ExperienceGainPercent => "기본 경험치",
                MetaUpgradeId.CreditGainPercent => "기본 코인",
                _ => upgradeId.ToString(),
            };
        }

        public static string GetPlayerDisplayName(ulong ownerClientId, int characterId)
        {
            return $"플레이어 {ownerClientId + 1} {GetCharacter(characterId).DisplayName}";
        }

        public static int GetCharacterMask(int characterId)
        {
            return 1 << NormalizeCharacterId(characterId);
        }

        public static int GetWeaponMask(WeaponUpgradeId weaponId)
        {
            return 1 << GetStarterWeaponIndex(weaponId);
        }

        public static bool IsCharacterInMask(int mask, int characterId)
        {
            return (mask & GetCharacterMask(characterId)) != 0;
        }

        public static bool IsWeaponInMask(int mask, WeaponUpgradeId weaponId)
        {
            return (mask & GetWeaponMask(weaponId)) != 0;
        }
    }
}

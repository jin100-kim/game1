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
                "화염술사",
                new Color(0.96f, 0.74f, 0.38f, 1f),
                0,
                true,
                CharacterUnlockSource.Default,
                WeaponUpgradeId.Fireball,
                new MetaBonusValues { attackPowerPercent = 10f },
                CharacterPassiveId.FireballLevelDamage,
                "레벨당 화염구 피해 +2%"),
            new(
                1,
                "검사",
                new Color(0.76f, 0.90f, 1f, 1f),
                0,
                false,
                CharacterUnlockSource.Achievement,
                WeaponUpgradeId.Slash,
                new MetaBonusValues { moveSpeedPercent = 10f },
                CharacterPassiveId.SlashLevelDamage,
                "레벨당 베기 피해 +2%",
                "weapon_slash_lv10"),
            new(
                2,
                "번개술사",
                new Color(0.4f, 0.7f, 1f, 1f),
                0,
                false,
                CharacterUnlockSource.Achievement,
                WeaponUpgradeId.LightningBolt,
                new MetaBonusValues { attackSpeedPercent = 10f },
                CharacterPassiveId.LightningLevelDamage,
                "레벨당 낙뢰 피해 +2%",
                "weapon_lightning_lv10"),
            new(
                3,
                "빙결술사",
                new Color(0.7f, 0.9f, 1f, 1f),
                0,
                false,
                CharacterUnlockSource.Achievement,
                WeaponUpgradeId.IceSpike,
                new MetaBonusValues { attackPowerPercent = 5f, moveSpeedPercent = 5f },
                CharacterPassiveId.IceSpikeLevelDamage,
                "레벨당 얼음 파편 피해 +2%",
                "weapon_ice_lv10"),
            new(
                4,
                "바람술사",
                new Color(0.6f, 1f, 0.8f, 1f),
                0,
                false,
                CharacterUnlockSource.Achievement,
                WeaponUpgradeId.WindBlade,
                new MetaBonusValues { moveSpeedPercent = 15f },
                CharacterPassiveId.WindBladeLevelDamage,
                "레벨당 칼날 바람 피해 +2%",
                "weapon_wind_lv10"),
            new(
                5,
                "거품술사",
                new Color(0.6f, 0.9f, 1f, 1f),
                0,
                false,
                CharacterUnlockSource.Achievement,
                WeaponUpgradeId.Bubble,
                new MetaBonusValues { attackPowerPercent = 20f },
                CharacterPassiveId.BubbleLevelDamage,
                "레벨당 추적 방울 피해 +2%",
                "weapon_bubble_lv10"),
        };

        private static readonly SharedWeaponDefinition[] StarterWeapons =
        {
            new(WeaponUpgradeId.Fireball, "화염구(Fireball)"),
            new(WeaponUpgradeId.Slash, "베기(Slash)"),
            new(WeaponUpgradeId.LightningBolt, "낙뢰(Lightning Bolt)"),
            new(WeaponUpgradeId.IceSpike, "얼음 파편(Ice Spike)"),
            new(WeaponUpgradeId.WindBlade, "칼날 바람(Wind Blade)"),
            new(WeaponUpgradeId.Bubble, "추적 방울(Bubble)"),
        };

        public static int CharacterCount => Characters.Length;
        public static int StarterWeaponCount => StarterWeapons.Length;
        public static System.Collections.Generic.IReadOnlyList<SharedCharacterDefinition> CharacterDefinitions => Characters;
        public static System.Collections.Generic.IReadOnlyList<SharedWeaponDefinition> StarterWeaponDefinitions => StarterWeapons;

        public static int NormalizeCharacterId(int characterId)
        {
            if (Characters.Length <= 0) return 0;
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
                if (Characters[i].DefaultUnlocked) return Characters[i].Id;
            }
            return 0;
        }

        public static WeaponUpgradeId GetStarterWeaponForCharacter(int characterId)
        {
            return GetCharacter(characterId).StarterWeaponId;
        }

        public static int NormalizeStarterWeaponIndex(int index)
        {
            if (StarterWeapons.Length <= 0) return 0;
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
                if (StarterWeapons[i].Id == weaponId) return i;
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
                if (StarterWeapons[i].Id == weaponId) return StarterWeapons[i].IsSelectable;
            }
            return false;
        }

        public static string GetWeaponDisplayName(WeaponUpgradeId weaponId)
        {
            for (var i = 0; i < StarterWeapons.Length; i++)
            {
                if (StarterWeapons[i].Id == weaponId) return StarterWeapons[i].DisplayName;
            }
            return "화염구";
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
                MetaUpgradeId.ExperiencePickupRadiusPercent => "XP 흡입 거리",
                MetaUpgradeId.ProjectileCount => "추가 투사체",
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

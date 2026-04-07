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
                "궁수",
                new Color(0.92f, 0.92f, 0.82f, 1f),
                0,
                true,
                CharacterUnlockSource.Default,
                WeaponUpgradeId.ShortBow,
                new MetaBonusValues { attackSpeedPercent = 10f },
                CharacterPassiveId.ArcherLevelAttackSpeed,
                "현재 레벨만큼 공속 +1%"),
            new(
                1,
                "흡혈귀",
                new Color(0.94f, 0.42f, 0.56f, 1f),
                100,
                false,
                CharacterUnlockSource.Shop,
                WeaponUpgradeId.Bat,
                new MetaBonusValues { healthRegenPerSecond = 1f },
                CharacterPassiveId.VampireMaxHealthDamage,
                "추가 체력 3당 피해량 +1%"),
            new(
                2,
                "검객",
                new Color(0.76f, 0.90f, 1f, 1f),
                100,
                false,
                CharacterUnlockSource.Shop,
                WeaponUpgradeId.Katana,
                new MetaBonusValues { moveSpeedPercent = 10f },
                CharacterPassiveId.SwordsmanLevelMoveSpeed,
                "현재 레벨만큼 이동 속도 +1%"),
            new(
                3,
                "도사",
                new Color(0.96f, 0.74f, 0.38f, 1f),
                100,
                false,
                CharacterUnlockSource.Shop,
                WeaponUpgradeId.FireCharm,
                new MetaBonusValues { attackPowerPercent = 10f },
                CharacterPassiveId.TaoistLevelDamage,
                "현재 레벨만큼 피해량 +1%"),
            new(
                4,
                "퇴마사",
                new Color(0.72f, 1f, 0.84f, 1f),
                100,
                false,
                CharacterUnlockSource.Shop,
                WeaponUpgradeId.Aura,
                new MetaBonusValues { attackRangePercent = 10f },
                CharacterPassiveId.ExorcistLevelRange,
                "현재 레벨만큼 범위 +1%",
                null),
            new(
                5,
                "뇌전술사",
                new Color(1f, 0.94f, 0.42f, 1f),
                100,
                false,
                CharacterUnlockSource.Shop,
                WeaponUpgradeId.ChainAttack,
                default,
                CharacterPassiveId.ThunderMageChainMastery,
                "뇌부 감쇠 제거, 연쇄 수 +2",
                null),
        };

        private static readonly SharedWeaponDefinition[] StarterWeapons =
        {
            new(WeaponUpgradeId.ShortBow, "단궁"),
            new(WeaponUpgradeId.FireCharm, "화부"),
            new(WeaponUpgradeId.Bat, "박쥐"),
            new(WeaponUpgradeId.Arquebus, "철포"),
            new(WeaponUpgradeId.BfSword, "대도"),
            new(WeaponUpgradeId.Katana, "환도"),
            new(WeaponUpgradeId.ChainAttack, "뇌부"),
            new(WeaponUpgradeId.SatelliteBeam, "천벌광"),
            new(WeaponUpgradeId.Drone, "식신", isSelectable: false),
            new(WeaponUpgradeId.RifleTurret, "노포"),
            new(WeaponUpgradeId.Aura, "퇴마진"),
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

            return "단궁";
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
                MetaUpgradeId.DamagePercent => "피해량",
                MetaUpgradeId.AttackSpeedPercent => "공속",
                MetaUpgradeId.MaxHealthFlat => "최대 체력",
                MetaUpgradeId.HealthRegenPerSecond => "체력 재생",
                MetaUpgradeId.MoveSpeedPercent => "이동 속도",
                MetaUpgradeId.RangePercent => "범위",
                MetaUpgradeId.Luck => "행운",
                MetaUpgradeId.ExperienceGainPercent => "XP 획득량",
                MetaUpgradeId.CreditGainPercent => "코인 획득량",
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

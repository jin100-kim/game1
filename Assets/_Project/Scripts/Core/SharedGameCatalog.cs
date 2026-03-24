using UnityEngine;

namespace EJR.Game.Core
{
    public readonly struct SharedCharacterDefinition
    {
        public SharedCharacterDefinition(int id, string displayName, Color color, int unlockCost, bool defaultUnlocked, MetaBonusValues traitBonuses)
        {
            Id = id;
            DisplayName = displayName;
            Color = color;
            UnlockCost = unlockCost;
            DefaultUnlocked = defaultUnlocked;
            TraitBonuses = traitBonuses;
        }

        public int Id { get; }
        public string DisplayName { get; }
        public Color Color { get; }
        public int UnlockCost { get; }
        public bool DefaultUnlocked { get; }
        public MetaBonusValues TraitBonuses { get; }
    }

    public readonly struct SharedWeaponDefinition
    {
        public SharedWeaponDefinition(WeaponUpgradeId id, string displayName, int unlockCost, bool defaultUnlocked, bool isSelectable = true)
        {
            Id = id;
            DisplayName = displayName;
            UnlockCost = unlockCost;
            DefaultUnlocked = defaultUnlocked;
            IsSelectable = isSelectable;
        }

        public WeaponUpgradeId Id { get; }
        public string DisplayName { get; }
        public int UnlockCost { get; }
        public bool DefaultUnlocked { get; }
        public bool IsSelectable { get; }
    }

    public static class SharedGameCatalog
    {
        private static readonly SharedCharacterDefinition[] Characters =
        {
            new(0, "스트라이커", new Color(0.97f, 0.95f, 0.70f, 1f), 0, true, new MetaBonusValues { attackPowerPercent = 8f }),
            new(1, "스카우트", new Color(0.62f, 0.90f, 1f, 1f), 60, false, new MetaBonusValues { moveSpeedPercent = 6f }),
            new(2, "뱅가드", new Color(1f, 0.67f, 0.74f, 1f), 80, false, new MetaBonusValues { maxHealthFlat = 20f }),
            new(3, "메딕", new Color(0.67f, 1f, 0.77f, 1f), 100, false, new MetaBonusValues { healthRegenPerSecond = 0.25f }),
        };

        private static readonly SharedWeaponDefinition[] StarterWeapons =
        {
            new(WeaponUpgradeId.Rifle, "라이플", 0, true),
            new(WeaponUpgradeId.Smg, "화염구", 0, true),
            new(WeaponUpgradeId.SniperRifle, "박쥐", 60, false),
            new(WeaponUpgradeId.Shotgun, "샷건", 80, false),
            new(WeaponUpgradeId.BfSword, "BF소드", 0, true),
            new(WeaponUpgradeId.Katana, "카타나", 100, false),
            new(WeaponUpgradeId.ChainAttack, "체인어택", 120, false),
            new(WeaponUpgradeId.SatelliteBeam, "메이스", 140, false),
            new(WeaponUpgradeId.Drone, "레거시 드론", 160, false, isSelectable: false),
            new(WeaponUpgradeId.RifleTurret, "터렛", 180, false),
            new(WeaponUpgradeId.Aura, "오라", 200, false),
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
            for (var i = 0; i < StarterWeapons.Length; i++)
            {
                if (StarterWeapons[i].DefaultUnlocked && StarterWeapons[i].IsSelectable)
                {
                    return StarterWeapons[i].Id;
                }
            }

            return WeaponUpgradeId.Rifle;
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

            return "라이플";
        }

        public static string GetStatDisplayName(StatUpgradeId statId)
        {
            return statId switch
            {
                StatUpgradeId.AttackPower => "공격력",
                StatUpgradeId.AttackSpeed => "공격 속도",
                StatUpgradeId.MaxHealth => "최대 체력",
                StatUpgradeId.HealthRegen => "체력 재생",
                StatUpgradeId.MoveSpeed => "이동 속도",
                StatUpgradeId.AttackRange => "공격 범위",
                StatUpgradeId.Luck => "행운",
                _ => statId.ToString(),
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

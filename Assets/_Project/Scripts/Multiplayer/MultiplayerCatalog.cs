using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Multiplayer
{
    public enum MultiplayerRunPhase
    {
        Lobby = 0,
        Running = 1,
        LevelChoice = 2,
        Result = 3,
    }

    public readonly struct MultiplayerCharacterDefinition
    {
        public MultiplayerCharacterDefinition(string displayName, Color color)
        {
            DisplayName = displayName;
            Color = color;
        }

        public string DisplayName { get; }
        public Color Color { get; }
    }

    public static class MultiplayerCatalog
    {
        private static readonly MultiplayerCharacterDefinition[] Characters =
        {
            new("Striker", new Color(0.97f, 0.95f, 0.70f, 1f)),
            new("Scout", new Color(0.62f, 0.90f, 1f, 1f)),
            new("Vanguard", new Color(1f, 0.67f, 0.74f, 1f)),
            new("Medic", new Color(0.67f, 1f, 0.77f, 1f)),
        };

        private static readonly WeaponUpgradeId[] StarterWeapons =
        {
            WeaponUpgradeId.Rifle,
            WeaponUpgradeId.Smg,
            WeaponUpgradeId.SniperRifle,
            WeaponUpgradeId.Shotgun,
        };

        public static int CharacterCount => Characters.Length;
        public static int StarterWeaponCount => StarterWeapons.Length;

        public static MultiplayerCharacterDefinition GetCharacter(int characterId)
        {
            return Characters[NormalizeCharacterId(characterId)];
        }

        public static int NormalizeCharacterId(int characterId)
        {
            if (Characters.Length <= 0)
            {
                return 0;
            }

            var normalized = characterId % Characters.Length;
            return normalized < 0 ? normalized + Characters.Length : normalized;
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

        public static WeaponUpgradeId GetStarterWeaponByIndex(int index)
        {
            return StarterWeapons[NormalizeStarterWeaponIndex(index)];
        }

        public static int GetStarterWeaponIndex(WeaponUpgradeId weaponId)
        {
            for (var i = 0; i < StarterWeapons.Length; i++)
            {
                if (StarterWeapons[i] == weaponId)
                {
                    return i;
                }
            }

            return 0;
        }

        public static string GetStarterWeaponDisplayName(int index)
        {
            return GetWeaponDisplayName(GetStarterWeaponByIndex(index));
        }

        public static string GetWeaponDisplayName(WeaponUpgradeId weaponId)
        {
            return weaponId switch
            {
                WeaponUpgradeId.Smg => "SMG",
                WeaponUpgradeId.SniperRifle => "Sniper",
                WeaponUpgradeId.Shotgun => "Shotgun",
                WeaponUpgradeId.Katana => "Katana",
                WeaponUpgradeId.ChainAttack => "Chain",
                WeaponUpgradeId.SatelliteBeam => "Beam",
                WeaponUpgradeId.Drone => "Drone",
                WeaponUpgradeId.RifleTurret => "Turret",
                WeaponUpgradeId.Aura => "Aura",
                _ => "Rifle",
            };
        }

        public static string GetStatDisplayName(StatUpgradeId statId)
        {
            return statId switch
            {
                StatUpgradeId.AttackPower => "Attack Power",
                StatUpgradeId.AttackSpeed => "Attack Speed",
                StatUpgradeId.MaxHealth => "Max Health",
                StatUpgradeId.HealthRegen => "Health Regen",
                StatUpgradeId.MoveSpeed => "Move Speed",
                StatUpgradeId.AttackRange => "Attack Range",
                _ => statId.ToString(),
            };
        }

        public static string GetCoreDisplayName(WeaponCoreElement coreElement)
        {
            return coreElement switch
            {
                WeaponCoreElement.Fire => "Fire",
                WeaponCoreElement.Wind => "Wind",
                WeaponCoreElement.Light => "Light",
                WeaponCoreElement.Water => "Water",
                _ => "Core",
            };
        }

        public static string GetPlayerDisplayName(ulong ownerClientId, int characterId)
        {
            var character = GetCharacter(characterId);
            return $"P{ownerClientId + 1} {character.DisplayName}";
        }
    }
}

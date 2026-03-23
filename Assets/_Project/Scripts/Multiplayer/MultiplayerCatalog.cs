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
        public static int CharacterCount => SharedGameCatalog.CharacterCount;
        public static int StarterWeaponCount => SharedGameCatalog.StarterWeaponCount;

        public static MultiplayerCharacterDefinition GetCharacter(int characterId)
        {
            var definition = SharedGameCatalog.GetCharacter(characterId);
            return new MultiplayerCharacterDefinition(definition.DisplayName, definition.Color);
        }

        public static int NormalizeCharacterId(int characterId)
        {
            return SharedGameCatalog.NormalizeCharacterId(characterId);
        }

        public static int NormalizeStarterWeaponIndex(int index)
        {
            return SharedGameCatalog.NormalizeStarterWeaponIndex(index);
        }

        public static WeaponUpgradeId GetStarterWeaponByIndex(int index)
        {
            return SharedGameCatalog.GetStarterWeaponByIndex(index);
        }

        public static int GetStarterWeaponIndex(WeaponUpgradeId weaponId)
        {
            return SharedGameCatalog.GetStarterWeaponIndex(weaponId);
        }

        public static string GetStarterWeaponDisplayName(int index)
        {
            return GetWeaponDisplayName(GetStarterWeaponByIndex(index));
        }

        public static string GetWeaponDisplayName(WeaponUpgradeId weaponId)
        {
            return SharedGameCatalog.GetWeaponDisplayName(weaponId);
        }

        public static string GetStatDisplayName(StatUpgradeId statId)
        {
            return SharedGameCatalog.GetStatDisplayName(statId);
        }

        public static string GetPlayerDisplayName(ulong ownerClientId, int characterId)
        {
            return SharedGameCatalog.GetPlayerDisplayName(ownerClientId, characterId);
        }
    }
}

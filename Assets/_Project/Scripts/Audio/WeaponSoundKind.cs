using UnityEngine;
using EJR.Game.Core;

namespace EJR.Game.Audio
{
    public enum WeaponSoundKind
    {
        Primary = 0,
        Turn = 1,
        Deploy = 2,
        Spawn = 3,
        Latch = 4,
        Return = 5,
        Secondary = 6,
        Hit = 7,
    }

    public readonly struct WeaponSoundRequest
    {
        public WeaponSoundRequest(WeaponUpgradeId weaponId, WeaponSoundKind kind, Vector3 worldPosition)
        {
            WeaponId = weaponId;
            Kind = kind;
            WorldPosition = worldPosition;
        }

        public WeaponUpgradeId WeaponId { get; }
        public WeaponSoundKind Kind { get; }
        public Vector3 WorldPosition { get; }
    }
}

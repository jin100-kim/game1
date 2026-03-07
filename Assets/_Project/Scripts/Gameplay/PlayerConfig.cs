using UnityEngine;

namespace EJR.Game.Gameplay
{
    [CreateAssetMenu(menuName = "EJR/Config/Player", fileName = "PlayerConfig")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Min(1f)] public float maxHealth = 100f;
        [Min(0.1f)] public float moveSpeed = 5f;
        [Min(0.1f)] public float pickupRadius = 1.2f;
        [Min(0.1f)] public float xpAttractRadius = 4f;
        [Min(0.1f)] public float xpAttractSpeed = 6f;
        [Header("Combat")]
        [Min(0f)] public float damageInvulnerabilitySeconds = 0.35f;

        [Header("Size")]
        [Min(0.1f)] public float visualScale = 0.7f;
        [Min(0.1f)] public float visualScaleMultiplier = 1.5f;
        public float visualYOffset = -1f;
        [Min(0.05f)] public float collisionRadius = 0.35f;

        [Header("Animation")]
        [Min(1f)] public float animationFps = 10f;
        public bool flipByMoveDirection = true;
        [Min(0)] public int idleStartFrame = 0;
        [Min(0)] public int idleEndFrame = 5;
        [Min(0)] public int moveStartFrame = 6;
        [Min(0)] public int moveEndFrame = 12;
        public bool useHurtAnimation = true;
        [Min(0)] public int hurtStartFrame = 13;
        [Min(0)] public int hurtEndFrame = 18;
        [Min(0)] public int dieStartFrame = 19;
        [Min(0)] public int dieEndFrame = 23;

        [Header("Weapon Visual")]
        public Vector2 weaponVisualOffset = new(0f, -0.05f);
        [Min(0.05f)] public float weaponAimDistance = 0.4f;
        public float weaponAimRotationOffsetDegrees = 0f;
        [Min(0.05f)] public float weaponVisualScale = 0.675f;
        [Min(1f)] public float weaponAnimationFps = 18f;
        [Min(0)] public int weaponIdleFrame = 0;
        [Min(0)] public int weaponAttackStartFrame = 1;
        [Min(0)] public int weaponAttackEndFrame = 2;
    }
}

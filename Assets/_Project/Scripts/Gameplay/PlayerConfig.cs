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

        [Header("Size")]
        [Min(0.1f)] public float visualScale = 0.7f;
        [Min(0.05f)] public float collisionRadius = 0.35f;
    }
}

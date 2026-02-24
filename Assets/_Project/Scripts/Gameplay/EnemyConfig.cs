using UnityEngine;

namespace EJR.Game.Gameplay
{
    [CreateAssetMenu(menuName = "EJR/Config/Enemy", fileName = "EnemyConfig")]
    public sealed class EnemyConfig : ScriptableObject
    {
        [Min(1f)] public float maxHealth = 30f;
        [Min(0.1f)] public float moveSpeed = 1.7f;
        [Min(1f)] public float contactDamage = 10f;
        [Min(0.1f)] public float contactDamageCooldown = 0.75f;
        [Min(1)] public int experienceOnDeath = 1;

        [Header("Spawner")]
        [Min(0.1f)] public float initialSpawnInterval = 1.2f;
        [Min(0.05f)] public float minimumSpawnInterval = 0.25f;
        [Min(1f)] public float spawnRampSeconds = 480f;
        [Min(0.1f)] public float minSpawnRadius = 8f;
        [Min(0.1f)] public float maxSpawnRadius = 12f;
    }
}

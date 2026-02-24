using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        private EnemyConfig _config;
        private Transform _target;
        private PlayerHealth _playerHealth;
        private EnemyRegistry _registry;
        private ExperienceSystem _experienceSystem;

        private float _elapsedSeconds;
        private float _spawnTimer;

        public void Initialize(
            EnemyConfig config,
            Transform target,
            PlayerHealth playerHealth,
            EnemyRegistry registry,
            ExperienceSystem experienceSystem)
        {
            _config = config;
            _target = target;
            _playerHealth = playerHealth;
            _registry = registry;
            _experienceSystem = experienceSystem;
            _spawnTimer = 0f;
        }

        private void Update()
        {
            if (_config == null || _target == null || _playerHealth == null || _registry == null)
            {
                return;
            }

            _elapsedSeconds += Time.deltaTime;
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer > 0f)
            {
                return;
            }

            SpawnEnemy();
            _spawnTimer = SpawnMath.CalculateSpawnInterval(
                _elapsedSeconds,
                _config.initialSpawnInterval,
                _config.minimumSpawnInterval,
                _config.spawnRampSeconds);
        }

        private void SpawnEnemy()
        {
            var angle = Random.value * Mathf.PI * 2f;
            var radius = Random.Range(_config.minSpawnRadius, _config.maxSpawnRadius);
            var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;

            var enemyObject = new GameObject("Enemy");
            enemyObject.transform.position = _target.position + offset;

            var renderer = enemyObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            renderer.color = new Color(1f, 0.3f, 0.35f);
            enemyObject.transform.localScale = Vector3.one * 0.6f;

            var enemy = enemyObject.AddComponent<EnemyController>();
            enemy.Initialize(_config, _target, _playerHealth, _registry, _experienceSystem);

            var healthBar = enemyObject.AddComponent<WorldHealthBar>();
            healthBar.Initialize(
                new Vector3(0f, 0.62f, 0f),
                0.82f,
                0.1f,
                new Color(1f, 0.3f, 0.35f, 0.95f),
                new Color(0f, 0f, 0f, 0.55f),
                24);
            healthBar.SetHealth(enemy.CurrentHealth, enemy.MaxHealth);
            enemy.Changed += healthBar.SetHealth;
        }
    }
}

using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class ExperienceSystem : MonoBehaviour
    {
        [SerializeField, Min(0)] private int orbPoolPrewarmCount = 48;

        private Transform _player;
        private PlayerConfig _playerConfig;
        private LevelUpSystem _levelUp;
        private readonly Queue<ExperienceOrb> _orbPool = new();
        private Transform _orbPoolRoot;

        public void Initialize(Transform player, PlayerConfig playerConfig, LevelUpSystem levelUp)
        {
            _player = player;
            _playerConfig = playerConfig;
            _levelUp = levelUp;
            EnsureOrbPool();
        }

        public void SpawnOrb(Vector3 position, int value)
        {
            if (_player == null || _playerConfig == null || _levelUp == null)
            {
                return;
            }

            var orb = GetPooledOrb();
            var orbObject = orb.gameObject;
            orbObject.transform.SetPositionAndRotation(position, Quaternion.identity);

            orb.Initialize(
                _player,
                this,
                Mathf.Max(1, value),
                _playerConfig.pickupRadius,
                _playerConfig.xpAttractRadius,
                _playerConfig.xpAttractSpeed,
                ReturnOrbToPool);
        }

        public void Collect(int value)
        {
            _levelUp?.AddExperience(value);
        }

        private void EnsureOrbPool()
        {
            if (_orbPoolRoot == null)
            {
                var root = new GameObject("XPOrbPool");
                root.transform.SetParent(transform, false);
                _orbPoolRoot = root.transform;
            }

            var targetCount = Mathf.Max(0, orbPoolPrewarmCount);
            while (_orbPool.Count < targetCount)
            {
                var orb = CreateOrbInstance();
                ReturnOrbToPool(orb);
            }
        }

        private ExperienceOrb GetPooledOrb()
        {
            while (_orbPool.Count > 0)
            {
                var pooled = _orbPool.Dequeue();
                if (pooled != null)
                {
                    pooled.gameObject.SetActive(true);
                    return pooled;
                }
            }

            var created = CreateOrbInstance();
            created.gameObject.SetActive(true);
            return created;
        }

        private ExperienceOrb CreateOrbInstance()
        {
            var orbObject = new GameObject("XP Orb");
            orbObject.transform.SetParent(_orbPoolRoot, false);
            orbObject.transform.localScale = Vector3.one * 0.2f;

            var renderer = orbObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            renderer.color = new Color(0.35f, 1f, 0.4f);

            var orb = orbObject.AddComponent<ExperienceOrb>();
            orbObject.SetActive(false);
            return orb;
        }

        private void ReturnOrbToPool(ExperienceOrb orb)
        {
            if (orb == null)
            {
                return;
            }

            var orbObject = orb.gameObject;
            orbObject.SetActive(false);
            orbObject.transform.SetParent(_orbPoolRoot, false);
            _orbPool.Enqueue(orb);
        }
    }
}

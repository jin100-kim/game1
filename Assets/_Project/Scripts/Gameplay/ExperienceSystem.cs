using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class ExperienceSystem : MonoBehaviour
    {
        private Transform _player;
        private PlayerConfig _playerConfig;
        private LevelUpSystem _levelUp;

        public void Initialize(Transform player, PlayerConfig playerConfig, LevelUpSystem levelUp)
        {
            _player = player;
            _playerConfig = playerConfig;
            _levelUp = levelUp;
        }

        public void SpawnOrb(Vector3 position, int value)
        {
            if (_player == null || _playerConfig == null || _levelUp == null)
            {
                return;
            }

            var orbObject = new GameObject("XP Orb");
            orbObject.transform.position = position;
            orbObject.transform.localScale = Vector3.one * 0.2f;

            var renderer = orbObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            renderer.color = new Color(0.35f, 1f, 0.4f);

            var orb = orbObject.AddComponent<ExperienceOrb>();
            orb.Initialize(
                _player,
                this,
                Mathf.Max(1, value),
                _playerConfig.pickupRadius,
                _playerConfig.xpAttractRadius,
                _playerConfig.xpAttractSpeed);
        }

        public void Collect(int value)
        {
            _levelUp?.AddExperience(value);
        }
    }
}

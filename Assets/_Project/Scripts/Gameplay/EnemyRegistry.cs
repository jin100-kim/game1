using System.Collections.Generic;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class EnemyRegistry : MonoBehaviour
    {
        private readonly List<EnemyController> _enemies = new();
        public IReadOnlyList<EnemyController> Enemies => _enemies;

        public void Register(EnemyController enemy)
        {
            if (enemy == null || _enemies.Contains(enemy))
            {
                return;
            }

            _enemies.Add(enemy);
        }

        public void Unregister(EnemyController enemy)
        {
            if (enemy == null)
            {
                return;
            }

            _enemies.Remove(enemy);
        }

        public EnemyController FindNearest(Vector3 position, float maxDistance)
        {
            EnemyController best = null;
            var bestDistanceSq = maxDistance * maxDistance;

            for (var i = _enemies.Count - 1; i >= 0; i--)
            {
                var enemy = _enemies[i];
                if (enemy == null)
                {
                    _enemies.RemoveAt(i);
                    continue;
                }

                var distanceSq = (enemy.transform.position - position).sqrMagnitude;
                if (distanceSq <= bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    best = enemy;
                }
            }

            return best;
        }
    }
}

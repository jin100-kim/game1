using System.Collections.Generic;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class EnemyRegistry : MonoBehaviour
    {
        private const float CellSize = 1.5f;

        private readonly List<EnemyController> _enemies = new();
        private readonly Dictionary<Vector2Int, List<EnemyController>> _cells = new();
        private readonly Dictionary<EnemyController, Vector2Int> _cellByEnemy = new();

        private float _maxCollisionRadius = 0.3f;
        private bool _maxCollisionRadiusDirty;

        public IReadOnlyList<EnemyController> Enemies => _enemies;

        public void Register(EnemyController enemy)
        {
            if (enemy == null || _cellByEnemy.ContainsKey(enemy))
            {
                return;
            }

            _enemies.Add(enemy);

            var cell = ToCell(enemy.transform.position);
            _cellByEnemy[enemy] = cell;
            AddToCell(cell, enemy);

            if (enemy.CollisionRadius > _maxCollisionRadius)
            {
                _maxCollisionRadius = enemy.CollisionRadius;
            }
        }

        public void Unregister(EnemyController enemy)
        {
            if (enemy == null)
            {
                return;
            }

            if (_cellByEnemy.TryGetValue(enemy, out var cell))
            {
                RemoveFromCell(cell, enemy);
                _cellByEnemy.Remove(enemy);
            }

            _enemies.Remove(enemy);

            if (enemy.CollisionRadius >= _maxCollisionRadius - 0.0001f)
            {
                _maxCollisionRadiusDirty = true;
            }
        }

        public void NotifyMoved(EnemyController enemy, Vector3 position)
        {
            if (enemy == null)
            {
                return;
            }

            if (!_cellByEnemy.TryGetValue(enemy, out var currentCell))
            {
                Register(enemy);
                return;
            }

            var nextCell = ToCell(position);
            if (nextCell == currentCell)
            {
                return;
            }

            RemoveFromCell(currentCell, enemy);
            AddToCell(nextCell, enemy);
            _cellByEnemy[enemy] = nextCell;
        }

        public float GetMaxCollisionRadius()
        {
            if (!_maxCollisionRadiusDirty)
            {
                return Mathf.Max(0.05f, _maxCollisionRadius);
            }

            var max = 0.05f;
            for (var i = 0; i < _enemies.Count; i++)
            {
                var enemy = _enemies[i];
                if (enemy == null)
                {
                    continue;
                }

                if (enemy.CollisionRadius > max)
                {
                    max = enemy.CollisionRadius;
                }
            }

            _maxCollisionRadius = max;
            _maxCollisionRadiusDirty = false;
            return _maxCollisionRadius;
        }

        public void GetNearby(Vector2 position, float radius, List<EnemyController> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            if (radius <= 0f)
            {
                return;
            }

            var centerCell = ToCell(position);
            var cellRadius = Mathf.CeilToInt(radius / CellSize);
            var radiusSq = radius * radius;

            for (var y = centerCell.y - cellRadius; y <= centerCell.y + cellRadius; y++)
            {
                for (var x = centerCell.x - cellRadius; x <= centerCell.x + cellRadius; x++)
                {
                    var key = new Vector2Int(x, y);
                    if (!_cells.TryGetValue(key, out var cellEnemies))
                    {
                        continue;
                    }

                    for (var i = 0; i < cellEnemies.Count; i++)
                    {
                        var enemy = cellEnemies[i];
                        if (enemy == null)
                        {
                            continue;
                        }

                        var distanceSq = ((Vector2)enemy.transform.position - position).sqrMagnitude;
                        if (distanceSq <= radiusSq)
                        {
                            results.Add(enemy);
                        }
                    }
                }
            }
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

        private static Vector2Int ToCell(Vector2 position)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / CellSize),
                Mathf.FloorToInt(position.y / CellSize));
        }

        private void AddToCell(Vector2Int cell, EnemyController enemy)
        {
            if (!_cells.TryGetValue(cell, out var list))
            {
                list = new List<EnemyController>(8);
                _cells[cell] = list;
            }

            list.Add(enemy);
        }

        private void RemoveFromCell(Vector2Int cell, EnemyController enemy)
        {
            if (!_cells.TryGetValue(cell, out var list))
            {
                return;
            }

            list.Remove(enemy);
            if (list.Count == 0)
            {
                _cells.Remove(cell);
            }
        }
    }
}

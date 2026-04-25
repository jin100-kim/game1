using System;
using System.Collections.Generic;
using UnityEngine;
using EJR.Game.Core;

namespace EJR.Game.Gameplay
{
    public struct BfSwordBladeSnapshot
    {
        public BfSwordBladeSnapshot(Vector2 start, Vector2 end, float bladeRadius, float recordedAt)
        {
            Start = start;
            End = end;
            BladeRadius = bladeRadius;
            RecordedAt = recordedAt;
        }

        public Vector2 Start { get; }
        public Vector2 End { get; }
        public float BladeRadius { get; }
        public float RecordedAt { get; }
    }

    public sealed class WeaponRuntime
    {
        public WeaponRuntime(WeaponUpgradeId id, int level)
        {
            WeaponId = id;
            Level = Mathf.Max(1, level);
            Cooldown = 0f;
            BurstShotsRemaining = 0;
            BurstShotCooldown = 0f;
            BurstDirection = Vector2.right;
            BurstTotalShots = 0;
            BurstOrigin = Vector2.zero;
            OrbitAngleDegrees = UnityEngine.Random.Range(0f, 360f);
        }

        public WeaponUpgradeId WeaponId { get; }
        public int Level { get; set; }
        public float Cooldown { get; set; }
        public int BurstShotsRemaining { get; set; }
        public float BurstShotCooldown { get; set; }
        public Vector2 BurstDirection { get; set; }
        public int BurstTotalShots { get; set; }
        public Vector2 BurstOrigin { get; set; }
        public float OrbitAngleDegrees { get; set; }
        public List<Transform> SatelliteVisuals { get; } = new(3);
        public Dictionary<EnemyController, float> SatelliteHitCooldownUntil { get; } = new();
        public HashSet<EnemyController> BfSwordInsideEnemies { get; } = new();
        public List<BfSwordBladeSnapshot> BfSwordBladeHistory { get; } = new(24);
        public Dictionary<EnemyController, float> BfSwordAfterimageHitCooldownUntil { get; } = new();
        public List<SpriteRenderer> BfSwordAfterimageRenderers { get; } = new(2);
        public List<BatRuntime> BatInstances { get; } = new(4);
        public HashSet<EnemyController> SwingMaceHitEnemies { get; } = new();
        public HashSet<EnemyController> SwingMaceStunnedEnemies { get; } = new();
        public Coroutine ActiveChainCoroutine { get; set; }
        public Transform SwingMaceVisualRoot { get; set; }
        public bool IsSwingMaceSwingActive { get; set; }
        public float SwingMaceSwingElapsed { get; set; }
        public Vector2 SwingMaceSwingDirection { get; set; }
        public float NextBfSwordSoundAt { get; set; }
        public float BatOverflowMaxHealthProgress { get; set; }
        public List<TurretRuntime> TurretInstances { get; } = new(4);
        public IWeaponStrategy Strategy { get; set; }
        public object CustomState { get; set; }
    }

    public sealed class TurretRuntime
    {
        public Transform Root;
        public Vector2 Position;
        public float ExpiresAt;
        public float ShotCooldown;
        public SpriteRenderer Renderer;
        public Sprite IdleFrame;
        public Sprite[] FireFrames;
        public Coroutine FireAnimationCoroutine;
    }

    public sealed class BatRuntime
    {
        public Transform Root;
        public SpriteRenderer Renderer;
        public EnemyController LatchedTarget;
        public float SpawnedAt;
        public float SeekAt;
        public float HitCooldown;
        public float OrbitSeedDegrees;
        public Vector2 LaunchDirection;
        public float PendingHealAmount;
        public int HitsLanded;
        public bool ReturningToOwner;
    }
}

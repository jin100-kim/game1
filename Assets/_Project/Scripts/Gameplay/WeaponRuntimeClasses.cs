using System;
using System.Collections.Generic;
using UnityEngine;
using EJR.Game.Core;

namespace EJR.Game.Gameplay
{
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
        }

        public WeaponUpgradeId WeaponId { get; }
        public int Level { get; set; }
        public float Cooldown { get; set; }
        
        // 공용 버스트/연사 데이터
        public int BurstShotsRemaining { get; set; }
        public float BurstShotCooldown { get; set; }
        public Vector2 BurstDirection { get; set; }
        public int BurstTotalShots { get; set; }
        public Vector2 BurstOrigin { get; set; }
        
        // 연쇄 번개 등 코루틴 기반 무기용 (호환성 유지)
        public Coroutine ActiveChainCoroutine { get; set; }
        
        public IWeaponStrategy Strategy { get; set; }
        public WeaponDefinition Definition { get; set; }
        public object CustomState { get; set; }
    }
}

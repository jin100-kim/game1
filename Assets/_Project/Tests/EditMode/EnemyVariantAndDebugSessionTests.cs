using System.Reflection;
using EJR.Game.Core;
using EJR.Game.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace EJR.Game.Tests.EditMode
{
    public sealed class EnemyVariantAndDebugSessionTests
    {
        [SetUp]
        public void SetUp()
        {
            ResetDebugSession();
        }

        [Test]
        public void CaptureTypedInput_UnlocksAcrossMultipleCalls()
        {
            Assert.That(DebugSessionService.IsUnlocked, Is.False);

            Assert.That(DebugSessionService.CaptureTypedInput("ad"), Is.False);
            Assert.That(DebugSessionService.IsUnlocked, Is.False);

            Assert.That(DebugSessionService.CaptureTypedInput("min"), Is.True);
            Assert.That(DebugSessionService.IsUnlocked, Is.True);
        }

        [Test]
        public void SharedEnemyVariantCatalog_ContainsExpectedDefinitions()
        {
            Assert.That(SharedEnemyVariantCatalog.All, Has.Count.EqualTo(6));
            Assert.That(SharedEnemyVariantCatalog.Get(EnemyVariantId.SlimeSplit)?.BehaviorKind, Is.EqualTo(EnemyVariantBehaviorKind.SplitOnDeath));
            Assert.That(SharedEnemyVariantCatalog.Get(EnemyVariantId.SlimeBomber)?.BehaviorKind, Is.EqualTo(EnemyVariantBehaviorKind.ProximityBomber));
            Assert.That(SharedEnemyVariantCatalog.Get(EnemyVariantId.MushroomShooter)?.BehaviorKind, Is.EqualTo(EnemyVariantBehaviorKind.Shooter));
            Assert.That(SharedEnemyVariantCatalog.Get(EnemyVariantId.MushroomHealer)?.BehaviorKind, Is.EqualTo(EnemyVariantBehaviorKind.Healer));
            Assert.That(SharedEnemyVariantCatalog.Get(EnemyVariantId.SkeletonCharger)?.BehaviorKind, Is.EqualTo(EnemyVariantBehaviorKind.Charger));
            Assert.That(SharedEnemyVariantCatalog.Get(EnemyVariantId.SkeletonArcher)?.BehaviorKind, Is.EqualTo(EnemyVariantBehaviorKind.Archer));
        }

        [Test]
        public void SharedEnemyVariantCatalog_IndexRoundTrip_IsStable()
        {
            for (var i = 0; i < SharedEnemyVariantCatalog.All.Count; i++)
            {
                var id = SharedEnemyVariantCatalog.GetByIndex(i);
                Assert.That(SharedEnemyVariantCatalog.GetIndex(id), Is.EqualTo(i));
                Assert.That(SharedEnemyVariantCatalog.Get(id), Is.Not.Null);
            }
        }

        [Test]
        public void SlimeBomber_LethalHit_ArmsInsteadOfDying()
        {
            var registryObject = new GameObject("Registry");
            var enemyObject = new GameObject("Enemy");
            var config = ScriptableObject.CreateInstance<EnemyConfig>();

            try
            {
                var registry = registryObject.AddComponent<EnemyRegistry>();
                var enemy = enemyObject.AddComponent<EnemyController>();
                enemy.Initialize(
                    config,
                    RuntimeSpriteFactory.EnemyVisualKind.Slime,
                    new EnemyStatProfile { visualKind = RuntimeSpriteFactory.EnemyVisualKind.Slime },
                    null,
                    null,
                    registry,
                    null,
                    0.3f,
                    0.3f);
                enemy.ConfigureVariant(SharedEnemyVariantCatalog.Get(EnemyVariantId.SlimeBomber));

                enemy.ReceiveWeaponDamage(enemy.CurrentHealth + 10f, WeaponUpgradeId.ShortBow);

                Assert.That(enemy.IsDead, Is.False);
                Assert.That(enemy.CurrentHealth, Is.EqualTo(1f).Within(0.001f));
                Assert.That(GetVariantActionState(enemy), Is.EqualTo("Windup"));
            }
            finally
            {
                Object.DestroyImmediate(enemyObject);
                Object.DestroyImmediate(registryObject);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void SlimeBomber_DuringWindup_DoesNotDieFromExtraHits()
        {
            var registryObject = new GameObject("Registry");
            var enemyObject = new GameObject("Enemy");
            var config = ScriptableObject.CreateInstance<EnemyConfig>();

            try
            {
                var registry = registryObject.AddComponent<EnemyRegistry>();
                var enemy = enemyObject.AddComponent<EnemyController>();
                enemy.Initialize(
                    config,
                    RuntimeSpriteFactory.EnemyVisualKind.Slime,
                    new EnemyStatProfile { visualKind = RuntimeSpriteFactory.EnemyVisualKind.Slime },
                    null,
                    null,
                    registry,
                    null,
                    0.3f,
                    0.3f);
                enemy.ConfigureVariant(SharedEnemyVariantCatalog.Get(EnemyVariantId.SlimeBomber));

                enemy.ReceiveWeaponDamage(enemy.CurrentHealth + 10f, WeaponUpgradeId.ShortBow);
                enemy.ReceiveWeaponDamage(999f, WeaponUpgradeId.ShortBow);

                Assert.That(enemy.IsDead, Is.False);
                Assert.That(enemy.CurrentHealth, Is.EqualTo(1f).Within(0.001f));
                Assert.That(GetVariantActionState(enemy), Is.EqualTo("Windup"));
            }
            finally
            {
                Object.DestroyImmediate(enemyObject);
                Object.DestroyImmediate(registryObject);
                Object.DestroyImmediate(config);
            }
        }

        private static void ResetDebugSession()
        {
            var method = typeof(DebugSessionService).GetMethod("ResetRuntimeState", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(null, null);
        }

        private static string GetVariantActionState(EnemyController enemy)
        {
            var field = typeof(EnemyController).GetField("_variantActionState", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            var value = field!.GetValue(enemy);
            Assert.That(value, Is.Not.Null);
            return value!.ToString();
        }
    }
}

using EJR.Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace EJR.Game.Tests.EditMode
{
    public sealed class GameplaySpeedServiceTests
    {
        [SetUp]
        public void SetUp()
        {
            GameplaySpeedService.ResetGameplaySpeedMultiplier();
            GameplaySpeedService.ApplyMenuTimeState();
        }

        [TearDown]
        public void TearDown()
        {
            GameplaySpeedService.ResetGameplaySpeedMultiplier();
            GameplaySpeedService.ApplyMenuTimeState();
        }

        [Test]
        public void ApplyGameplayTimeState_UsesConfiguredMultiplierAndFixedDeltaTime()
        {
            GameplaySpeedService.SetGameplaySpeedMultiplier(2.3f);
            GameplaySpeedService.ApplyGameplayTimeState(paused: false);

            Assert.That(GameplaySpeedService.GameplaySpeedMultiplier, Is.EqualTo(2.3f).Within(0.001f));
            Assert.That(Time.timeScale, Is.EqualTo(2.3f).Within(0.001f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.046f).Within(0.001f));
        }

        [Test]
        public void ApplyMenuTimeState_ResetsTimeScaleButKeepsStoredGameplayMultiplier()
        {
            GameplaySpeedService.SetGameplaySpeedMultiplier(4f);
            GameplaySpeedService.ApplyGameplayTimeState(paused: false);

            GameplaySpeedService.ApplyMenuTimeState();

            Assert.That(GameplaySpeedService.GameplaySpeedMultiplier, Is.EqualTo(4f).Within(0.001f));
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.02f).Within(0.001f));
        }

        [Test]
        public void ApplyGameplayTimeState_PausedZeroesTimeScaleAndKeepsScaledFixedDeltaTime()
        {
            GameplaySpeedService.SetGameplaySpeedMultiplier(1.5f);
            GameplaySpeedService.ApplyGameplayTimeState(paused: true);

            Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.001f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.03f).Within(0.001f));
        }
    }
}

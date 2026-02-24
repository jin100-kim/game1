using EJR.Game.Core;
using NUnit.Framework;

namespace EJR.Game.Tests.EditMode
{
    public sealed class SpawnMathTests
    {
        [Test]
        public void CalculateSpawnInterval_DecreasesAndClamps()
        {
            var initial = SpawnMath.CalculateSpawnInterval(0f, 1.2f, 0.25f, 480f);
            var middle = SpawnMath.CalculateSpawnInterval(240f, 1.2f, 0.25f, 480f);
            var end = SpawnMath.CalculateSpawnInterval(1000f, 1.2f, 0.25f, 480f);

            Assert.That(initial, Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(middle, Is.LessThan(initial));
            Assert.That(end, Is.EqualTo(0.25f).Within(0.0001f));
        }
    }
}

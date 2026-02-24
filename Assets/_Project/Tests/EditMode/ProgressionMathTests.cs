using EJR.Game.Core;
using NUnit.Framework;

namespace EJR.Game.Tests.EditMode
{
    public sealed class ProgressionMathTests
    {
        [Test]
        public void RequiredExperienceForLevel_IncreasesByRule()
        {
            Assert.That(ProgressionMath.RequiredExperienceForLevel(1), Is.EqualTo(5));
            Assert.That(ProgressionMath.RequiredExperienceForLevel(2), Is.EqualTo(8));
            Assert.That(ProgressionMath.RequiredExperienceForLevel(10), Is.EqualTo(32));
        }
    }
}

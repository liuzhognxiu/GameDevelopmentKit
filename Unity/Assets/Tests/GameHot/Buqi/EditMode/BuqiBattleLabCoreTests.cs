using Game.Hot.Buqi.BattleLab;
using NUnit.Framework;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiBattleLabCoreTests
    {
        [Test]
        public void HeadlessContracts_PassInUnityAssembly()
        {
            Assert.That(BuqiBattleLabContractChecks.RunAll(), Is.Empty);
        }
    }
}

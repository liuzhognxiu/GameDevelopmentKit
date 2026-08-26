using NUnit.Framework;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiRunSupplyTests
    {
        [Test]
        public void DeterministicSupplyContractsPass()
        {
            Assert.That(BuqiSupplyTestSuite.RunAll(), Is.Empty);
        }

        [Test]
        public void ProductionBazaarSupplyContractsPass()
        {
            Assert.That(BuqiBazaarSupplyViewSourceTestSuite.RunAll(), Is.Empty);
        }

        [Test]
        public void ProductionBazaarRunContractsPass()
        {
            Assert.That(BuqiBazaarSupplyRunIntegrationTestSuite.RunAll(), Is.Empty);
        }
    }
}

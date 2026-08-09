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
    }
}

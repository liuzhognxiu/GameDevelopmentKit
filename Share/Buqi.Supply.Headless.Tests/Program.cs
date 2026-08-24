using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Tests;

namespace Buqi.Supply.Headless.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            var failures = new List<string>();
            failures.AddRange(BuqiSupplyTestSuite.RunAll());
            failures.AddRange(BuqiBazaarSupplyViewSourceTestSuite.RunAll());
            failures.AddRange(BuqiSupplyConfigTestSuite.RunAll());
            foreach (string failure in failures)
                Console.Error.WriteLine(failure);

            Console.WriteLine($"buqi-supply-failures={failures.Count}");
            return failures.Count == 0 ? 0 : 1;
        }
    }
}

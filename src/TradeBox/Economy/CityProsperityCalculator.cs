using Lous12.TradeBox.Resources;

namespace Lous12.TradeBox.Economy
{
    public static class CityProsperityCalculator
    {
        public static float Calculate(CityEconomy economy)
        {
            if (economy == null)
            {
                return 0f;
            }

            float coverageTotal = 0f;
            float trendHealthTotal = 0f;
            int count = 0;

            for (int i = 0; i < VanillaResourceIds.Core.Length; i++)
            {
                string resourceId = VanillaResourceIds.Core[i];
                ResourceBalance balance = economy.GetOrCreateResource(resourceId);

                coverageTotal += balance.Coverage;
                trendHealthTotal += GetTrendHealth(balance.Trend);
                count++;
            }

            if (count == 0)
            {
                return 50f;
            }

            float coverageScore = coverageTotal / count * 100f;
            float trendScore = trendHealthTotal / count * 100f;

            // Reserve coverage is the main signal. Trend matters, but cannot
            // overpower actual shortages or healthy stocks.
            float prosperity = coverageScore * 0.8f + trendScore * 0.2f;
            if (prosperity < 0f)
            {
                return 0f;
            }
            if (prosperity > 100f)
            {
                return 100f;
            }
            return prosperity;
        }

        public static float CalculateCoverageScore(CityEconomy economy)
        {
            if (economy == null)
            {
                return 0f;
            }

            float total = 0f;
            int count = 0;
            for (int i = 0; i < VanillaResourceIds.Core.Length; i++)
            {
                ResourceBalance balance = economy.GetOrCreateResource(VanillaResourceIds.Core[i]);
                total += balance.Coverage;
                count++;
            }

            return count == 0 ? 0f : total / count * 100f;
        }

        public static float CalculateTrendScore(CityEconomy economy)
        {
            if (economy == null)
            {
                return 50f;
            }

            float total = 0f;
            int count = 0;
            for (int i = 0; i < VanillaResourceIds.Core.Length; i++)
            {
                ResourceBalance balance = economy.GetOrCreateResource(VanillaResourceIds.Core[i]);
                total += GetTrendHealth(balance.Trend);
                count++;
            }

            return count == 0 ? 50f : total / count * 100f;
        }

        private static float GetTrendHealth(int trend)
        {
            if (trend > 0)
            {
                return 1f;
            }
            if (trend < 0)
            {
                return 0f;
            }
            return 0.5f;
        }
    }
}

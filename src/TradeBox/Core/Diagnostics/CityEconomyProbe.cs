using System;
using Lous12.TradeBox.Economy;
using Lous12.TradeBox.Resources;

namespace Lous12.TradeBox.Core.Diagnostics
{
    internal static class CityEconomyProbe
    {
        public static void LogSnapshot(
            CityEconomy economy,
            int snapshotNumber,
            Action<string> log
        )
        {
            log("[TradeBox-Probe] ===== CITY ECONOMY SNAPSHOT " + snapshotNumber + "/2 =====");
            log(
                "[TradeBox-Probe] cityId=" + economy.CityId +
                " prosperity=" + economy.Prosperity.ToString("0.0") +
                " coverageScore=" + CityProsperityCalculator.CalculateCoverageScore(economy).ToString("0.0") +
                " trendScore=" + CityProsperityCalculator.CalculateTrendScore(economy).ToString("0.0")
            );

            for (int i = 0; i < VanillaResourceIds.Core.Length; i++)
            {
                string resourceId = VanillaResourceIds.Core[i];
                ResourceBalance balance = economy.GetOrCreateResource(resourceId);
                log(
                    "[TradeBox-Probe] " + resourceId +
                    " stock=" + balance.Stock +
                    " previous=" + balance.PreviousStock +
                    " baseline=" + balance.BaselineStock +
                    " trend=" + FormatTrend(balance.Trend) +
                    " target=" + balance.TargetReserve +
                    " coverage=" + (balance.Coverage * 100f).ToString("0.0") + "%" +
                    " surplus=" + balance.Surplus +
                    " deficit=" + balance.Deficit
                );
            }

            log("[TradeBox-Probe] ===== END CITY ECONOMY SNAPSHOT " + snapshotNumber + "/2 =====");
        }

        private static string FormatTrend(int trend)
        {
            return trend > 0 ? "+" + trend : trend.ToString();
        }
    }
}

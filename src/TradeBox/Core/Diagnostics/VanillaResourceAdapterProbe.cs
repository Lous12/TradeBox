using System;
using Lous12.TradeBox.Resources;

namespace Lous12.TradeBox.Core.Diagnostics
{
    internal static class VanillaResourceAdapterProbe
    {
        public static void Run(City city, Action<string> log)
        {
            if (city == null || log == null)
            {
                return;
            }

            log("[TradeBox-Probe] ===== VANILLA RESOURCE ADAPTER PROBE =====");

            for (int i = 0; i < VanillaResourceIds.Core.Length; i++)
            {
                string resourceId = VanillaResourceIds.Core[i];
                log(
                    "[TradeBox-Probe] READ " + resourceId + "=" +
                    VanillaResourceAdapter.GetAmount(city, resourceId)
                );
            }

            int before = VanillaResourceAdapter.GetAmount(city, VanillaResourceIds.Wood);
            int versionBefore = VanillaResourceAdapter.GetStorageVersion(city);
            int addReturn = VanillaResourceAdapter.Add(city, VanillaResourceIds.Wood, 1);
            int afterAdd = VanillaResourceAdapter.GetAmount(city, VanillaResourceIds.Wood);
            int versionAfterAdd = VanillaResourceAdapter.GetStorageVersion(city);

            log(
                "[TradeBox-Probe] ROUNDTRIP before=" + before +
                " addReturn=" + addReturn +
                " afterAdd=" + afterAdd +
                " storageVersion=" + versionBefore + "->" + versionAfterAdd
            );

            if (afterAdd != before + 1)
            {
                log("[TradeBox-Probe] RESULT=INCONCLUSIVE adapter add did not produce +1; no take performed");
                log("[TradeBox-Probe] ===== END VANILLA RESOURCE ADAPTER PROBE =====");
                return;
            }

            bool took = VanillaResourceAdapter.TryTake(city, VanillaResourceIds.Wood, 1);
            int finalAmount = VanillaResourceAdapter.GetAmount(city, VanillaResourceIds.Wood);
            int versionAfterTake = VanillaResourceAdapter.GetStorageVersion(city);

            log(
                "[TradeBox-Probe] ROUNDTRIP take=" + took +
                " final=" + finalAmount +
                " storageVersion=" + versionAfterTake
            );

            if (took && finalAmount == before)
            {
                log("[TradeBox-Probe] RESULT=PASS VanillaResourceAdapter read/add/take works");
            }
            else
            {
                log(
                    "[TradeBox-Probe] RESULT=FAIL adapter round-trip did not restore original amount; " +
                    "before=" + before + " final=" + finalAmount
                );
            }

            log("[TradeBox-Probe] ===== END VANILLA RESOURCE ADAPTER PROBE =====");
        }
    }
}

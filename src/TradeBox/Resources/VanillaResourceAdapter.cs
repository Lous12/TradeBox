using System;

namespace Lous12.TradeBox.Resources
{
    /// <summary>
    /// Single integration point between TradeBox and WorldBox city resource storage.
    /// Keep direct vanilla resource calls here instead of spreading them across economy/trade code.
    /// </summary>
    public static class VanillaResourceAdapter
    {
        public static int GetAmount(City city, string resourceId)
        {
            if (city == null || string.IsNullOrEmpty(resourceId))
            {
                return 0;
            }

            return city.getResourcesAmount(resourceId);
        }

        /// <summary>
        /// Adds a resource through WorldBox stockpile logic.
        /// Returns WorldBox's raw result from addResourcesToRandomStockpile.
        /// </summary>
        public static int Add(City city, string resourceId, int amount)
        {
            if (city == null || string.IsNullOrEmpty(resourceId) || amount <= 0)
            {
                return GetAmount(city, resourceId);
            }

            return city.addResourcesToRandomStockpile(resourceId, amount);
        }

        public static bool TryTake(City city, string resourceId, int amount)
        {
            if (city == null || string.IsNullOrEmpty(resourceId) || amount <= 0)
            {
                return false;
            }

            int before = city.getResourcesAmount(resourceId);
            if (before < amount)
            {
                return false;
            }

            city.takeResource(resourceId, amount);
            return city.getResourcesAmount(resourceId) == before - amount;
        }

        public static int GetStorageVersion(City city)
        {
            return city == null ? -1 : city.getStorageVersion();
        }
    }

    public static class VanillaResourceIds
    {
        public const string Wood = "wood";
        public const string Stone = "stone";
        public const string CommonMetals = "common_metals";
        public const string Gold = "gold";

        public static readonly string[] Core =
        {
            Wood,
            Stone,
            CommonMetals,
            Gold
        };
    }
}

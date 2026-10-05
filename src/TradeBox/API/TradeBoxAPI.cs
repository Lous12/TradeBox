using Lous12.TradeBox.Data;
using Lous12.TradeBox.Economy;

namespace Lous12.TradeBox.API
{
    public static class TradeBoxAPI
    {
        public const string Version = "0.1";

        public static bool TryGetCityEconomy(string cityId, out CityEconomy economy)
        {
            return TradeBoxState.Cities.TryGetValue(cityId, out economy);
        }
    }
}

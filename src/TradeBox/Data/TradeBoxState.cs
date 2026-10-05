using System.Collections.Generic;
using Lous12.TradeBox.Economy;
using Lous12.TradeBox.Trade;

namespace Lous12.TradeBox.Data
{
    public static class TradeBoxState
    {
        public static readonly Dictionary<string, CityEconomy> Cities =
            new Dictionary<string, CityEconomy>();

        public static readonly List<TradeRoute> Routes =
            new List<TradeRoute>();

        public static readonly List<TradeTransaction> RecentTransactions =
            new List<TradeTransaction>();

        public static void Reset()
        {
            Cities.Clear();
            Routes.Clear();
            RecentTransactions.Clear();
        }
    }
}

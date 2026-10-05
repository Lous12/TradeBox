using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Lous12.TradeBox.Data;

namespace Lous12.TradeBox.Economy
{
    public sealed class CityEconomyService
    {
        private readonly Dictionary<City, CityEconomy> _economies =
            new Dictionary<City, CityEconomy>();

        public int Count
        {
            get { return _economies.Count; }
        }

        public void Reset()
        {
            _economies.Clear();
        }

        public CityEconomy GetOrCreate(City city)
        {
            if (city == null)
            {
                return null;
            }

            CityEconomy economy;
            if (_economies.TryGetValue(city, out economy))
            {
                return economy;
            }

            string runtimeId = "runtime:" + RuntimeHelpers.GetHashCode(city).ToString("X8");
            economy = new CityEconomy(runtimeId);
            _economies[city] = economy;
            TradeBoxState.Cities[runtimeId] = economy;
            return economy;
        }

        public CityEconomy Capture(City city)
        {
            CityEconomy economy = GetOrCreate(city);
            if (economy != null)
            {
                economy.Capture(city);
            }
            return economy;
        }

        public void RegisterTradeDelta(City city, string resourceId, int delta)
        {
            CityEconomy economy = GetOrCreate(city);
            if (economy == null)
            {
                return;
            }

            economy.GetOrCreateResource(resourceId).RegisterKnownDelta(delta);
        }
    }
}

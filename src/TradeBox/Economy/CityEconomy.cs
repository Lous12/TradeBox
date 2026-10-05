using System.Collections.Generic;
using Lous12.TradeBox.Resources;

namespace Lous12.TradeBox.Economy
{
    public sealed class CityEconomy
    {
        public string CityId { get; private set; }
        public float Prosperity { get; set; }
        public Dictionary<string, ResourceBalance> Resources { get; private set; }

        public CityEconomy(string cityId)
        {
            CityId = cityId;
            Prosperity = 50f;
            Resources = new Dictionary<string, ResourceBalance>();
        }

        public ResourceBalance GetOrCreateResource(string resourceId)
        {
            ResourceBalance balance;
            if (!Resources.TryGetValue(resourceId, out balance))
            {
                balance = new ResourceBalance(resourceId);
                Resources[resourceId] = balance;
            }

            return balance;
        }

        public void Capture(City city)
        {
            for (int i = 0; i < VanillaResourceIds.Core.Length; i++)
            {
                string resourceId = VanillaResourceIds.Core[i];
                GetOrCreateResource(resourceId).Capture(
                    VanillaResourceAdapter.GetAmount(city, resourceId)
                );
            }

            Prosperity = CityProsperityCalculator.Calculate(this);
        }
    }
}

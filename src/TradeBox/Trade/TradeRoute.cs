namespace Lous12.TradeBox.Trade
{
    public sealed class TradeRoute
    {
        public string SourceCityId { get; set; }
        public string TargetCityId { get; set; }
        public float Distance { get; set; }
        public bool Active { get; set; }

        public TradeRoute(string sourceCityId, string targetCityId)
        {
            SourceCityId = sourceCityId;
            TargetCityId = targetCityId;
            Active = true;
        }
    }
}

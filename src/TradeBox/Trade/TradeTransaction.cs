namespace Lous12.TradeBox.Trade
{
    public sealed class TradeTransaction
    {
        public string SourceCityId { get; set; }
        public string SourceCityName { get; set; }
        public string TargetCityId { get; set; }
        public string TargetCityName { get; set; }
        public string ResourceId { get; set; }
        public int Amount { get; set; }
        public int WorldYear { get; set; }
    }
}

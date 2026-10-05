namespace Lous12.TradeBox.Economy
{
    public sealed class ResourceBalance
    {
        public string ResourceId { get; private set; }
        public int Stock { get; private set; }
        public int PreviousStock { get; private set; }
        public int BaselineStock { get; private set; }
        public int TargetReserve { get; private set; }
        public bool HasSnapshot { get; private set; }

        private int _pendingKnownDelta;
        private int _lastKnownDelta;

        public int RawTrend
        {
            get { return HasSnapshot ? Stock - PreviousStock : 0; }
        }

        // Vanilla/economic trend with known TradeBox transfers removed.
        public int Trend
        {
            get { return HasSnapshot ? RawTrend - _lastKnownDelta : 0; }
        }

        public int Surplus
        {
            get { return Stock > TargetReserve ? Stock - TargetReserve : 0; }
        }

        public int Deficit
        {
            get { return Stock < TargetReserve ? TargetReserve - Stock : 0; }
        }

        public float Coverage
        {
            get
            {
                if (TargetReserve <= 0)
                {
                    return 1f;
                }

                float value = (float)Stock / TargetReserve;
                if (value < 0f)
                {
                    return 0f;
                }
                if (value > 1f)
                {
                    return 1f;
                }
                return value;
            }
        }

        public ResourceBalance(string resourceId)
        {
            ResourceId = resourceId;
        }

        public void Capture(int stock)
        {
            if (!HasSnapshot)
            {
                Stock = stock;
                PreviousStock = stock;
                BaselineStock = stock;
                TargetReserve = stock;
                _pendingKnownDelta = 0;
                _lastKnownDelta = 0;
                HasSnapshot = true;
                return;
            }

            PreviousStock = Stock;
            Stock = stock;
            _lastKnownDelta = _pendingKnownDelta;
            _pendingKnownDelta = 0;
            UpdateAdaptiveReserve();
        }

        public void RegisterKnownDelta(int delta)
        {
            _pendingKnownDelta += delta;
        }

        public void SetTargetReserve(int targetReserve)
        {
            TargetReserve = targetReserve < 0 ? 0 : targetReserve;
        }

        private void UpdateAdaptiveReserve()
        {
            // Only vanilla/economic drawdown raises reserve. Known TradeBox transfers
            // are removed from Trend so exporting goods does not look like consumption.
            int drawdown = -Trend;
            if (drawdown <= 0)
            {
                return;
            }

            int observedSafetyTarget = Stock + drawdown * 3;
            if (observedSafetyTarget < BaselineStock)
            {
                observedSafetyTarget = BaselineStock;
            }

            if (observedSafetyTarget > TargetReserve)
            {
                TargetReserve = observedSafetyTarget;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Lous12.TradeBox.Data;
using Lous12.TradeBox.Economy;
using Lous12.TradeBox.Trade;
using UnityEngine;

namespace Lous12.TradeBox
{
    public partial class Main
    {
        private const float CityWarmupSeconds = 10f;
        private const float EconomyTickSeconds = 10f;
        private const float CityRediscoverySeconds = 5f;

        private readonly CityEconomyService _cityEconomies = new CityEconomyService();
        private InternalTradeService _internalTrade;

        private object _observedWorld;
        private List<City> _cities = new List<City>();
        private float _nextCityDiscoveryTime;
        private float _nextEconomyTickTime;
        private int _economyTicksCaptured;
        private bool _runtimeReady;

        private void Update()
        {
            object currentWorld = null;
            try
            {
                currentWorld = World.world;
            }
            catch
            {
                currentWorld = null;
            }

            if (!object.ReferenceEquals(currentWorld, _observedWorld))
            {
                _observedWorld = currentWorld;
                _cities = new List<City>();
                _nextCityDiscoveryTime = Time.unscaledTime + 2f;
                _nextEconomyTickTime = -1f;
                _economyTicksCaptured = 0;
                _runtimeReady = false;
                _cityEconomies.Reset();
                TradeBoxState.Reset();
                _internalTrade = new InternalTradeService(_cityEconomies);
                ResetCityEconomyUiState();

                if (currentWorld != null)
                {
                    LogInfo("[TradeBox] New world detected. Live economy runtime armed.");
                }
            }

            RefreshCityEconomyUi();

            if (currentWorld == null)
            {
                return;
            }

            if (Time.unscaledTime >= _nextCityDiscoveryTime)
            {
                _nextCityDiscoveryTime = Time.unscaledTime + CityRediscoverySeconds;
                RefreshCityList(currentWorld);
            }

            if (_cities.Count == 0)
            {
                return;
            }

            if (!_runtimeReady)
            {
                _runtimeReady = true;
                _nextEconomyTickTime = Time.unscaledTime + CityWarmupSeconds;
                LogInfo(
                    "[TradeBox] Economy runtime discovered " + _cities.Count +
                    " kingdom-bound cities. Warmup=" + CityWarmupSeconds + "s."
                );
                return;
            }

            if (Time.unscaledTime < _nextEconomyTickTime)
            {
                return;
            }

            _nextEconomyTickTime = Time.unscaledTime + EconomyTickSeconds;

            try
            {
                CaptureAllCities();
                _economyTicksCaptured++;
                LogEconomySummary(_economyTicksCaptured);

                if (_economyTicksCaptured >= 2 && _internalTrade != null)
                {
                    _internalTrade.TryExecuteOneTrade(_cities, LogInfo);
                }
            }
            catch (Exception exception)
            {
                LogWarning("[TradeBox] Live economy tick failed: " + exception);
            }
        }

        private void RefreshCityList(object currentWorld)
        {
            try
            {
                List<City> rawCities = InternalTradeService.FindAllCities(currentWorld);
                List<City> kingdomBound = InternalTradeService.FilterKingdomBoundCities(rawCities);

                if (kingdomBound.Count != _cities.Count)
                {
                    LogInfo(
                        "[TradeBox] City discovery raw=" + rawCities.Count +
                        " kingdomBound=" + kingdomBound.Count
                    );
                }

                _cities = kingdomBound;
            }
            catch (Exception exception)
            {
                LogWarning("[TradeBox] City rediscovery failed: " + exception.Message);
            }
        }

        private void CaptureAllCities()
        {
            for (int i = 0; i < _cities.Count; i++)
            {
                City city = _cities[i];
                if (city != null)
                {
                    _cityEconomies.Capture(city);
                }
            }
        }

        private void LogEconomySummary(int tickNumber)
        {
            int surplusResources = 0;
            int deficitResources = 0;
            float prosperityTotal = 0f;
            int economyCount = 0;

            for (int i = 0; i < _cities.Count; i++)
            {
                CityEconomy economy = _cityEconomies.GetOrCreate(_cities[i]);
                if (economy == null)
                {
                    continue;
                }

                prosperityTotal += economy.Prosperity;
                economyCount++;

                foreach (ResourceBalance balance in economy.Resources.Values)
                {
                    if (balance.Surplus > 0)
                    {
                        surplusResources++;
                    }
                    if (balance.Deficit > 0)
                    {
                        deficitResources++;
                    }
                }
            }

            float averageProsperity = economyCount == 0 ? 0f : prosperityTotal / economyCount;
            LogInfo(
                "[TradeBox] ECONOMY TICK " + tickNumber +
                " cities=" + economyCount +
                " avgProsperity=" + averageProsperity.ToString("0.0") +
                " surplusEntries=" + surplusResources +
                " deficitEntries=" + deficitResources +
                " trades=" + TradeBoxState.RecentTransactions.Count
            );
        }
    }
}

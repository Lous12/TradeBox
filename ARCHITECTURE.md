# Architecture

## Entry point

`src/TradeBox/Main.cs` defines the NeoModLoader mod class. Runtime/UI behavior is split through partial `Main` files.

## Runtime

- `Core/Runtime/TradeBox.Runtime.cs` — world detection, city rediscovery, economy ticks and automatic internal-trade execution.
- `Core/Runtime/TradeBox.Bootstrap.cs` — startup/bootstrap work.

Current timing in the snapshot:

- city warmup: 10 seconds;
- economy tick: 10 seconds;
- city rediscovery: 5 seconds.

## Economy

- `Economy/CityEconomy.cs` — tracked city economy state.
- `Economy/ResourceBalance.cs` — stock/reserve/surplus/deficit/trend values.
- `Economy/CityEconomyService.cs` — capture/update service.
- `Economy/CityProsperityCalculator.cs` — prosperity and summary scores.

## Vanilla resource adapter

`Resources/VanillaResourceAdapter.cs` is the boundary to actual WorldBox city resources. Internal trade should use this layer so the mod moves real vanilla stock rather than only editing TradeBox's mirror state.

## Trade

- `Trade/InternalTradeService.cs` — source selection and same-kingdom transfers.
- `Trade/TradeRoute.cs` — route model placeholder/foundation.
- `Trade/TradeTransaction.cs` — recorded transfers.

## State

`Data/TradeBoxState.cs` currently stores cities, routes and recent transactions in runtime collections. It resets on world changes. Persistent serialization was planned but is not part of `dev10.7`.

## UI

`UI/CityEconomy.NativeUI.cs` implements the city Economy interface.

Important invariant from `dev10.7`: the Economy panel is intentionally detached from the normal `CityWindow` descendant tree while retaining its own ScrollRect. This prevents other city-window tab logic (including Political World) from treating the inner Economy ScrollRect as the window's primary scroll view.

## Public API

`API/TradeBoxAPI.cs` currently exposes a very small `0.1` API: callers can query a tracked `CityEconomy` by city ID.

Do not expand this API casually until persistence and identifiers are stable.

# Project status

**Development:** paused  
**Snapshot:** `0.1.0-dev10.7`  
**WorldBox target:** build `719`  
**Dependencies:** none

## Implemented in this snapshot

- Discovery of kingdom-bound cities.
- Periodic economy capture from vanilla city resource storage.
- Per-resource stock, target reserve, surplus, deficit and trend values.
- Prosperity calculation plus reserve-coverage and trend-health summaries.
- Same-kingdom internal trade that moves actual vanilla resources from a surplus city to a deficit city.
- Nearest-source selection for internal trade where distance can be resolved.
- Recent trade transaction tracking.
- City Economy UI with its own scrolling content.
- Resource mutation / round-trip diagnostic probes.
- Minimal public API for querying a tracked city economy.

## Important unfinished areas

- Custom TradeBox economic state is currently runtime state; full save/load persistence was planned next and is not implemented in this snapshot.
- International trade is not implemented.
- Visible trade caravans / markets are not implemented.
- Economic systems, monopolies, taxation/offices and Political World integration are design ideas, not implemented features.
- The current city-discovery/resource-access code contains compatibility/reflection fallbacks and still needs hardening.

## UI note

`dev10.7` uses a deliberate workaround: the Economy panel is detached from the `CityWindow` descendant tree while the Economy page keeps its own ScrollRect. This was done to stop Politics/Economy scroll/tab conflicts. Treat that layout as a tested invariant until a replacement is proven safer.

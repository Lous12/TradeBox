# TradeBox

Experimental economy and trade expansion for **WorldBox**.

> **Status:** unfinished / development paused.  
> **Current source snapshot:** `0.1.0-dev10.7`  
> **Target WorldBox build:** `719`

TradeBox was designed as a **standalone** expansion of the vanilla economy. It reads and mutates real vanilla city resources instead of inventing a completely separate fake stockpile layer.

The long-term idea was to make resources, shortages, reserves and trade visible enough to matter without turning WorldBox into a spreadsheet.

## What exists in the current snapshot

- city economy tracking based on vanilla resources;
- stock / target reserve / surplus / deficit / trend calculations;
- prosperity, reserve-coverage and trend-health summaries;
- automatic same-kingdom internal resource transfers;
- recent internal-trade transaction display;
- an Economy page integrated into the city UI;
- public `TradeBoxAPI.TryGetCityEconomy(...)` access;
- diagnostic probes for vanilla resource reads/writes and internal-trade transfers.

The `dev10.7` UI deliberately detaches the Economy panel from the normal `CityWindow` descendant tree while keeping its own ScrollRect. That avoids repeated Politics/Economy nested-scroll conflicts discovered during testing. Do not casually undo that structure without reproducing the old conflict first.

## Repository layout

```text
src/TradeBox/          Current mod source snapshot
README.md              Project overview
VISION.md              Original direction and design goals
STATUS.md              Implemented / unfinished systems
ROADMAP.md             Ideas, not dates
ARCHITECTURE.md        Code map and important invariants
CONTRIBUTING.md        Fork / PR guidance
AGENTS.md              Context for AI-assisted work
CHANGELOG.md           Current archived snapshot
```

## Political World relationship

TradeBox is intentionally standalone. Political World integration was planned as a **separate addon/bridge**, especially after international trade existed. Political World should not become a hard dependency of TradeBox core.

## License

MIT. See [LICENSE](LICENSE).

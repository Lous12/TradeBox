# Contributing

Forks, experiments and pull requests are welcome.

Before changing code:

1. Read `VISION.md`, `STATUS.md`, `ARCHITECTURE.md` and `AGENTS.md`.
2. Test against the WorldBox build you are targeting.
3. Preserve real vanilla resource mutation through the adapter layer.
4. Do not make Political World a hard dependency of TradeBox core.
5. Do not rewrite the `dev10.7` Economy UI hierarchy without testing the old nested-scroll/tab-conflict regression.
6. Be careful with city identity: persistence was not finished, so runtime identifiers and future save identifiers must not be mixed casually.

Bug reports are much more useful with a Player.log, reproduction steps and a save when relevant.

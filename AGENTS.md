# AI / agent context

Read first:

1. `README.md`
2. `VISION.md`
3. `STATUS.md`
4. `ARCHITECTURE.md`
5. the source file you intend to change

Rules:

- This is an unfinished development snapshot. Do not invent persistence, international trade or policy systems as if they already exist.
- TradeBox core must remain standalone. Political World integration belongs in a separate addon/bridge.
- Internal trade should mutate real vanilla resources through `VanillaResourceAdapter`.
- The Economy UI hierarchy in `dev10.7` is deliberate. The detached panel fixes a real nested ScrollRect / Politics-tab conflict.
- `TradeBoxState` is runtime-only in this snapshot. Do not claim save compatibility until a real persistence layer exists and is tested.
- Prefer one small change, one test, one log over large speculative rewrites.
- Keep public IDs/API contracts stable once external addons start using them.

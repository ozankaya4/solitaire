# Shared test vectors

Language-agnostic JSON fixtures used to keep the two engine implementations in
sync:

- **C#** — `backend/src/Solitaire.Engine` (verified by `Solitaire.Engine.Tests`)
- **TypeScript** — the frontend engine (to be added)

Each vector describes an initial state plus a sequence of moves and the expected
resulting state, so both engines can replay the same scenario and must agree.

## Layout

```
shared/
  test-vectors/     # *.json fixtures (added as the engine takes shape)
```

## Conventions (draft)

- One scenario per file, named `NNN-description.json`.
- Use a fixed RNG seed for any shuffle so results are deterministic.
- Keep field names identical across languages; prefer explicit enums
  (e.g. `"hearts"`, not `0`) so fixtures are readable and portable.

# Contingency Zero

A Total Annihilation-style RTS (with Red Alert 2 readability) built in Godot 4 with C#.
Design doc: https://claude.ai/artifact/27MRCyBndqGCe4d7uZUm5k

## Layout

- `sim/` — the deterministic simulation. Plain C# (.NET 8), **no Godot types**. This is the game.
- `sim.tests/` — xUnit tests. Determinism, economy, combat, replays, golden hashes.
- `game/` — Godot 4 project. Rendering, input, UI, audio. Reads sim state, sends commands. Never changes sim state directly.
- `data/` — unit, weapon and map definitions as JSON. Balance lives here, not in code.

## Hard rules for `sim/`

The sim must produce bit-identical results on every machine, every OS, every run. Online play and replays depend on it.

1. **No floating point.** Never use `float`, `double`, `decimal`, `Math.*` or `MathF.*` in sim code. Use `Fix64` and `FixVec2`.
2. **No ambient randomness or time.** Never use `System.Random`, `Guid.NewGuid`, `DateTime`, `Stopwatch` or `Environment.TickCount`. Use the world's `DeterministicRng`.
3. **Ordered iteration only.** Never iterate a `HashSet`, `Dictionary` or anything with undefined order when the result affects state. Iterate entity arrays by id.
4. **State changes only through commands.** The renderer, UI and AI all send `Command`s. Nothing else writes to `World`.
5. **Every sim change gets a test.** If a change alters the golden hash, update it in the same commit and say why in the message.
6. **Stats come from `data/`.** Don't hard-code unit numbers in sim code.

## Commands

```bash
dotnet test                      # all sim tests
dotnet build game/ContingencyZero.Game.csproj   # check the Godot C# project compiles
```

Open `game/project.godot` in Godot 4.3+ (.NET edition) to run the game.

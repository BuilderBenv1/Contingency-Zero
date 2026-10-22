# Contingency Zero

A machine-war RTS in the spirit of Total Annihilation, with Red Alert 2's readability and isometric look. Two rival machine factions, a flow economy, wreckage you reclaim, and a Commander you can't afford to lose. Built in Godot 4 with C#.

## Status: M1, simulation core

What works today:

- **Deterministic simulation** in fixed-point maths, 15 ticks a second, no floating point anywhere in game logic
- **Flow economy:** metal and energy income, storage, overflow, and stalls that slow every build by the same ratio
- **Building and reclaim:** builders walk to a site and build; destroyed units leave wrecks worth 60% of their metal
- **Combat:** target acquisition through a spatial grid, reload, accuracy rolls, deaths and wrecks
- **Commander win condition:** lose your Commander, lose the match
- **Replays:** a match is a seed, a scenario and its commands; a 1,000-unit minute saves to about 70 KB
- **Godot demo scene:** a 200-a-side line battle drawn from the sim, isometric orthographic camera

The M1 gate is the golden-hash test: a 1,000-unit battle must end in the same state hash on Linux, Windows and macOS. CI runs it on all three.

## Layout

| Folder | What |
| --- | --- |
| `sim/` | The game itself: plain C#, no Godot types. Fixed-point maths, world, economy, combat, orders, replays |
| `sim.tests/` | xUnit tests: maths, RNG, economy, combat, determinism, golden hash, source rules |
| `game/` | Godot 4 project. Reads the sim and sends commands; never writes sim state |
| `data/units/` | Unit stats as JSON, one file per unit. Balance lives here |

## Running it

```bash
dotnet test                 # all sim tests
```

To see it: install **Godot 4.3+ .NET edition**, open `game/project.godot`, and press Play. WASD pans, the mouse wheel zooms, right-click sends your blue army, Space pauses.

## Rules for the sim

Read [CLAUDE.md](CLAUDE.md) before changing anything in `sim/`. In short: no floats, no `System.Random`, no clocks, ordered iteration only, every change tested. A test scans the sim source and fails if any of those slip in.

## Next: M2, first playable skirmish

Selection and orders by mouse, factories and build menus, the Breach Cannon, eight units a faction, and an Easy AI to play against.

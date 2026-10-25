# Handoff: Defilade-style battlefield (terrain, battle marks, clutter, atmosphere)

Brief for the next coding agent. Read `CLAUDE.md` first, then this whole file before touching anything.

## Goal

Ben (the owner) wants the battlefield to look like **Defilade** (an upcoming realistic RTS): calm, realistic
ground; the battle written onto it in tank tracks, craters and scorch; small-scale clutter (ruins, walls,
palms, rocks) that units move *among*; warm haze and tall smoke columns. Today the ground is a busy photo
texture that clashes with the hand-built unit models.

Key insight from the last round: **the richness comes from what's on top of the ground, not from the
texture.** The base texture should be quiet; tracks, craters, clutter and atmosphere do the work.

Do it **one theme at a time, desert first** (closest to Defilade), get Ben's approval from before/after
screenshots, then roll out to temperate, arctic and lava.

## Ground rules

- `sim/` is deterministic lockstep code. **Don't change it.** Everything here is visual and lives in `game/`
  and `tools/`. Never use sim state for anything but reading. Visual randomness must be deterministic per
  position (hash of coordinates), not `Random`, so every player sees the same map.
- Don't hand-edit `data/maps/*.json` or `data/units/*.json` (generated). Don't touch golden hashes.
- Work on a branch (`terrain-defilade`), commit as you go, push the branch. Don't merge to `main` and
  don't create tags or releases; Ben reviews screenshots first.
- `dotnet build game/ContingencyZero.Game.csproj` must stay at 0 errors; `dotnet test` must stay green.
- Licences: only CC0 assets (Poly Haven, ambientCG). Record each source in `README.md` credits and
  `docs/HOW_TO_PLAY.txt` credits.
- Performance: must run smoothly on a mid-range PC and a Steam Deck. Keep per-frame work cheap; batch with
  `MultiMesh`; nothing that scales with map area every frame.

## Setup (Windows)

- .NET 8 SDK; Godot **4.3 .NET (mono)** edition; Python 3 with Pillow and numpy.
- Run from source: open `game/project.godot` in Godot, or from a terminal:
  `Godot_v4.3-stable_mono_win64.exe --path game -- --dev-skirmish=tablelands --dev-self-ai=1 --dev-fastforward=360 --dev-zoom=70 --dev-shots=C:\shots --dev-shot-times=8 --dev-seconds=12`
  (writes screenshots and quits; flags are parsed in `game/src/App/DevAutomation.cs`).
- Useful dev flags: `--dev-showcase` (set-piece tank battle on proving_grounds), `--dev-parade` (every unit
  and building lined up), `--dev-reveal`, `--dev-look=x,z`, `--dev-zoom=36..700`, `--dev-retro=0|1`
  (old-school pixel look off/on; judge both, default is on).
- Theme per map: desert = `tablelands`, `sandriver_valley`; temperate = `proving_grounds`;
  arctic = `glacier_gorge`, `frozen_mere`; lava = `ember_rift`, `the_boneyard`.

## Where things are

| File | What |
|---|---|
| `game/src/View/TerrainView.cs` | Ground mesh, `GroundShader` (5 layers per theme: shore, low, patch, high, rock, each with a normal map; height blend; fog of war), `ApplyGroundTextures`, scorch and crater recolouring (`RecomputeCorners`, `Scorch`), `SyncWith` (craters from the sim), water, lighting (`ConfigureLighting`) |
| `game/textures/<theme>_<slot>.png` / `_n.png` | Ground colour and normal maps (1024 / 512 px). `*.photo.png` are pre-paint originals for temperate |
| `tools/textures.py` | Builds those from a local ambientCG mirror in Source 2 format (`tools/vtex.py`). Writes `game/src/View/TextureSet.g.cs` (average colour per layer — the shader needs it) |
| `tools/stylise.py` | Optional painterly pass (Kuwahara + cavity). Ben found it too subtle; probably not needed for a realistic look |
| `game/src/View/FeatureView.cs` | Trees, rocks, wrecks, rubble (map features from the sim); uses hand-built models from `game/models/features/` when present |
| `game/src/View/PropView.cs` | Visual-only scatter of tiny ground props per terrain tile (hash-placed, avoids cliffs, water, buildings) |
| `game/src/View/UnitView.cs` | Units; `DrawContactShadows` (soft shadow under each unit/building) |
| `game/src/View/EffectsView.cs` | Explosions, smoke, fire; calls `_terrain.Scorch(...)` on impacts and deaths |
| `game/src/View/RetroLook.cs` | Old-school pixel/palette post-process (canvas layer) |
| `game/src/Match/MatchScene.cs` | Builds the match scene; vignette; add new views here |
| `tools/models/` | Blender-scripted models (`kit.py`, one script per model, `features/` for nature). Needs `pip install bpy` (Python 3.13) |

## Tasks (desert first)

### 1. Calm base textures
Replace the desert layers with calm, large-scale CC0 surfaces. Ben liked Poly Haven `sand_01` for the main
ground. Download 2K colour + normal maps (OpenGL normal convention; flip green if DirectX). Suggested:
- shore/low: `sand_01`; patch: a darker compact/wet sand or gravel; high: a pale cracked ground;
  rock: a layered sandstone cliff (`rock_face` family or ambientCG `Rock` sets).
Add a simple path in `tools/textures.py` to take plain PNGs from a folder (e.g. `tools/source_textures/<theme>/<slot>_color.png`,
`<slot>_normal.png`), resize to 1024/512, write the game files, and regenerate `TextureSet.g.cs`.
Then tune `GroundShader` uniforms (`tile`, `tint`, `bump`) for the desert so it reads calm at zoom 70–120:
visible soft ripples, no visible repeat (keep the two-scale anti-tiling), no busy speckle.

### 2. Battle marks (the biggest win)
- **Tank tracks:** ground vehicles (`def.MoveClass` vehicle/tracked, not bots, air or ships) stamp short track
  marks behind them as they move; marks build up and fade very slowly (minutes), and are capped (ring buffer,
  a few thousand stamps) so memory is bounded. Recommended approach: a single "marks" texture over the whole
  map (e.g. 2048² RG8, R = darkening, G = tread pattern mask), drawn into on the CPU with an `Image` and
  uploaded with `ImageTexture.Update` at most a few times per second, sampled by `GroundShader` in world UV.
  Draw marks as two parallel strips per vehicle width, oriented to heading (positions/headings from
  `UnitView` snapshots — `_curr`/`_prev` arrays). Bots can leave faint footprints or nothing.
- **Craters:** when `EffectsView` scorches for explosions, also stamp a crater decal into the marks texture
  (dark rim ring, lighter blasted centre, radial ejecta). The sim already deforms terrain for big blasts;
  this adds the visible bowl for small ones too.
- Must work in replays and when the camera is elsewhere (stamp from sim positions every tick, not only for
  visible units). Must reset with the map.

### 3. Clutter
Desert set, built like `tools/models/features/*.py` (Blender, CC0-free since generated): mud-brick ruined
walls and corners, a small ruined house, low stone field walls, palm clusters, scrub, sand-blown rocks,
abandoned barrels/crates. Place them **visual-only** in hashed clusters (villages/oases) on flat, open,
non-buildable-looking ground: extend `PropView` with a "cluster" pass (few, larger objects; cluster centres
from a coarse hashed grid; nothing within ~2 tiles of start positions, metal spots, cliffs, water). Keep
pathing unaffected (they're decoration; units drive over/through them — keep them low or at the edges of
open ground so it doesn't look wrong).

### 4. Atmosphere
- Warm desert grade: slightly warmer sun, a little haze toward the top of the screen (screen-space gradient
  or Godot `Environment` fog if compatible with the GL Compatibility renderer — test), soft contrast.
- Smoke columns: burning wrecks and destroyed buildings emit a tall, slowly rising, wind-leaning dark smoke
  plume for 30–90 s (GPU or CPU particles, billboard quads), visible from the RTS camera. Cap the count.
- Check the old-school look (`--dev-retro=1`) still reads well.

## Acceptance

- Desert before/after screenshots at zoom ~70 and ~110, in a base and in a big battle (`--dev-showcase` is
  temperate; for desert run `--dev-skirmish=tablelands --dev-self-ai=1 --dev-fastforward=600`), with old-school
  look on and off. Send to Ben; wait for approval before other themes.
- No visible texture repeat; tracks visibly accumulate over a match; craters visible; clutter reads as
  settlements/oases, not noise; frame rate unchanged within ~10%.
- `dotnet build` clean, `dotnet test` green, nothing in `sim/` or `data/` changed.

## Context you won't find in code

- Ben loved the hand-built unit models (`tools/models/`, Blender-scripted). Don't change units in this task.
  Whether to mute team colours/glows toward Defilade is an open question for Ben — ask, don't do.
- Releases: Ben publishes drafts himself. Bump `VERSION` and push `main` to `release-check` only when Ben
  says so.
- Planned later (don't build): 8- and 16-player variants (see `ROADMAP.md`).

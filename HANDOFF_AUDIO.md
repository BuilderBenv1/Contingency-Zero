# Handoff: real sound effects and music

Brief for the next coding agent. Read `CLAUDE.md` first (especially **Originality**), then all of this.

## Goal

All sound effects and music are synthesised in code (`game/src/Audio/SfxSynth.cs`, `MusicSynth.cs`).
Ben's playtest: the sound and some SFX "don't really fit the game". The game now looks gritty and
realistic (hand-built models, photo terrain, tracks, craters, smoke); the synthesised beeps and tones
sound thin and electronic next to it. Replace them with recorded, licensed sounds that feel heavy,
mechanical and industrial: the spirit of a 1997 machine-war RTS, not sci-fi bleeps.

## Licensing (hard rule)

Only sounds we may ship in a paid Steam game, with no attribution problems:
- **CC0**: Kenney audio packs (kenney.nl), Freesound items marked CC0, OpenGameArt CC0 items.
- **Royalty-free with commercial use**: the Sonniss GDC Game Audio Bundles (free each year).
- Never: sounds ripped from other games (including TA, C&C, Supreme Commander), CC-BY-NC, or
  anything whose licence you can't record.
Record every file's source and licence in `game/audio/CREDITS.md` and add a credits line to
`README.md` and `docs/HOW_TO_PLAY.txt`.

This environment may not reach those sites; Ben can download packs on his PC into
`tools/source_audio/` (git-ignored if large) and you pick, trim and convert from there.

## What exists

- `SfxKind.cs` lists every effect: Click, Hover; Laser, Cannon, Artillery, Missile, Flak, Bomb, DGun;
  ImpactSmall, ExplosionSmall, ExplosionLarge, ExplosionHuge; BuildStart, BuildComplete, UnitReady,
  Nano, Reclaim; Select, Acknowledge, Error, Alert, MissionMessage; Victory, Defeat.
- `Sfx.cs` renders a few variants per kind from `SfxSynth.Render(kind, variant)` into WAV streams,
  and plays them with `Play` (UI) / `PlayAt` (positional, attenuated by camera distance and zoom),
  with per-kind traits (cooldowns, voice limits) from `SfxSynth.Traits`.
- `MusicPlayer.cs` / `MusicLibrary.cs` / `MusicSynth.cs` generate and play music; `AudioBuses.cs` has
  the master / SFX / music buses tied to Settings volumes.

## Tasks

1. **Loader with fallback.** For each `SfxKind`, load `res://audio/sfx/<kind>_<n>.ogg` (n = 0..k,
   2–4 variants for anything that repeats: weapons, impacts, explosions, Select, Acknowledge).
   Fall back to the synthesised version when no file exists, so the game always has sound.
   Keep `Traits` (cooldowns, voice caps), and add slight random pitch (±5%) per play.
2. **Pick the sounds** (mono, 44.1/48 kHz, OGG, trimmed, peak-normalised, consistent loudness):
   - Weapons: punchy, low-end-heavy cannon thumps; artillery with a distant boom tail; missiles with
     a whoosh; flak as rapid dull pops; lasers as short crackling energy bursts (not "pew"); the
     DGun (Breach Cannon) as a huge, distinctive charged blast.
   - Explosions: layered (crack + body + debris), three sizes, the huge one for Commanders/reactors.
   - Construction: a mechanical whirr/hum for nanolathes (`Nano`, loopable), heavy clanks for
     BuildStart/BuildComplete, a satisfying industrial chime for UnitReady.
   - UI/feedback: chunky mechanical clicks; Select/Acknowledge as short servo/radio chirps, not
     voices with words (no voice acting for now); Alert as an urgent klaxon.
3. **Ambient beds per theme** (new, quiet, looping): wind and sand (desert), birds and breeze
   (temperate), howling wind (arctic), rumbling lava and hiss (lava). One loop per theme on the
   music bus or a new ambience bus with its own volume slider.
4. **Unit movement sounds** (new, subtle): tread rumble for vehicles, servo steps for bots, engine
   drone for aircraft, played only for units near the camera centre and capped (e.g. 6 voices).
5. **Music**: replace the generated loops with licensed tracks (moody industrial / orchestral-war,
   menu theme plus 3–4 in-match tracks), keeping `MusicPlayer`'s menu / calm / combat switching.
   If no suitable licensed music is found, keep the synthesised music and report back.

## Rules

- Visual and audio only: don't touch `sim/` or `data/`. `dotnet test` stays green, game builds clean.
- Keep the total audio under ~40 MB (OGG, sensible bitrates).
- Work on branch `audio-pass`, commit and push as you go, don't merge to `main` or make releases.

## Acceptance

- Every `SfxKind` has a recorded sound (or a stated reason it stays synthesised).
- A short report listing each sound file, its source and licence.
- A 30–60 s screen recording or a description of a test battle on proving_grounds
  (`--dev-showcase`), plus volume levels checked: weapons and explosions sit above UI, music under
  SFX, nothing clips.
- Stop for Ben's review.

# Roadmap

Planned, not started.

## 8- and 16-player variants
Larger games: maps and lobbies for 8 and 16 players (FFA and teams). Needs bigger maps from
tools/mapgen.py with balanced starts, lobby/UI support for more slots and colours, AI and
network load testing at 16 players, and a check of sim performance and lockstep bandwidth.

## Ground art to match the models
The ground uses photo-scanned textures; the units are stylised hand-built models, so they
still clash. Options: stylise the ground textures (simplified, painterly, stronger colour),
contact shadows under units and buildings, hand-built cliff and outcrop meshes, decals
(tracks, paths, scorch).

## AI and pathfinding (ideas from studying EA's GPL C&C source; no code copied)
Ranked by value for effort. All deterministic-safe; sim changes need tests and may move golden hashes.
1. **Connectivity zones per move class** (~1 day): flood-fill reachable regions in `Terrain`; reject or
   snap unreachable goals before A*. Removes worst-case 12k-expansion searches; prerequisite for naval AI.
2. **Path budget by cells expanded, not requests** (hours): `ServePathRequests` spends expansions.
3. **Destination reservation** (~1 day): each unit claims its goal tile; spiral to the nearest free
   passable one (`NearestPassable`). Stops units fighting over a spot and slots landing in water.
4. **AI enemy choice + zone filtering** (~1 day): score enemies by distance, other AIs already attacking,
   who hit us; stick until dead. Never pick targets a wave can't reach.
5. **Island mode for the AI** (3-4 days, needs 1): if the enemy isn't land-reachable, build a shipyard
   on water, favour ships/amphibious/air. Fixes Shoal Chain.
6. **Shared group paths in lanes** (~1 week): one path per group, units follow it in 2-3 offset columns;
   formation maths moves into the sim. The real fix for chokepoint drag on Ember Rift.
Also: corridor-guided A* (coarse then fine), traffic-aware costs and local path patching, speed-matched
groups with straggler regroup, lane waypoints in mapgen, AI harass targets (builders, extractors).

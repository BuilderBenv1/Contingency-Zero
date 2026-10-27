## Contingency Zero 1.4.1

- **Classic Total Annihilation mouse** (now the default): left click to select, left click again to give orders, right click to deselect. The modern right-click-to-order scheme is still in Settings.
- **Edge scrolling up and down works in a window:** moving the mouse to the top or bottom of the screen now scrolls even when the title bar or taskbar sits in the way.
- **Steady resource bars:** the metal and energy readouts no longer shift sideways when warnings flash.
- **Bigger units:** units are drawn larger and the camera starts closer, so armies read better against the map.


## Contingency Zero 1.4

**New in 1.4**

The battlefield:
- **Realistic terrain for every theme:** calm, natural ground for desert, grassland, arctic and lava maps, with cliffs that look like rock (sandstone, grey stone, snowy crags, black basalt).
- **The battle leaves its mark:** tank tracks build up across the ground over a match, explosions leave craters, and burning wrecks send up tall smoke columns.
- **Settlements and landmarks:** mud-brick villages and palm oases in the desert, farms, stone walls, hedges and copses on grassland, huts, fuel dumps and buried wrecks in the snow, scorched ruins, basalt spires and charred trees on lava.
- **Soft shadows under every unit and building**, and rubble at the foot of cliffs.

Smarter units and opponents:
- **Units know where they can go.** Ordering a unit somewhere it can't reach sends it to the nearest point it can, instead of stalling while it searches.
- **Units no longer pile onto the same spot** when moving as a group; each finds its own place.
- **The computer picks targets more sensibly** in team games, retaliates against attackers and never sends units somewhere they can't reach.
- **The computer now plays island maps:** on maps like Shoal Chain it builds a shipyard and attacks with ships, amphibious units and aircraft.
- Big armies move more smoothly; computer games on bridge maps such as Ember Rift finish much sooner.


## Contingency Zero 1.3.1

**New in 1.3.1**

- **Ground to match the models:** every theme now has five high-resolution ground textures (shore, ground, patches, high ground, cliffs) at the scale of pebbles and grass, each with a bump map so the sun picks out stones and cracks. Layers meet with crisp, natural edges.
- **Hand-built nature:** trees, rocks, wrecks and rubble are now proper models for each theme: firs, oaks and pines; cacti and palms; snow-laden spruce; charred trunks with glowing embers. Rocks still show their metal-ore crystals.
- **Ground detail:** grass tufts, ferns, scrub, stones, ice shards and cinders are scattered across every map, kept clear of buildings and cliffs.
- **Smaller download:** textures are now GPU-compressed.


## Contingency Zero 1.3

**New in 1.3**

A new look:
- **Every unit and building rebuilt as a proper 3D model:** all 86, both factions, from Commanders to experimental gantries. Real tracks and road wheels, riveted armour, cupolas, smokestacks, nano arms, spinning turbine blades and radar dishes, with baked shading, worn paint edges and grime.
- **Old-school look, on by default:** the battlefield is drawn in chunky pixels with a 1997-style palette and dithering, while the menus and HUD stay sharp. Settings → "Old-school look" switches it off.
- **Real ground textures:** each map theme (temperate, desert, arctic, lava) blends photo-sourced textures for shore, low ground, high ground and cliffs. Textures from ambientCG (CC0).
- **Metal deposits** are now dark, ragged patches of ore on the ground instead of glowing rings.

Maps:
- **Eight new maps:** Glacier Gorge, The Boneyard, Frozen Mere, Tablelands, Sandriver Valley, Shoal Chain, Ember Rift and Grand Continent, for 2 to 8 players. Shoal Chain is a naval map; the computer doesn't use ships yet, so play it against friends.

Fixes:
- A building could get stuck at 100% and never finish while your economy was stalled. It now completes.


### Earlier in 1.2

The battlefield:
- **Deformable terrain:** big explosions dig craters. Deep craters block vehicles but bots can still climb through.
- **Persistent ruins:** destroyed buildings leave broken foundations, burn marks stay, and the D-gun leaves a glassy scar.
- **Salvage:** reclaim enough enemy wrecks of one type (3 to 6, depending on tier) and you can build that unit yourself.

Commands and quality of life:
- **Area orders:** press A, R or G, then drag a circle to attack, reclaim or repair everything inside it.
- **Rally routes:** factories take up to 4 rally points (Shift+right-click), sending units to each in turn or every Nth unit to an extra point.
- **Build templates:** save a group of buildings as a layout (Ctrl+T) and place it again in one go, with either faction.
- **Economy forecast:** the resource bar warns things like "Stall in 40s" or "Full: wasting 26/s".

Replays, spectating and sharing:
- **Replay bookmarks:** a timeline marks Commander attacks, lost factories, incoming waves and big battles. Click a marker to jump to it.
- **Watch only:** spectate computer-vs-computer games with a casting panel comparing both economies.
- **Share codes:** copy and paste replays and maps as text codes.

New modes:
- **Co-op campaign:** three missions for two players, played online or alone with a computer ally.
- **Survival:** hold out against escalating waves alone or with up to three friends, and beat your best score on each map.

Controller support:
- **Controller and Steam Deck:** an on-screen cursor, order and build wheels, menus you can navigate with a pad, and a Steam Deck preset.


### Earlier in 1.1

 a grittier, more Total Annihilation look:
- a north-up camera at a steeper angle, instead of the isometric diamond view;
- textured, weathered ground with no grid;
- grimy metal units with worn, muted team paint;
- harsher lighting with darker shadows;
- steel-grey menus.


A machine-war RTS in the spirit of Total Annihilation: two factions, a flow economy, wreckage you reclaim, and a Commander you can't afford to lose.

### Download and play

- **Windows:** download `ContingencyZero-…-windows.zip`, unzip it anywhere, and run `ContingencyZero.exe`. Windows SmartScreen may warn that the app isn't signed: click **More info → Run anyway**.
- **Linux:** download `ContingencyZero-…-linux.tar.gz`, extract it, and run `./ContingencyZero.x86_64`.

No install, and nothing else to download. `HOW_TO_PLAY.txt` in the folder covers the controls.

### What's in it

- **Skirmish** on fourteen maps against up to seven computer players, at Easy, Normal, Hard or Brutal difficulty.
- **Two campaigns** of five missions each, one for the Lattice and one for the Crucible.
- **Multiplayer** over a LAN or by IP address, with computer players allowed in the same match.
- **Map editor** for sculpting terrain and placing water, metal spots, trees, rocks and start positions. Your maps appear in Skirmish and Multiplayer.
- **Replays** of every match, with playback speed controls and a choice of whose view to watch.

### Known limits

- Internet play needs port 7777 (UDP) forwarded on the host's router. There's no matchmaking server.
- There's no macOS build yet.

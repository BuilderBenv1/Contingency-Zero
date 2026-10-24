"""Crucible Solar Collector: a yard of three racked panel rows facing south. Run: python <this> <out_dir>

A riveted steel apron; three long rows of dark cells, each split into team-framed modules and
capped with a team header beam, on girder trestles (short front legs, tall back legs); a cable
tray links the rows to a squat inverter shed with a vent stack and a warning lamp.
Footprint 2 (16 units): kept inside 15 across.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('crucible')
body = []
E = 7.2
# ---- Apron ----
body.append(box('apron', (0, 0, 0.22), (2 * E, 2 * E, 0.44), 'hull_dark', 0.08))
for s in (-1, 1):
    body += ep.rivet_line(f'arx{s}', (-E + 0.4, s * (E - 0.25), 0.44), (E - 0.4, s * (E - 0.25), 0.44), 12, 'metal', 0.07)
    body += ep.rivet_line(f'ary{s}', (s * (E - 0.25), -E + 0.9, 0.44), (s * (E - 0.25), E - 0.9, 0.44), 10, 'metal', 0.07)
# Sleepers under each row.
ROWS = [-4.35, 0.35, 5.05]
TILT = math.radians(28)
D, Wd = 3.9, 13.4          # panel depth (along the slope) and width
for r, y in enumerate(ROWS):
    for x in (-5.6, -1.85, 1.85, 5.6):
        body.append(box(f'sleeper{r}{x}', (x, y, 0.5), (0.6, 3.4, 0.16), 'hull', 0.03))
    # Panel plane: hinge line at the front (south) edge.
    front = Vector((0, y - D / 2 * math.cos(TILT), 1.25))
    xf = Matrix.Translation(front) @ Matrix.Rotation(TILT, 4, 'X')
    row = []
    row.append(box(f'back{r}', (0, D / 2, -0.12), (Wd, D, 0.2), 'hull', 0.04))
    mods = 3
    mw = (Wd - 0.5) / mods
    for k in range(mods):
        cx = -Wd / 2 + 0.25 + mw * (k + 0.5)
        row.append(box(f'frame{r}{k}', (cx, D / 2 - 0.1, 0.04), (mw - 0.04, D - 0.2, 0.18), 'team', 0.03))
        row.append(box(f'cells{r}{k}', (cx, D / 2 - 0.25, 0.12), (mw - 0.9, D - 1.3, 0.06), 'solar', 0, 1))
        for j in range(1, 4):
            row.append(box(f'grid{r}{k}{j}', (cx - (mw - 0.9) / 2 + j * (mw - 0.9) / 4, D / 2 - 0.25, 0.16), (0.05, D - 1.3, 0.03), 'metal', 0, 1))
        row.append(box(f'gridh{r}{k}', (cx, D / 2 - 0.25, 0.16), (mw - 0.9, 0.05, 0.03), 'metal', 0, 1))
    # Team header along the top edge, with rivets.
    row.append(box(f'header{r}', (0, D - 0.2, 0.14), (Wd, 0.55, 0.3), 'team', 0.04))
    for k in range(mods + 1):
        row.append(box(f'mullion{r}{k}', (-Wd / 2 + 0.25 + mw * k, D / 2, 0.16), (0.32, D, 0.12), 'team_dark', 0.02, 1))
    for o in row:
        o.data.transform(xf)
    body += row
    body += ep.rivet_line(f'hdr{r}', tuple(xf @ Vector((-Wd / 2 + 0.6, D - 0.2, 0.31))), tuple(xf @ Vector((Wd / 2 - 0.6, D - 0.2, 0.31))), 10, 'metal', 0.06)
    # Trestles: short front post, tall back post, a diagonal brace.
    for x in (-5.6, -1.85, 1.85, 5.6):
        f = xf @ Vector((x, 0.4, -0.25))
        b = xf @ Vector((x, D - 0.5, -0.25))
        body.append(ep.beam(f'tf{r}{x}', (x, f.y, 0.55), (x, f.y, f.z), 0.26, 0.26, 'mech', 0.02))
        body.append(ep.beam(f'tb{r}{x}', (x, b.y, 0.55), (x, b.y, b.z), 0.3, 0.3, 'mech', 0.02))
        body.append(ep.beam(f'tx{r}{x}', (x, f.y, 0.6), (x, b.y, b.z - 0.4), 0.12, 0.12, 'metal', 0))
    # Cable tray under the row, feeding the trunk on the right.
    body.append(box(f'tray{r}', (0, y + 0.6, 0.62), (Wd - 1.0, 0.3, 0.14), 'accent', 0.02))
# Trunk cable tray along the right edge to the shed.
body.append(box('trunk', (6.55, 0.0, 0.66), (0.4, 11.0, 0.2), 'accent', 0.02))
# ---- Inverter shed at the front-left, with stack and lamp ----
sx, sy = -5.3, -6.55
body.append(box('shed', (sx, sy, 1.05), (3.2, 1.0, 1.25), 'hull', 0.05))
body.append(box('shedroof', (sx, sy, 1.73), (3.4, 1.15, 0.12), 'team', 0.02))
for i in range(5):
    body.append(box(f'shedlv{i}', (sx - 1.1 + i * 0.55, sy - 0.51, 1.0), (0.3, 0.04, 0.7), 'mech', 0.01, 1))
body.append(box('shedlamp', (sx + 1.35, sy - 0.53, 1.45), (0.25, 0.04, 0.16), 'glow', 0, 1))
body += ep.stack('shedstack', (sx + 1.1, sy + 0.15, 1.8), 0.2, 1.1, hot=False)
body += ep.pipe('shedcable', [(sx + 1.6, sy + 0.2, 0.7), (6.55, sy + 0.4, 0.7), (6.55, -5.5, 0.7)], 0.1, 'tread')

B = kit.join('Body', body)
ep.finish('crucible_solar_collector', [B], 1024, bake_scale=0.3)

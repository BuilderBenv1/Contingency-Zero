"""Crucible Colossus Gantry. Run: python tools/models/crucible_experimental_gantry.py <out_dir>

Footprint 4 (32 units). Two towering riveted walls, braced by stepped buttresses and capped
in team paint, carry a pair of heavy box girders across the bay, cross-braced, with a huge
trolley hanging the main nanolathe over the square double-rimmed pad. Four wall arms work the
frame from the sides; a furnace block with four glowing stacks closes the back, and caged
ladders, pipework and hazard stripes finish the yard. The bay opens to the door side (-y).
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels
import factories_parts as fp
from factories_parts import slab, beam, studs

OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else '.'
kit.reset('crucible')
F = 'crucible'
body = []
WI, WO, WH = 10.6, 14.0, 15.0

# ---- Floor, pad, door ----
body.append(box('floor', (0, 0, 0.3), (30.8, 30.8, 0.6), 'hull_dark', 0.08, 1))
body += fp.pad_square('pad', (0, 0, 0.6), 7.6)
body.append(box('padrim', (0, 0, 0.62), (19.6, 19.6, 0.04), 'mech', 0, 1))
for sd in (-1, 1):
    body.append(box(f'orx{sd}', (0, sd * 9.4, 0.66), (18.0, 0.3, 0.06), 'glow_dim', 0, 1))
    body.append(box(f'ory{sd}', (sd * 9.4, 0, 0.66), (0.3, 18.0, 0.06), 'glow_dim', 0, 1))
body.append(box('door', (0, -15.05, 0.65), (20.0, 0.4, 0.1), 'glow', 0, 1))
body += fp.hazard_strip('dhz', -10.0, 10.0, -14.4, 0.66, 0.8, 0.08, 16)

# ---- Walls ----
for sx in (-1, 1):
    xc = sx * (WI + WO) / 2
    w = box(f'wall{sx}', (xc, -0.4, WH / 2 + 0.3), (WO - WI, 29.2, WH - 0.6), 'hull', 0.1, 1)
    inset_panels(w, 0.35, 0.1, lambda f: abs(f.normal.z) < 0.5 and f.calc_area() > 20)
    body.append(w)
    body.append(box(f'wtop{sx}', (xc, -0.6, WH + 0.07), (WO - WI - 0.4, 29.2, 0.14), 'team', 0.03, 1))
    body += studs(f'rva{sx}', (sx * (WI + 0.25), -14.6, WH + 0.05), (sx * (WI + 0.25), 13.4, WH + 0.05), 13, 'metal', 0.18)
    body += studs(f'rvb{sx}', (sx * (WO - 0.25), -14.6, WH + 0.05), (sx * (WO - 0.25), 13.4, WH + 0.05), 13, 'metal', 0.18)
    # Team band low on the bay face, and riveted plates.
    body.append(box(f'wband{sx}', (sx * (WI - 0.04), -0.6, 1.8), (0.1, 28.0, 1.6), 'team_dark', 0, 1))
    # Stepped buttresses down the outer face, out to the footprint edge.
    for k in range(5):
        y = -12.0 + k * 6.0
        bt = hull_from(f'butt{sx}{k}', [(sx * WO, y + dy, z) for dy in (-0.9, 0.9) for z in (0.6, 12.0)] +
                       [(sx * 15.4, y + dy, z) for dy in (-0.9, 0.9) for z in (0.6, 4.0)], 'hull_dark', bevel=0.06)
        fp.two_tone(bt, 'hull_dark', 'team_dark', 0.3)
        body.append(bt)
    # Furnace vents on the bay face.
    for k in range(3):
        body.append(box(f'iv{sx}{k}', (sx * (WI - 0.04), -6.0 + k * 5.0, 6.2), (0.1, 1.6, 2.4), 'mech', 0, 1))
        body.append(box(f'ivg{sx}{k}', (sx * (WI - 0.06), -6.0 + k * 5.0, 6.2), (0.06, 1.2, 2.0), 'glow_dim', 0, 1))
    # Two wall arms per side.
    for i, (y, z) in enumerate(((-5.0, 4.0), (4.0, 8.0))):
        body.append(box(f'armbr{sx}{i}', (sx * (WI - 0.5), y, z - 0.35), (1.0, 2.2, 0.7), 'hull_dark', 0.05, 1))
        body += fp.nano_arm(f'arm{sx}{i}', (sx * (WI - 0.8), y, z), (0, 0, 2.0 + i * 2.0), 3.6, F)
    # Caged ladder on the front face and a lamp.
    body.append(box(f'wfront{sx}', (xc, -15.03, 6.0), (2.4, 0.1, 5.0), 'team', 0.03, 1))
    body.append(box(f'wlamp{sx}', (xc, -15.06, 10.0), (2.0, 0.1, 0.35), 'glow', 0, 1))
    for k in range(10):
        body.append(box(f'rung{sx}{k}', (sx * (WI + 0.6), -15.15, 1.4 + k * 1.3), (0.9, 0.08, 0.08), 'metal', 0, 1))
    for k in range(4):
        body.append(box(f'hoop{sx}{k}', (sx * (WI + 0.6), -15.42, 6.0 + k * 2.5), (1.4, 0.06, 0.08), 'mech', 0, 1))
    # Pipe run along the outer wall.
    body += fp.pipe_run(f'pipe{sx}', [(sx * (WO - 0.4), -13.0, WH + 0.6), (sx * (WO - 0.4), 12.0, WH + 0.6)], 0.32)

# ---- Furnace block with four stacks ----
fb = box('furnace', (0, 13.0, 6.3), (21.2, 4.8, 11.4), 'hull', 0.1, 1)
inset_panels(fb, 0.35, 0.1, lambda f: abs(f.normal.z) < 0.5 and f.calc_area() > 20)
body.append(fb)
body.append(box('ftop', (0, 13.0, 12.07), (20.8, 4.4, 0.14), 'team', 0.03, 1))
body.append(box('fmouth', (0, 10.55, 4.0), (14.0, 0.1, 6.4), 'mech', 0, 1))
body.append(box('fmouthg', (0, 10.5, 2.0), (12.4, 0.1, 1.6), 'glow', 0, 1))
body.append(box('fmouthg2', (0, 10.5, 4.0), (12.4, 0.1, 0.35), 'glow_dim', 0, 1))
body.append(box('fteam', (0, 10.5, 10.0), (19.0, 0.12, 3.0), 'team', 0.03, 1))
body.append(box('flintel', (0, 10.4, 7.6), (15.0, 0.6, 1.0), 'hull_dark', 0.05, 1))
body += studs('flr', (-7.0, 10.05, 7.6), (7.0, 10.05, 7.6), 11, 'metal', 0.17)
for x in (-7.5, -2.5, 2.5, 7.5):
    body += fp.stack(f'stack{x}', (x, 13.2, 12.0), 1.05, 5.8)
body += fp.pipe_run('fpipe', [(-9.6, 11.4, 12.6), (9.6, 11.4, 12.6)], 0.3)

# ---- Twin box girders, cross-bracing, and the main trolley ----
GYS = (-4.6, 2.2)
for gy in GYS:
    for sx in (-1, 1):
        body.append(box(f'gpost{gy}{sx}', (sx * 12.3, gy, WH + 0.9), (2.4, 2.0, 1.8), 'hull_dark', 0.05, 1))
    body.append(box(f'gird{gy}', (0, gy, WH + 2.4), (27.0, 1.8, 1.6), 'hull_dark', 0.06, 1))
    body.append(box(f'girdt{gy}', (0, gy, WH + 3.27), (26.6, 1.6, 0.14), 'team', 0.02, 1))
    body += studs(f'grv{gy}', (-12.6, gy - 0.92, WH + 2.4), (12.6, gy - 0.92, WH + 2.4), 13, 'metal', 0.14)
    body.append(box(f'grail{gy}', (0, gy, WH + 3.42), (24.0, 0.3, 0.16), 'metal', 0, 1))
for k in range(6):
    x = -12.0 + k * 4.8
    body.append(beam(f'gx{k}', (x, GYS[0], WH + 2.0), (x + 2.4, GYS[1], WH + 2.0), 0.4, 0.4, 'mech', 0.02))
TY = (GYS[0] + GYS[1]) / 2
body.append(box('trolley', (0, TY, WH + 2.6), (4.0, 8.4, 2.2), 'mech', 0.06, 1))
body.append(box('trolleyt', (0, TY, WH + 3.76), (3.6, 4.0, 0.14), 'team', 0.02, 1))
body.append(box('trolleycab', (2.6, TY - 1.8, WH + 1.6), (1.4, 1.8, 1.6), 'hull', 0.04, 1))
body.append(box('trolleycabw', (2.6, TY - 2.72, WH + 1.8), (1.0, 0.06, 0.6), 'glow_dim', 0, 1))
body.append(cyl('drum', (-1.6, TY, WH + 4.2), (1.6, TY, WH + 4.2), 0.7, 'accent', 12, bevel=0.04))
body.append(box('trolleyg', (0, TY, WH + 1.44), (2.4, 2.4, 0.06), 'glow_dim', 0, 1))
body += fp.nano_arm('tarm', (0, TY, WH + 1.4), (0, 0, 3.0), 4.4, F, hang=True)

fp.finish('crucible_experimental_gantry', OUT, body, s=5.0)

"""Crucible Heavy Works (advanced assembly). Run: python tools/models/crucible_advanced_assembly.py <out_dir>

The tier 2 works: tall riveted walls braced by sloping buttresses, a furnace block at the back
with a row of three glowing stacks, and a heavy portal crane over the front of the bay whose
trolley hangs a big nanolathe over the square pad. Wall arms and a steam manifold along the
wall tops complete it. Finished units leave by the door side (-y).
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
WI, WO, WH = 7.6, 10.2, 9.6

# ---- Floor, pad, door ----
body.append(box('floor', (0, 0, 0.25), (22.8, 22.8, 0.5), 'hull_dark', 0.06, 1))
body += fp.pad_square('pad', (0, 0, 0.5), 4.8)
body.append(box('door', (0, -11.05, 0.55), (14.0, 0.35, 0.1), 'glow', 0, 1))
body += fp.hazard_strip('dhz', -7.0, 7.0, -10.5, 0.56, 0.6, 0.06, 14)
for i in range(6):
    body.append(box(f'fseam{i}', (0, -9.4 + i * 1.3, 0.505), (12.0, 0.1, 0.02), 'mech', 0, 1))

# ---- Walls with buttresses, team tops, manifold ----
for sx in (-1, 1):
    xc = sx * (WI + WO) / 2
    w = box(f'wall{sx}', (xc, 0, WH / 2 + 0.25), (WO - WI, 22.8, WH - 0.5), 'hull', 0.08, 1)
    inset_panels(w, 0.3, 0.08, lambda f: abs(f.normal.z) < 0.5 and f.calc_area() > 15)
    body.append(w)
    body.append(box(f'wtop{sx}', (xc, 0, WH + 0.06), (WO - WI - 0.3, 22.4, 0.12), 'team', 0.02, 1))
    body += studs(f'rv{sx}', (sx * (WI + 0.2), -10.8, WH + 0.04), (sx * (WI + 0.2), 10.8, WH + 0.04), 11, 'metal', 0.15)
    # Buttresses: sloping wedges out to the footprint edge, team-capped.
    for k in range(4):
        y = -8.4 + k * 5.4
        bt = hull_from(f'butt{sx}{k}', [(sx * WO, y + dy, z) for dy in (-0.7, 0.7) for z in (0.5, 7.6)] +
                       [(sx * 11.4, y + dy, z) for dy in (-0.7, 0.7) for z in (0.5, 2.0)], 'hull_dark', bevel=0.05)
        fp.two_tone(bt, 'hull_dark', 'team_dark', 0.3)
        body.append(bt)
    # Steam manifold along the wall top, with valve wheels.
    body += fp.pipe_run(f'man{sx}', [(xc + sx * 0.5, -10.4, WH + 0.5), (xc + sx * 0.5, 6.0, WH + 0.5)], 0.28)
    for k in range(3):
        y = -7.0 + k * 5.0
        body.append(cyl(f'valve{sx}{k}', (xc + sx * 0.5, y, WH + 0.8), (xc + sx * 0.5, y, WH + 1.2), 0.15, 'metal', 6, bevel=0))
        body.append(cyl(f'vwheel{sx}{k}', (xc + sx * 0.5, y, WH + 1.2), (xc + sx * 0.5, y, WH + 1.28), 0.4, 'accent', 10, bevel=0))
    # Inner face: furnace vents and a heavy wall arm.
    for k in range(4):
        body.append(box(f'iv{sx}{k}', (sx * (WI - 0.04), -7.5 + k * 1.3, 3.0), (0.1, 0.9, 2.2), 'mech', 0, 1))
        body.append(box(f'ivg{sx}{k}', (sx * (WI - 0.06), -7.5 + k * 1.3, 3.0), (0.06, 0.6, 1.8), 'glow_dim', 0, 1))
    body.append(box(f'armbr{sx}', (sx * (WI - 0.45), 2.4, 4.0), (0.9, 2.0, 0.7), 'hull_dark', 0.05, 1))
    body += fp.nano_arm(f'arm{sx}', (sx * (WI - 0.7), 2.4, 4.35), (0, 0.4, 1.2), 3.2, F)
    # Front face of the wall: team plate and a lamp.
    body.append(box(f'wfront{sx}', (xc, -11.42, 3.0), (2.0, 0.1, 4.0), 'team', 0.03, 1))
    body.append(box(f'wlamp{sx}', (xc, -11.45, 7.0), (1.4, 0.1, 0.3), 'glow', 0, 1))

# ---- Furnace block with three stacks ----
fb = box('furnace', (0, 9.2, 5.35), (15.2, 4.4, 9.7), 'hull', 0.08, 1)
inset_panels(fb, 0.3, 0.08, lambda f: abs(f.normal.z) < 0.5 and f.calc_area() > 15)
body.append(fb)
body.append(box('ftop', (0, 9.2, 10.26), (14.8, 4.0, 0.12), 'team', 0.02, 1))
body.append(box('fmouth', (0, 6.95, 3.2), (9.0, 0.1, 5.0), 'mech', 0, 1))
body.append(box('fmouthg', (0, 6.9, 1.6), (8.0, 0.1, 1.2), 'glow', 0, 1))
body.append(box('fmouthg2', (0, 6.9, 3.0), (8.0, 0.1, 0.3), 'glow_dim', 0, 1))
body.append(box('fteam', (0, 6.95, 8.2), (14.0, 0.12, 2.8), 'team', 0.03, 1))
body.append(box('flintel', (0, 6.8, 6.0), (10.0, 0.5, 0.8), 'hull_dark', 0.04, 1))
body += studs('flr', (-4.6, 6.52, 6.0), (4.6, 6.52, 6.0), 9, 'metal', 0.15)
for x in (-4.6, 0.0, 4.6):
    body += fp.stack(f'stack{x}', (x, 9.4, 10.2), 1.0, 6.4)
body += fp.pipe_run('fpipe', [(-6.6, 8.0, 10.6), (6.6, 8.0, 10.6)], 0.25)

# ---- Portal crane over the front of the bay ----
GYS = (-8.2, -4.6)
for gy in GYS:
    for sx in (-1, 1):
        body.append(box(f'gpost{gy}{sx}', (sx * 8.9, gy, WH + 0.8), (1.6, 1.4, 1.6), 'hull_dark', 0.04, 1))
    body.append(box(f'gird{gy}', (0, gy, WH + 2.2), (19.6, 1.4, 1.2), 'hull_dark', 0.05, 1))
    body.append(box(f'girdt{gy}', (0, gy, WH + 2.86), (19.2, 1.2, 0.12), 'team', 0.02, 1))
    body += studs(f'grv{gy}', (-9.0, gy - 0.72, WH + 2.2), (9.0, gy - 0.72, WH + 2.2), 10, 'metal', 0.12)
for k in range(5):
    x = -8.0 + k * 4.0
    body.append(beam(f'gx{k}', (x, GYS[0], WH + 1.8), (x + 2.0, GYS[1], WH + 1.8), 0.3, 0.3, 'mech', 0.02))
TX = -1.8
body.append(box('trolley', (TX, (GYS[0] + GYS[1]) / 2, WH + 2.2), (2.4, 5.2, 1.6), 'mech', 0.05, 1))
body.append(box('trolleyt', (TX, (GYS[0] + GYS[1]) / 2, WH + 3.06), (2.2, 2.4, 0.12), 'team', 0.02, 1))
body.append(box('trolleyg', (TX, (GYS[0] + GYS[1]) / 2, WH + 1.36), (1.6, 1.6, 0.06), 'glow_dim', 0, 1))
body += fp.nano_arm('tarm', (TX, (GYS[0] + GYS[1]) / 2, WH + 1.4), (0, 0, 1.2), 3.4, F, hang=True)

fp.finish('crucible_advanced_assembly', OUT, body)

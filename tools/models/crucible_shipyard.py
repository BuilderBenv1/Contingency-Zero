"""Crucible Drydock (shipyard). Run: python tools/models/crucible_shipyard.py <out_dir>

Floats on the water (z = 0 is the surface): two massive riveted piers with team decks and
tyre fenders along the basin, a caisson block at the back with twin glowing stacks, square
pillars carrying two team-painted box girders, and a travelling crane bridge with a cab that
hangs a heavy nanolathe over the ship's frame. Open lock gates at the mouth (-y), striped in
hazard paint, let finished ships out.
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
PI, PO, PT, PB = 6.6, 11.3, 2.6, -1.6

# ---- Piers ----
for sx in (-1, 1):
    xc = sx * (PI + PO) / 2
    p = box(f'pier{sx}', (xc, 0, (PT + PB) / 2), (PO - PI, 22.6, PT - PB), 'hull', 0.08, 1)
    inset_panels(p, 0.3, 0.08, lambda f: abs(f.normal.z) < 0.5 and f.calc_area() > 10)
    body.append(p)
    body.append(box(f'deck{sx}', (xc, 0.3, PT + 0.06), (PO - PI - 0.8, 21.4, 0.12), 'team', 0.02, 1))
    body.append(box(f'kerbi{sx}', (sx * (PI + 0.25), 0, PT + 0.15), (0.5, 22.4, 0.3), 'hull_dark', 0.03, 1))
    body.append(box(f'kerbo{sx}', (sx * (PO - 0.25), 0, PT + 0.15), (0.5, 22.4, 0.3), 'hull_dark', 0.03, 1))
    body += studs(f'rv{sx}', (sx * (PI + 0.25), -10.6, PT + 0.3), (sx * (PI + 0.25), 10.6, PT + 0.3), 10, 'metal', 0.14)
    body += studs(f'rvo{sx}', (sx * (PO - 0.25), -10.6, PT + 0.3), (sx * (PO - 0.25), 10.6, PT + 0.3), 10, 'metal', 0.14)
    # Tyre fenders hung along the basin face.
    for i in range(7):
        y = -8.4 + i * 2.6
        body.append(cyl(f'tyre{sx}{i}', (sx * (PI - 0.3), y, 0.1), (sx * (PI - 0.3), y, 1.3), 0.45, 'tread', 10, bevel=0.06))
        body.append(cyl(f'chain{sx}{i}', (sx * (PI - 0.3), y, 1.3), (sx * (PI - 0.1), y, PT), 0.05, 'mech', 4, bevel=0))
    # Waterline glow at the pier ends.
    body.append(box(f'endg{sx}', (xc, -11.32, 1.8), (3.0, 0.08, 0.3), 'glow', 0, 1))
    # Bollards on the deck.
    for i in range(4):
        y = -9.0 + i * 5.2
        body.append(cyl(f'bol{sx}{i}', (sx * (PO - 1.2), y, PT), (sx * (PO - 1.2), y, PT + 0.6), 0.3, 'accent', 8, bevel=0.03))
        body.append(cyl(f'bolc{sx}{i}', (sx * (PO - 1.2), y, PT + 0.6), (sx * (PO - 1.2), y, PT + 0.75), 0.4, 'mech', 8, bevel=0.02))
    # Open lock gate leaf, folded back against the pier at the mouth.
    body.append(box(f'gate{sx}', (sx * (PI - 0.5), -9.6, 0.9), (0.6, 3.0, 3.4), 'hull_dark', 0.04, 1))
    for k in range(4):
        body.append(box(f'ghz{sx}{k}', (sx * (PI - 0.5), -10.75 + k * 0.75, 2.72), (0.62, 0.74, 0.25), 'accent' if k % 2 == 0 else 'tread', 0, 1))
    body.append(cyl(f'hinge{sx}', (sx * (PI - 0.3), -11.1, -0.6), (sx * (PI - 0.3), -11.1, 2.8), 0.25, 'metal', 8, bevel=0))

# ---- Caisson block at the back with twin stacks ----
cb = box('caisson', (0, 9.6, (4.6 + PB) / 2), (13.2, 3.8, 4.6 - PB), 'hull', 0.08, 1)
inset_panels(cb, 0.3, 0.08, lambda f: abs(f.normal.z) < 0.5 and f.calc_area() > 10)
body.append(cb)
body.append(box('caitop', (0, 9.6, 4.66), (12.6, 3.4, 0.12), 'team', 0.02, 1))
body.append(box('caiface', (0, 7.66, 1.4), (9.0, 0.1, 2.0), 'mech', 0, 1))
body.append(box('caiglow', (0, 7.62, 1.0), (8.0, 0.08, 0.5), 'glow', 0, 1))
body += studs('cair', (-5.8, 7.9, 4.72), (5.8, 7.9, 4.72), 9, 'metal', 0.14)
for sx in (-1, 1):
    body += fp.stack(f'stack{sx}', (sx * 4.0, 10.2, 4.6), 0.9, 4.2)
body += fp.pipe_run('cpipe', [(-4.0, 8.6, 5.4), (4.0, 8.6, 5.4)], 0.2)

# ---- Pillars and the two box girders ----
GYS = (-3.0, 5.4)
for gy in GYS:
    for sx in (-1, 1):
        body.append(box(f'pil{gy}{sx}', (sx * 9.0, gy, (PT + 8.8) / 2), (1.3, 1.3, 8.8 - PT), 'hull_dark', 0.05, 1))
        body.append(box(f'pilf{gy}{sx}', (sx * 9.0, gy, PT + 0.3), (2.0, 2.0, 0.6), 'hull', 0.04, 1))
        for k in range(2):
            body.append(box(f'pilb{gy}{sx}{k}', (sx * 9.0, gy, 4.6 + k * 2.4), (1.45, 1.45, 0.25), 'accent', 0.02, 1))
    g = box(f'girder{gy}', (0, gy, 9.4), (22.4, 1.5, 1.2), 'hull_dark', 0.05, 1)
    body.append(g)
    body.append(box(f'girdert{gy}', (0, gy, 10.06), (22.0, 1.3, 0.12), 'team', 0.02, 1))
    body += studs(f'grv{gy}', (-10.4, gy - 0.78, 9.4), (10.4, gy - 0.78, 9.4), 11, 'metal', 0.12)
    body.append(box(f'grail{gy}', (0, gy, 10.2), (20.0, 0.3, 0.16), 'metal', 0, 1))
# Cross bracing between the pillars along each pier.
for sx in (-1, 1):
    body.append(beam(f'brace{sx}a', (sx * 9.0, GYS[0], PT + 0.6), (sx * 9.0, GYS[1], 8.4), 0.35, 0.35, 'mech', 0.02))
    body.append(beam(f'brace{sx}b', (sx * 9.0, GYS[0], 8.4), (sx * 9.0, GYS[1], PT + 0.6), 0.35, 0.35, 'mech', 0.02))

# ---- Travelling crane: a truss bridge riding the girders, a cab, and a heavy hanging arm ----
CX = 1.4
body += fp.truss('crane', (CX, GYS[0] - 0.6, 11.0), (CX, GYS[1] + 0.6, 11.0), 1.6, 1.2, 'accent', n=6, r=0.08)
for gy in GYS:
    body.append(box(f'ctruck{gy}', (CX, gy, 10.55), (2.2, 1.6, 0.7), 'hull_dark', 0.04, 1))
body.append(box('ccab', (CX, 0.8, 10.6), (2.6, 2.4, 1.8), 'hull', 0.05, 1))
body.append(box('ccabt', (CX, 0.8, 11.56), (2.4, 2.2, 0.12), 'team', 0.02, 1))
body.append(box('ccabw', (CX, -0.42, 10.8), (2.0, 0.06, 0.6), 'glow_dim', 0, 1))
body += fp.nano_arm('arm', (CX, 0.8, 9.7), (0, 0.8, 0.6), 3.0, F, hang=True)

fp.finish('crucible_shipyard', OUT, body)

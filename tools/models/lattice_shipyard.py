"""Lattice Shipyard. Run: python tools/models/lattice_shipyard.py <out_dir>

Floats on the water (z = 0 is the surface): two long, sloped, team-decked piers reach out
from a dock block at the back, leaving open water between them where the ship's nanoframe
sits. A slipway runs down into the water from the block, two slim arched gantries span the
basin with nanolathe arms hanging from them, glow strips mark the pier faces at the waterline,
and lit buoys mark the mouth (-y) where finished ships sail out.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels
import factories_parts as fp
from factories_parts import slab, beam, ring

OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else '.'
kit.reset('lattice')
F = 'lattice'
body = []
PI, PO, PT, PB = 6.8, 11.2, 2.4, -1.6     # pier inner/outer x, deck top, keel

# ---- Piers: sloped hulls sitting in the water, team decks ----
for sx in (-1, 1):
    pts = []
    for x, xt in ((sx * PI, sx * PI), (sx * PO, sx * (PO - 0.9))):
        pts += [(x, -11.0, PB), (x, 11.2, PB), (x, 11.2, 0.9), (x, -11.0, 0.6),
                (xt, -9.6, PT), (xt, 11.2, PT)]
    pts += [(sx * PI, -11.2, 0.2)]
    p = hull_from(f'pier{sx}', pts, 'hull', bevel=0.1, segs=2)
    fp.two_tone(p, 'hull', 'team', 0.5)
    inset_panels(p, 0.22, 0.05, lambda f: f.calc_area() > 6 and abs(f.normal.z) < 0.6)
    body.append(p)
    # Waterline glow strips on the basin face and a dark band at the water.
    for i in range(5):
        body.append(box(f'wl{sx}{i}', (sx * (PI - 0.03), -7.6 + i * 3.6, 1.15), (0.1, 2.2, 0.2), 'glow', 0, 1))
    body.append(box(f'wband{sx}', (sx * (PI - 0.03), 0.0, 0.2), (0.12, 21.6, 0.4), 'hull_dark', 0, 1))
    # Deck detail: a kerb along the basin edge, bollards and deck plates between the ribs.
    body.append(box(f'kerb{sx}', (sx * (PI + 0.3), 0.3, PT + 0.1), (0.5, 20.6, 0.2), 'hull_dark', 0.03, 1))
    for i in range(5):
        y = -8.4 + i * 4.0
        body.append(cyl(f'bol{sx}{i}', (sx * (PI + 0.9), y, PT), (sx * (PI + 0.9), y, PT + 0.55), 0.22, 'metal', 8, bevel=0.02))
        body.append(box(f'rib{sx}{i}', (sx * 8.8, y + 2.0, PT + 0.04), (3.4, 0.2, 0.1), 'hull', 0.02, 1))
    body.append(box(f'prowg{sx}', (sx * 8.8, -9.75, 2.0), (3.0, 0.12, 0.14), 'glow', 0, 1))
    # Outer face windows.
    for i in range(4):
        body.append(box(f'win{sx}{i}', (sx * (PO - 0.02), -6.0 + i * 4.2, 0.9), (0.1, 2.4, 0.35), 'glass', 0.02, 1))

# ---- Dock block at the back with a team roof, lit slot and a slipway into the basin ----
db = slab('dock', (0, 9.4, 1.3), (13.8, 3.6, 5.8), 'hull', 'team', chamfer=0.5, bevel=0.1, segs=2)
inset_panels(db, 0.22, 0.05, lambda f: f.calc_area() > 6)
body.append(db)
body.append(box('dslot', (0, 7.58, 3.2), (8.0, 0.1, 0.25), 'glow', 0, 1))
for k in range(5):
    body.append(box(f'dwin{k}', (-4.8 + k * 2.4, 7.58, 2.4), (1.6, 0.08, 0.6), 'glass', 0.02, 1))
slip = hull_from('slip', [(x, y, z) for x in (-4.2, 4.2) for y, z in ((7.6, 0.6), (7.6, -0.6), (2.6, -1.0))], 'hull_dark', bevel=0.04)
body.append(slip)
for sx in (-1, 1):
    body.append(beam(f'sliprail{sx}', (sx * 2.4, 7.6, 0.65), (sx * 2.4, 2.8, -0.95), 0.25, 0.12, 'metal', 0))
    body.append(beam(f'slipg{sx}', (sx * 4.0, 7.6, 0.62), (sx * 4.0, 2.8, -0.98), 0.12, 0.1, 'glow_dim', 0))
# Roof sensor and mast.
body.append(slab('dsens', (-3.6, 9.6, 4.45), (3.0, 1.8, 0.5), 'hull_dark', 'hull', chamfer=0.2, bevel=0.03))
body.append(box('dsensg', (-3.6, 8.68, 4.45), (2.2, 0.06, 0.16), 'glow', 0, 1))
body.append(cyl('mast', (4.0, 9.8, 4.2), (4.0, 9.8, 8.6), 0.15, 'metal', 8, r2=0.08, bevel=0))
body.append(sphere('mastorb', (4.0, 9.8, 8.7), 0.32, 'glow', 10, 6))

# ---- Two arched gantries over the basin, each hanging a nanolathe arm ----
for g, gy in enumerate((-3.6, 3.0)):
    for sx in (-1, 1):
        for dy in (-1.1, 1.1):
            body.append(beam(f'leg{g}{sx}{dy}', (sx * 9.4, gy + dy, PT), (sx * 8.0, gy + dy * 0.3, 8.6), 0.5, 0.5, 'hull', 0.04))
        body.append(slab(f'foot{g}{sx}', (sx * 9.4, gy, PT + 0.2), (1.4, 3.0, 0.4), 'hull_dark', 'hull', chamfer=0.15, bevel=0.03))
        body.append(box(f'legg{g}{sx}', (sx * 8.75, gy, 5.5), (0.12, 0.3, 4.0), 'glow_dim', 0, 1))
    # The bridge: a chamfered slab, lower edge arched by two sloping soffits.
    br = slab(f'bridge{g}', (0, gy, 9.15), (18.0, 1.6, 1.1), 'hull', 'team', chamfer=0.3, bevel=0.05)
    body.append(br)
    for sx in (-1, 1):
        body.append(hull_from(f'soffit{g}{sx}', [(x, gy + dy, z) for dy in (-0.5, 0.5) for x, z in
                                                 ((sx * 8.0, 8.6), (sx * 8.0, 7.4), (sx * 2.0, 8.6))], 'hull_dark', bevel=0.03))
    body.append(box(f'bglow{g}', (0, gy - 0.82, 9.0), (12.0, 0.06, 0.12), 'glow', 0, 1))
    body.append(slab(f'pod{g}', (0, gy, 8.35), (2.0, 1.8, 0.5), 'hull_dark', 'hull', chamfer=0.1, bevel=0.03))
    body += fp.nano_arm(f'arm{g}', (0, gy, 8.1), (0, gy * 0.3, 0.6), 2.6, F, hang=True)

# ---- Buoys marking the basin mouth ----
for sx in (-1, 1):
    body.append(cyl(f'buoy{sx}', (sx * 5.4, -10.6, -0.4), (sx * 5.4, -10.6, 0.6), 0.5, 'team', 10, bevel=0.03))
    body.append(cyl(f'buoyp{sx}', (sx * 5.4, -10.6, 0.6), (sx * 5.4, -10.6, 1.6), 0.12, 'metal', 6, bevel=0))
    body.append(sphere(f'buoyl{sx}', (sx * 5.4, -10.6, 1.7), 0.25, 'glow', 8, 5))

fp.finish('lattice_shipyard', OUT, body)

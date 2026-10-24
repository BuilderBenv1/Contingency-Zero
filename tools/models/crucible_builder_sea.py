"""Crucible "Dredger" naval constructor. Run: python tools/models/crucible_builder_sea.py <out_dir>

A riveted dredging barge: a bucket ladder hung from a team A-frame over the bow and dipping into
the water, a team cargo hatch amidships, a hydraulic nanolathe crane with an orange-glowing tip,
an aft deckhouse with a team roof, a lit window slot and a hot smokestack, tyre fenders and
bollards. The waterline is z = 0: the hull dips below it.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
from mathutils import Vector
import airsea_parts as A

kit.reset('crucible')
F = 'crucible'
HW, DECK, KEEL = 0.44, 0.24, -0.14
body, secs = A.ship_hull(F, HW, DECK, KEEL)
V = Vector
for sx in A.SIDES:
    body += rivets(f'drv{sx}', (sx * HW * 0.82, 0.85, DECK + 0.005), (sx * HW * 0.82, -1.0, DECK + 0.005), 9, 'metal', 0.016)
    for k, y in enumerate((0.3, -0.15, -0.6)):
        body.append(cyl(f'tyre{sx}{k}', (sx * (HW + 0.005), y, 0.12), (sx * (HW + 0.05), y, 0.12), 0.07, 'tread', 10, bevel=0.01))
    body.append(box(f'bitt{sx}', (sx * 0.32, -0.95, DECK + 0.03), (0.06, 0.06, 0.06), 'metal', 0.008, 1))

# ---- Aft deckhouse ----
dh = box('deckhouse', (0, -0.64, 0.4), (0.48, 0.42, 0.32), 'hull', 0.02, 2)
inset_panels(dh, 0.03, 0.008, lambda f: abs(f.normal.x) > 0.9)
body.append(dh)
roof = box('roof', (0, -0.64, 0.575), (0.52, 0.46, 0.035), 'team', 0.01, 1)
inset_panels(roof, 0.03, 0.006, lambda f: f.normal.z > 0.9)
body.append(roof)
body.append(box('window', (0, -0.425, 0.48), (0.38, 0.02, 0.05), 'glow_dim', 0, 1))
for x in (-0.09, 0.0, 0.09):
    body.append(box(f'wbar{x}', (x, -0.42, 0.48), (0.02, 0.02, 0.06), 'mech', 0, 1))
body += rivets('rr', (-0.23, -0.44, 0.6), (0.23, -0.44, 0.6), 6, 'metal', 0.016)
body.append(cyl('stack', (-0.14, -0.76, 0.59), (-0.14, -0.76, 0.86), 0.055, 'metal', 10, r2=0.048, bevel=0.004))
body.append(cyl('stband', (-0.14, -0.76, 0.68), (-0.14, -0.76, 0.73), 0.06, 'accent', 10, bevel=0))
body.append(cyl('strim', (-0.14, -0.76, 0.84), (-0.14, -0.76, 0.88), 0.058, 'mech', 10, bevel=0))
body.append(cyl('stglow', (-0.14, -0.76, 0.875), (-0.14, -0.76, 0.882), 0.045, 'glow', 10, bevel=0))
body.append(cyl('mast', (0.14, -0.7, 0.59), (0.14, -0.7, 0.86), 0.012, 'metal', 6, bevel=0))
body.append(sphere('masttip', (0.14, -0.7, 0.87), 0.02, 'glow', 8, 4))

# ---- Cargo hatch amidships ----
hatch = box('hatch', (0, 0.06, DECK + 0.06), (0.5, 0.4, 0.12), 'hull_dark', 0.014, 1)
body.append(hatch)
hl = box('hatchlid', (0, 0.06, DECK + 0.13), (0.46, 0.36, 0.025), 'team', 0.006, 1)
inset_panels(hl, 0.025, 0.006, lambda f: f.normal.z > 0.9)
body.append(hl)
for k in range(3):
    body.append(box(f'hrib{k}', (0, -0.06 + k * 0.12, DECK + 0.145), (0.47, 0.025, 0.012), 'accent', 0.003, 1))

# ---- Bucket ladder over the bow, hung from a team A-frame ----
top, bot = V((0, 0.5, 0.36)), V((0, 1.26, -0.1))
body.append(A.beam('ladder', top, bot, 0.14, 0.06, 'metal', bevel=0.008))
for sx in A.SIDES:
    body.append(A.beam(f'lrail{sx}', top + V((sx * 0.08, 0, 0.02)), bot + V((sx * 0.08, 0, 0.02)), 0.025, 0.05, 'mech', bevel=0.004))
d = (bot - top).normalized()
n = V((0, -d.z, d.y))
for i in range(6):
    p = top.lerp(bot, 0.08 + i * 0.16) + n * 0.06
    b = box(f'bucket{i}', tuple(p), (0.2, 0.08, 0.07), 'accent', 0.008, 1)
    A.xform([b], tuple(p), (math.atan2(d.z, d.y), 0, 0))
    body.append(b)
    body.append(box(f'lip{i}', tuple(p + n * 0.035 + d * 0.03), (0.2, 0.02, 0.02), 'metal', 0, 1))
body.append(cyl('tumbler', (-0.1, 0.5, 0.36), (0.1, 0.5, 0.36), 0.06, 'mech', 10, bevel=0.004))
for sx in A.SIDES:
    body.append(A.beam(f'aframe{sx}', (sx * 0.32, 0.72, DECK), (sx * 0.04, 0.66, 0.6), 0.05, 0.05, 'team', bevel=0.006))
body.append(A.beam('aframetop', (-0.08, 0.66, 0.6), (0.08, 0.66, 0.6), 0.05, 0.06, 'team', bevel=0.006))
body.append(cyl('hoist', (0, 0.66, 0.58), top + V((0, 0.25, 0.04)), 0.008, 'metal', 4, bevel=0))
body.append(box('chute', (0, 0.36, DECK + 0.06), (0.16, 0.2, 0.08), 'hull_dark', 0.008, 1))

# ---- Hydraulic nanolathe crane ----
base = V((0.2, -0.26, DECK))
body.append(cyl('cbase', base, base + V((0, 0, 0.08)), 0.11, 'mech', 12, r2=0.09, bevel=0.006))
sh, el, wr = base + V((0, 0, 0.1)), base + V((0, 0.18, 0.5)), base + V((-0.06, 0.6, 0.36))
body.append(A.beam('cboom', sh, el, 0.09, 0.1, 'accent', bevel=0.01))
body.append(box('celbow', tuple(el), (0.1, 0.1, 0.1), 'mech', 0.01, 1))
body.append(A.beam('cfore', el, wr, 0.07, 0.08, 'hull_dark', bevel=0.008))
body.append(cyl('cram', sh + V((0, 0.06, 0.0)), el.lerp(wr, 0.3), 0.022, 'metal', 6, bevel=0))
body += A.nano_tip('ctip', wr, wr + V((0, 0.12, -0.1)), 0.04)

A.finish('crucible_builder_sea', [('Body', body, None)], 512)

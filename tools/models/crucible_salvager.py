"""Crucible "Scrapper" salvager. Run: python tools/models/crucible_salvager.py <out_dir>

A scrap-eating tractor on short tracks: a toothed grinder drum spans the front on heavy arms,
a feed chute runs back to a big team-painted scrap hopper heaped with twisted metal, and a
furnace stack glows behind the cab. No weapons.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import vehicles_parts as vp

kit.reset('crucible')
body = []
body += vp.crucible_tracks(0.44, 0.76, -1.05, 0.84, r_end=0.19, wheels=5, cleats=18, skirt='team_dark', skirt_top=0.44, skirt_rivets=7)

HW = 0.44
body.append(box('tub', (0, -0.1, 0.27), (2 * HW, 1.8, 0.24), 'hull_dark', 0.02))
upper = prism_x('upper', [(0.84, 0.3), (0.68, 0.54), (-0.98, 0.54), (-1.02, 0.46), (-1.02, 0.28), (0.84, 0.28)],
                -0.56, 0.56, 'hull', 0.025, 2)
body.append(upper)
body += rivets('frv', (-0.48, 0.66, 0.555), (0.48, 0.66, 0.555), 7, 'metal', 0.018)

# ---- Grinder drum across the front ----
DY, DZ = 1.06, 0.3
body.append(cyl('drum', (-0.62, DY, DZ), (0.62, DY, DZ), 0.2, 'metal', 16, bevel=0.01))
for sx in (-1, 1):
    body.append(cyl(f'drumcap{sx}', (sx * 0.62, DY, DZ), (sx * 0.66, DY, DZ), 0.22, 'mech', 16, bevel=0.006))
# Teeth: staggered rows of chisel teeth round the drum.
for i in range(8):
    a = i * 2 * math.pi / 8
    for j in range(5):
        x = -0.5 + j * 0.25 + (0.12 if i % 2 else 0)
        if x > 0.58:
            continue
        c = (x, DY + math.cos(a) * 0.21, DZ + math.sin(a) * 0.21)
        tip = (x, DY + math.cos(a + 0.35) * 0.29, DZ + math.sin(a + 0.35) * 0.29)
        body.append(vp.tapered(f'tooth{i}{j}', c, tip, 0.06, 0.05, 0.02, 0.02, 'accent', 0.004))
# Arms from the hull to the drum ends, with rams and a guard hood over the top.
for sx in (-1, 1):
    body.append(vp.beam(f'darm{sx}', (sx * 0.56, 0.5, 0.42), (sx * 0.7, DY, DZ), 0.1, 0.14, 'hull_dark'))
    body.append(cyl(f'dram{sx}', (sx * 0.4, 0.6, 0.55), (sx * 0.56, 0.92, 0.48), 0.035, 'metal', 8, bevel=0))
    body.append(cyl(f'dramc{sx}', (sx * 0.4, 0.6, 0.55), (sx * 0.46, 0.74, 0.53), 0.05, 'mech', 8, bevel=0))
hood = prism_x('hood', [(0.8, 0.46), (0.98, 0.6), (1.18, 0.56), (1.24, 0.46), (1.2, 0.44), (1.0, 0.54), (0.82, 0.42)],
               -0.6, 0.6, 'team', 0.01, 1)
body.append(hood)
body += rivets('hoodrv', (-0.54, 1.04, 0.59), (0.54, 1.04, 0.59), 7, 'metal', 0.016)
body.append(box('hazard', (0, 1.2, 0.5), (1.16, 0.03, 0.05), 'accent', 0.004, 1))

# ---- Feed chute and cab ----
body.append(prism_x('chute', [(0.82, 0.48), (0.82, 0.62), (0.16, 0.86), (0.08, 0.86), (0.08, 0.72), (0.7, 0.5)],
                    -0.16, 0.16, 'hull_dark', 0.01, 1))
for k in range(4):
    body.append(box(f'chslat{k}', (0, 0.72 - k * 0.15, 0.6 + k * 0.06), (0.26, 0.03, 0.03), 'metal', 0.004, 1))
body.append(box('cab', (0.34, 0.38, 0.7), (0.24, 0.32, 0.32), 'hull', 0.02, 2))
body.append(box('cabglass', (0.34, 0.545, 0.76), (0.18, 0.01, 0.1), 'glass', 0, 1))
body.append(box('cabroof', (0.34, 0.38, 0.875), (0.27, 0.35, 0.03), 'team', 0.008, 1))
body.append(sphere('beacon', (0.4, 0.3, 0.91), 0.03, 'glow', 8, 5))

# ---- Scrap hopper ----
hop = hull_from('hopper', [(-0.5, 0.18, 0.54), (0.5, 0.18, 0.54), (-0.5, -1.0, 0.54), (0.5, -1.0, 0.54),
                           (-0.54, 0.22, 1.0), (0.54, 0.22, 1.0), (-0.54, -1.04, 1.0), (0.54, -1.04, 1.0)], 'team', 0.025, 2)
inset_panels(hop, 0.04, 0.012, lambda f: abs(f.normal.x) > 0.8 or abs(f.normal.y) > 0.8)
body.append(hop)
# Hopper ribs and a top lip.
for y in (0.1, -0.41, -0.92):
    for sx in (-1, 1):
        body.append(box(f'hrib{sx}{y}', (sx * 0.55, y, 0.77), (0.04, 0.06, 0.46), 'hull_dark', 0.008, 1))
body.append(box('lip', (0, -0.41, 1.0), (1.1, 1.28, 0.04), 'hull_dark', 0.008, 1))
body.append(box('pit', (0, -0.41, 1.015), (0.96, 1.14, 0.02), 'mech', 0.004, 1))
# Heap of twisted scrap in the hopper.
import random
rnd = random.Random(7)
for i in range(14):
    x, y = rnd.uniform(-0.38, 0.38), rnd.uniform(-0.9, 0.08)
    s = rnd.uniform(0.07, 0.15)
    pts = [(x + rnd.uniform(-s, s), y + rnd.uniform(-s, s), 1.02 + rnd.uniform(0, s * 0.8)) for _ in range(7)]
    body.append(hull_from(f'scrap{i}', pts, rnd.choice(('metal', 'metal', 'hull', 'accent', 'mech')), 0.005, 1))
body.append(vp.beam('girder', (-0.3, -0.7, 1.06), (0.32, -0.2, 1.12), 0.05, 0.05, 'accent', 0.004))
body.append(box('furnaceg', (0, -1.05, 0.68), (0.4, 0.02, 0.06), 'glow', 0, 1))
body += vp.stack('stack', (-0.36, 0.42, 0.54), 0.07, 0.52)

vp.finish('crucible_salvager', body)

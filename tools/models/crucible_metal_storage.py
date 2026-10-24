"""Crucible Metal Storage: four banded, riveted holding tanks on a steel apron. Run: python <this> <out_dir>

Round tanks with iron hoops and team-painted conical lids, a manifold of pipes meeting at a
valve house with a handwheel, a catwalk across the lids with a ladder, and stacked ingots on
pallets at the front. Footprint 2 (16 units): kept inside 15 across.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('crucible')
body = []
E = 7.2
body.append(box('apron', (0, 0, 0.25), (2 * E, 2 * E, 0.5), 'hull_dark', 0.08))
for s in (-1, 1):
    body += ep.rivet_line(f'arx{s}', (-E + 0.4, s * (E - 0.25), 0.5), (E - 0.4, s * (E - 0.25), 0.5), 12, 'metal', 0.07)
TR = 2.75
TH = 3.4
for i, (sx, sy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
    c = (sx * 3.55, sy * 3.55)
    body.append(cyl(f'tank{i}', (c[0], c[1], 0.5), (c[0], c[1], 0.5 + TH), TR, 'hull', 20, bevel=0.04))
    body.append(cyl(f'skirt{i}', (c[0], c[1], 0.5), (c[0], c[1], 0.9), TR + 0.15, 'hull_dark', 20, bevel=0.03))
    for z in (1.6, 2.9):
        body.append(cyl(f'hoop{i}{z}', (c[0], c[1], z), (c[0], c[1], z + 0.22), TR + 0.08, 'mech', 20, bevel=0.01))
    body += ep.rivet_ring(f'hr{i}a', c, TR + 0.09, 1.71, 14, 'metal', 0.06)
    lid = cyl(f'lid{i}', (c[0], c[1], 0.5 + TH), (c[0], c[1], 0.5 + TH + 0.9), TR + 0.05, 'team', 20, r2=1.0, bevel=0.03)
    body.append(lid)
    body.append(cyl(f'lidrim{i}', (c[0], c[1], 0.5 + TH - 0.05), (c[0], c[1], 0.5 + TH + 0.12), TR + 0.14, 'mech', 20, bevel=0.01))
    body += ep.rivet_ring(f'lr{i}', c, TR - 0.1, 0.5 + TH + 0.2, 12, 'metal', 0.07)
    body.append(cyl(f'manhole{i}', (c[0], c[1], 0.5 + TH + 0.85), (c[0], c[1], 0.5 + TH + 1.15), 0.8, 'hull_dark', 12, bevel=0.02))
    body.append(cyl(f'mhcap{i}', (c[0], c[1], 0.5 + TH + 1.15), (c[0], c[1], 0.5 + TH + 1.22), 0.6, 'accent', 12, bevel=0.01))
    # Level window on the outward face.
    body.append(box(f'level{i}', (c[0] + sx * 1.95, c[1] + sy * 1.95, 2.2), (0.25, 0.25, 1.2), 'glow_dim', 0, 1))
# ---- Pipe manifold to the central valve house ----
body.append(box('valve', (0, 0, 1.6), (1.6, 1.6, 2.2), 'hull', 0.06))
body.append(box('valvetop', (0, 0, 2.75), (1.8, 1.8, 0.14), 'accent', 0.03))
for i, (sx, sy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
    body += ep.pipe(f'man{i}', [(sx * 0.75, sy * 0.75, 2.2), (sx * 1.6, sy * 1.6, 2.2), (sx * 1.6, sy * 1.6, 3.0), (sx * 2.2, sy * 2.2, 3.0)], 0.2, 'metal')
    body.append(cyl(f'flange{i}', (sx * 1.1, sy * 1.1, 2.2), (sx * 1.25, sy * 1.25, 2.2), 0.3, 'mech', 10, bevel=0))
body.append(cyl('wheelax', (0, -0.8, 1.9), (0, -1.15, 1.9), 0.06, 'metal', 6, bevel=0))
body.append(ep.ring('handwheel', (0, 0), 0.5, 0.4, -0.05, 0.05, 'accent', 12))
body[-1].data.transform(Matrix.Translation((0, -1.18, 1.9)) @ Matrix.Rotation(math.pi / 2, 4, 'X'))
body.append(box('gaugebox', (0.5, -0.82, 2.25), (0.4, 0.1, 0.4), 'mech', 0.02, 1))
body.append(cyl('gauge', (0.5, -0.88, 2.25), (0.5, -0.9, 2.25), 0.14, 'glow', 10, bevel=0))
# ---- Catwalk across the lids (east-west at the back) and a ladder ----
cz = 0.5 + TH + 1.0
body.append(box('catwalk', (0, 3.55, cz), (5.6, 0.8, 0.1), 'hull', 0.02))
body += ep.rail('cwrail', [(-2.8, 3.15, cz), (2.8, 3.15, cz)], 0.6, 'accent', post_every=1, r=0.035)
body += ep.ladder('ladder', (3.55 + 0.0, 3.55 - 2.95, 0.5), cz - 0.4, 'mech', w=0.45, normal=(0, -1, 0))
# ---- Pallets of ingots at the front ----
for k, x in enumerate((-1.3, 1.3)):
    body.append(box(f'pallet{k}', (x, -6.3, 0.62), (1.6, 1.0, 0.24), 'accent', 0.02))
    for row in range(2):
        for j in range(3):
            body.append(hull_from(f'ingot{k}{row}{j}', [(x - 0.65 + j * 0.45 + dx * 0.18, -6.3 + dy * 0.4, 0.74 + row * 0.22 + 0.0)
                                                          for dx in (-1, 1) for dy in (-1, 1)] +
                                  [(x - 0.65 + j * 0.45 + dx * 0.12, -6.3 + dy * 0.33, 0.74 + row * 0.22 + 0.2) for dx in (-1, 1) for dy in (-1, 1)],
                                  'metal', 0.02, 1))

B = kit.join('Body', body)
ep.finish('crucible_metal_storage', [B], 1024, bake_scale=0.3)

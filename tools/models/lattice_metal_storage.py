"""Lattice Metal Storage: a squat octagonal silo under a faceted team dome. Run: python <this> <out_dir>

Panelled octagonal walls on a bevelled footing, a lit seam and a glowing fill gauge, four
buttress slabs on the diagonals, a faceted dome with a sensor hatch, and a loading port at
the front. Footprint 2 (16 units): kept inside 15 across.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('lattice')
body = []
P8 = math.pi / 8
oct_ = lambda r, z, ph=P8: [(math.cos(ph + k * math.pi / 4) * r, math.sin(ph + k * math.pi / 4) * r, z) for k in range(8)]
# ---- Footing and walls ----
body.append(ep.ngon('footing', (0, 0), 7.1, 6.7, 0.0, 0.6, 8, 'hull_dark', bevel=0.08, segs=2))
body.append(ep.ring('footglow', (0, 0), 7.13, 6.9, 0.28, 0.36, 'glow', 8, phase=P8))
wall = hull_from('wall', oct_(5.75, 0.6) + oct_(5.55, 3.3), 'hull', 0.06)
inset_panels(wall, 0.18, 0.05, lambda f: abs(f.normal.z) < 0.5)
body.append(wall)
body.append(ep.ring('seam', (0, 0), 5.7, 5.2, 3.3, 3.42, 'glow', 8, phase=P8))
# ---- Faceted dome ----
dome = hull_from('dome', oct_(5.6, 3.42) + oct_(4.7, 4.55, P8) + oct_(2.9, 5.4, P8), 'team', 0.06)
inset_panels(dome, 0.16, 0.05, lambda f: f.normal.z > 0.2 and f.calc_area() > 1.0)
body.append(dome)
# Ribs between dome facets.
for k in range(8):
    a = k * math.pi / 4 + P8
    body.append(ep.beam(f'rib{k}', (math.cos(a) * 5.6, math.sin(a) * 5.6, 3.5), (math.cos(a) * 4.72, math.sin(a) * 4.72, 4.62), 0.22, 0.14, 'hull', 0.02))
    body.append(ep.beam(f'ribb{k}', (math.cos(a) * 4.72, math.sin(a) * 4.72, 4.62), (math.cos(a) * 2.9, math.sin(a) * 2.9, 5.47), 0.2, 0.12, 'hull', 0.02))
# Crown hatch with a sensor ring and a mast.
body.append(ep.ngon('crown', (0, 0), 2.0, 1.6, 5.35, 5.8, 8, 'hull', bevel=0.04))
body.append(ep.ring('crownglow', (0, 0), 1.62, 1.35, 5.8, 5.86, 'glow', 8, phase=P8))
body.append(ep.ngon('hatch', (0, 0), 1.3, 1.1, 5.8, 6.05, 8, 'mech', bevel=0.02))
body.append(box('hatchbar', (0, 0, 6.08), (1.6, 0.18, 0.08), 'metal', 0.01, 1))
body.append(ep.rod('mast', (1.3, 1.0, 5.6), (1.3, 1.0, 7.0), 0.05, 'mech', 6))
body.append(sphere('masttip', (1.3, 1.0, 7.02), 0.1, 'glow', 6, 4))
# ---- Buttresses on the diagonals ----
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    ca, sa = math.cos(a), math.sin(a)
    perp = (-sa, ca)
    pts = []
    for r, z0, z1, w in ((6.75, 0.6, 1.1, 0.9), (5.45, 0.6, 3.7, 0.7)):
        for s in (-1, 1):
            for z in (z0, z1):
                pts.append((ca * r + perp[0] * s * w / 2, sa * r + perp[1] * s * w / 2, z))
    body.append(hull_from(f'butt{k}', pts, 'hull_dark', 0.04))
    tp = []
    for r, z, w in ((6.7, 1.15, 0.6), (5.55, 3.75, 0.5)):
        for s in (-1, 1):
            for dz in (0, 0.08):
                tp.append((ca * r + perp[0] * s * w / 2, sa * r + perp[1] * s * w / 2, z + dz))
    body.append(hull_from(f'butttop{k}', tp, 'team', 0.01, 1))
    body.append(box(f'buttlight{k}', (ca * 6.85, sa * 6.85, 0.95), (0.18, 0.18, 0.12), 'glow', 0, 1))
# ---- Fill gauge: glowing segments up the right wall ----
for i in range(5):
    z = 1.0 + i * 0.42
    g = box(f'gauge{i}', (0, 0, 0), (0.7, 0.06, 0.24), 'glow' if i < 3 else 'glow_dim', 0, 1)
    a = -math.pi / 4 + math.pi / 8 * 0  # on the front-right facet
    g.data.transform(Matrix.Translation((math.cos(-math.pi / 4) * 5.5, math.sin(-math.pi / 4) * 5.5, z)) @ Matrix.Rotation(math.pi / 4, 4, 'Z'))
    body.append(g)
# ---- Loading port at the front ----
body.append(box('port', (0, -5.75, 1.45), (2.6, 0.8, 1.7), 'hull_dark', 0.06))
body.append(box('portdoor', (0, -6.17, 1.35), (1.9, 0.06, 1.3), 'mech', 0.02, 1))
for i in range(4):
    body.append(box(f'portslat{i}', (0, -6.22, 0.85 + i * 0.32), (1.8, 0.04, 0.06), 'metal', 0, 1))
body.append(box('portlamp', (0, -6.2, 2.15), (1.2, 0.04, 0.1), 'glow', 0, 1))
body.append(hull_from('ramp', [(-1.1, -6.2, 0.6), (1.1, -6.2, 0.6), (-1.1, -7.0, 0.0), (1.1, -7.0, 0.0),
                               (-1.1, -6.2, 0.0), (1.1, -6.2, 0.0)], 'hull', 0.03))
body.append(box('porttop', (0, -5.85, 2.36), (2.8, 1.0, 0.12), 'team', 0.02))

B = kit.join('Body', body)
ep.finish('lattice_metal_storage', [B], 1024, bake_scale=0.3)

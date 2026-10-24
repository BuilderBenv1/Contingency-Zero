"""Lattice Energy Storage: a capacitor spire ringed with glowing coils. Run: python <this> <out_dir>

An octagonal column on a bevelled footing, three stacked glowing coil rings held off the column
by insulator spokes, a faceted team crown with a discharge node, and three team-painted
capacitor pylons tied to the column by bus bars. Footprint 2 (16 units): within 15 across.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('lattice')
body = []
P8 = math.pi / 8
oct_ = lambda r, z: [(math.cos(P8 + k * math.pi / 4) * r, math.sin(P8 + k * math.pi / 4) * r, z) for k in range(8)]
body.append(ep.ngon('footing', (0, 0), 6.9, 6.4, 0.0, 0.7, 8, 'hull_dark', bevel=0.08, segs=2))
body.append(ep.ring('footteam', (0, 0), 6.35, 4.2, 0.7, 0.82, 'team', 8, phase=P8, bevel=0.02))
col = hull_from('column', oct_(3.4, 0.7) + oct_(2.9, 6.3), 'hull', 0.06)
inset_panels(col, 0.14, 0.05, lambda f: abs(f.normal.z) < 0.5)
body.append(col)
# Coil rings: glowing bands with dark clamps, held off the column on spokes.
for j, z in enumerate((1.9, 3.4, 4.9)):
    rr = 3.35 - (z - 0.7) / 5.6 * 0.5
    body.append(ep.ring(f'coil{j}', (0, 0), rr + 0.75, rr + 0.35, z, z + 0.4, 'glow', 24))
    body.append(ep.ring(f'coilin{j}', (0, 0), rr + 0.36, rr + 0.05, z + 0.05, z + 0.35, 'mech', 8, phase=P8))
    for k in range(4):
        a = k * math.pi / 2 + j * math.pi / 4
        body.append(box(f'clamp{j}{k}', (0, 0, 0), (0.5, 0.45, 0.62), 'hull_dark', 0.03))
        body[-1].data.transform(Matrix.Translation((math.cos(a) * (rr + 0.55), math.sin(a) * (rr + 0.55), z + 0.2)) @ Matrix.Rotation(a, 4, 'Z'))
# Crown: faceted team cap, ribbed, with a glowing discharge node.
crown = hull_from('crown', oct_(3.05, 6.3) + oct_(2.4, 7.2) + oct_(1.0, 7.65), 'team', 0.05)
inset_panels(crown, 0.12, 0.04, lambda f: f.normal.z > 0.2 and f.calc_area() > 0.6)
body.append(crown)
body.append(ep.ngon('nodebase', (0, 0), 0.75, 0.55, 7.6, 8.0, 8, 'mech'))
body.append(sphere('node', (0, 0, 8.35), 0.48, 'glow', 10, 6))
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    body.append(ep.beam(f'nodeprong{k}', (math.cos(a) * 0.55, math.sin(a) * 0.55, 7.9), (math.cos(a) * 0.4, math.sin(a) * 0.4, 8.75), 0.1, 0.1, 'metal', 0.01))
# ---- Three capacitor pylons ----
for i in range(3):
    a = i * 2 * math.pi / 3 + math.pi / 2 + math.pi / 3
    ca, sa = math.cos(a), math.sin(a)
    c = (ca * 5.35, sa * 5.35)
    body.append(ep.ngon(f'pbase{i}', c, 1.25, 1.1, 0.7, 1.2, 6, 'hull_dark', phase=a))
    py = hull_from(f'pylon{i}', [(c[0] + math.cos(a + k * math.pi / 3) * 0.95, c[1] + math.sin(a + k * math.pi / 3) * 0.95, 1.2) for k in range(6)] +
                   [(c[0] + math.cos(a + k * math.pi / 3) * 0.55, c[1] + math.sin(a + k * math.pi / 3) * 0.55, 4.6) for k in range(6)], 'team', 0.04)
    inset_panels(py, 0.08, 0.03, lambda f: abs(f.normal.z) < 0.5)
    body.append(py)
    for z in (1.9, 2.6, 3.3):
        body.append(ep.ring(f'pins{i}{z}', c, 0.95 - (z - 1.2) * 0.12 + 0.12, 0.5, z, z + 0.14, 'mech', 6, phase=a))
    body.append(ep.ngon(f'ptop{i}', c, 0.55, 0.35, 4.6, 4.9, 6, 'metal', phase=a))
    body.append(sphere(f'ptip{i}', (c[0], c[1], 5.1), 0.3, 'glow', 8, 5))
    # Bus bar from the pylon top to the column.
    body.append(ep.beam(f'bus{i}', (c[0] - ca * 0.4, c[1] - sa * 0.4, 4.4), (ca * 3.05, sa * 3.05, 5.6), 0.28, 0.22, 'metal', 0.02))
    body.append(ep.beam(f'busg{i}', (c[0] - ca * 0.4, c[1] - sa * 0.4, 4.62), (ca * 3.05, sa * 3.05, 5.82), 0.1, 0.06, 'glow', 0))
# Hazard door at the front.
body.append(box('door', (0, -3.25, 1.55), (1.5, 0.3, 1.6), 'hull_dark', 0.03))
body.append(box('doorlamp', (0, -3.42, 2.5), (1.0, 0.04, 0.1), 'glow', 0, 1))

B = kit.join('Body', body)
ep.finish('lattice_energy_storage', [B], 1024, bake_scale=0.3)

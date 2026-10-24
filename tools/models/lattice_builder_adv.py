"""Lattice "Architect" advanced builder. Run: python tools/models/lattice_builder_adv.py <out_dir>

A broad, high-tech constructor on wide Lattice pods: two slim nanolathe cranes splayed forward,
a raised fabricator core at the back crowned with a glowing ring and a floating crystal, and a
survey dish on a mast for laying out heavy structures.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import vehicles_parts as vp

kit.reset('lattice')
body = []
body += vp.lattice_pods(0.66, 0.32, -1.1, 1.15, 0.4, cleats=17)

def ring(y, hw, ht, z0, z1, z2):
    return [(-hw * 0.85, y, z0), (hw * 0.85, y, z0), (hw, y, z1), (-hw, y, z1), (ht, y, z2), (-ht, y, z2)]
hull = hull_from('hull', ring(1.1, 0.34, 0.22, 0.14, 0.26, 0.32) + ring(0.66, 0.52, 0.4, 0.12, 0.38, 0.5) +
                 ring(-0.82, 0.52, 0.4, 0.12, 0.38, 0.5) + ring(-1.06, 0.44, 0.32, 0.14, 0.32, 0.42), 'hull', 0.025, 2)
inset_panels(hull, 0.03, 0.01, lambda f: abs(f.normal.x) > 0.5 and f.calc_area() > 0.03)
body.append(hull)
body.append(box('belly', (0, 0.0, 0.1), (0.8, 1.95, 0.06), 'mech', 0.01, 1))
body.append(prism_x('glacis', [(1.04, 0.33), (0.7, 0.515), (0.62, 0.515), (0.98, 0.32)], -0.3, 0.3, 'team', 0.006, 1))
body += vp.lattice_eye('noseeye', (0, 1.13, 0.27), 0.36)
for sx in (-1, 1):
    body.append(box(f'lamp{sx}', (sx * 0.3, 1.0, 0.36), (0.06, 0.02, 0.02), 'glow', 0, 1))

# ---- Two nanolathe cranes splayed forward ----
for sx in (-1, 1):
    body.append(box(f'armpad{sx}', (sx * 0.3, 0.2, 0.505), (0.3, 0.3, 0.02), 'hull_dark', 0.006, 1))
    body += vp.nano_arm(f'arm{sx}', (sx * 0.3, 0.2, 0.51), 0.85, -sx * 17, 'lattice')

# ---- Fabricator core at the back ----
core = hull_from('core', [(-0.36, -0.1, 0.48), (0.36, -0.1, 0.48), (-0.36, -0.76, 0.48), (0.36, -0.76, 0.48),
                          (-0.28, -0.16, 0.74), (0.28, -0.16, 0.74), (-0.28, -0.7, 0.74), (0.28, -0.7, 0.74)], 'hull', 0.025, 2)
inset_panels(core, 0.03, 0.01, lambda f: f.normal.z < 0.8 and f.calc_area() > 0.03)
body.append(core)
body.append(hull_from('coretop', [(-0.27, -0.17, 0.74), (0.27, -0.17, 0.74), (-0.27, -0.69, 0.74), (0.27, -0.69, 0.74),
                                  (-0.22, -0.2, 0.77), (0.22, -0.2, 0.77), (-0.22, -0.66, 0.77), (0.22, -0.66, 0.77)], 'team', 0.006, 1))
CC = (0, -0.43, 0.77)
body.append(cyl('ringbase', CC, (0, -0.43, 0.81), 0.2, 'mech', 20, bevel=0.005))
# Glowing ring (a torus of short segments) and a floating crystal over it.
N = 16
for i in range(N):
    a0, a1 = i * 2 * math.pi / N, (i + 1) * 2 * math.pi / N
    body.append(cyl(f'ring{i}', (math.cos(a0) * 0.2, -0.43 + math.sin(a0) * 0.2, 0.84),
                    (math.cos(a1) * 0.2, -0.43 + math.sin(a1) * 0.2, 0.84), 0.025, 'glow', 6, bevel=0))
for k in range(3):
    a = k * 2 * math.pi / 3
    body.append(cyl(f'ringpost{k}', (math.cos(a) * 0.2, -0.43 + math.sin(a) * 0.2, 0.8),
                    (math.cos(a) * 0.2, -0.43 + math.sin(a) * 0.2, 0.84), 0.02, 'metal', 6, bevel=0))
body.append(hull_from('crystal', [(0, -0.43, 0.9), (0.08, -0.43, 1.0), (-0.04, -0.36, 1.0), (-0.04, -0.5, 1.0), (0, -0.43, 1.14)],
                      'glow', 0.004, 1))
for sx in (-1, 1):
    body.append(box(f'coreln{sx}', (sx * 0.33, -0.43, 0.62), (0.008, 0.4, 0.02), 'glow_dim', 0, 1))

# ---- Survey dish on a mast at the back ----
body.append(cyl('mast', (0.36, -0.88, 0.48), (0.36, -0.88, 0.98), 0.025, 'metal', 8, bevel=0))
dc = (0.36, -0.9, 1.02)
body.append(cyl('dish', (dc[0], dc[1] + 0.02, dc[2] - 0.06), dc, 0.04, 'hull', 14, r2=0.17, bevel=0.004))
body.append(cyl('dishface', (dc[0], dc[1] - 0.005, dc[2] + 0.005), (dc[0], dc[1] - 0.008, dc[2] + 0.012), 0.15, 'team', 14, bevel=0))
body.append(cyl('feed', dc, (dc[0], dc[1] - 0.06, dc[2] + 0.12), 0.007, 'mech', 4, bevel=0))
body.append(sphere('feedtip', (dc[0], dc[1] - 0.06, dc[2] + 0.13), 0.022, 'glow', 8, 4))
# Rear fins.
for i in range(5):
    body.append(box(f'fin{i}', (-0.2 + i * 0.1, -1.02, 0.44), (0.02, 0.07, 0.07), 'metal', 0.003, 1))

vp.finish('lattice_builder_adv', body)

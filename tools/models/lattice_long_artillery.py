"""Lattice "Longbow" long-range artillery. Run: python tools/models/lattice_long_artillery.py <out_dir>

A strategic railgun on the Lattice pods: four splayed stabiliser legs braced into the ground, a
capacitor bank behind a low mount, and an enormous accelerator barrel at 32 degrees, ringed with
team collars and glowing coil segments, ending in a split muzzle. A data-link dish sits on the
hull because it fires on radar contacts.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import vehicles_parts as vp

kit.reset('lattice')
body = []
body += vp.lattice_pods(0.57, 0.3, -1.15, 1.0, 0.36)

def ring(y, hw, ht, z0, z1, z2):
    return [(-hw * 0.85, y, z0), (hw * 0.85, y, z0), (hw, y, z1), (-hw, y, z1), (ht, y, z2), (-ht, y, z2)]
hull = hull_from('hull', ring(0.98, 0.28, 0.18, 0.14, 0.22, 0.28) + ring(0.56, 0.43, 0.32, 0.12, 0.33, 0.44) +
                 ring(-0.88, 0.43, 0.32, 0.12, 0.33, 0.44) + ring(-1.12, 0.37, 0.26, 0.14, 0.3, 0.37), 'hull', 0.025, 2)
inset_panels(hull, 0.03, 0.01, lambda f: abs(f.normal.x) > 0.5 and f.calc_area() > 0.03)
body.append(hull)
body.append(box('belly', (0, -0.08, 0.1), (0.66, 1.9, 0.06), 'mech', 0.01, 1))
body.append(prism_x('glacis', [(0.94, 0.3), (0.6, 0.45), (0.3, 0.45), (0.3, 0.44), (0.9, 0.29)], -0.26, 0.26, 'team', 0.006, 1))
body += vp.lattice_eye('noseeye', (0, 1.0, 0.24), 0.3)
# Stabiliser legs: reverse-jointed struts reaching out and down to splayed feet.
for sx in (-1, 1):
    for y in (0.7, -0.85):
        hip = (sx * 0.72, y, 0.32)
        knee = (sx * 0.98, y - 0.05, 0.36)
        foot = (sx * 1.06, y - 0.1, 0.04)
        body.append(box(f'hip{sx}{y}', hip, (0.1, 0.12, 0.1), 'mech', 0.01, 1))
        body.append(vp.tapered(f'thigh{sx}{y}', hip, knee, 0.08, 0.09, 0.07, 0.07, 'team', 0.01))
        body.append(sphere(f'knee{sx}{y}', knee, 0.045, 'mech', 8, 5))
        body.append(vp.tapered(f'shin{sx}{y}', knee, foot, 0.06, 0.06, 0.05, 0.05, 'hull', 0.008))
        body.append(hull_from(f'foot{sx}{y}', [(foot[0] - 0.09, foot[1] - 0.09, 0), (foot[0] + 0.09, foot[1] - 0.09, 0),
                                               (foot[0] - 0.09, foot[1] + 0.09, 0), (foot[0] + 0.09, foot[1] + 0.09, 0),
                                               (foot[0] - 0.05, foot[1] - 0.05, 0.05), (foot[0] + 0.05, foot[1] - 0.05, 0.05),
                                               (foot[0] - 0.05, foot[1] + 0.05, 0.05), (foot[0] + 0.05, foot[1] + 0.05, 0.05)],
                              'hull_dark', 0.006, 1))
        body.append(box(f'footg{sx}{y}', (foot[0] + sx * 0.06, foot[1], 0.03), (0.008, 0.08, 0.012), 'glow', 0, 1))
# Data-link dish at the front left, and a rear power coupling.
body.append(cyl('dishpost', (-0.24, 0.36, 0.44), (-0.24, 0.36, 0.6), 0.02, 'metal', 6, bevel=0))
dc = (-0.24, 0.38, 0.64)
body.append(cyl('dish', (dc[0], dc[1] - 0.02, dc[2] - 0.04), dc, 0.03, 'hull', 14, r2=0.13, bevel=0.004))
body.append(cyl('dishface', (dc[0], dc[1] + 0.003, dc[2] + 0.003), (dc[0], dc[1] + 0.006, dc[2] + 0.008), 0.115, 'team', 14, bevel=0))
body.append(sphere('dishtip', (dc[0], dc[1] + 0.05, dc[2] + 0.09), 0.018, 'glow', 8, 4))
body.append(cyl('feed', dc, (dc[0], dc[1] + 0.05, dc[2] + 0.09), 0.006, 'mech', 4, bevel=0))
body.append(prism_x('reartop', [(-0.9, 0.445), (-0.94, 0.445), (-1.1, 0.375), (-1.06, 0.375)], -0.22, 0.22, 'team', 0.004, 1))
body.append(cyl('tring', (0, -0.42, 0.44), (0, -0.42, 0.47), 0.34, 'mech', 20, bevel=0.006))

# ---- Turret ----
PIV = (0.0, -0.42, 0.44)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = []
tur.append(hull_from('slab', [P(-0.32, 0.36, 0.0), P(0.32, 0.36, 0.0), P(-0.32, -0.5, 0.0), P(0.32, -0.5, 0.0),
                              P(-0.24, 0.42, 0.08), P(0.24, 0.42, 0.08), P(-0.26, -0.46, 0.17), P(0.26, -0.46, 0.17),
                              P(-0.26, 0.3, 0.17), P(0.26, 0.3, 0.17)], 'hull', 0.02, 2))
# Capacitor bank behind the trunnion: three glowing cells under a team cover.
tur.append(box('capbase', P(0, -0.36, 0.28), (0.44, 0.24, 0.2), 'hull_dark', 0.015, 2))
tur.append(hull_from('capcover', [P(-0.23, -0.24, 0.38), P(0.23, -0.24, 0.38), P(-0.23, -0.48, 0.38), P(0.23, -0.48, 0.38),
                                  P(-0.19, -0.27, 0.42), P(0.19, -0.27, 0.42), P(-0.19, -0.45, 0.42), P(0.19, -0.45, 0.42)], 'team', 0.006, 1))
for k in range(3):
    tur.append(cyl(f'cap{k}', P(-0.14 + k * 0.14, -0.49, 0.28), P(-0.14 + k * 0.14, -0.53, 0.28), 0.06, 'glow_dim', 10, bevel=0))
for sx in (-1, 1):
    tur.append(hull_from(f'cheek{sx}', [P(sx * 0.12, 0.3, 0.16), P(sx * 0.2, 0.3, 0.16), P(sx * 0.12, -0.22, 0.16), P(sx * 0.2, -0.22, 0.16),
                                        P(sx * 0.12, 0.1, 0.5), P(sx * 0.2, 0.1, 0.5), P(sx * 0.12, -0.1, 0.5), P(sx * 0.2, -0.1, 0.5)],
                         'team', 0.012, 1))
tur.append(cyl('trun', P(-0.23, 0.0, 0.38), P(0.23, 0.0, 0.38), 0.06, 'mech', 12))
root = P(0, 0.0, 0.38)
E = lambda d, dx=0.0, dz=0.0: vp.elevated((root[0] + dx, root[1], root[2] + dz), 32, d)
tur.append(vp.beam('breech', E(-0.36), E(0.12), 0.2, 0.18, 'hull_dark', 0.015))
tur.append(cyl('barrel', E(-0.1), E(2.06), 0.06, 'metal', 12, r2=0.05))
for k, d in enumerate((0.3, 0.7, 1.1, 1.5)):
    tur.append(cyl(f'collar{k}', E(d), E(d + 0.1), 0.1, 'team', 12, r2=0.095, bevel=0.006))
    tur.append(cyl(f'coil{k}', E(d + 0.12), E(d + 0.3), 0.075, 'glow_dim', 10, bevel=0))
# Split muzzle: two prongs with a glowing gap.
for sx in (-1, 1):
    tur.append(vp.beam(f'prong{sx}', E(1.9, sx * 0.05), E(2.2, sx * 0.05), 0.04, 0.1, 'mech', 0.006))
tur.append(cyl('muzzleg', E(2.0), E(2.16), 0.025, 'glow', 8, bevel=0))
tur.append(vp.beam('spine', E(0.1, 0, 0.08), E(1.8, 0, 0.065), 0.03, 0.03, 'hull', 0.004))
tur += vp.lattice_eye('teye', P(0, 0.43, 0.06), 0.2)

vp.finish('lattice_long_artillery', body, tur, PIV)

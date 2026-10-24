"""Lattice "Lancer Array" artillery vehicle. Run: python tools/models/lattice_artillery_vehicle.py <out_dir>

A precision howitzer on the Lattice pods: a low bevelled turret set well back, carrying one long
slim rail-lance raised at 30 degrees between two guide rails, banded with team collars and
ending in a glowing emitter muzzle. A rangefinder head and a ranging array sit on the hull.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import vehicles_parts as vp

kit.reset('lattice')
body = []
body += vp.lattice_pods(0.57, 0.3, -1.12, 1.1, 0.36)

def ring(y, hw, ht, z0, z1, z2):
    return [(-hw * 0.85, y, z0), (hw * 0.85, y, z0), (hw, y, z1), (-hw, y, z1), (ht, y, z2), (-ht, y, z2)]
hull = hull_from('hull', ring(1.06, 0.28, 0.18, 0.14, 0.22, 0.28) + ring(0.6, 0.43, 0.32, 0.12, 0.33, 0.44) +
                 ring(-0.84, 0.43, 0.32, 0.12, 0.33, 0.44) + ring(-1.08, 0.37, 0.26, 0.14, 0.3, 0.37), 'hull', 0.025, 2)
inset_panels(hull, 0.03, 0.01, lambda f: abs(f.normal.x) > 0.5 and f.calc_area() > 0.03)
body.append(hull)
body.append(box('belly', (0, -0.02, 0.1), (0.66, 1.9, 0.06), 'mech', 0.01, 1))
# Front: a team glacis and a raised rangefinder head with twin lenses.
body.append(prism_x('glacis', [(1.0, 0.3), (0.64, 0.45), (0.3, 0.45), (0.3, 0.44), (0.96, 0.29)], -0.26, 0.26, 'team', 0.006, 1))
body += vp.lattice_eye('noseeye', (0, 1.08, 0.24), 0.3)
body.append(hull_from('rfhead', [(-0.2, 0.42, 0.44), (0.2, 0.42, 0.44), (-0.2, 0.2, 0.44), (0.2, 0.2, 0.44),
                                 (-0.16, 0.4, 0.56), (0.16, 0.4, 0.56), (-0.16, 0.22, 0.56), (0.16, 0.22, 0.56)], 'hull_dark', 0.015, 2))
for sx in (-1, 1):
    body.append(cyl(f'rflens{sx}', (sx * 0.12, 0.42, 0.5), (sx * 0.12, 0.45, 0.5), 0.035, 'glow', 10, bevel=0))
body.append(box('rftop', (0, 0.31, 0.565), (0.24, 0.12, 0.012), 'team', 0.003, 1))
# Rear: stabiliser jacks folded at the corners, and a ranging array.
for sx in (-1, 1):
    body.append(vp.beam(f'jack{sx}', (sx * 0.34, -1.02, 0.34), (sx * 0.34, -1.14, 0.12), 0.07, 0.07, 'hull_dark'))
    body.append(box(f'jackfoot{sx}', (sx * 0.34, -1.16, 0.04), (0.14, 0.12, 0.04), 'mech', 0.008, 1))
body.append(prism_x('reartop', [(-0.86, 0.445), (-0.9, 0.445), (-1.06, 0.38), (-1.02, 0.38)], -0.22, 0.22, 'team', 0.004, 1))
body.append(cyl('tring', (0, -0.38, 0.44), (0, -0.38, 0.47), 0.32, 'mech', 20, bevel=0.006))

# ---- Turret ----
PIV = (0.0, -0.38, 0.44)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = []
slab = hull_from('slab', [P(-0.31, 0.3, 0.0), P(0.31, 0.3, 0.0), P(-0.31, -0.34, 0.0), P(0.31, -0.34, 0.0),
                          P(-0.24, 0.36, 0.08), P(0.24, 0.36, 0.08), P(-0.25, -0.3, 0.17), P(0.25, -0.3, 0.17),
                          P(-0.25, 0.24, 0.17), P(0.25, 0.24, 0.17)], 'hull', 0.02, 2)
tur.append(slab)
tur.append(hull_from('slabtop', [P(-0.22, 0.2, 0.17), P(0.22, 0.2, 0.17), P(-0.22, -0.26, 0.17), P(0.22, -0.26, 0.17),
                                 P(-0.19, 0.18, 0.19), P(0.19, 0.18, 0.19), P(-0.19, -0.24, 0.19), P(0.19, -0.24, 0.19)], 'team', 0.006, 1))
# Two upright cheeks (team faces) holding the lance trunnion.
for sx in (-1, 1):
    tur.append(hull_from(f'cheek{sx}', [P(sx * 0.12, 0.24, 0.16), P(sx * 0.2, 0.24, 0.16), P(sx * 0.12, -0.24, 0.16), P(sx * 0.2, -0.24, 0.16),
                                        P(sx * 0.12, 0.06, 0.46), P(sx * 0.2, 0.06, 0.46), P(sx * 0.12, -0.12, 0.46), P(sx * 0.2, -0.12, 0.46)],
                         'team', 0.012, 1))
    tur.append(box(f'cheekln{sx}', P(sx * 0.202, -0.04, 0.3), (0.006, 0.2, 0.015), 'glow_dim', 0, 1))
tur.append(cyl('trun', P(-0.22, -0.04, 0.34), P(0.22, -0.04, 0.34), 0.055, 'mech', 12))
# The lance: a slim barrel at 30 degrees between two guide rails, team collars, glowing muzzle.
root = P(0, -0.04, 0.34)
A = 30
E = lambda d, dx=0.0, dz=0.0: vp.elevated((root[0] + dx, root[1], root[2] + dz), A, d)
tur.append(cyl('breech', E(-0.32), E(0.1), 0.085, 'hull_dark', 12))
tur.append(cyl('lance', E(-0.1), E(1.72), 0.05, 'metal', 12, r2=0.042))
for sx in (-1, 1):
    tur.append(vp.beam(f'grail{sx}', E(0.0, sx * 0.075), E(1.45, sx * 0.075), 0.022, 0.05, 'hull', 0.004))
for d in (0.35, 0.75, 1.15):
    tur.append(vp.beam(f'band{d}', E(d), E(d + 0.09), 0.2, 0.11, 'team', 0.008))
tur.append(cyl('muzzle', E(1.66), E(1.8), 0.075, 'mech', 12, r2=0.06))
tur.append(cyl('muzzleg', E(1.72), E(1.76), 0.08, 'glow', 12, bevel=0))
tur.append(cyl('bore', E(1.8), E(1.81), 0.035, 'glow', 10, bevel=0))
# Counterweight block and heat fins behind the trunnion.
tur.append(box('cw', P(0, -0.34, 0.2), (0.3, 0.12, 0.12), 'hull_dark', 0.012, 1))
for k in range(4):
    tur.append(box(f'cwfin{k}', P(-0.11 + k * 0.073, -0.41, 0.2), (0.02, 0.04, 0.1), 'metal', 0.003, 1))
tur += vp.lattice_eye('teye', P(0, 0.37, 0.06), 0.18)

vp.finish('lattice_artillery_vehicle', body, tur, PIV)

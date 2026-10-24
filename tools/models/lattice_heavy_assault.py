"""Lattice "Arbiter" heavy assault tank. Run: python tools/models/lattice_heavy_assault.py <out_dir>

The Strider grown into a siege tank: tall faired pods with angled armour skirts, a broad wedge
hull, and a big faceted turret carrying twin long cannons in team shrouds plus a coaxial laser
emitter pod with a glowing lens on the right cheek.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import vehicles_parts as vp

kit.reset('lattice')
body = []
body += vp.lattice_pods(0.68, 0.36, -1.1, 1.16, 0.44, cleats=18)
# Angled armour skirts hung outside the pods (team on the top bevel).
for sx in (-1, 1):
    x = sx * 0.885
    body.append(hull_from(f'skirt{sx}', [(x, 0.95, 0.18), (x, -0.95, 0.18), (x - sx * 0.04, 1.0, 0.44), (x - sx * 0.04, -1.0, 0.44),
                                         (x - sx * 0.05, 0.95, 0.18), (x - sx * 0.05, -0.95, 0.18), (x - sx * 0.09, 1.0, 0.44), (x - sx * 0.09, -1.0, 0.44)],
                         'hull_dark', 0.01, 1))
    body.append(hull_from(f'skirttop{sx}', [(x - sx * 0.04, 1.0, 0.44), (x - sx * 0.04, -1.0, 0.44), (x - sx * 0.12, 0.96, 0.49), (x - sx * 0.12, -0.96, 0.49),
                                            (x - sx * 0.09, 1.0, 0.43), (x - sx * 0.09, -1.0, 0.43)], 'team', 0.006, 1))
    for k in range(4):
        body.append(box(f'skirtln{sx}{k}', (x + sx * 0.005, 0.66 - k * 0.44, 0.3), (0.008, 0.2, 0.02), 'glow_dim', 0, 1))
    body += rivets(f'skrv{sx}', (x + sx * 0.008, 0.9, 0.22), (x + sx * 0.008, -0.9, 0.22), 8, 'metal', 0.013)

def ring(y, hw, ht, z0, z1, z2):
    return [(-hw * 0.85, y, z0), (hw * 0.85, y, z0), (hw, y, z1), (-hw, y, z1), (ht, y, z2), (-ht, y, z2)]
hull = hull_from('hull', ring(1.14, 0.34, 0.24, 0.14, 0.26, 0.34) + ring(0.62, 0.5, 0.38, 0.12, 0.42, 0.57) +
                 ring(-0.76, 0.5, 0.38, 0.12, 0.42, 0.57) + ring(-1.06, 0.44, 0.32, 0.14, 0.36, 0.46), 'hull', 0.025, 2)
inset_panels(hull, 0.03, 0.01, lambda f: abs(f.normal.x) > 0.5 and f.calc_area() > 0.03)
body.append(hull)
body.append(box('belly', (0, 0.0, 0.1), (0.8, 1.95, 0.06), 'mech', 0.01, 1))
body.append(prism_x('glacis', [(1.08, 0.36), (0.66, 0.585), (0.6, 0.585), (1.02, 0.35)], -0.32, 0.32, 'team', 0.006, 1))
body.append(hull_from('visor', [(-0.28, 1.12, 0.24), (0.28, 1.12, 0.24), (-0.3, 1.17, 0.28), (0.3, 1.17, 0.28),
                                (-0.28, 1.12, 0.32), (0.28, 1.12, 0.32)], 'mech', 0.008, 1))
body.append(box('eye', (0, 1.175, 0.28), (0.44, 0.012, 0.028), 'glow', 0, 1))
# Rear heat-sink deck.
body.append(box('sinkbase', (0, -0.84, 0.59), (0.56, 0.32, 0.05), 'hull_dark', 0.01, 1))
for i in range(7):
    x = -0.24 + i * 0.08
    body.append(box(f'fin{i}', (x, -0.84, 0.64), (0.02, 0.28, 0.08), 'metal', 0.004, 1))
    if i < 6:
        body.append(box(f'finglow{i}', (x + 0.04, -0.84, 0.62), (0.04, 0.24, 0.01), 'glow_dim', 0, 1))
body.append(prism_x('reartop', [(-0.66, 0.575), (-0.7, 0.575), (-1.04, 0.465), (-1.0, 0.465)], -0.28, 0.28, 'team', 0.004, 1))
for sx in (-1, 1):
    body.append(sphere(f'dome{sx}', (sx * 0.36, 0.44, 0.57), 0.05, 'glass', 10, 5))
    body.append(cyl(f'domering{sx}', (sx * 0.36, 0.44, 0.56), (sx * 0.36, 0.44, 0.58), 0.06, 'mech', 10, bevel=0))
body.append(cyl('tring', (0, -0.14, 0.56), (0, -0.14, 0.6), 0.38, 'mech', 20, bevel=0.006))

# ---- Turret ----
PIV = (0.0, -0.14, 0.56)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = []
shell = hull_from('tshell', [P(-0.22, 0.56, 0.04), P(0.22, 0.56, 0.04), P(-0.48, 0.22, 0.02), P(0.48, 0.22, 0.02),
                             P(-0.48, -0.42, 0.02), P(0.48, -0.42, 0.02), P(-0.36, -0.56, 0.05), P(0.36, -0.56, 0.05),
                             P(-0.15, 0.52, 0.17), P(0.15, 0.52, 0.17), P(-0.34, 0.18, 0.29), P(0.34, 0.18, 0.29),
                             P(-0.34, -0.4, 0.29), P(0.34, -0.4, 0.29), P(-0.27, -0.53, 0.22), P(0.27, -0.53, 0.22)],
                 'hull', bevel=0.025, segs=2)
inset_panels(shell, 0.03, 0.01, lambda f: f.normal.z < 0.7 and f.calc_area() > 0.02)
tur.append(shell)
tur.append(hull_from('troof', [P(-0.31, 0.16, 0.29), P(0.31, 0.16, 0.29), P(-0.31, -0.38, 0.29), P(0.31, -0.38, 0.29),
                               P(-0.12, 0.48, 0.18), P(0.12, 0.48, 0.18),
                               P(-0.28, 0.14, 0.315), P(0.28, 0.14, 0.315), P(-0.28, -0.35, 0.315), P(0.28, -0.35, 0.315),
                               P(-0.1, 0.44, 0.2), P(0.1, 0.44, 0.2)], 'team', 0.008, 1))
tur.append(box('roofspine', P(0, -0.1, 0.33), (0.04, 0.4, 0.02), 'glow_dim', 0, 1))
for sx in (-1, 1):
    tur.append(hull_from(f'cheek{sx}', [P(sx * 0.48, 0.2, 0.03), P(sx * 0.53, 0.12, 0.05), P(sx * 0.53, -0.36, 0.05),
                                        P(sx * 0.48, -0.42, 0.03), P(sx * 0.48, 0.16, 0.19), P(sx * 0.48, -0.38, 0.19),
                                        P(sx * 0.43, 0.2, 0.03), P(sx * 0.43, -0.42, 0.03), P(sx * 0.43, 0.16, 0.2),
                                        P(sx * 0.43, -0.38, 0.2)], 'team_dark', 0.008, 1))
# Twin guns.
tur.append(hull_from('mantlet', [P(-0.24, 0.5, 0.04), P(0.24, 0.5, 0.04), P(-0.24, 0.5, 0.24), P(0.24, 0.5, 0.24),
                                 P(-0.2, 0.64, 0.08), P(0.2, 0.64, 0.08), P(-0.2, 0.64, 0.2), P(0.2, 0.64, 0.2)], 'hull_dark', 0.012, 1))
for sx in (-1, 1):
    x = sx * 0.11
    tur.append(vp.tapered(f'shroud{sx}', P(x, 0.62, 0.14), P(x, 0.98, 0.14), 0.12, 0.11, 0.095, 0.085, 'team', 0.01))
    tur.append(cyl(f'barrel{sx}', P(x, 0.96, 0.14), P(x, 1.36, 0.14), 0.042, 'metal', 14))
    tur.append(cyl(f'collar{sx}', P(x, 1.1, 0.14), P(x, 1.15, 0.14), 0.054, 'mech', 14, bevel=0.004))
    tur.append(vp.tapered(f'muzzle{sx}', P(x, 1.34, 0.14), P(x, 1.46, 0.14), 0.1, 0.08, 0.088, 0.068, 'mech', 0.01))
    tur.append(cyl(f'bore{sx}', P(x, 1.455, 0.14), P(x, 1.47, 0.14), 0.028, 'glow', 10, bevel=0))
tur.append(box('gunbridge', P(0, 0.86, 0.14), (0.14, 0.08, 0.06), 'hull_dark', 0.006, 1))
# Laser emitter pod on the right cheek.
tur.append(hull_from('lpod', [P(0.3, 0.42, 0.24), P(0.44, 0.42, 0.24), P(0.3, -0.1, 0.24), P(0.44, -0.1, 0.24),
                              P(0.32, 0.4, 0.36), P(0.42, 0.4, 0.36), P(0.32, -0.08, 0.36), P(0.42, -0.08, 0.36)], 'hull_dark', 0.012, 1))
tur.append(cyl('lbarrel', P(0.37, 0.4, 0.3), P(0.37, 0.7, 0.3), 0.03, 'metal', 12))
tur.append(cyl('lring1', P(0.37, 0.52, 0.3), P(0.37, 0.55, 0.3), 0.045, 'glow_dim', 12, bevel=0))
tur.append(cyl('lhead', P(0.37, 0.66, 0.3), P(0.37, 0.76, 0.3), 0.05, 'mech', 12, r2=0.04))
tur.append(cyl('llens', P(0.37, 0.76, 0.3), P(0.37, 0.77, 0.3), 0.038, 'glow', 12, bevel=0))
tur.append(box('lpodtop', P(0.37, 0.16, 0.365), (0.08, 0.4, 0.01), 'team', 0, 1))
# Sight, hatch, mast, bustle.
tur += vp.lattice_eye('teye', P(-0.28, 0.38, 0.2), 0.1)
tur.append(box('sight', P(-0.2, 0.0, 0.345), (0.1, 0.16, 0.06), 'hull_dark', 0.012, 1))
tur.append(box('sightlens', P(-0.2, 0.083, 0.345), (0.07, 0.006, 0.03), 'glow', 0, 1))
tur.append(cyl('hatch', P(0.1, -0.2, 0.315), P(0.1, -0.2, 0.335), 0.09, 'hull_dark', 12, bevel=0.004))
tur.append(cyl('mast', P(-0.24, -0.46, 0.25), P(-0.24, -0.5, 0.72), 0.011, 'mech', 5, bevel=0))
tur.append(sphere('masttip', P(-0.24, -0.5, 0.73), 0.024, 'glow', 8, 4))
tur.append(box('bustle', P(0, -0.62, 0.15), (0.5, 0.16, 0.16), 'hull_dark', 0.015, 1))
for k in range(5):
    tur.append(box(f'bfin{k}', P(-0.2 + k * 0.1, -0.71, 0.15), (0.02, 0.04, 0.13), 'metal', 0.003, 1))

vp.finish('lattice_heavy_assault', body, tur, PIV)

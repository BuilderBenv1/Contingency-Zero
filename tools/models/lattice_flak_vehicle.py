"""Lattice "Prism" flak vehicle. Run: python tools/models/lattice_flak_vehicle.py <out_dir>

Anti-air on the Lattice pods: a turret built round a tall prism housing with a glowing crystal
ridge, flanked by two team-painted pods, each with a pair of slim barrels raised steeply skyward,
and a small tracking dish behind.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import vehicles_parts as vp

kit.reset('lattice')
body = []
body += vp.lattice_pods(0.61, 0.3, -1.05, 1.12, 0.36)

def ring(y, hw, ht, z0, z1, z2):
    return [(-hw * 0.85, y, z0), (hw * 0.85, y, z0), (hw, y, z1), (-hw, y, z1), (ht, y, z2), (-ht, y, z2)]
hull = hull_from('hull', ring(1.08, 0.3, 0.2, 0.14, 0.22, 0.28) + ring(0.62, 0.46, 0.34, 0.12, 0.33, 0.45) +
                 ring(-0.74, 0.46, 0.34, 0.12, 0.33, 0.45) + ring(-1.0, 0.4, 0.28, 0.14, 0.3, 0.37), 'hull', 0.025, 2)
inset_panels(hull, 0.03, 0.01, lambda f: abs(f.normal.x) > 0.5 and f.calc_area() > 0.03)
body.append(hull)
body.append(box('belly', (0, 0.0, 0.1), (0.7, 1.9, 0.06), 'mech', 0.01, 1))
body.append(prism_x('glacis', [(1.02, 0.3), (0.66, 0.46), (0.6, 0.46), (0.96, 0.29)], -0.27, 0.27, 'team', 0.006, 1))
body += vp.lattice_eye('noseeye', (0, 1.1, 0.24), 0.3)
# Forward sensor array: three small lenses in a row on the glacis top.
for k in range(3):
    body.append(box(f'arr{k}', (-0.12 + k * 0.12, 0.6, 0.47), (0.08, 0.06, 0.03), 'hull_dark', 0.006, 1))
    body.append(box(f'arrg{k}', (-0.12 + k * 0.12, 0.632, 0.47), (0.05, 0.004, 0.015), 'glow', 0, 1))
# Rear deck: power cells either side of a cooling grille.
body.append(prism_x('reartop', [(-0.62, 0.455), (-0.68, 0.455), (-0.98, 0.375), (-0.92, 0.375)], -0.24, 0.24, 'team', 0.004, 1))
for sx in (-1, 1):
    body.append(cyl(f'cell{sx}', (sx * 0.3, -0.6, 0.46), (sx * 0.3, -0.92, 0.46), 0.05, 'hull_dark', 10))
    body.append(cyl(f'cellg{sx}', (sx * 0.3, -0.72, 0.46), (sx * 0.3, -0.8, 0.46), 0.054, 'glow_dim', 10, bevel=0))
for i in range(5):
    body.append(box(f'grl{i}', (-0.12 + i * 0.06, -0.78, 0.465), (0.02, 0.24, 0.02), 'mech', 0.003, 1))
body.append(cyl('tring', (0, -0.12, 0.44), (0, -0.12, 0.48), 0.32, 'mech', 20, bevel=0.006))

# ---- Turret ----
PIV = (0.0, -0.12, 0.44)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = []
tur.append(cyl('base', P(0, 0, 0), P(0, 0, 0.08), 0.34, 'hull_dark', 16, r2=0.31, bevel=0.01))
# Central prism: triangular cross-section (ridge on top) running front to back.
tri = [(P(0, 0, 0)[1] + 0.32, 0), (0, 0)]
prism = hull_from('prism', [P(-0.2, 0.36, 0.08), P(0.2, 0.36, 0.08), P(-0.22, -0.36, 0.08), P(0.22, -0.36, 0.08),
                            P(0, 0.2, 0.46), P(0, -0.3, 0.5)], 'hull', 0.02, 2)
inset_panels(prism, 0.03, 0.01, lambda f: f.calc_area() > 0.04)
tur.append(prism)
tur.append(hull_from('ridge', [P(-0.035, 0.19, 0.44), P(0.035, 0.19, 0.44), P(-0.035, -0.29, 0.48), P(0.035, -0.29, 0.48),
                               P(0, 0.2, 0.5), P(0, -0.3, 0.54)], 'glow', 0.004, 1))
for sx in (-1, 1):
    # Team flanks on the prism.
    tur.append(hull_from(f'flank{sx}', [P(sx * 0.18, 0.3, 0.12), P(sx * 0.2, -0.32, 0.12), P(sx * 0.05, 0.18, 0.38), P(sx * 0.05, -0.26, 0.41),
                                        P(sx * 0.16, 0.3, 0.12), P(sx * 0.18, -0.32, 0.12), P(sx * 0.03, 0.18, 0.37), P(sx * 0.03, -0.26, 0.4)],
                         'team', 0.005, 1))
    # Gun pods with two barrels each, raised to ~68 degrees.
    cx = sx * 0.36
    tur.append(hull_from(f'gpod{sx}', [P(cx - 0.08, 0.18, 0.1), P(cx + 0.08, 0.18, 0.1), P(cx - 0.08, -0.2, 0.1), P(cx + 0.08, -0.2, 0.1),
                                       P(cx - 0.07, 0.12, 0.36), P(cx + 0.07, 0.12, 0.36), P(cx - 0.07, -0.18, 0.32), P(cx + 0.07, -0.18, 0.32)],
                         'team', 0.015, 2))
    tur.append(box(f'gpodside{sx}', P(cx + sx * 0.082, -0.02, 0.2), (0.012, 0.26, 0.14), 'hull_dark', 0.004, 1))
    tur.append(box(f'gpodline{sx}', P(cx + sx * 0.089, -0.02, 0.2), (0.004, 0.2, 0.015), 'glow_dim', 0, 1))
    tur.append(cyl(f'trun{sx}', P(sx * 0.2, -0.02, 0.24), P(sx * 0.3, -0.02, 0.24), 0.05, 'mech', 10))
    for k, dx in enumerate((-0.035, 0.035)):
        root = P(cx + dx, 0.06, 0.32)
        tip = vp.elevated(root, 68, 0.72)
        tur.append(cyl(f'brl{sx}{k}', root, tip, 0.022, 'metal', 10, bevel=0.003))
        tur.append(cyl(f'brlj{sx}{k}', root, vp.elevated(root, 68, 0.18), 0.034, 'hull', 10, bevel=0.004))
        a = vp.elevated(root, 68, 0.64)
        tur.append(cyl(f'flash{sx}{k}', a, vp.elevated(root, 68, 0.76), 0.03, 'mech', 8, bevel=0.003))
        tur.append(cyl(f'tipg{sx}{k}', vp.elevated(root, 68, 0.76), vp.elevated(root, 68, 0.77), 0.018, 'glow', 8, bevel=0))
    # Bracing collar joining each pair of barrels.
    tur.append(box(f'collar{sx}', vp.elevated(P(cx, 0.06, 0.32), 68, 0.4), (0.13, 0.04, 0.04), 'hull_dark', 0.005, 1))
# Tracking dish on the back of the prism, and a fire-control eye on the front.
tur.append(cyl('dishpost', P(0, -0.38, 0.16), P(0, -0.46, 0.34), 0.02, 'mech', 6, bevel=0))
dc = P(0, -0.47, 0.36)
tur.append(cyl('dish', dc, (dc[0], dc[1] - 0.03, dc[2] + 0.07), 0.11, 'hull', 14, r2=0.03, bevel=0.004))
tur.append(cyl('dishface', (dc[0], dc[1] + 0.003, dc[2] - 0.006), (dc[0], dc[1] + 0.006, dc[2] - 0.012), 0.1, 'team_dark', 14, bevel=0))
tur.append(sphere('dishtip', (dc[0], dc[1] + 0.06, dc[2] - 0.1), 0.018, 'glow', 8, 4))
tur.append(cyl('feed', dc, (dc[0], dc[1] + 0.06, dc[2] - 0.1), 0.006, 'mech', 4, bevel=0))
tur += vp.lattice_eye('feye', P(0, 0.37, 0.16), 0.16)

vp.finish('lattice_flak_vehicle', body, tur, PIV)

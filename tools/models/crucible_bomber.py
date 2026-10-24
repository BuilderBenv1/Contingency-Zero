"""Crucible "Anvilfall" heavy bomber. Run: python tools/models/crucible_bomber.py <out_dir>

A four-engined flying furnace: a fat riveted fuselage with a glazed bombardier nose and stepped
cockpit, broad straight team-painted wings, four fat jet nacelles glowing orange, a twin-finned
tailplane, a dorsal gun turret and a tail gun, and a long bomb bay underneath.
Modelled around z = 0 (belly near 0); the game lifts it to flight altitude.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A

kit.reset('crucible')
body = []

# ---- Fuselage ----
secs = [
    A.hexring(1.2, 0.13, 0.08, 0.06, 0.18, 0.26),
    A.hexring(1.0, 0.22, 0.16, -0.02, 0.24, 0.38),
    A.hexring(0.8, 0.27, 0.21, -0.04, 0.26, 0.42),
    A.hexring(-0.55, 0.28, 0.22, -0.04, 0.26, 0.42),
    A.hexring(-1.1, 0.12, 0.08, 0.14, 0.26, 0.32),
]
band = {0: 'hull_dark', 1: 'hull', 5: 'hull', 2: 'hull_dark', 4: 'hull_dark', 3: 'hull'}
fus = A.loft('fus', secs, 'hull', lambda j: band[j], cap0='glass', cap1='mech', bevel=0.014)
inset_panels(fus, 0.03, 0.008, lambda f: f.normal.z > 0.6 and f.calc_area() > 0.1)
body.append(fus)
# Glazed bombardier nose with frame bars, and a stepped cockpit.
body.append(hull_from('noseglass', [(0.1, 1.2, 0.07), (-0.1, 1.2, 0.07), (0.1, 1.2, 0.24), (-0.1, 1.2, 0.24),
                                    (0.16, 1.05, 0.0), (-0.16, 1.05, 0.0), (0.06, 1.27, 0.12), (-0.06, 1.27, 0.12),
                                    (0.06, 1.25, 0.2), (-0.06, 1.25, 0.2)], 'glass', bevel=0.01))
for k in range(3):
    body.append(box(f'nbar{k}', (0, 1.16 - k * 0.04, 0.05 + k * 0.08), (0.24 - k * 0.04, 0.02, 0.02), 'mech', 0, 1))
body.append(hull_from('cockpit', [(0.16, 0.92, 0.38), (-0.16, 0.92, 0.38), (0.15, 0.7, 0.48), (-0.15, 0.7, 0.48),
                                  (0.16, 0.6, 0.42), (-0.16, 0.6, 0.42), (0.15, 0.6, 0.48), (-0.15, 0.6, 0.48)], 'glass', bevel=0.01))
for k in range(3):
    body.append(box(f'cbar{k}', (-0.08 + k * 0.08, 0.8, 0.44), (0.02, 0.24, 0.02), 'mech', 0, 1))
    A.xform([body[-1]], (0, 0.8, 0.44), (-0.4, 0, 0))
# Team band round the fuselage behind the cockpit and a riveted spine.
body.append(box('band', (0, 0.42, 0.42), (0.46, 0.14, 0.02), 'team', 0.006, 1))
body += rivets('spine', (0, 0.25, 0.43), (0, -0.85, 0.36), 9, 'metal', 0.018)
for sx in A.SIDES:
    body += rivets(f'srv{sx}', (sx * 0.27, 0.7, 0.3), (sx * 0.27, -0.5, 0.3), 9, 'metal', 0.014)

# ---- Wings: broad and straight, team tops ----
plan = [(0.26, 0.34), (1.24, 0.2), (1.24, -0.18), (0.26, -0.32)]
body += A.wing('wing', plan, 0.2, 0.09, 0.06, 'team', 'hull_dark')
for sx in A.SIDES:
    body.append(box(f'wtip{sx}', (sx * 1.25, 0.01, 0.2), (0.04, 0.4, 0.09), 'hull_dark', 0.01, 1))
    body.append(sphere(f'navlt{sx}', (sx * 1.275, 0.2, 0.2), 0.022, 'glow', 8, 4))
    body.append(box(f'flap{sx}', (sx * 0.72, -0.27, 0.205), (0.86, 0.08, 0.035), 'hull', 0.006, 1))
    body += rivets(f'wr{sx}', (sx * 0.32, 0.29, 0.247), (sx * 1.18, 0.17, 0.235), 7, 'metal', 0.014)
    # Four fat nacelles: intake ring, spinner, housing, nozzle.
    for x in (0.52, 0.94):
        X = sx * x
        body.append(cyl(f'nac{sx}{x}', (X, 0.5, 0.13), (X, -0.24, 0.13), 0.12, 'hull_dark', 12, bevel=0.01))
        body.append(cyl(f'lip{sx}{x}', (X, 0.5, 0.13), (X, 0.56, 0.13), 0.13, 'metal', 12, r2=0.11, bevel=0.004))
        body.append(cyl(f'fan{sx}{x}', (X, 0.52, 0.13), (X, 0.555, 0.13), 0.1, 'mech', 12, bevel=0))
        body.append(cyl(f'spin{sx}{x}', (X, 0.55, 0.13), (X, 0.62, 0.13), 0.04, 'metal', 8, r2=0.004, bevel=0))
        body.append(cyl(f'nband{sx}{x}', (X, 0.32, 0.13), (X, 0.27, 0.13), 0.125, 'accent', 12, bevel=0))
        body += A.thruster(f'eng{sx}{x}', (X, -0.22, 0.13), (X, -0.42, 0.13), 0.1, verts=12)
        body += rivets(f'nr{sx}{x}', (X, 0.42, 0.252), (X, -0.16, 0.252), 4, 'metal', 0.013)

# ---- Tail: tailplane with twin end fins in team paint ----
body += A.wing('tail', [(0.08, -0.84), (0.62, -0.9), (0.62, -1.08), (0.08, -1.1)], 0.28, 0.05, 0.04, 'team', 'hull_dark', panels=False)
for sx in A.SIDES:
    body.append(prism_x(f'fin{sx}', [(-0.86, 0.22), (-0.98, 0.62), (-1.1, 0.62), (-1.12, 0.22)], sx * 0.62 - 0.03, sx * 0.62 + 0.03, 'team', 0.01, 1))
    body.append(box(f'fincap{sx}', (sx * 0.62, -1.04, 0.63), (0.08, 0.16, 0.03), 'hull_dark', 0.006, 1))
# Tail gun: a caged turret with twin guns pointing back.
body.append(sphere('tailgun', (0, -1.12, 0.24), 0.07, 'glass', 10, 6))
for sx in A.SIDES:
    body.append(cyl(f'tg{sx}', (sx * 0.025, -1.15, 0.23), (sx * 0.025, -1.32, 0.23), 0.012, 'metal', 6, bevel=0))
# Dorsal turret: a ring, a glass dome and twin guns.
body.append(cyl('dtring', (0, -0.3, 0.41), (0, -0.3, 0.46), 0.12, 'mech', 14, bevel=0.006))
body.append(sphere('dtdome', (0, -0.3, 0.47), 0.09, 'glass', 12, 6))
for sx in A.SIDES:
    body.append(cyl(f'dg{sx}', (sx * 0.035, -0.36, 0.5), (sx * 0.035, -0.62, 0.52), 0.014, 'metal', 6, bevel=0))
body.append(cyl('ant', (0.1, 0.1, 0.42), (0.14, -0.1, 0.58), 0.006, 'mech', 4, bevel=0))

# ---- Bomb bay underneath ----
body.append(box('bay', (0, -0.05, -0.05), (0.32, 0.8, 0.04), 'mech', 0.006, 1))
for sx in A.SIDES:
    body.append(box(f'door{sx}', (sx * 0.08, -0.05, -0.075), (0.14, 0.76, 0.014), 'hull_dark', 0.003, 1))
body.append(box('baylight', (0, 0.36, -0.07), (0.1, 0.02, 0.01), 'glow_dim', 0, 1))

A.finish('crucible_bomber', [('Body', body, None)], 512)

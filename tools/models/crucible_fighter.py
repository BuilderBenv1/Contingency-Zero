"""Crucible "Hornet" air-superiority fighter. Run: python tools/models/crucible_fighter.py <out_dir>

Stubby and brutal: a fat riveted fuselage with boxy intakes, straight square-tipped wings in team
paint, twin fat engines glowing orange, one tall fin and square tailplanes, a framed canopy,
and chunky AA rockets slung on rails under the wings.
Modelled around z = 0 (belly near 0); the game lifts it to flight altitude.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A

kit.reset('crucible')
body = []

# ---- Fuselage: blunt and fat, team top ----
secs = [
    A.hexring(1.16, 0.07, 0.05, 0.08, 0.15, 0.2),
    A.hexring(1.0, 0.15, 0.1, 0.05, 0.17, 0.28),
    A.hexring(0.68, 0.2, 0.17, 0.02, 0.2, 0.34),
    A.hexring(-0.5, 0.26, 0.2, 0.0, 0.22, 0.37),
    A.hexring(-0.82, 0.25, 0.19, 0.02, 0.2, 0.33),
]
band = {0: 'hull_dark', 1: 'hull', 5: 'hull', 2: 'hull_dark', 4: 'hull_dark', 3: 'hull'}
fus = A.loft('fus', secs, 'hull', lambda j: band[j], cap0='hull_dark', cap1='mech', bevel=0.014)
inset_panels(fus, 0.025, 0.008, lambda f: f.normal.z > 0.6 and f.calc_area() > 0.08)
body.append(fus)
# Nose gun: a stubby cannon in a chin fairing.
body.append(box('chin', (0, 0.95, 0.05), (0.12, 0.2, 0.06), 'hull_dark', 0.012, 1))
body += A.barrel('gun', (0, 1.0, 0.05), (0, 1.24, 0.05), 0.02, brake=False, paint='metal')
body.append(cyl('gunmz', (0, 1.22, 0.05), (0, 1.26, 0.05), 0.028, 'mech', 8, bevel=0))
# Framed slab canopy: glass block with three frame bars.
body.append(hull_from('canopy', [(0.1, 0.78, 0.29), (-0.1, 0.78, 0.29), (0.11, 0.36, 0.34), (-0.11, 0.36, 0.34),
                                 (0.08, 0.68, 0.38), (-0.08, 0.68, 0.38), (0.085, 0.4, 0.43), (-0.085, 0.4, 0.43)], 'glass', bevel=0.01))
for k, y in enumerate((0.69, 0.56, 0.42)):
    body.append(box(f'cbar{k}', (0, y, 0.4 + (0.69 - y) * 0.1), (0.2, 0.025, 0.035), 'mech', 0.004, 1))
body.append(box('cspine', (0, 0.56, 0.425), (0.02, 0.32, 0.02), 'mech', 0, 1))
# Boxy intakes either side with grille bars.
for sx in A.SIDES:
    body.append(box(f'intake{sx}', (sx * 0.31, 0.2, 0.17), (0.15, 0.44, 0.22), 'hull_dark', 0.014, 2))
    body.append(box(f'mouth{sx}', (sx * 0.31, 0.42, 0.17), (0.11, 0.02, 0.16), 'mech', 0, 1))
    for k in range(3):
        body.append(box(f'grill{sx}{k}', (sx * 0.31, 0.435, 0.12 + k * 0.05), (0.12, 0.012, 0.012), 'metal', 0, 1))
    body += rivets(f'irv{sx}', (sx * 0.31, 0.38, 0.285), (sx * 0.31, 0.02, 0.285), 4, 'metal', 0.014)

# ---- Wings: straight, square-tipped, team tops ----
plan = [(0.24, 0.2), (1.0, 0.06), (1.0, -0.4), (0.24, -0.46)]
body += A.wing('wing', plan, 0.15, 0.075, 0.05, 'team', 'hull_dark')
for sx in A.SIDES:
    # Wingtip plate, a bolted root fairing, flaps.
    body.append(box(f'tip{sx}', (sx * 1.01, -0.17, 0.15), (0.04, 0.5, 0.1), 'hull_dark', 0.01, 1))
    body.append(sphere(f'navlt{sx}', (sx * 1.035, 0.05, 0.15), 0.02, 'glow', 8, 4))
    body.append(box(f'flap{sx}', (sx * 0.52, -0.42, 0.155), (0.5, 0.07, 0.03), 'hull', 0.006, 1))
    body += rivets(f'wrv{sx}', (sx * 0.3, 0.12, 0.192), (sx * 0.3, -0.38, 0.192), 4, 'metal', 0.014)
    body += rivets(f'wrv2{sx}', (sx * 0.36, -0.4, 0.192), (sx * 0.92, -0.4, 0.192), 5, 'metal', 0.013)
    # Two fat AA rockets per side on rails under the wing.
    for x in (0.56, 0.82):
        X = sx * x
        body.append(box(f'urail{sx}{x}', (X, -0.12, 0.09), (0.03, 0.5, 0.04), 'mech', 0.004, 1))
        body.append(cyl(f'rkt{sx}{x}', (X, -0.42, 0.04), (X, 0.16, 0.04), 0.042, 'accent', 8, bevel=0.004))
        body.append(cyl(f'rktn{sx}{x}', (X, 0.16, 0.04), (X, 0.26, 0.04), 0.042, 'metal', 8, r2=0.01, bevel=0))
        body.append(cyl(f'rktb{sx}{x}', (X, -0.02, 0.04), (X, 0.02, 0.04), 0.046, 'mech', 8, bevel=0))
        for k in range(4):
            a = k * math.pi / 2 + math.pi / 4
            body.append(box(f'rktf{sx}{x}{k}', (X + math.cos(a) * 0.05, -0.38, 0.04 + math.sin(a) * 0.05), (0.012, 0.08, 0.012), 'hull_dark', 0, 1))

# ---- Tail: one tall fin (team), square tailplanes ----
body.append(prism_x('fin', [(-0.42, 0.35), (-0.82, 0.76), (-1.0, 0.76), (-1.0, 0.35)], -0.03, 0.03, 'team', 0.01, 1))
body.append(box('fintop', (0, -0.91, 0.77), (0.07, 0.2, 0.03), 'hull_dark', 0.006, 1))
body.append(sphere('finlt', (0, -1.0, 0.78), 0.018, 'glow', 8, 4))
body += rivets('frv', (0.032, -0.55, 0.42), (0.032, -0.95, 0.42), 4, 'metal', 0.012)
body += A.wing('tailplane', [(0.16, -0.66), (0.56, -0.74), (0.56, -0.98), (0.16, -1.0)], 0.21, 0.04, 0.03, 'team', 'hull_dark', panels=False)

# ---- Twin fat engines, orange glow ----
for sx in A.SIDES:
    body += A.thruster(f'eng{sx}', (sx * 0.15, -0.7, 0.17), (sx * 0.15, -1.15, 0.17), 0.15, verts=12)
    body.append(cyl(f'engband{sx}', (sx * 0.15, -0.88, 0.17), (sx * 0.15, -0.94, 0.17), 0.148, 'accent', 12, bevel=0))
# Dorsal spine: a riveted strap down the team top, an air brake panel and antenna.
body += rivets('srv', (0, 0.3, 0.375), (0, -0.7, 0.375), 7, 'metal', 0.016)
body.append(box('brake', (0, -0.62, 0.372), (0.18, 0.18, 0.02), 'hull_dark', 0.004, 1))
body.append(cyl('ant', (0.08, -0.2, 0.37), (0.1, -0.3, 0.5), 0.006, 'mech', 4, bevel=0))
# Belly: a fuel tank between the intakes.
body.append(cyl('tank', (0, -0.3, -0.02), (0, 0.4, -0.02), 0.07, 'hull_dark', 10, bevel=0.006))

A.finish('crucible_fighter', [('Body', body, None)], 512)

"""Crucible "Flak Truck". Run: python tools/models/crucible_flak_vehicle.py <out_dir>

A six-wheeled armoured truck: a riveted, team-painted cab up front with a slit windscreen and
grille, team fenders over big lugged wheels, an exhaust stack, and on the flatbed a traversing
mount with a shielded quad autocannon raised almost straight up, fed from ammo drums.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import vehicles_parts as vp

kit.reset('crucible')
body = []

# ---- Wheels and chassis ----
for sx in (-1, 1):
    for y in (0.72, -0.12, -0.72):
        body += vp.crucible_wheel(f'w{sx}{y}', (sx * 0.62, y, 0.26), 0.26, 0.2, sx)
body.append(box('frame', (0, -0.02, 0.36), (0.9, 2.1, 0.16), 'mech', 0.015))
for sx in (-1, 1):
    body.append(box(f'rail{sx}', (sx * 0.4, -0.02, 0.3), (0.08, 2.14, 0.1), 'hull_dark', 0.01, 1))
# Axle housings.
for y in (0.72, -0.12, -0.72):
    body.append(cyl(f'axle{y}', (-0.52, y, 0.26), (0.52, y, 0.26), 0.05, 'mech', 8))

# ---- Cab ----
cab = hull_from('cab', [(-0.52, 1.02, 0.44), (0.52, 1.02, 0.44), (-0.52, 0.5, 0.44), (0.52, 0.5, 0.44),
                        (-0.5, 1.0, 0.76), (0.5, 1.0, 0.76), (-0.48, 0.88, 0.92), (0.48, 0.88, 0.92),
                        (-0.5, 0.5, 0.92), (0.5, 0.5, 0.92)], 'team', 0.025, 2)
inset_panels(cab, 0.03, 0.01, lambda f: abs(f.normal.x) > 0.8 and f.calc_area() > 0.05)
body.append(cab)
body.append(box('cabroof', (0, 0.66, 0.935), (0.86, 0.36, 0.04), 'team_dark', 0.01, 1))
body.append(hull_from('screen', [(-0.4, 0.945, 0.82), (0.4, 0.945, 0.82), (-0.38, 0.895, 0.89), (0.38, 0.895, 0.89),
                                 (-0.4, 0.93, 0.81), (0.4, 0.93, 0.81), (-0.38, 0.88, 0.88), (0.38, 0.88, 0.88)], 'glass', 0.003, 1))
for k in range(3):
    body.append(box(f'screenbar{k}', (-0.2 + k * 0.2, 0.93, 0.85), (0.03, 0.04, 0.09), 'mech', 0.004, 1))
body.append(box('grille', (0, 1.035, 0.58), (0.6, 0.03, 0.2), 'mech', 0.006, 1))
for k in range(6):
    body.append(box(f'gslat{k}', (0, 1.05, 0.5 + k * 0.032), (0.56, 0.015, 0.012), 'metal', 0, 1))
body.append(box('bumper', (0, 1.1, 0.36), (1.0, 0.1, 0.12), 'hull_dark', 0.015, 1))
for sx in (-1, 1):
    body.append(cyl(f'lamp{sx}', (sx * 0.4, 1.03, 0.66), (sx * 0.4, 1.07, 0.66), 0.045, 'glow', 8, bevel=0))
    body.append(cyl(f'lamphood{sx}', (sx * 0.4, 1.01, 0.66), (sx * 0.4, 1.05, 0.66), 0.06, 'mech', 8, bevel=0))
    body.append(box(f'hook{sx}', (sx * 0.22, 1.17, 0.36), (0.05, 0.05, 0.06), 'metal', 0.01, 1))
body += rivets('cabrv', (-0.44, 1.03, 0.72), (0.44, 1.03, 0.72), 7, 'metal', 0.016)
body += rivets('roofrv', (-0.4, 0.5, 0.96), (0.4, 0.5, 0.96), 6, 'metal', 0.016)
# Mirror arms.
for sx in (-1, 1):
    body.append(cyl(f'mirarm{sx}', (sx * 0.52, 0.9, 0.8), (sx * 0.62, 0.95, 0.82), 0.01, 'mech', 5, bevel=0))
    body.append(box(f'mirror{sx}', (sx * 0.63, 0.95, 0.84), (0.03, 0.02, 0.08), 'mech', 0.004, 1))

# ---- Fenders (team) over the wheels, with steps ----
for sx in (-1, 1):
    for y in (0.72, -0.42):
        ln = 0.62 if y > 0 else 0.98
        body.append(prism_x(f'fender{sx}{y}', [(y + ln / 2, 0.56), (y + ln / 2 - 0.08, 0.64), (y - ln / 2 + 0.08, 0.64), (y - ln / 2, 0.56)],
                            sx * 0.5 if sx > 0 else sx * 0.76, sx * 0.76 if sx > 0 else sx * 0.5, 'team', 0.008, 1))
    body.append(box(f'step{sx}', (sx * 0.58, 0.28, 0.5), (0.14, 0.12, 0.03), 'metal', 0.006, 1))

# ---- Flatbed ----
bed = box('bed', (0, -0.42, 0.5), (1.0, 1.24, 0.12), 'hull_dark', 0.015)
body.append(bed)
body += rivets('bedrv', (-0.46, -1.02, 0.565), (0.46, -1.02, 0.565), 7, 'metal', 0.016)
for sx in (-1, 1):
    body += rivets(f'bedsr{sx}', (sx * 0.47, 0.12, 0.565), (sx * 0.47, -0.98, 0.565), 6, 'metal', 0.016)
# Ammo crates at the back corners, the stack behind the cab.
for sx in (-1, 1):
    body.append(box(f'crate{sx}', (sx * 0.34, -0.92, 0.62), (0.2, 0.16, 0.12), 'accent', 0.01, 1))
    body.append(box(f'crateband{sx}', (sx * 0.34, -0.92, 0.62), (0.21, 0.02, 0.125), 'mech', 0, 1))
body += vp.stack('stack', (-0.42, 0.4, 0.56), 0.06, 0.52)
body.append(box('tank', (0.38, 0.38, 0.62), (0.16, 0.18, 0.12), 'hull', 0.01, 1))
for sx in (-1, 1):
    body.append(box(f'tail{sx}', (sx * 0.4, -1.045, 0.46), (0.08, 0.02, 0.04), 'glow_dim', 0, 1))
body.append(cyl('tring', (0, -0.36, 0.56), (0, -0.36, 0.6), 0.38, 'mech', 16, bevel=0.006))

# ---- Turret: pivot on the bed ----
PIV = (0.0, -0.36, 0.58)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = []
tur.append(cyl('ring', P(0, 0, 0), P(0, 0, 0.1), 0.38, 'hull_dark', 12, r2=0.36, bevel=0.012))
tur += rivets('ringrv', P(-0.3, 0.2, 0.105), P(0.3, 0.2, 0.105), 5, 'metal', 0.016)
# Cradle: a heavy box with trunnion cheeks.
tur.append(box('cradle', P(0, -0.02, 0.24), (0.36, 0.38, 0.24), 'hull', 0.02))
for sx in (-1, 1):
    tur.append(box(f'cheek{sx}', P(sx * 0.22, -0.02, 0.3), (0.08, 0.32, 0.34), 'team', 0.015, 1))
    tur.append(cyl(f'trun{sx}', P(sx * 0.26, 0.0, 0.34), P(sx * 0.3, 0.0, 0.34), 0.07, 'mech', 10))
    # Ammo drums on the outside.
    tur.append(cyl(f'drum{sx}', P(sx * 0.32, -0.12, 0.2), P(sx * 0.42, -0.12, 0.2), 0.11, 'accent', 12))
    tur.append(cyl(f'drumcap{sx}', P(sx * 0.42, -0.12, 0.2), P(sx * 0.44, -0.12, 0.2), 0.06, 'mech', 8, bevel=0))
# Gun shield: angled team plate in front of the guns, with vision slits.
tur.append(hull_from('shield', [P(-0.34, 0.24, 0.1), P(0.34, 0.24, 0.1), P(-0.32, 0.3, 0.52), P(0.32, 0.3, 0.52),
                                P(-0.34, 0.18, 0.1), P(0.34, 0.18, 0.1), P(-0.32, 0.24, 0.52), P(0.32, 0.24, 0.52)], 'team', 0.012, 1))
tur += rivets('shrv', P(-0.28, 0.305, 0.48), P(0.28, 0.305, 0.48), 6, 'metal', 0.016)
tur.append(box('slit', P(0, 0.3, 0.38), (0.12, 0.012, 0.025), 'glow', 0, 1))
# Quad autocannons raised to 65 degrees, poking up through the shield top.
for sx in (-1, 1):
    for k, off in enumerate((0.07, 0.17)):
        root = P(sx * off, 0.08, 0.34)
        tur.append(cyl(f'brl{sx}{k}', root, vp.elevated(root, 65, 0.78), 0.026, 'metal', 8, bevel=0.003))
        tur.append(cyl(f'jacket{sx}{k}', root, vp.elevated(root, 65, 0.3), 0.04, 'hull_dark', 8, bevel=0.004))
        tur.append(cyl(f'flash{sx}{k}', vp.elevated(root, 65, 0.7), vp.elevated(root, 65, 0.82), 0.036, 'mech', 8, bevel=0.003))
        tur.append(cyl(f'mz{sx}{k}', vp.elevated(root, 65, 0.82), vp.elevated(root, 65, 0.83), 0.022, 'glow_dim', 8, bevel=0))
tur.append(box('brace', vp.elevated(P(0, 0.08, 0.34), 65, 0.5), (0.42, 0.05, 0.05), 'hull_dark', 0.006, 1))
# Gunner seat and sight at the back of the mount.
tur.append(box('seat', P(0, -0.26, 0.2), (0.16, 0.12, 0.14), 'hull_dark', 0.01, 1))
tur.append(cyl('sightpost', P(0.12, -0.2, 0.36), P(0.12, -0.2, 0.5), 0.015, 'mech', 6, bevel=0))
tur.append(cyl('sightring', P(0.12, -0.2, 0.52), P(0.12, -0.16, 0.54), 0.05, 'metal', 10, bevel=0))

vp.finish('crucible_flak_vehicle', body, tur, PIV)

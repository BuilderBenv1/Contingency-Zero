"""Crucible "Hauler" flying constructor. Run: python tools/models/crucible_builder_air.py <out_dir>

A boxy rotor lifter: a riveted cargo body with a team roof, a blunt cab with a slab windscreen,
team side pods carrying two hydraulic nanolathe arms that reach forward with orange-glowing tips,
a winch and hanging crane hook under the belly, twin exhausts at the back, and a three-bladed
main rotor with team tips. The rotor is the spinning part (Rotor_Y, pivot at the hub).
Modelled around z = 0; the game lifts it to flight altitude.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A

kit.reset('crucible')
body = []

# ---- Cargo body and cab ----
cargo = box('cargo', (0, -0.08, 0.22), (0.66, 1.3, 0.42), 'hull', 0.03, 2)
inset_panels(cargo, 0.035, 0.01, lambda f: abs(f.normal.x) > 0.9 and f.calc_area() > 0.1)
body.append(cargo)
roof = box('roof', (0, -0.12, 0.44), (0.56, 1.0, 0.04), 'team', 0.012, 1)
inset_panels(roof, 0.03, 0.008, lambda f: f.normal.z > 0.9)
body.append(roof)
body.append(hull_from('cab', [(0.33, 0.56, 0.02), (-0.33, 0.56, 0.02), (0.33, 0.56, 0.42), (-0.33, 0.56, 0.42),
                              (0.25, 0.94, 0.04), (-0.25, 0.94, 0.04), (0.25, 0.98, 0.18), (-0.25, 0.98, 0.18),
                              (0.27, 0.78, 0.38), (-0.27, 0.78, 0.38)], 'hull_dark', bevel=0.02))
body.append(hull_from('screen', [(0.22, 0.79, 0.37), (-0.22, 0.79, 0.37), (0.22, 0.95, 0.21), (-0.22, 0.95, 0.21),
                                 (0.22, 0.8, 0.39), (-0.22, 0.8, 0.39), (0.22, 0.97, 0.22), (-0.22, 0.97, 0.22)], 'glass', bevel=0.004))
body.append(box('sbar', (0, 0.88, 0.31), (0.025, 0.2, 0.025), 'mech', 0, 1))
A.xform([body[-1]], (0, 0.88, 0.31), (-0.8, 0, 0))
for sx in A.SIDES:
    body.append(cyl(f'lamp{sx}', (sx * 0.2, 0.96, 0.08), (sx * 0.2, 1.0, 0.08), 0.035, 'glow', 8, bevel=0))
    body += rivets(f'rr{sx}', (sx * 0.3, 0.5, 0.44), (sx * 0.3, -0.66, 0.44), 6, 'metal', 0.017)
# Cargo straps (accent) across the body sides and roof.
for y in (0.25, -0.4):
    body.append(box(f'strap{y}', (0, y, 0.24), (0.68, 0.05, 0.45), 'accent', 0.006, 1))

# ---- Side pods with hydraulic nanolathe arms ----
for sx in A.SIDES:
    X = sx * 0.42
    body.append(box(f'pod{sx}', (X, -0.3, 0.22), (0.16, 0.6, 0.28), 'team', 0.02, 2))
    body.append(box(f'podvent{sx}', (sx * 0.505, -0.3, 0.22), (0.01, 0.4, 0.1), 'glow_dim', 0, 1))
    body += rivets(f'pr{sx}', (X, -0.04, 0.37), (X, -0.56, 0.37), 4, 'metal', 0.015)
    # Arm: shoulder block, upper boom, hydraulic ram, forearm, emitter.
    sh = (sx * 0.46, 0.08, 0.24)
    el = (sx * 0.52, 0.5, 0.34)
    wr = (sx * 0.4, 0.92, 0.14)
    body.append(box(f'shoulder{sx}', sh, (0.12, 0.14, 0.14), 'mech', 0.012, 1))
    body.append(cyl(f'boom{sx}', sh, el, 0.045, 'accent', 8, bevel=0.004))
    body.append(cyl(f'ram{sx}', (sx * 0.46, 0.06, 0.16), (sx * 0.5, 0.42, 0.28), 0.018, 'metal', 6, bevel=0))
    body.append(box(f'elbow{sx}', el, (0.09, 0.09, 0.09), 'mech', 0.01, 1))
    body.append(cyl(f'fore{sx}', el, wr, 0.035, 'hull_dark', 8, bevel=0.004))
    body += A.nano_tip(f'tip{sx}', wr, (sx * 0.38, 1.02, 0.08), 0.03)
    # Exhausts.
    body += A.thruster(f'ex{sx}', (sx * 0.2, -0.72, 0.3), (sx * 0.2, -0.98, 0.3), 0.09, verts=10)
    body.append(box(f'exmount{sx}', (sx * 0.2, -0.74, 0.3), (0.2, 0.06, 0.2), 'hull_dark', 0.01, 1))

# ---- Winch and hanging crane hook under the belly ----
body.append(box('winch', (0, -0.1, -0.02), (0.3, 0.2, 0.06), 'mech', 0.01, 1))
body.append(cyl('drum', (-0.12, -0.1, -0.06), (0.12, -0.1, -0.06), 0.04, 'accent', 10, bevel=0))
body.append(cyl('cable', (0, -0.1, -0.06), (0, -0.1, -0.24), 0.008, 'metal', 4, bevel=0))
body.append(box('block', (0, -0.1, -0.27), (0.08, 0.05, 0.08), 'hull_dark', 0.01, 1))
body.append(cyl('hook', (0, -0.1, -0.31), (0, -0.04, -0.36), 0.015, 'metal', 6, bevel=0))
body.append(sphere('hookglow', (0, -0.1, -0.32), 0.022, 'glow', 8, 4))

# ---- Rotor mast ----
body.append(cyl('mastbase', (0, 0.0, 0.44), (0, 0.0, 0.52), 0.14, 'hull_dark', 10, r2=0.11, bevel=0.006))
body.append(cyl('mast', (0, 0.0, 0.52), (0, 0.0, 0.6), 0.07, 'metal', 10, bevel=0.004))
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    body.append(cyl(f'brace{k}', (math.cos(a) * 0.22, math.sin(a) * 0.22, 0.45), (math.cos(a) * 0.08, math.sin(a) * 0.08, 0.56), 0.012, 'metal', 4, bevel=0))

# ---- Main rotor: three wide blades, team tips ----
PIV = (0, 0.0, 0.6)
rot = [cyl('hub', (0, 0, 0.56), (0, 0, 0.66), 0.12, 'metal', 10, r2=0.1, bevel=0.006),
       cyl('cap', (0, 0, 0.66), (0, 0, 0.7), 0.07, 'mech', 10, r2=0.03, bevel=0)]
for i in range(3):
    a = i * 2 * math.pi / 3
    parts = [box(f'grip{i}', (0, 0.16, 0.62), (0.09, 0.14, 0.05), 'mech', 0.008, 1),
             box(f'blade{i}', (0, 0.66, 0.625), (0.17, 1.0, 0.026), 'mech', 0.006, 1),
             box(f'tip{i}', (0, 1.12, 0.627), (0.18, 0.18, 0.032), 'team', 0.005, 1),
             box(f'stripe{i}', (0, 0.94, 0.627), (0.175, 0.04, 0.03), 'accent', 0.003, 1)]
    A.xform(parts, PIV, (0, 0, a))
    rot += parts

A.finish('crucible_builder_air', [('Body', body, None), ('Rotor_Y', rot, PIV)], 512)

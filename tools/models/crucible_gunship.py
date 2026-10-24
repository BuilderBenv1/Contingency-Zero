"""Crucible "Buzzsaw" gunship. Run: python tools/models/crucible_gunship.py <out_dir>

A brutal attack helicopter: an armoured riveted tub with a slab windscreen, stub wings carrying
twin rotary gun pods, skids, twin exhausts glowing orange, a tail boom with a team fin and tail
rotor, and a four-bladed main rotor with team-painted tips. The main rotor is the spinning part
(Rotor_Y, pivot at the hub). Modelled around z = 0; the game lifts it to flight altitude.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A

kit.reset('crucible')
body = []

# ---- Armoured tub ----
secs = [
    A.hexring(1.05, 0.11, 0.07, 0.1, 0.2, 0.28),
    A.hexring(0.86, 0.22, 0.16, 0.04, 0.26, 0.4),
    A.hexring(0.62, 0.28, 0.22, 0.02, 0.3, 0.5),
    A.hexring(-0.3, 0.28, 0.22, 0.02, 0.3, 0.5),
    A.hexring(-0.56, 0.15, 0.1, 0.17, 0.3, 0.4),
]
band = {0: 'hull_dark', 1: 'hull', 5: 'hull', 2: 'hull_dark', 4: 'hull_dark', 3: 'team'}
tub = A.loft('tub', secs, 'hull', lambda j: band[j], cap0='hull_dark', cap1='hull_dark', bevel=0.014)
inset_panels(tub, 0.03, 0.008, lambda f: f.normal.z > 0.6 and f.calc_area() > 0.06)
body.append(tub)
# Slab windscreen with frame bars; a chin sensor with an orange eye.
body.append(hull_from('screen', [(0.2, 0.86, 0.3), (-0.2, 0.86, 0.3), (0.2, 0.86, 0.38), (-0.2, 0.86, 0.38),
                                 (0.2, 0.64, 0.5), (-0.2, 0.64, 0.5), (0.2, 0.62, 0.42), (-0.2, 0.62, 0.42)], 'glass', bevel=0.008))
for x in (-0.07, 0.07):
    body.append(box(f'sbar{x}', (x, 0.76, 0.43), (0.025, 0.26, 0.025), 'mech', 0, 1))
    A.xform([body[-1]], (x, 0.76, 0.43), (-0.62, 0, 0))
body.append(box('chin', (0, 0.98, 0.07), (0.14, 0.12, 0.1), 'hull_dark', 0.012, 1))
body.append(box('chineye', (0, 1.045, 0.08), (0.08, 0.01, 0.03), 'glow', 0, 1))
for sx in A.SIDES:
    body += rivets(f'trv{sx}', (sx * 0.27, 0.55, 0.36), (sx * 0.27, -0.25, 0.36), 6, 'metal', 0.016)
    # Armour plates bolted on the flanks.
    body.append(box(f'flank{sx}', (sx * 0.29, 0.15, 0.17), (0.03, 0.5, 0.18), 'accent', 0.008, 1))

# ---- Tail boom, fin, tail rotor ----
boom = [A.hexring(-0.45, 0.11, 0.09, 0.22, 0.32, 0.4), A.hexring(-1.15, 0.05, 0.04, 0.31, 0.36, 0.4)]
body.append(A.loft('boom', boom, 'hull', lambda j: 'team_dark' if j == 3 else None, cap0='hull', cap1='hull', bevel=0.008))
body.append(prism_x('fin', [(-0.98, 0.34), (-1.1, 0.64), (-1.22, 0.64), (-1.22, 0.32)], -0.02, 0.02, 'team', 0.008, 1))
body.append(box('stab', (0, -1.0, 0.36), (0.42, 0.1, 0.025), 'hull_dark', 0.006, 1))
body.append(cyl('trhub', (0.02, -1.12, 0.5), (0.07, -1.12, 0.5), 0.03, 'mech', 8, bevel=0))
for k in range(3):
    a = k * 2 * math.pi / 3 + 0.3
    body.append(box(f'trb{k}', (0.07, -1.12 + math.cos(a) * 0.1, 0.5 + math.sin(a) * 0.1), (0.012, 0.035, 0.2), 'mech', 0, 1))
    A.xform([body[-1]], (0.07, -1.12 + math.cos(a) * 0.1, 0.5 + math.sin(a) * 0.1), (-a + math.pi / 2, 0, 0))
body.append(sphere('taillt', (0, -1.23, 0.62), 0.018, 'glow', 8, 4))

# ---- Stub wings with rotary gun pods ----
body += A.wing('wing', [(0.24, 0.12), (0.66, 0.1), (0.66, -0.14), (0.24, -0.18)], 0.15, 0.06, 0.05, 'team', 'hull_dark')
for sx in A.SIDES:
    X = sx * 0.6
    body.append(box(f'pod{sx}', (X, 0.0, 0.06), (0.14, 0.4, 0.14), 'hull_dark', 0.016, 2))
    body.append(box(f'podband{sx}', (X, 0.1, 0.06), (0.15, 0.05, 0.15), 'accent', 0.006, 1))
    body.append(cyl(f'podring{sx}', (X, 0.2, 0.06), (X, 0.24, 0.06), 0.06, 'mech', 10, bevel=0))
    for k in range(3):
        a = k * 2 * math.pi / 3
        body += A.barrel(f'gb{sx}{k}', (X + math.cos(a) * 0.028, 0.22, 0.06 + math.sin(a) * 0.028),
                         (X + math.cos(a) * 0.028, 0.5, 0.06 + math.sin(a) * 0.028), 0.014, brake=False, verts=6)
    body.append(cyl(f'muz{sx}', (X, 0.44, 0.06), (X, 0.47, 0.06), 0.052, 'mech', 10, bevel=0))
    body += rivets(f'prv{sx}', (X, 0.15, 0.135), (X, -0.15, 0.135), 3, 'metal', 0.013)
    # Skids: two struts and an upturned rail.
    for y in (-0.3, 0.3):
        body.append(cyl(f'strut{sx}{y}', (sx * 0.2, y, 0.06), (sx * 0.3, y, -0.14), 0.02, 'metal', 6, bevel=0))
    body.append(cyl(f'skid{sx}', (sx * 0.3, -0.45, -0.15), (sx * 0.3, 0.42, -0.15), 0.022, 'metal', 6, bevel=0))
    body.append(cyl(f'skidtip{sx}', (sx * 0.3, 0.42, -0.15), (sx * 0.3, 0.52, -0.09), 0.022, 'metal', 6, bevel=0))
    # Twin exhausts on top, glowing orange at the back.
    body.append(box(f'cowl{sx}', (sx * 0.17, -0.1, 0.52), (0.14, 0.4, 0.08), 'hull_dark', 0.012, 1))
    body += A.thruster(f'ex{sx}', (sx * 0.17, -0.12, 0.53), (sx * 0.17, -0.42, 0.53), 0.07, verts=10)
    for k in range(3):
        body.append(box(f'vent{sx}{k}', (sx * 0.17, 0.0 - k * 0.06, 0.565), (0.1, 0.025, 0.012), 'mech', 0, 1))
# Rotor mast.
body.append(cyl('mastbase', (0, 0.1, 0.48), (0, 0.1, 0.56), 0.13, 'hull_dark', 10, r2=0.1, bevel=0.006))
body.append(cyl('mast', (0, 0.1, 0.56), (0, 0.1, 0.64), 0.07, 'metal', 10, bevel=0.004))

# ---- Main rotor (spinning part): four blades, team tips ----
PIV = (0, 0.1, 0.64)
rot = []
rot.append(cyl('hub', (0, 0.1, 0.6), (0, 0.1, 0.7), 0.1, 'metal', 10, r2=0.08, bevel=0.006))
rot.append(cyl('hubcap', (0, 0.1, 0.7), (0, 0.1, 0.74), 0.06, 'mech', 10, r2=0.03, bevel=0))
for i in range(4):
    a = i * math.pi / 2 + 0.4
    parts = [box(f'grip{i}', (0, 0.1 + 0.14, 0.66), (0.07, 0.12, 0.04), 'mech', 0.006, 1),
             box(f'blade{i}', (0, 0.1 + 0.68, 0.665), (0.13, 1.08, 0.022), 'mech', 0.006, 1),
             box(f'tip{i}', (0, 0.1 + 1.17, 0.667), (0.14, 0.18, 0.028), 'team', 0.005, 1),
             box(f'stripe{i}', (0, 0.1 + 0.96, 0.667), (0.135, 0.04, 0.026), 'accent', 0.003, 1)]
    A.xform(parts, PIV, (0, 0, a))
    rot += parts

A.finish('crucible_gunship', [('Body', body, None), ('Rotor_Y', rot, PIV)], 512)

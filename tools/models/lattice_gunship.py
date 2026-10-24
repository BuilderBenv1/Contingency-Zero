"""Lattice "Seraph" gunship. Run: python tools/models/lattice_gunship.py <out_dir>

A twin-duct VTOL: a faceted pod fuselage with a team spine, a wraparound canopy, stub wings
carrying two big ducted fans (team rims, blue-glowing fan discs, stator vanes), a canted V-tail,
and a chin-mounted ball turret with a long beam emitter for strafing ground targets.
Modelled around z = 0 (belly near 0); the game lifts it to flight altitude.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A

kit.reset('lattice')
body = []

# ---- Fuselage pod ----
secs = [
    A.octring(1.02, 0.03, 0.05, 0.03, 0.07, 0.12, 0.16, 0.2),
    A.octring(0.72, 0.13, 0.22, 0.1, -0.02, 0.1, 0.24, 0.34),
    A.octring(0.3, 0.16, 0.27, 0.13, -0.04, 0.1, 0.26, 0.41),
    A.octring(-0.35, 0.15, 0.26, 0.12, -0.02, 0.1, 0.26, 0.4),
    A.octring(-0.8, 0.06, 0.12, 0.06, 0.12, 0.17, 0.25, 0.3),
    A.octring(-1.14, 0.03, 0.05, 0.03, 0.19, 0.21, 0.25, 0.27),
]
band = {0: 'hull_dark', 1: 'hull_dark', 7: 'hull_dark', 2: 'hull', 6: 'hull', 3: 'hull', 5: 'hull', 4: 'team'}
body.append(A.loft('fus', secs, 'hull', lambda j: band[j], cap0='hull', cap1='mech', bevel=0.01))
body.append(hull_from('canopy', [(0.1, 0.86, 0.22), (-0.1, 0.86, 0.22), (0.15, 0.52, 0.34), (-0.15, 0.52, 0.34),
                                 (0.07, 0.66, 0.38), (-0.07, 0.66, 0.38), (0.08, 0.42, 0.42), (-0.08, 0.42, 0.42),
                                 (0.17, 0.6, 0.22), (-0.17, 0.6, 0.22)], 'glass', bevel=0.012, segs=2))
body.append(box('cbar', (0, 0.6, 0.4), (0.18, 0.02, 0.02), 'mech', 0, 1))
body.append(box('noseeye', (0, 1.0, 0.13), (0.07, 0.03, 0.025), 'glow', 0, 1))
# Spine: a sensor mast and blue data strips beside the team spine.
body.append(hull_from('spinebox', [(0.07, 0.1, 0.39), (-0.07, 0.1, 0.39), (0.07, -0.45, 0.38), (-0.07, -0.45, 0.38),
                                   (0.04, 0.0, 0.45), (-0.04, 0.0, 0.45), (0.04, -0.38, 0.44), (-0.04, -0.38, 0.44)], 'hull', bevel=0.008))
body.append(cyl('mast', (0, -0.2, 0.44), (0, -0.25, 0.6), 0.012, 'metal', 6, bevel=0))
body.append(sphere('mastlt', (0, -0.25, 0.61), 0.022, 'glow', 8, 4))
for sx in A.SIDES:
    body.append(box(f'strip{sx}', (sx * 0.135, -0.1, 0.4), (0.012, 0.5, 0.008), 'glow_dim', 0, 1))

# ---- Stub wings and the two ducted fans ----
body += A.wing('wing', [(0.2, 0.14), (0.66, 0.08), (0.66, -0.22), (0.2, -0.28)], 0.17, 0.07, 0.05, 'team', 'hull_dark')
for sx in A.SIDES:
    c = (sx * 0.84, -0.06, 0.12)
    body.append(A.ring(f'duct{sx}', c, 0.37, 0.29, 0.22, 'hull', top='team', verts=24))
    body.append(A.ring(f'ductlip{sx}', (c[0], c[1], 0.235), 0.375, 0.35, 0.02, 'hull_dark', verts=24, bevel=0))
    body.append(cyl(f'fan{sx}', (c[0], c[1], 0.07), (c[0], c[1], 0.09), 0.29, 'glow_dim', 24, bevel=0))
    body.append(cyl(f'hub{sx}', (c[0], c[1], 0.06), (c[0], c[1], 0.16), 0.07, 'mech', 12, r2=0.05, bevel=0.004))
    for k in range(7):
        a = k * 2 * math.pi / 7
        bl = box(f'blade{sx}{k}', (c[0] + math.cos(a) * 0.18, c[1] + math.sin(a) * 0.18, 0.11), (0.2, 0.05, 0.012), 'mech', 0, 1)
        A.xform([bl], (c[0] + math.cos(a) * 0.18, c[1] + math.sin(a) * 0.18, 0.11), (0.35, 0, a))
        body.append(bl)
    # Stator struts across the duct, and a nav light on the rim.
    for a in (0.0, math.pi / 2):
        body.append(box(f'strut{sx}{a}', (c[0], c[1], 0.17), (0.6 * abs(math.cos(a)) + 0.03, 0.6 * abs(math.sin(a)) + 0.03, 0.025), 'hull', 0.004, 1))
    body.append(sphere(f'navlt{sx}', (sx * 1.22, -0.06, 0.2), 0.022, 'glow', 8, 4))

# ---- Canted V-tail ----
for sx in A.SIDES:
    fin = prism_x(f'fin{sx}', [(-0.68, 0.26), (-0.98, 0.58), (-1.12, 0.58), (-1.1, 0.24)], -0.016, 0.016, 'team', 0.006, 1)
    A.xform([fin], (0, 0, 0.25), (0, sx * 0.55, 0), (sx * 0.06, 0, 0))
    body.append(fin)

# ---- Chin ball turret with the beam emitter ----
body.append(cyl('turretmount', (0, 0.62, 0.0), (0, 0.62, 0.06), 0.1, 'hull_dark', 12, bevel=0.004))
body.append(sphere('ball', (0, 0.66, -0.02), 0.1, 'mech', 14, 8))
body.append(box('ballplate', (0, 0.7, -0.02), (0.1, 0.12, 0.13), 'hull', 0.02, 2))
body.append(cyl('lance', (0, 0.72, -0.02), (0, 1.22, -0.03), 0.045, 'metal', 10, r2=0.036, bevel=0.003))
for k in range(2):
    y = 0.88 + k * 0.13
    body.append(cyl(f'coil{k}', (0, y, -0.025), (0, y + 0.04, -0.025), 0.065, 'glow', 10, bevel=0))
body.append(cyl('lens', (0, 1.2, -0.03), (0, 1.28, -0.03), 0.06, 'mech', 10, r2=0.05, bevel=0))
body.append(sphere('lensglow', (0, 1.28, -0.03), 0.04, 'glow', 10, 6))

A.finish('lattice_gunship', [('Body', body, None)], 512)

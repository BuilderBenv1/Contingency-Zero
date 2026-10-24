"""Lattice "Lancet" bomber. Run: python tools/models/lattice_bomber.py <out_dir>

A stealthy flying wing: a sawtooth trailing edge, a raised centre body with a slit canopy and a
glowing sensor visor, team chevron plates across both wings, buried engines with dorsal intakes
and flat blue-glowing exhaust slots, and bomb-bay doors underneath.
Modelled around z = 0 (belly near 0); the game lifts it to flight altitude.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A

kit.reset('lattice')
body = []

# ---- The wing: sawtooth flying wing, thick at the root, knife-thin at the tips ----
plan = [(0.0, 1.18), (1.14, -0.5), (1.04, -0.76), (0.7, -0.56), (0.38, -0.84), (0.0, -0.64)]
body += A.wing('wing', plan, 0.12, 0.12, 0.022, 'hull', 'hull_dark', x_root=0.15, x_tip=1.14, dihedral=-0.02, panels=True)
# Team chevron plates riding on the wing tops.
for sx in A.SIDES:
    def zw(x):
        f = max(0, min(1, (x - 0.15) / 0.99))
        return 0.12 - 0.02 * (x - 0.15) + (0.12 + (0.022 - 0.12) * f) / 2
    pts = []
    for x, y in ((0.2, 0.86), (1.02, -0.36), (0.9, -0.56), (0.2, 0.28)):
        pts += [(sx * x, y, zw(x) + 0.004), (sx * x, y, zw(x) + 0.018)]
    plate = hull_from(f'chev{sx}', pts, 'team', bevel=0.004, segs=1)
    inset_panels(plate, 0.025, 0.004, lambda f: f.normal.z > 0.5)
    body.append(plate)
    # Outer grey panel and a thin blue edge light along the leading edge.
    pts = []
    for x, y in ((1.02, -0.36), (1.12, -0.5), (1.03, -0.72), (0.92, -0.58)):
        pts += [(sx * x, y, zw(x) + 0.002), (sx * x, y, zw(x) + 0.012)]
    body.append(hull_from(f'tipplate{sx}', pts, 'hull_dark', bevel=0.003, segs=1))
    body.append(cyl(f'edge{sx}', (sx * 0.26, 0.77, 0.172), (sx * 0.9, -0.17, 0.118), 0.008, 'glow', 6, bevel=0))
    body.append(sphere(f'navlt{sx}', (sx * 1.12, -0.52, 0.11), 0.018, 'glow', 8, 4))
    # Elevons on the sawtooth trailing edge.
    body.append(box(f'elev{sx}', (sx * 0.87, -0.655, 0.1), (0.3, 0.05, 0.016), 'mech', 0.003, 1))
    A.xform([body[-1]], (sx * 0.87, -0.655, 0.1), (0, 0, -sx * 0.53))

# ---- Centre body: a smooth hump with a slit canopy and sensor visor ----
secs = [
    A.octring(1.12, 0.01, 0.02, 0.01, 0.1, 0.12, 0.14, 0.15),
    A.octring(0.8, 0.1, 0.16, 0.08, 0.02, 0.12, 0.2, 0.28),
    A.octring(0.3, 0.16, 0.26, 0.13, 0.0, 0.12, 0.22, 0.36),
    A.octring(-0.3, 0.17, 0.27, 0.13, 0.0, 0.12, 0.22, 0.35),
    A.octring(-0.62, 0.14, 0.24, 0.1, 0.04, 0.11, 0.18, 0.25),
]
band = {0: 'hull_dark', 1: 'hull_dark', 7: 'hull_dark', 2: 'hull', 6: 'hull', 3: 'hull', 5: 'hull', 4: 'team_dark'}
hump = A.loft('hump', secs, 'hull', lambda j: band[j], cap0='hull', cap1='mech', bevel=0.01)
body.append(hump)
body.append(hull_from('canopy', [(0.07, 0.78, 0.29), (-0.07, 0.78, 0.29), (0.1, 0.5, 0.34), (-0.1, 0.5, 0.34),
                                 (0.05, 0.56, 0.37), (-0.05, 0.56, 0.37), (0.06, 0.36, 0.375), (-0.06, 0.36, 0.375)], 'glass', bevel=0.008))
body.append(box('visor', (0, 0.93, 0.165), (0.16, 0.02, 0.02), 'glow', 0, 1))
A.xform([body[-1]], (0, 0.93, 0.165), (-0.5, 0, 0))
# Dorsal spine: sensor blister and data strips.
body.append(sphere('blister', (0, 0.05, 0.36), 0.06, 'glass', 12, 6))
for sx in A.SIDES:
    body.append(box(f'strip{sx}', (sx * 0.13, -0.15, 0.352), (0.012, 0.5, 0.006), 'glow_dim', 0, 1))

# ---- Buried engines: dorsal intakes, flat glowing exhaust slots ----
for sx in A.SIDES:
    x = sx * 0.36
    body.append(hull_from(f'nacelle{sx}', [(x - 0.13, 0.36, 0.14), (x + 0.13, 0.36, 0.14), (x - 0.05, 0.22, 0.235), (x + 0.05, 0.22, 0.235),
                                           (x - 0.12, -0.6, 0.11), (x + 0.12, -0.6, 0.11), (x - 0.06, -0.5, 0.2), (x + 0.06, -0.5, 0.2),
                                           (x - 0.14, -0.1, 0.13), (x + 0.14, -0.1, 0.13)],
                          'hull', bevel=0.01))
    body.append(box(f'intake{sx}', (x, 0.296, 0.194), (0.13, 0.12, 0.03), 'mech', 0.004, 1))
    A.xform([body[-1]], (x, 0.296, 0.194), (-0.6, 0, 0))
    body.append(box(f'intakelip{sx}', (x, 0.35, 0.163), (0.15, 0.015, 0.02), 'metal', 0.003, 1))
    body.append(box(f'nlight{sx}', (x, -0.2, 0.2), (0.01, 0.4, 0.006), 'glow_dim', 0, 1))
    body.append(box(f'slot{sx}', (x, -0.6, 0.13), (0.2, 0.03, 0.04), 'mech', 0.004, 1))
    body.append(box(f'slotglow{sx}', (x, -0.614, 0.13), (0.17, 0.006, 0.025), 'glow', 0, 1))
    for k in range(3):
        body.append(box(f'vane{sx}{k}', (x - 0.06 + k * 0.06, -0.615, 0.13), (0.008, 0.012, 0.035), 'metal', 0, 1))

# ---- Underside: bomb bay doors and a payload hint ----
for sx in A.SIDES:
    body.append(box(f'bay{sx}', (sx * 0.08, -0.05, 0.002), (0.14, 0.62, 0.012), 'mech', 0.003, 1))
body.append(box('bayglow', (0, -0.05, 0.0), (0.012, 0.58, 0.006), 'glow_dim', 0, 1))

A.finish('lattice_bomber', [('Body', body, None)], 512)

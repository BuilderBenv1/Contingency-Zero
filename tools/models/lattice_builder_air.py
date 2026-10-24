"""Lattice "Kite" flying constructor. Run: python tools/models/lattice_builder_air.py <out_dir>

A diamond-planform VTOL: a team-painted diamond wing with two glowing lift fans, a slim central
pod with a bubble canopy, twin nanolathe prongs reaching forward from the nose with blue-glowing
emitter beads, a belly nanolathe turret, small tip fins and twin rear thrusters.
Modelled around z = 0 (belly near 0); the game lifts it to flight altitude.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A

kit.reset('lattice')
body = []

# ---- Diamond wing ----
body += A.wing('wing', [(0.0, 1.08), (0.84, -0.04), (0.0, -0.94)], 0.12, 0.09, 0.03, 'team', 'hull_dark', x_root=0.12, x_tip=0.84)
for sx in A.SIDES:
    # Grey bevelled edge plates along the trailing edges break up the team diamond.
    pts = []
    for x, y in ((0.12, -0.8), (0.78, -0.08), (0.84, -0.04), (0.2, -0.92)):
        z = 0.12 + (0.09 + (0.03 - 0.09) * max(0, (x - 0.12) / 0.72)) / 2
        pts += [(sx * x, y, z), (sx * x, y, z + 0.016)]
    body.append(hull_from(f'edge{sx}', pts, 'hull', bevel=0.004, segs=1))
    # Lift fan: rim ring, glowing disc, hub and vanes.
    c = (sx * 0.36, 0.2, 0.16)
    body.append(A.ring(f'fanrim{sx}', c, 0.17, 0.13, 0.1, 'hull', top='hull_dark', verts=20))
    body.append(cyl(f'fandisc{sx}', (c[0], c[1], 0.158), (c[0], c[1], 0.166), 0.13, 'glow_dim', 20, bevel=0))
    body.append(cyl(f'fanhub{sx}', (c[0], c[1], 0.15), (c[0], c[1], 0.2), 0.035, 'mech', 10, bevel=0))
    for k in range(5):
        a = k * 2 * math.pi / 5
        v = box(f'vane{sx}{k}', (c[0] + math.cos(a) * 0.08, c[1] + math.sin(a) * 0.08, 0.18), (0.1, 0.022, 0.01), 'mech', 0, 1)
        A.xform([v], (c[0] + math.cos(a) * 0.08, c[1] + math.sin(a) * 0.08, 0.18), (0.4, 0, a))
        body.append(v)
    # Tip fin with a nav light.
    body.append(prism_x(f'tipfin{sx}', [(0.08, 0.13), (-0.12, 0.13), (-0.16, 0.3), (-0.06, 0.3)], sx * 0.8 - 0.012, sx * 0.8 + 0.012, 'hull', 0.004, 1))
    body.append(sphere(f'navlt{sx}', (sx * 0.8, -0.16, 0.31), 0.02, 'glow', 8, 4))
    # Twin rear thrusters.
    body += A.thruster(f'eng{sx}', (sx * 0.24, -0.38, 0.14), (sx * 0.24, -0.66, 0.14), 0.08, verts=12)
    body.append(hull_from(f'nacelle{sx}', [(sx * 0.17, -0.1, 0.1), (sx * 0.31, -0.1, 0.1), (sx * 0.2, -0.18, 0.21), (sx * 0.28, -0.18, 0.21),
                                           (sx * 0.16, -0.42, 0.06), (sx * 0.32, -0.42, 0.06), (sx * 0.17, -0.42, 0.22), (sx * 0.31, -0.42, 0.22)],
                          'hull', bevel=0.008))

# ---- Central pod with bubble canopy ----
secs = [
    A.octring(0.86, 0.02, 0.04, 0.02, 0.1, 0.13, 0.16, 0.18),
    A.octring(0.5, 0.1, 0.17, 0.08, 0.02, 0.1, 0.2, 0.3),
    A.octring(-0.3, 0.11, 0.18, 0.09, 0.02, 0.1, 0.2, 0.31),
    A.octring(-0.72, 0.05, 0.08, 0.04, 0.09, 0.12, 0.18, 0.22),
]
band = {0: 'hull_dark', 1: 'hull_dark', 7: 'hull_dark', 2: 'hull', 6: 'hull', 3: 'hull', 5: 'hull', 4: 'hull_dark'}
body.append(A.loft('pod', secs, 'hull', lambda j: band[j], cap0='hull', cap1='mech', bevel=0.01))
body.append(hull_from('canopy', [(0.06, 0.66, 0.24), (-0.06, 0.66, 0.24), (0.1, 0.42, 0.29), (-0.1, 0.42, 0.29),
                                 (0.05, 0.5, 0.36), (-0.05, 0.5, 0.36), (0.06, 0.3, 0.36), (-0.06, 0.3, 0.36),
                                 (0.08, 0.2, 0.3), (-0.08, 0.2, 0.3)], 'glass', bevel=0.014, segs=2))
body.append(box('spine', (0, -0.2, 0.31), (0.06, 0.6, 0.02), 'glow_dim', 0.004, 1))
body.append(cyl('ant', (0, -0.55, 0.22), (0, -0.62, 0.42), 0.008, 'metal', 4, bevel=0))
body.append(sphere('antlt', (0, -0.62, 0.43), 0.016, 'glow', 6, 4))

# ---- Nanolathe: twin forward prongs, and a belly turret ----
for sx in A.SIDES:
    body.append(box(f'prongbase{sx}', (sx * 0.1, 0.72, 0.13), (0.06, 0.16, 0.06), 'hull_dark', 0.01, 1))
    body.append(cyl(f'prong{sx}', (sx * 0.1, 0.74, 0.13), (sx * 0.13, 1.08, 0.11), 0.022, 'accent', 8, bevel=0.003))
    body += A.nano_tip(f'ptip{sx}', (sx * 0.13, 1.06, 0.11), (sx * 0.14, 1.16, 0.09), 0.024)
    for k in range(2):
        y = 0.86 + k * 0.1
        body.append(cyl(f'pcoil{sx}{k}', (sx * (0.1 + (y - 0.74) * 0.09), y, 0.125), (sx * (0.1 + (y - 0.74) * 0.09), y + 0.025, 0.124), 0.03, 'glow', 8, bevel=0))
body.append(cyl('bellyring', (0, 0.15, 0.03), (0, 0.15, -0.02), 0.11, 'hull_dark', 14, bevel=0.004))
body.append(sphere('bellyball', (0, 0.15, -0.04), 0.07, 'mech', 10, 6))
body += A.nano_tip('btip', (0, 0.18, -0.06), (0, 0.38, -0.14), 0.03)

A.finish('lattice_builder_air', [('Body', body, None)], 512)

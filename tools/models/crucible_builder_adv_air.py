"""Crucible "Skycrane" flying engineer. Run: python tools/models/crucible_builder_adv_air.py <out_dir>

A jet-lift crane: an open riveted spine girder with a blunt armoured cab at the front, four
ducted lift fans on outriggers glowing orange inside their rings, a heavy gantry hanging under
the spine carrying two hydraulic nanolathe arms and a hook block, and a team-painted tail
block with twin exhausts. No rotor, unlike the Hauler; it reads as a flying gantry crane.
Modelled around z = 0; the game lifts it to flight altitude.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A

kit.reset('crucible')
body = []
# Spine girder: two rails with cross braces
for sx in A.SIDES:
    body.append(box(f'rail{sx}', (sx * 0.14, -0.1, 0.42), (0.06, 1.8, 0.1), 'hull_dark', 0.01, 1))
for k in range(7):
    y = -0.9 + k * 0.27
    body.append(box(f'brace{k}', (0, y, 0.42), (0.28, 0.04, 0.06), 'mech', 0.004, 1))
    if k % 2 == 0:
        body.append(cyl(f'diag{k}', (-0.14, y, 0.38), (0.14, y + 0.27, 0.46), 0.012, 'metal', 5, bevel=0))
# Cab at the front
cab = hull_from('cab', [(-0.24, 0.72, 0.22), (0.24, 0.72, 0.22), (-0.24, 1.08, 0.24), (0.24, 1.08, 0.24),
                        (-0.24, 0.72, 0.56), (0.24, 0.72, 0.56), (-0.2, 1.0, 0.52), (0.2, 1.0, 0.52)], 'hull', 0.02, 2)
body.append(cab)
body.append(box('cabroof', (0, 0.86, 0.565), (0.44, 0.3, 0.03), 'team', 0.008, 1))
body.append(hull_from('screen', [(-0.18, 1.06, 0.3), (0.18, 1.06, 0.3), (-0.16, 1.01, 0.48), (0.16, 1.01, 0.48),
                                 (-0.18, 1.04, 0.3), (0.18, 1.04, 0.3), (-0.16, 0.99, 0.48), (0.16, 0.99, 0.48)], 'glass', 0.004, 1))
for sx in A.SIDES:
    body.append(cyl(f'lamp{sx}', (sx * 0.2, 1.06, 0.24), (sx * 0.2, 1.1, 0.24), 0.03, 'glow', 8, bevel=0))
body += rivets('crv', (-0.2, 1.07, 0.27), (0.2, 1.07, 0.27), 5, 'metal', 0.013)
# Four ducted lift fans on outriggers
for sx in A.SIDES:
    for y in (0.45, -0.55):
        body.append(box(f'out{sx}{y}', (sx * 0.38, y, 0.42), (0.5, 0.08, 0.06), 'hull_dark', 0.008, 1))
        c = (sx * 0.68, y, 0.42)
        body.append(A.ring(f'duct{sx}{y}', c, 0.26, 0.22, 0.14, 'team', top='hull_dark'))
        body.append(cyl(f'fang{sx}{y}', (c[0], c[1], c[2] - 0.05), (c[0], c[1], c[2] - 0.04), 0.21, 'glow', 14, bevel=0))
        body.append(cyl(f'hub{sx}{y}', (c[0], c[1], c[2] - 0.04), (c[0], c[1], c[2] + 0.05), 0.06, 'mech', 10, bevel=0))
        for k in range(4):
            a = k * math.pi / 2
            body.append(box(f'strut{sx}{y}{k}', (c[0] + math.cos(a) * 0.12, c[1] + math.sin(a) * 0.12, c[2] + 0.04), (0.2 if k % 2 == 0 else 0.03, 0.03 if k % 2 == 0 else 0.2, 0.02), 'mech', 0, 1))
# Hanging gantry with two nanolathe arms and a hook block
for sx in A.SIDES:
    body.append(box(f'hang{sx}', (sx * 0.14, -0.05, 0.22), (0.05, 0.05, 0.36), 'mech', 0.004, 1))
body.append(box('gantry', (0, -0.05, 0.04), (0.5, 0.5, 0.08), 'accent', 0.012, 1))
body += rivets('gtr', (-0.2, -0.28, 0.085), (0.2, -0.28, 0.085), 5, 'metal', 0.013)
for sx in A.SIDES:
    body.append(box(f'arm{sx}', (sx * 0.2, 0.14, -0.02), (0.06, 0.34, 0.06), 'hull', 0.006, 1))
    body += A.nano_tip(f'tip{sx}', (sx * 0.2, 0.3, -0.04), (sx * 0.2, 0.42, -0.1), 0.03)
body.append(cyl('cable', (0, -0.12, 0.0), (0, -0.12, -0.2), 0.008, 'metal', 5, bevel=0))
body.append(box('hook', (0, -0.12, -0.24), (0.1, 0.06, 0.08), 'hull_dark', 0.01, 1))
# Two nanolathe booms on top of the spine reaching out over the nose
for sx in A.SIDES:
    body.append(cyl(f'bbase{sx}', (sx * 0.12, 0.25, 0.47), (sx * 0.12, 0.25, 0.55), 0.07, 'mech', 8, bevel=0))
    body.append(box(f'boom{sx}', (sx * 0.15, 0.7, 0.6), (0.07, 0.9, 0.08), 'team', 0.006, 1))
    body.append(box(f'boomb{sx}', (sx * 0.15, 0.45, 0.56), (0.08, 0.3, 0.06), 'accent', 0.006, 1))
    body += A.nano_tip(f'btip{sx}', (sx * 0.15, 1.12, 0.6), (sx * 0.17, 1.3, 0.48), 0.035)
# Tail block with exhausts
body.append(box('tail', (0, -0.98, 0.42), (0.4, 0.3, 0.24), 'team', 0.02, 1))
for sx in A.SIDES:
    body += A.thruster(f'ex{sx}', (sx * 0.1, -1.1, 0.42), (sx * 0.1, -1.22, 0.42), 0.06, verts=10)

A.finish('crucible_builder_adv_air', [('Body', body, None)], 512)

"""Crucible "Foreman" advanced builder. Run: python tools/models/crucible_builder_adv.py <out_dir>

A big tracked works vehicle: two heavy hydraulic nanolathe cranes up front, a riveted
team-painted workshop block at the back with twin glowing stacks, a girder rack and spare
plates, and a site floodlight bar on the workshop roof.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import vehicles_parts as vp

kit.reset('crucible')
body = []
body += vp.crucible_tracks(0.5, 0.88, -1.1, 1.08, r_end=0.22, wheels=6, cleats=21, skirt='team', skirt_top=0.5, skirt_rivets=9)

HW = 0.5
body.append(box('tub', (0, 0.0, 0.3), (2 * HW, 2.06, 0.28), 'hull_dark', 0.02))
upper = prism_x('upper', [(1.12, 0.34), (0.8, 0.62), (-1.0, 0.62), (-1.08, 0.54), (-1.08, 0.32), (1.12, 0.32)],
                -0.64, 0.64, 'hull', 0.025, 2)
inset_panels(upper, 0.03, 0.01, lambda f: f.normal.z > 0.3 and f.calc_area() > 0.05)
body.append(upper)
body += rivets('glrv', (-0.56, 0.86, 0.6), (0.56, 0.86, 0.6), 8, 'metal', 0.02)
body.append(prism_x('glacisplate', [(1.06, 0.4), (0.84, 0.6), (0.8, 0.615), (1.02, 0.415)], -0.44, 0.44, 'team', 0.006, 1))
for sx in (-1, 1):
    body.append(cyl(f'lamp{sx}', (sx * 0.4, 1.06, 0.46), (sx * 0.4, 1.11, 0.46), 0.04, 'glow', 8, bevel=0))
    body.append(cyl(f'lamphood{sx}', (sx * 0.4, 1.03, 0.46), (sx * 0.4, 1.07, 0.46), 0.055, 'mech', 8, bevel=0))
    body.append(box(f'hook{sx}', (sx * 0.22, 1.16, 0.33), (0.05, 0.06, 0.07), 'metal', 0.01, 1))

# ---- Two heavy cranes on a team deck plate ----
body.append(box('deckplate', (0, 0.38, 0.63), (1.12, 0.78, 0.025), 'team', 0.008, 1))
for sx in (-1, 1):
    body.append(box(f'armpad{sx}', (sx * 0.3, 0.32, 0.64), (0.4, 0.4, 0.04), 'hull_dark', 0.008, 1))
    body += rivets(f'padrv{sx}', (sx * 0.3 - 0.16, 0.14, 0.665), (sx * 0.3 + 0.16, 0.14, 0.665), 4, 'metal', 0.014)
    body += vp.nano_arm(f'arm{sx}', (sx * 0.3, 0.32, 0.66), 0.82, -sx * 14, 'crucible')

# ---- Workshop block ----
ws = box('workshop', (0, -0.5, 0.82), (0.9, 0.74, 0.4), 'team', 0.025, 2)
inset_panels(ws, 0.035, 0.012, lambda f: f.calc_area() > 0.08 and f.normal.z < 0.5)
body.append(ws)
body.append(box('wsroof', (0, -0.5, 1.035), (0.94, 0.78, 0.04), 'team', 0.01, 1))
body += rivets('wsrv1', (-0.4, -0.12, 1.06), (0.4, -0.12, 1.06), 6, 'metal', 0.018)
body += rivets('wsrv2', (-0.4, -0.88, 1.06), (0.4, -0.88, 1.06), 6, 'metal', 0.018)
# Roll-up door on the front face and a vent.
body.append(box('door', (0, -0.125, 0.78), (0.36, 0.02, 0.28), 'hull_dark', 0.006, 1))
for k in range(5):
    body.append(box(f'doorrib{k}', (0, -0.112, 0.67 + k * 0.055), (0.34, 0.01, 0.012), 'mech', 0, 1))
body.append(box('doorglow', (0, -0.11, 0.64), (0.3, 0.008, 0.02), 'glow_dim', 0, 1))
# Twin stacks at the back corners.
for sx in (-1, 1):
    body += vp.stack(f'stack{sx}', (sx * 0.3, -0.76, 1.05), 0.07, 0.34)
# Floodlight bar on the roof.
body.append(cyl('floodbar', (-0.34, -0.2, 1.12), (0.34, -0.2, 1.12), 0.015, 'mech', 6, bevel=0))
for sx in (-1, 0, 1):
    body.append(cyl(f'floodpost{sx}', (sx * 0.3, -0.2, 1.055), (sx * 0.3, -0.2, 1.12), 0.012, 'mech', 6, bevel=0))
for sx in (-1, 1):
    body.append(box(f'flood{sx}', (sx * 0.18, -0.18, 1.15), (0.12, 0.06, 0.06), 'hull_dark', 0.008, 1))
    body.append(box(f'floodg{sx}', (sx * 0.18, -0.148, 1.15), (0.09, 0.006, 0.04), 'glow', 0, 1))
body.append(sphere('beacon', (0, -0.4, 1.09), 0.035, 'glow', 8, 5))
# Girder rack and spare plates on the hull sides beside the workshop.
for sx in (-1, 1):
    body.append(vp.beam(f'girder{sx}', (sx * 0.56, 0.0, 0.67), (sx * 0.56, -0.98, 0.67), 0.08, 0.06, 'accent', 0.006))
    body.append(box(f'plate{sx}', (sx * 0.5, -0.5, 0.82), (0.04, 0.5, 0.26), 'hull_dark', 0.008, 1))
body.append(box('drum', (0.3, -1.12, 0.46), (0.3, 0.07, 0.16), 'accent', 0.01, 1))

vp.finish('crucible_builder_adv', body)

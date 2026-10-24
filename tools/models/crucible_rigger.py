"""Crucible Rigger (walking constructor). Run: python tools/models/crucible_rigger.py <out_dir>

A boxy riveted work-cab on stubby forward-kneed stomper legs: a team roof under a roll cage,
twin glowing visor slits, a toolbox and gas bottles, a hot exhaust stack, and a heavy hydraulic
nanolathe crane with jaws round a glowing orange tip.
Matches the procedural model: hips at 0.5, cab z 0.49..0.87, crane base (-0.14, -0.12, 0.87).
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, inset_panels
from bots_parts import crucible_leg, beam, obox, stack, crane, finish, add, lerp, rivet_row

kit.reset('crucible')
body = []
for sx in (-1, 1):
    body += crucible_leg(f'leg{sx}', (sx * 0.3, -0.05, 0.5), (sx * 0.37, 0.04 if sx < 0 else -0.04, 0.0), 0.15)

# Cab: a chamfered block, team roof and a sloped riveted front plate with visor slits
cab = hull_from('cab', [(-0.38, 0.34, 0.5), (0.38, 0.34, 0.5), (-0.38, -0.48, 0.5), (0.38, -0.48, 0.5),
                        (-0.38, 0.4, 0.66), (0.38, 0.4, 0.66), (-0.36, 0.3, 0.86), (0.36, 0.3, 0.86),
                        (-0.38, -0.48, 0.86), (0.38, -0.48, 0.86)], 'hull', 0.025, 2)
inset_panels(cab, 0.03, 0.01, lambda f: abs(f.normal.x) > 0.7)
body.append(cab)
body.append(box('roof', (0, -0.08, 0.875), (0.7, 0.72, 0.035), 'team', 0.012, 1))
body.append(hull_from('front', [(-0.33, 0.41, 0.52), (0.33, 0.41, 0.52), (-0.33, 0.44, 0.66), (0.33, 0.44, 0.66),
                                (-0.31, 0.34, 0.84), (0.31, 0.34, 0.84), (-0.33, 0.37, 0.52), (0.33, 0.37, 0.52),
                                (-0.31, 0.3, 0.84), (0.31, 0.3, 0.84)], 'team_dark', 0.01, 1))
for z in (0.6, 0.68):
    body.append(box(f'slit{z}', (0, 0.445, z), (0.4, 0.02, 0.026), 'glow', 0, 1))
body += rivet_row('fr', (-0.28, 0.43, 0.76), (0.28, 0.43, 0.76), 5, 0.018)
body += rivet_row('fr2', (-0.28, 0.4, 0.54), (0.28, 0.4, 0.54), 5, 0.016)
body.append(box('bumper', (0, 0.44, 0.5), (0.6, 0.08, 0.06), 'mech', 0.01, 1))
for sx in (-1, 1):
    body.append(cyl(f'lamp{sx}', (sx * 0.3, 0.43, 0.82), (sx * 0.3, 0.47, 0.82), 0.035, 'glow', 8, bevel=0))
    body.append(cyl(f'lamph{sx}', (sx * 0.3, 0.41, 0.82), (sx * 0.3, 0.45, 0.82), 0.045, 'mech', 8, bevel=0))
# Roll cage over the roof
for sx in (-1, 1):
    body.append(cyl(f'cagep{sx}0', (sx * 0.33, 0.24, 0.88), (sx * 0.33, 0.24, 0.98), 0.02, 'mech', 6, bevel=0))
    body.append(cyl(f'cagep{sx}1', (sx * 0.33, -0.42, 0.88), (sx * 0.33, -0.42, 0.98), 0.02, 'mech', 6, bevel=0))
    body.append(cyl(f'cagel{sx}', (sx * 0.33, 0.24, 0.98), (sx * 0.33, -0.42, 0.98), 0.02, 'mech', 6, bevel=0))
body.append(cyl('cagef', (-0.33, 0.24, 0.98), (0.33, 0.24, 0.98), 0.02, 'mech', 6, bevel=0))
# Side: gas bottles and a toolbox, a hot stack at the front right
for k in range(2):
    body.append(cyl(f'bottle{k}', (-0.42, -0.1 - k * 0.14, 0.54), (-0.42, -0.1 - k * 0.14, 0.82), 0.055, 'accent', 10))
    body.append(cyl(f'bcap{k}', (-0.42, -0.1 - k * 0.14, 0.82), (-0.42, -0.1 - k * 0.14, 0.86), 0.03, 'metal', 8, bevel=0))
body.append(box('strap', (-0.42, -0.17, 0.7), (0.13, 0.32, 0.03), 'mech', 0.004, 1))
body.append(box('toolbox', (0.43, -0.16, 0.66), (0.1, 0.36, 0.16), 'accent', 0.012, 1))
body.append(box('tlatch', (0.485, -0.16, 0.7), (0.012, 0.05, 0.03), 'metal', 0, 1))
body += stack('stack', (0.24, 0.08, 0.89), 0.06, 0.34)
body.append(box('rear', (0, -0.5, 0.68), (0.5, 0.06, 0.22), 'hull_dark', 0.01, 1))
for k in range(4):
    body.append(box(f'rearv{k}', (0, -0.535, 0.6 + k * 0.05), (0.4, 0.012, 0.02), 'glow_dim', 0, 1))
body += crane('crane', (-0.14, -0.12, 0.89), 0.72, 'crucible')

finish('crucible_rigger', body)

"""Crucible "Sapper" fortification engineer. Run: python tools/models/crucible_builder_adv_bot.py <out_dir>

A four-legged armoured work-crawler: a low riveted cab on four stomper legs, a team roof under
a heavy blast shield at the front with glowing vision slits, two hydraulic nanolathe cranes,
sandbag-like armour blocks and a hot stack. Reads as a builder (the cranes) but heavier and
four-legged next to the two-legged Rigger.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels
from bots_parts import crucible_leg, beam, obox, stack, crane, finish, rivet_row

kit.reset('crucible')
body = []
for sx in (-1, 1):
    for sy in (-1, 1):
        body += crucible_leg(f'leg{sx}{sy}', (sx * 0.34, sy * 0.3, 0.46), (sx * 0.5, sy * 0.42, 0.0), 0.13)
cab = hull_from('cab', [(-0.4, 0.44, 0.42), (0.4, 0.44, 0.42), (-0.42, -0.5, 0.42), (0.42, -0.5, 0.42),
                        (-0.4, 0.5, 0.58), (0.4, 0.5, 0.58), (-0.38, 0.36, 0.78), (0.38, 0.36, 0.78),
                        (-0.42, -0.5, 0.78), (0.42, -0.5, 0.78)], 'hull', 0.025, 2)
inset_panels(cab, 0.03, 0.01, lambda f: abs(f.normal.x) > 0.7)
body.append(cab)
body.append(box('roof', (0, -0.08, 0.795), (0.76, 0.84, 0.035), 'team', 0.012, 1))
# Blast shield across the front
body.append(hull_from('shield', [(-0.5, 0.52, 0.4), (0.5, 0.52, 0.4), (-0.5, 0.6, 0.4), (0.5, 0.6, 0.4),
                                 (-0.46, 0.5, 0.86), (0.46, 0.5, 0.86), (-0.46, 0.56, 0.86), (0.46, 0.56, 0.86)], 'team_dark', 0.012, 1))
for z in (0.62, 0.7):
    body.append(box(f'slit{z}', (0, 0.6, z), (0.5, 0.02, 0.024), 'glow', 0, 1))
body += rivet_row('shr', (-0.42, 0.6, 0.8), (0.42, 0.6, 0.8), 7, 0.018)
body += rivet_row('shr2', (-0.42, 0.61, 0.46), (0.42, 0.61, 0.46), 7, 0.018)
# Armour blocks along the flanks
for sx in (-1, 1):
    for k in range(3):
        body.append(box(f'blk{sx}{k}', (sx * 0.45, 0.2 - k * 0.28, 0.6), (0.08, 0.24, 0.2), 'accent' if k == 1 else 'hull_dark', 0.02, 1))
body += stack('stack', (0.26, -0.36, 0.81), 0.06, 0.3)
body.append(box('rear', (0, -0.53, 0.6), (0.5, 0.06, 0.24), 'hull_dark', 0.01, 1))
for k in range(4):
    body.append(box(f'rearv{k}', (0, -0.565, 0.52 + k * 0.05), (0.4, 0.012, 0.02), 'glow_dim', 0, 1))
body += crane('craneL', (-0.22, -0.1, 0.81), 0.7, 'crucible')
body += crane('craneR', (0.12, 0.1, 0.81), 0.55, 'crucible', reach=0.8)

finish('crucible_builder_adv_bot', body)

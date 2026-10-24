"""Crucible "Cinder" AA bot. Run: python tools/models/crucible_aa_bot.py <out_dir>

Hunched stomper infantry carrying one big six-cell team missile box on its back, pitched up
over its head, with heavy arm blocks and an ammo feed. No ground gun. The upper body is the
Turret. Matches the procedural model: pivot (0, -0.05, 0.58), launcher at z +0.5 over the pivot.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere
from bots_parts import (crucible_infantry_legs, crucible_infantry_torso, beam, obox, missile_pod, finish, rivet_row)

kit.reset('crucible')
body = crucible_infantry_legs(0.52)
PIV = (0, -0.05, 0.58)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = crucible_infantry_torso(P, top_paint='team_dark')
# Launcher cradle: two side arms holding a trunnion, the box pitched 52 degrees
for sx in (-1, 1):
    tur.append(box(f'cradle{sx}', P(sx * 0.27, -0.12, 0.46), (0.05, 0.2, 0.16), 'mech', 0.01, 1))
    tur.append(cyl(f'trun{sx}', P(sx * 0.24, -0.1, 0.5), P(sx * 0.31, -0.1, 0.5), 0.05, 'metal', 8))
tur += missile_pod('launcher', P(0, -0.08, 0.52), (0.48, 0.42, 0.3), 52, 3, 2, 'team', tip='accent')
tur += rivet_row('lr', P(-0.2, -0.24, 0.42), P(0.2, -0.24, 0.42), 4, 0.016)
tur.append(cyl('feed', P(0.2, -0.24, 0.3), P(0.12, -0.3, 0.44), 0.025, 'accent', 6, bevel=0))
# Heavy arm blocks
for sx in (-1, 1):
    tur.append(box(f'shoulder{sx}', P(sx * 0.37, 0.0, 0.26), (0.14, 0.26, 0.14), 'hull_dark', 0.015, 1))
    tur.append(box(f'arm{sx}', P(sx * 0.37, 0.02, 0.1), (0.14, 0.24, 0.22), 'hull', 0.015, 1))
    tur.append(box(f'armplate{sx}', P(sx * 0.445, 0.02, 0.12), (0.02, 0.18, 0.14), 'team', 0.004, 1))
    tur.append(box(f'fist{sx}', P(sx * 0.37, 0.16, 0.04), (0.11, 0.08, 0.1), 'mech', 0.01, 1))
tur.append(box('radar', P(-0.2, 0.08, 0.43), (0.1, 0.06, 0.05), 'mech', 0.006, 1))
tur.append(box('radarg', P(-0.2, 0.113, 0.43), (0.07, 0.004, 0.02), 'glow', 0, 1))

finish('crucible_aa_bot', body, tur, PIV)

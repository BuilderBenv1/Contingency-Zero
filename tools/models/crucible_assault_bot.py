"""Crucible "Brawler" assault bot. Run: python tools/models/crucible_assault_bot.py <out_dir>

Hunched laser infantry on short stomper legs: a riveted block torso with team top and chest
plate, chunky team shoulder blocks, and a heavy laser cannon on each forearm with a glowing
heat jacket. A hot stack on its back. The upper body is the Turret.
Matches the procedural model: pivot (0, -0.05, 0.58), arm guns at x +-0.41 reaching y 0.6.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere
from bots_parts import (crucible_infantry_legs, crucible_infantry_torso, beam, obox, barrel, stack, finish, rivet_row)

kit.reset('crucible')
body = crucible_infantry_legs(0.52)
PIV = (0, -0.05, 0.58)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = crucible_infantry_torso(P)
for sx in (-1, 1):
    tur.append(box(f'shoulder{sx}', P(sx * 0.41, 0, 0.29), (0.2, 0.32, 0.2), 'team', 0.02, 1))
    tur += rivet_row(f'shr{sx}', P(sx * 0.41, -0.11, 0.395), P(sx * 0.41, 0.11, 0.395), 3, 0.016)
    tur.append(box(f'shrim{sx}', P(sx * 0.41, 0, 0.18), (0.22, 0.34, 0.03), 'hull_dark', 0.006, 1))
    tur.append(beam(f'uarm{sx}', P(sx * 0.41, 0, 0.2), P(sx * 0.41, 0.0, 0.08), 0.1, 0.1, 'mech'))
    # Forearm gun housing, barrel with heat jacket and a glowing muzzle
    tur.append(box(f'gun{sx}', P(sx * 0.41, 0.08, 0.08), (0.16, 0.38, 0.16), 'hull_dark', 0.016, 1))
    tur.append(box(f'guntop{sx}', P(sx * 0.41, 0.06, 0.165), (0.12, 0.28, 0.02), 'team_dark', 0.004, 1))
    tur.append(cyl(f'jacket{sx}', P(sx * 0.41, 0.27, 0.08), P(sx * 0.41, 0.42, 0.08), 0.062, 'mech', 10))
    for k in range(3):
        tur.append(cyl(f'jg{sx}{k}', P(sx * 0.41, 0.29 + k * 0.045, 0.08), P(sx * 0.41, 0.305 + k * 0.045, 0.08), 0.066, 'glow_dim', 10, bevel=0))
    tur += barrel(f'bar{sx}', P(sx * 0.41, 0.42, 0.08), P(sx * 0.41, 0.6, 0.08), 0.036, brake=False)
    tur.append(cyl(f'muz{sx}', P(sx * 0.41, 0.58, 0.08), P(sx * 0.41, 0.62, 0.08), 0.048, 'mech', 10, bevel=0))
    tur.append(cyl(f'muzg{sx}', P(sx * 0.41, 0.62, 0.08), P(sx * 0.41, 0.625, 0.08), 0.03, 'glow', 10, bevel=0))
tur += stack('stack', P(0.13, -0.18, 0.3), 0.05, 0.28)
tur.append(box('pack', P(-0.08, -0.24, 0.24), (0.22, 0.1, 0.2), 'accent', 0.012, 1))

finish('crucible_assault_bot', body, tur, PIV)

"""Lattice "Paladin" heavy assault walker. Run: python tools/models/lattice_heavy_bot.py <out_dir>

Tier 2 armoured walker: the Lattice infantry frame grown broad and heavy, on thick legs with
armoured knee guards. Huge stepped team pauldrons, a chest shield, a long cannon carried on the
right forearm and a beam emitter on the left, and a twin-cell power pack on its back.
The upper body is the Turret, pivot (0, -0.04, 0.68).
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere
from bots_parts import lattice_infantry_legs, lattice_infantry_torso, beam, emitter, barrel, finish
import t2_base as T

kit.reset('lattice')
body = lattice_infantry_legs(0.15, 0.62, 0.07)
T.transform(body, (1.35, 1.1, 1.0))
for sx in (-1, 1):
    body.append(hull_from(f'knee{sx}', [(sx * 0.3, 0.06, 0.26), (sx * 0.4, 0.06, 0.26), (sx * 0.3, 0.2, 0.36), (sx * 0.4, 0.2, 0.36),
                                        (sx * 0.31, 0.12, 0.46), (sx * 0.39, 0.12, 0.46)], 'team', 0.01, 1))
body.append(box('skirt', (0, -0.02, 0.56), (0.5, 0.32, 0.08), 'hull_dark', 0.02, 1))
PIV = (0, -0.04, 0.68)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = lattice_infantry_torso(P, 'team')
T.transform(tur, (1.45, 1.2, 1.1), pivot=PIV)
# Pauldrons: stepped blocks well proud of the shoulders.
for sx in (-1, 1):
    tur.append(hull_from(f'paul{sx}', [P(sx * 0.3, -0.2, 0.36), P(sx * 0.3, 0.2, 0.36), P(sx * 0.52, -0.18, 0.32), P(sx * 0.52, 0.18, 0.32),
                                       P(sx * 0.56, -0.16, 0.16), P(sx * 0.56, 0.16, 0.16), P(sx * 0.34, -0.2, 0.42), P(sx * 0.34, 0.2, 0.42)],
                         'team', 0.015, 1))
    tur.append(hull_from(f'paul2{sx}', [P(sx * 0.32, -0.12, 0.42), P(sx * 0.32, 0.12, 0.42), P(sx * 0.48, -0.1, 0.37),
                                        P(sx * 0.48, 0.1, 0.37), P(sx * 0.34, -0.1, 0.48), P(sx * 0.34, 0.1, 0.48)], 'hull', 0.01, 1))
    tur.append(box(f'paulg{sx}', P(sx * 0.565, 0, 0.24), (0.01, 0.2, 0.03), 'glow', 0, 1))
    tur.append(beam(f'uarm{sx}', P(sx * 0.42, 0, 0.2), P(sx * 0.44, 0.04, -0.02), 0.11, 0.12, 'mech'))
    tur.append(sphere(f'elb{sx}', P(sx * 0.44, 0.04, -0.02), 0.07, 'metal', 8, 5))
# Right forearm: the cannon.
tur.append(beam('farmR', P(0.44, 0.04, -0.02), P(0.44, 0.3, 0.0), 0.14, 0.14, 'hull'))
tur.append(box('breech', P(0.44, 0.2, 0.05), (0.13, 0.26, 0.12), 'hull_dark', 0.012, 1))
tur += barrel('can', P(0.44, 0.32, 0.05), P(0.44, 0.78, 0.05), 0.045)
tur.append(box('cantop', P(0.44, 0.2, 0.115), (0.08, 0.22, 0.01), 'team', 0.003, 1))
# Left forearm: the beam emitter.
tur.append(beam('farmL', P(-0.44, 0.04, -0.02), P(-0.44, 0.28, 0.0), 0.13, 0.13, 'hull'))
tur += emitter('las', P(-0.44, 0.26, 0.0), P(-0.44, 0.6, 0.0), 0.04)
# Chest shield and head guard.
tur.append(hull_from('shield', [P(-0.22, 0.2, 0.06), P(0.22, 0.2, 0.06), P(-0.26, 0.2, 0.3), P(0.26, 0.2, 0.3),
                                P(-0.2, 0.25, 0.1), P(0.2, 0.25, 0.1), P(-0.22, 0.24, 0.28), P(0.22, 0.24, 0.28)], 'hull', 0.012, 1))
tur.append(box('shieldg', P(0, 0.255, 0.19), (0.2, 0.01, 0.025), 'glow', 0, 1))
tur.append(hull_from('helm', [P(-0.11, -0.1, 0.4), P(0.11, -0.1, 0.4), P(-0.1, 0.16, 0.42), P(0.1, 0.16, 0.42),
                              P(-0.08, -0.08, 0.6), P(0.08, -0.08, 0.6), P(-0.06, 0.1, 0.58), P(0.06, 0.1, 0.58)], 'hull', 0.012, 1))
tur.append(box('helmv', P(0, 0.165, 0.48), (0.14, 0.01, 0.03), 'glow', 0, 1))
# Back power pack.
tur.append(box('pack', P(0, -0.27, 0.2), (0.36, 0.14, 0.3), 'hull_dark', 0.015, 1))
for sx in (-1, 1):
    tur.append(cyl(f'cell{sx}', P(sx * 0.1, -0.3, 0.36), P(sx * 0.1, -0.3, 0.56), 0.05, 'metal', 10, bevel=0.004))
    tur.append(cyl(f'cellg{sx}', P(sx * 0.1, -0.3, 0.56), P(sx * 0.1, -0.3, 0.58), 0.04, 'glow', 10, bevel=0))

finish('lattice_heavy_bot', body, tur, PIV)

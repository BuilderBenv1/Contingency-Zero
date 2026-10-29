"""Lattice "Marksman" sniper bot. Run: python tools/models/lattice_sniper_bot.py <out_dir>

Tier 2 long-range marksman: tall and slender on long reverse-jointed legs, a narrow hooded
torso and a very long rail rifle held level, with a large scope and a coil-wrapped barrel
reaching far ahead of the body. A pair of tall sensor vanes on its back.
The upper body is the Turret, pivot (0, -0.04, 0.82).
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere
from bots_parts import lattice_infantry_legs, lattice_infantry_torso, beam, emitter, finish
import t2_base as T

kit.reset('lattice')
body = lattice_infantry_legs(0.085, 0.82, 0.04)
T.transform(body, (0.9, 1.0, 1.0))
PIV = (0, -0.04, 0.82)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = lattice_infantry_torso(P, 'team')
T.transform(tur, (0.85, 0.95, 1.0), pivot=PIV)
# Hood over the head.
tur.append(hull_from('hood', [P(-0.09, -0.12, 0.34), P(0.09, -0.12, 0.34), P(-0.08, 0.14, 0.44), P(0.08, 0.14, 0.44),
                              P(-0.07, -0.1, 0.52), P(0.07, -0.1, 0.52), P(-0.05, 0.08, 0.53), P(0.05, 0.08, 0.53)], 'team_dark', 0.01, 1))
# Arms bracing the rifle.
tur.append(beam('uarmR', P(0.22, 0, 0.22), P(0.24, 0.04, 0.06), 0.06, 0.07, 'mech'))
tur.append(beam('uarmL', P(-0.22, 0, 0.22), P(-0.12, 0.12, 0.06), 0.06, 0.07, 'mech'))
tur.append(beam('farmL', P(-0.12, 0.12, 0.06), P(0.18, 0.42, 0.08), 0.055, 0.06, 'hull'))
# The rail rifle.
RX, RZ = 0.2, 0.1
tur.append(hull_from('stock', [P(RX - 0.04, -0.3, RZ - 0.06), P(RX + 0.04, -0.3, RZ - 0.06), P(RX - 0.04, 0.2, RZ - 0.03), P(RX + 0.04, 0.2, RZ - 0.03),
                               P(RX - 0.035, -0.28, RZ + 0.04), P(RX + 0.035, -0.28, RZ + 0.04), P(RX - 0.035, 0.2, RZ + 0.05),
                               P(RX + 0.035, 0.2, RZ + 0.05)], 'hull_dark', 0.008, 1))
tur.append(box('rail', P(RX, 0.6, RZ), (0.05, 0.8, 0.05), 'hull', 0.008, 1))
tur.append(box('railtop', P(RX, 0.6, RZ + 0.03), (0.03, 0.78, 0.01), 'team', 0.003, 1))
tur += emitter('bar', P(RX, 0.95, RZ), P(RX, 1.35, RZ), 0.022)
for k in range(5):
    y = 0.3 + k * 0.12
    tur.append(cyl(f'coil{k}', P(RX, y, RZ), P(RX, y + 0.03, RZ), 0.042, 'glow_dim', 10, bevel=0))
tur.append(cyl('scope', P(RX, 0.05, RZ + 0.1), P(RX, 0.36, RZ + 0.1), 0.035, 'mech', 10, bevel=0.004))
tur.append(cyl('scopeg', P(RX, 0.36, RZ + 0.1), P(RX, 0.37, RZ + 0.1), 0.028, 'glow', 10, bevel=0))
# Back sensor vanes.
for sx in (-1, 1):
    tur.append(hull_from(f'vane{sx}', [P(sx * 0.07, -0.14, 0.3), P(sx * 0.09, -0.14, 0.3), P(sx * 0.07, -0.24, 0.3),
                                       P(sx * 0.14, -0.3, 0.72), P(sx * 0.16, -0.3, 0.72), P(sx * 0.14, -0.34, 0.68)], 'hull', 0.004, 1))
    tur.append(sphere(f'vaneg{sx}', P(sx * 0.15, -0.31, 0.73), 0.025, 'glow', 8, 5))

finish('lattice_sniper_bot', body, tur, PIV)

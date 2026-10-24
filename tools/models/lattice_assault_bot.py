"""Lattice "Needle" assault bot. Run: python tools/models/lattice_assault_bot.py <out_dir>

Slim laser infantry on reverse-jointed legs, holding a long needle-like laser rifle at the
ready in both arms: team chest and angular shoulder plates, a visored head, a dorsal fin.
The upper body is the Turret. Matches the procedural model: pivot (0, -0.04, 0.72), rifle at
x 0.26 reaching y 0.78.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere
from bots_parts import (lattice_infantry_legs, lattice_infantry_torso, beam, obox, emitter, finish)

kit.reset('lattice')
body = lattice_infantry_legs(0.1, 0.66)
PIV = (0, -0.04, 0.72)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = lattice_infantry_torso(P)
for sx in (-1, 1):
    tur.append(hull_from(f'pad{sx}', [P(sx * 0.2, -0.13, 0.26), P(sx * 0.2, 0.13, 0.26), P(sx * 0.22, -0.1, 0.33), P(sx * 0.22, 0.09, 0.33),
                                      P(sx * 0.37, -0.12, 0.26), P(sx * 0.37, 0.12, 0.26), P(sx * 0.38, -0.1, 0.16), P(sx * 0.38, 0.1, 0.16),
                                      P(sx * 0.28, -0.1, 0.15), P(sx * 0.28, 0.1, 0.15)], 'team', 0.012, 1))
    tur.append(beam(f'padg{sx}', P(sx * 0.375, -0.08, 0.2), P(sx * 0.375, 0.08, 0.2), 0.008, 0.02, 'glow', bevel=0))
# Arms: right holds the rifle grip, left reaches across to the fore-grip
tur.append(beam('uarmR', P(0.3, 0, 0.2), P(0.31, 0.02, 0.04), 0.07, 0.08, 'mech'))
tur.append(beam('farmR', P(0.31, 0.02, 0.04), P(0.27, 0.1, 0.03), 0.07, 0.08, 'hull'))
tur.append(beam('uarmL', P(-0.3, 0, 0.2), P(-0.24, 0.06, 0.04), 0.07, 0.08, 'mech'))
tur.append(beam('farmL', P(-0.24, 0.06, 0.04), P(0.2, 0.3, 0.05), 0.065, 0.07, 'hull'))
for sx, y in ((1, 0.02), (-1, 0.06)):
    tur.append(sphere(f'elb{sx}', P(sx * (0.31 if sx > 0 else 0.24), y, 0.04), 0.045, 'metal', 8, 5))
# The rifle: a slim team-topped receiver, a long barrel with glowing coils and a lens tip
tur.append(hull_from('rcv', [P(0.22, -0.1, 0.0), P(0.3, -0.1, 0.0), P(0.22, 0.38, 0.02), P(0.3, 0.38, 0.02),
                             P(0.23, -0.08, 0.09), P(0.29, -0.08, 0.09), P(0.235, 0.3, 0.085), P(0.285, 0.3, 0.085)], 'hull_dark', 0.01, 1))
tur.append(box('rcvtop', P(0.26, 0.12, 0.093), (0.05, 0.34, 0.01), 'team', 0.003, 1))
tur.append(box('scope', P(0.26, 0.1, 0.12), (0.03, 0.12, 0.035), 'mech', 0.005, 1))
tur.append(box('scopeg', P(0.26, 0.162, 0.12), (0.02, 0.004, 0.02), 'glow', 0, 1))
tur += emitter('las', P(0.26, 0.38, 0.05), P(0.26, 0.8, 0.05), 0.022)
for k in range(3):
    y = 0.46 + k * 0.07
    tur.append(cyl(f'coil{k}', P(0.26, y, 0.05), P(0.26, y + 0.025, 0.05), 0.034, 'mech', 10, bevel=0))
tur.append(box('stock', P(0.26, -0.16, 0.03), (0.05, 0.12, 0.06), 'mech', 0.008, 1))
# Back: dorsal fin and a small power cell
tur.append(hull_from('fin', [P(-0.012, -0.08, 0.3), P(0.012, -0.08, 0.3), P(-0.012, -0.2, 0.2), P(0.012, -0.2, 0.2),
                             P(-0.008, -0.24, 0.52), P(0.008, -0.24, 0.52), P(-0.008, -0.16, 0.5), P(0.008, -0.16, 0.5)], 'team', 0.004, 1))
tur.append(box('cell', P(0, -0.18, 0.16), (0.18, 0.08, 0.16), 'hull_dark', 0.012, 1))
tur.append(box('cellg', P(0, -0.222, 0.16), (0.1, 0.006, 0.03), 'glow', 0, 1))

finish('lattice_assault_bot', body, tur, PIV)

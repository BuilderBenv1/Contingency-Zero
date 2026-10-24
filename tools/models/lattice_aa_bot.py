"""Lattice "Halo" AA bot. Run: python tools/models/lattice_aa_bot.py <out_dir>

Slim reverse-jointed infantry with a twin-cell team missile pod on each shoulder, pitched
steeply skyward, and a glowing targeting halo floating over its head. No ground gun.
The upper body is the Turret. Matches the procedural model: pivot (0, -0.04, 0.72), pods at
x +-0.36, halo ring at z +0.58 over the pivot.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere
from bots_parts import (lattice_infantry_legs, lattice_infantry_torso, beam, obox, missile_pod, finish, xform)
from mathutils import Matrix, Vector

kit.reset('lattice')
body = lattice_infantry_legs(0.1, 0.66)
PIV = (0, -0.04, 0.72)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = lattice_infantry_torso(P)
for sx in (-1, 1):
    # Shoulder yoke and the pod on it, pitched 58 degrees
    tur.append(beam(f'yoke{sx}', P(sx * 0.2, 0.0, 0.24), P(sx * 0.3, 0.0, 0.26), 0.08, 0.1, 'mech'))
    tur.append(cyl(f'trun{sx}', P(sx * 0.26, 0.0, 0.3), P(sx * 0.29, 0.0, 0.3), 0.06, 'metal', 10))
    tur += missile_pod(f'pod{sx}', P(sx * 0.37, 0.02, 0.32), (0.16, 0.38, 0.2), 58, 1, 2, 'team', tip='hull')
    tur.append(xform([box(f'podfin{sx}', (0, 0, 0), (0.012, 0.3, 0.04), 'glow_dim', 0, 1)],
                     Matrix.Translation(Vector(P(sx * 0.455, 0.02, 0.32))) @ Matrix.Rotation(math.radians(58), 4, 'X'))[0])
    # Slim arms hanging at the sides
    tur.append(beam(f'uarm{sx}', P(sx * 0.24, 0, 0.18), P(sx * 0.26, 0.04, 0.0), 0.06, 0.07, 'mech'))
    tur.append(beam(f'farm{sx}', P(sx * 0.26, 0.04, 0.0), P(sx * 0.25, 0.16, -0.04), 0.06, 0.065, 'hull'))
# Targeting halo: a glowing ring on three thin struts from the back
ring_c = P(0, -0.02, 0.58)
for k in range(16):
    a0, a1 = k * 2 * math.pi / 16, (k + 1) * 2 * math.pi / 16
    p0 = (ring_c[0] + math.cos(a0) * 0.18, ring_c[1] + math.sin(a0) * 0.18, ring_c[2])
    p1 = (ring_c[0] + math.cos(a1) * 0.18, ring_c[1] + math.sin(a1) * 0.18, ring_c[2])
    tur.append(beam(f'halo{k}', p0, p1, 0.03, 0.022, 'glow', side=(0, 0, 1), bevel=0))
tur.append(cyl('mast', P(0, -0.14, 0.3), P(0, -0.17, 0.56), 0.016, 'mech', 6, bevel=0))
tur.append(sphere('emit', P(0, -0.17, 0.57), 0.03, 'metal', 8, 5))
tur.append(box('pack', P(0, -0.17, 0.2), (0.2, 0.08, 0.18), 'hull_dark', 0.012, 1))

finish('lattice_aa_bot', body, tur, PIV)

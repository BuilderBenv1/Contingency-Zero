"""Lattice "Glint" scout bot. Run: python tools/models/lattice_scout_bot.py <out_dir>

Tiny, light and long-legged: a dart-shaped team hull on tall slender reverse-jointed legs,
a big glowing optic at the nose (it sees far), a swept sensor fin and whip antenna, and a
slim laser under the chin.
Matches the procedural model: hips at 0.7, hull z 0.62..0.9, laser at z 0.64.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels
from bots_parts import lattice_leg, beam, obox, emitter, finish, add, lerp

kit.reset('lattice')
body = []
for sx in (-1, 1):
    body += lattice_leg(f'leg{sx}', (sx * 0.15, -0.1, 0.7), (sx * 0.24, 0.03 if sx < 0 else -0.05, 0.0), 0.085)

# Dart hull: pointed nose, widest just behind the middle, tapering to a tail
secs = [(0.6, 0.06, 0.03, 0.74, 0.77, 0.79), (0.3, 0.2, 0.12, 0.64, 0.8, 0.88),
        (-0.16, 0.22, 0.13, 0.62, 0.8, 0.9), (-0.46, 0.1, 0.05, 0.7, 0.78, 0.82)]
pts = []
for y, hw, ht, z0, z1, z2 in secs:
    pts += [(-hw * 0.8, y, z0), (hw * 0.8, y, z0), (hw, y, z1), (ht, y, z2), (-ht, y, z2), (-hw, y, z1)]
hull = hull_from('hull', pts, 'hull', 0.02, 2)
body.append(hull_from('top', [(-0.12, 0.3, 0.885), (0.12, 0.3, 0.885), (-0.13, -0.16, 0.905), (0.13, -0.16, 0.905),
                              (-0.05, -0.44, 0.83), (0.05, -0.44, 0.83), (-0.03, 0.56, 0.8), (0.03, 0.56, 0.8),
                              (-0.1, 0.28, 0.91), (0.1, 0.28, 0.91), (-0.11, -0.15, 0.93), (0.11, -0.15, 0.93)], 'team', 0.01, 1))
inset_panels(hull, 0.02, 0.006, lambda f: f.normal.z > 0.5 and f.calc_area() > 0.02)
body.append(hull)
# Under-belly in hull steel, side strakes, a dark spine
body.append(hull_from('belly', [(-0.15, 0.3, 0.63), (0.15, 0.3, 0.63), (-0.17, -0.16, 0.61), (0.17, -0.16, 0.61),
                                (-0.18, 0.28, 0.74), (0.18, 0.28, 0.74), (-0.2, -0.16, 0.74), (0.2, -0.16, 0.74),
                                (-0.04, 0.5, 0.72), (0.04, 0.5, 0.72), (-0.06, -0.42, 0.72), (0.06, -0.42, 0.72)], 'hull', 0.015, 1))
for sx in (-1, 1):
    body.append(beam(f'strake{sx}', (sx * 0.2, 0.28, 0.79), (sx * 0.21, -0.2, 0.8), 0.02, 0.03, 'glow_dim', bevel=0))
body.append(beam('spine', (0, 0.34, 0.885), (0, -0.4, 0.83), 0.05, 0.03, 'hull_dark', bevel=0.006))
# The optic: a big glowing lens in a hood at the nose
body.append(cyl('opthood', (0, 0.26, 0.8), (0, 0.44, 0.82), 0.075, 'hull_dark', 12))
body.append(cyl('optic', (0, 0.44, 0.82), (0, 0.46, 0.82), 0.06, 'glow', 12, bevel=0))
body.append(cyl('optring', (0, 0.42, 0.82), (0, 0.445, 0.82), 0.082, 'metal', 12, bevel=0))
# Swept sensor fin and whip antenna
body.append(hull_from('fin', [(-0.012, -0.05, 0.88), (0.012, -0.05, 0.88), (-0.012, -0.34, 0.83), (0.012, -0.34, 0.83),
                              (-0.008, -0.38, 1.0), (0.008, -0.38, 1.0), (-0.008, -0.28, 1.0), (0.008, -0.28, 1.0)], 'team_dark', 0.004, 1))
body.append(box('fintip', (0, -0.33, 1.0), (0.02, 0.1, 0.012), 'glow', 0, 1))
body.append(cyl('ant', (0.07, -0.2, 0.86), (0.12, -0.36, 1.25), 0.008, 'metal', 5, bevel=0))
body.append(sphere('anttip', (0.12, -0.36, 1.26), 0.018, 'glow', 8, 5))
# Chin laser
body += emitter('laser', (0, 0.18, 0.64), (0, 0.62, 0.64), 0.026)
body.append(box('chin', (0, 0.22, 0.65), (0.08, 0.14, 0.05), 'mech', 0.008, 1))

finish('lattice_scout_bot', body)

"""Lattice Surveyor (walking constructor). Run: python tools/models/lattice_surveyor.py <out_dir>

A sleek sealed pod on reverse-jointed legs: smooth sloped hull with a team top, a sensor
eye and survey lens at the nose, side equipment pods with glowing nano emitters, and a slim
articulated nanolathe crane on its back reaching forward to a pronged glowing emitter.
Matches the procedural model: hips at 0.62, pod z 0.56..0.94, crane base (0, -0.16, 0.92).
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, inset_panels
from bots_parts import lattice_leg, beam, obox, emitter, crane, finish, add, lerp, rivet_row

kit.reset('lattice')
body = []
for sx in (-1, 1):
    body += lattice_leg(f'leg{sx}', (sx * 0.24, -0.06, 0.62), (sx * 0.33, 0.04 if sx < 0 else -0.04, 0.0), 0.11)

# Pod hull: lofted sections, nose at +Y, as a convex hull with a separate team top
secs = [(0.62, 0.24, 0.1, 0.64, 0.74, 0.79), (0.34, 0.33, 0.22, 0.56, 0.8, 0.93),
        (-0.28, 0.34, 0.24, 0.56, 0.82, 0.95), (-0.5, 0.25, 0.16, 0.62, 0.78, 0.87)]
pts = []
for y, hw, ht, z0, z1, z2 in secs:
    pts += [(-hw * 0.8, y, z0), (hw * 0.8, y, z0), (hw, y, z1), (ht, y, z2), (-ht, y, z2), (-hw, y, z1)]
pod = hull_from('pod', pts, 'hull', 0.03, 2)
inset_panels(pod, 0.025, 0.008, lambda f: abs(f.normal.x) > 0.6 and f.calc_area() > 0.04)
body.append(pod)
top = []
for y, hw, ht, z0, z1, z2 in secs[1:3]:
    top += [(-ht * 0.95, y - 0.02 * (1 if y > 0 else -1), z2 + 0.004), (ht * 0.95, y - 0.02 * (1 if y > 0 else -1), z2 + 0.004),
            (-ht * 0.85, y, z2 + 0.03), (ht * 0.85, y, z2 + 0.03)]
top += [(-0.08, 0.6, 0.8), (0.08, 0.6, 0.8)]
body.append(hull_from('top', top, 'team', 0.012, 1))
# Chevrons on the sloped flanks (team), a dark keel, the nose eye and survey lens
for sx in (-1, 1):
    body.append(hull_from(f'flank{sx}', [(sx * 0.3, 0.3, 0.82), (sx * 0.31, -0.26, 0.84), (sx * 0.22, 0.3, 0.925), (sx * 0.24, -0.26, 0.945),
                                         (sx * 0.33, 0.3, 0.8), (sx * 0.34, -0.26, 0.82)], 'team_dark', 0.006, 1))
body.append(box('keel', (0, -0.02, 0.55), (0.42, 0.7, 0.04), 'hull_dark', 0.01, 1))
body.append(hull_from('brow', [(-0.17, 0.5, 0.8), (0.17, 0.5, 0.8), (-0.12, 0.64, 0.79), (0.12, 0.64, 0.79),
                               (-0.15, 0.46, 0.84), (0.15, 0.46, 0.84)], 'hull_dark', 0.006, 1))
body.append(hull_from('eye', [(-0.13, 0.6, 0.73), (0.13, 0.6, 0.73), (-0.11, 0.645, 0.75), (0.11, 0.645, 0.75),
                              (-0.12, 0.6, 0.77), (0.12, 0.6, 0.77)], 'glow', 0, 1))
body.append(cyl('lens', (0.0, 0.58, 0.66), (0.0, 0.66, 0.66), 0.05, 'mech', 12))
body.append(cyl('lensg', (0.0, 0.66, 0.66), (0.0, 0.67, 0.66), 0.035, 'glass', 12, bevel=0))
# Side equipment pods with small forward nano emitters
for sx in (-1, 1):
    x = sx * 0.38
    body.append(hull_from(f'spod{sx}', [(x - 0.05, 0.28, 0.66), (x + 0.05, 0.28, 0.66), (x - 0.05, -0.12, 0.64), (x + 0.05, -0.12, 0.64),
                                        (x - 0.045, 0.24, 0.8), (x + 0.045, 0.24, 0.8), (x - 0.045, -0.12, 0.8), (x + 0.045, -0.12, 0.8)],
                          'hull_dark', 0.012, 1))
    body.append(box(f'spodtop{sx}', (x, 0.06, 0.805), (0.08, 0.3, 0.012), 'team', 0.004, 1))
    body.append(cyl(f'spodem{sx}', (x, 0.27, 0.72), (x, 0.36, 0.72), 0.03, 'metal', 8))
    body.append(cyl(f'spodg{sx}', (x, 0.36, 0.72), (x, 0.375, 0.72), 0.024, 'glow', 8, bevel=0))
    body.append(beam(f'spodline{sx}', (x + sx * 0.05, 0.2, 0.73), (x + sx * 0.05, -0.08, 0.73), 0.01, 0.018, 'glow_dim', bevel=0))
# Back: a sloped battery block with a glowing cell window, an antenna
body.append(hull_from('batt', [(-0.18, -0.38, 0.62), (0.18, -0.38, 0.62), (-0.16, -0.6, 0.66), (0.16, -0.6, 0.66),
                               (-0.16, -0.36, 0.86), (0.16, -0.36, 0.86), (-0.13, -0.56, 0.8), (0.13, -0.56, 0.8)], 'hull_dark', 0.015, 1))
body.append(box('battg', (0, -0.6, 0.72), (0.18, 0.012, 0.05), 'glow', 0, 1))
body.append(cyl('ant', (0.17, -0.42, 0.88), (0.21, -0.52, 1.3), 0.008, 'mech', 5, bevel=0))
body.append(sphere('anttip', (0.21, -0.52, 1.31), 0.02, 'glow', 8, 5))
body += crane('crane', (0, -0.16, 0.92), 0.78, 'lattice')

finish('lattice_surveyor', body)

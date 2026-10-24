"""Crucible "Mudskipper" amphibious crawler. Run: python tools/models/crucible_amphibious.py <out_dir>

A boat-hulled, riveted tracked crawler: real tracks under team mud skirts, a raised prow,
a tall snorkel exhaust, a team engine hatch and a low twin-laser turret.
Matches the procedural model: tracks at x +-0.56 (0.3 wide, y -0.98..0.98, 0.44 tall),
hull z 0.18..0.74, turret pivot (0, 0.12, 0.73), twin emitters at x +-0.1 reaching y 0.62.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, inset_panels
from bots_parts import beam, obox, emitter, stack, finish, add, lerp, rivet_row

kit.reset('crucible')
body = []
TR_IN, TR_OUT = 0.41, 0.71
Y0, Y1, R = -0.98, 0.98, 0.2
for sx in (-1, 1):
    x0, x1 = sorted((sx * TR_IN, sx * TR_OUT))
    xc = (x0 + x1) / 2
    pts = []
    for i in range(8):
        a = -math.pi / 2 + math.pi * i / 7
        pts.append((Y1 - R + math.cos(a) * R, 0.21 + math.sin(a) * R))
    for i in range(8):
        a = math.pi / 2 + math.pi * i / 7
        pts.append((Y0 + R + math.cos(a) * R, 0.21 + math.sin(a) * R))
    body.append(prism_x(f'band{sx}', pts, x0, x1, 'tread', bevel=0.01, segs=1))
    for i in range(14):
        y = Y0 + R + (Y1 - Y0 - 2 * R) * i / 13
        body.append(box(f'cleat{sx}_{i}', (xc, y, 0.005), (x1 - x0 + 0.02, 0.05, 0.03), 'mech', 0.006, 1))
    for i in range(5):
        y = -0.64 + i * 0.32
        body.append(cyl(f'wheel{sx}_{i}', (x0 - 0.01, y, 0.16), (x1 + 0.01, y, 0.16), 0.13, 'metal', 12, bevel=0.01))
        hx = x1 + 0.02 if sx > 0 else x0 - 0.02
        body.append(cyl(f'hub{sx}_{i}', (hx - sx * 0.03, y, 0.16), (hx, y, 0.16), 0.05, 'mech', 8, bevel=0))
    for y in (Y1 - R, Y0 + R):
        body.append(cyl(f'end{sx}_{y}', (x0 - 0.015, y, 0.21), (x1 + 0.015, y, 0.21), 0.15, 'mech', 10, bevel=0.01))
    # Team mud skirt over the upper run, with a slanted front lip
    sk = prism_x(f'skirt{sx}', [(1.0, 0.5), (0.9, 0.3), (-0.92, 0.3), (-1.0, 0.5)],
                 sx * (TR_OUT + 0.025) - 0.025, sx * (TR_OUT + 0.025) + 0.025, 'team', 0.01, 1)
    body.append(sk)
    body.append(box(f'fender{sx}', (xc + sx * 0.02, 0.0, 0.47), (0.36, 1.86, 0.04), 'team_dark', 0.008, 1))
    body += rivet_row(f'skr{sx}', (sx * (TR_OUT + 0.055), 0.85, 0.46), (sx * (TR_OUT + 0.055), -0.85, 0.46), 8, 0.016)

# Boat hull: a raised prow, flat deck, sloping stern
hull = prism_x('hull', [(1.2, 0.46), (0.98, 0.72), (-0.82, 0.72), (-1.06, 0.6), (-1.08, 0.3), (-0.8, 0.18), (0.86, 0.18)],
               -0.4, 0.4, 'hull', 0.03, 2)
inset_panels(hull, 0.03, 0.01, lambda f: abs(f.normal.x) > 0.7)
body.append(hull)
body.append(hull_from('prow', [(-0.38, 0.98, 0.72), (0.38, 0.98, 0.72), (-0.3, 1.2, 0.46), (0.3, 1.2, 0.46),
                               (-0.36, 0.98, 0.75), (0.36, 0.98, 0.75), (-0.28, 1.2, 0.49), (0.28, 1.2, 0.49)], 'team_dark', 0.01, 1))
body += rivet_row('prowr', (-0.3, 1.1, 0.63), (0.3, 1.1, 0.63), 4, 0.018)
body.append(box('deck', (0, 0.0, 0.73), (0.76, 1.6, 0.03), 'hull_dark', 0.01, 1))
for sx in (-1, 1):
    body.append(cyl(f'lamp{sx}', (sx * 0.24, 1.08, 0.6), (sx * 0.24, 1.14, 0.6), 0.04, 'glow', 8, bevel=0))
    body.append(cyl(f'lamph{sx}', (sx * 0.24, 1.04, 0.6), (sx * 0.24, 1.1, 0.6), 0.055, 'mech', 8, bevel=0))
# Engine deck at the back: a team hatch, grille and a tall snorkel stack
body.append(box('hatch', (-0.16, -0.58, 0.77), (0.32, 0.4, 0.06), 'team', 0.012, 1))
body += rivet_row('hr', (-0.3, -0.42, 0.8), (-0.02, -0.42, 0.8), 3, 0.015)
for k in range(5):
    body.append(box(f'grille{k}', (0.2, -0.44 - k * 0.06, 0.755), (0.24, 0.03, 0.02), 'mech', 0.004, 1))
body += stack('snorkel', (0.24, -0.62, 0.74), 0.07, 0.62, hot=False, band='team')
body.append(cyl('snorkelhood', (0.24, -0.62, 1.38), (0.24, -0.5, 1.38), 0.08, 'mech', 8))
body.append(box('winch', (0, -1.06, 0.5), (0.3, 0.08, 0.14), 'accent', 0.01, 1))
body += rivet_row('sr', (-0.3, -1.08, 0.62), (0.3, -1.08, 0.62), 4, 0.016)

PIV = (0, 0.12, 0.74)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = [cyl('ring', P(0, 0, 0), P(0, 0, 0.04), 0.26, 'mech', 10)]
tb = hull_from('tbody', [P(-0.25, -0.26, 0.02), P(0.25, -0.26, 0.02), P(-0.25, 0.2, 0.02), P(0.25, 0.2, 0.02),
                         P(-0.22, 0.28, 0.1), P(0.22, 0.28, 0.1),
                         P(-0.22, -0.24, 0.2), P(0.22, -0.24, 0.2), P(-0.2, 0.16, 0.2), P(0.2, 0.16, 0.2)], 'team', 0.02, 2)
inset_panels(tb, 0.025, 0.008, lambda f: f.normal.z > 0.6)
tur.append(tb)
tur += rivet_row('tr', P(-0.18, 0.22, 0.17), P(0.18, 0.22, 0.17), 4, 0.015)
tur.append(cyl('periscope', P(-0.1, -0.1, 0.2), P(-0.1, -0.1, 0.27), 0.045, 'hull_dark', 8))
tur.append(box('periscopeg', P(-0.1, -0.06, 0.25), (0.05, 0.01, 0.02), 'glow', 0, 1))
for sx in (-1, 1):
    tur.append(box(f'mount{sx}', P(sx * 0.1, 0.27, 0.1), (0.1, 0.08, 0.1), 'hull_dark', 0.01, 1))
    tur += emitter(f'las{sx}', P(sx * 0.1, 0.3, 0.1), P(sx * 0.1, 0.62, 0.1), 0.035, 'crucible')
    tur.append(cyl(f'lasjk{sx}', P(sx * 0.1, 0.34, 0.1), P(sx * 0.1, 0.44, 0.1), 0.05, 'accent', 8))

finish('crucible_amphibious', body, tur, PIV)

"""Crucible "Mantle Crawler" experimental. Run: python tools/models/crucible_experimental.py <out_dir>

A four-tracked mobile smelter. Four track units run under team-roofed sponsons studded with
rivets and lit by furnace vents; the armoured hull carries a glowing furnace mouth behind grille
bars on the glacis, an ore hopper heaped with scrap and molten slag, a bank of four tall
smokestacks with glowing mouths, point-defence lasers on the front corners, and a massive slag
cannon (the turret part) whose barrel glows along a heat seam.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
from mathutils import Vector
import airsea_parts as A

kit.reset('crucible')
body = []
V = Vector

# ---- Four track units ----
for sx in A.SIDES:
    x0, x1 = sorted((sx * 0.57, sx * 0.95))
    for k, (y0, y1) in enumerate(((0.08, 1.18), (-1.18, -0.08))):
        body += A.track(f'tr{sx}{k}', x0, x1, y0, y1, 0.2, 0.2, 4, 14, wheel_r=0.12)
        # Team skirt over the outer face of the track.
        xo = sx * 0.975
        body.append(box(f'skirt{sx}{k}', (xo, (y0 + y1) / 2, 0.3), (0.035, (y1 - y0) * 0.92, 0.16), 'team_dark', 0.008, 1))
        body += rivets(f'skr{sx}{k}', (xo + sx * 0.02, y1 - 0.12, 0.33), (xo + sx * 0.02, y0 + 0.12, 0.33), 5, 'metal', 0.02)
    # Track coupling between the front and rear units.
    body.append(box(f'coupling{sx}', (sx * 0.76, 0.0, 0.2), (0.24, 0.3, 0.2), 'mech', 0.02, 1))

# ---- Hull ----
prof = [(1.06, 0.2), (1.06, 0.42), (0.8, 0.78), (-0.96, 0.78), (-1.1, 0.62), (-1.1, 0.2)]
hull = prism_x('hull', prof, -0.58, 0.58, 'hull', bevel=0.03, segs=2)
inset_panels(hull, 0.04, 0.012, lambda f: abs(f.normal.x) > 0.9)
body.append(hull)
body.append(box('belly', (0, -0.02, 0.17), (1.0, 2.0, 0.1), 'hull_dark', 0.02, 1))
# Sponsons over the tracks: team roofs, furnace vents on the inner faces, rivets.
for sx in A.SIDES:
    sp = box(f'sponson{sx}', (sx * 0.76, 0.0, 0.5), (0.42, 2.3, 0.2), 'hull_dark', 0.025, 2)
    body.append(sp)
    roof = box(f'sproof{sx}', (sx * 0.77, 0.0, 0.605), (0.38, 2.18, 0.02), 'team', 0.008, 1)
    inset_panels(roof, 0.035, 0.008, lambda f: f.normal.z > 0.9)
    body.append(roof)
    for i in range(4):
        y = -0.6 + i * 0.4
        body.append(box(f'vent{sx}{i}', (sx * 0.968, y, 0.5), (0.02, 0.22, 0.08), 'mech', 0.004, 1))
        body.append(box(f'ventg{sx}{i}', (sx * 0.975, y, 0.5), (0.012, 0.18, 0.05), 'glow_dim', 0, 1))
        for k in range(3):
            body.append(box(f'ventbar{sx}{i}{k}', (sx * 0.982, y, 0.48 + k * 0.02), (0.01, 0.2, 0.008), 'mech', 0, 1))
    body += rivets(f'spr{sx}', (sx * 0.6, 1.08, 0.62), (sx * 0.6, -1.08, 0.62), 9, 'metal', 0.022)
    body += rivets(f'spr2{sx}', (sx * 0.94, 1.08, 0.62), (sx * 0.94, -1.08, 0.62), 9, 'metal', 0.022)
    # Front mudguard wedge and rear tow block.
    body.append(hull_from(f'guard{sx}', [(sx * 0.55, 1.15, 0.4), (sx * 0.97, 1.15, 0.4), (sx * 0.55, 1.15, 0.6), (sx * 0.97, 1.15, 0.6),
                                         (sx * 0.55, 1.25, 0.42), (sx * 0.97, 1.25, 0.42)], 'hull_dark', bevel=0.01))

# ---- Furnace mouth on the glacis ----
G = (0, 0.93, 0.6)
furn = [box('fplate', G, (0.74, 0.34, 0.05), 'mech', 0.01, 1),
        box('fglow', (0, 0.93, 0.625), (0.62, 0.24, 0.02), 'glow', 0, 1)]
for i in range(5):
    furn.append(box(f'grille{i}', (-0.24 + i * 0.12, 0.93, 0.645), (0.035, 0.28, 0.03), 'mech', 0.004, 1))
furn.append(box('fhood', (0, 0.93 - 0.18, 0.64), (0.8, 0.04, 0.08), 'hull_dark', 0.008, 1))
A.xform(furn, G, (math.radians(-54), 0, 0))
body += furn
body += rivets('glr', (-0.5, 1.0, 0.52), (0.5, 1.0, 0.52), 7, 'metal', 0.02)
for sx in A.SIDES:
    body.append(cyl(f'lamp{sx}', (sx * 0.45, 1.06, 0.34), (sx * 0.45, 1.1, 0.34), 0.05, 'glow', 10, bevel=0))
    body.append(cyl(f'lamphood{sx}', (sx * 0.45, 1.03, 0.34), (sx * 0.45, 1.08, 0.34), 0.065, 'mech', 10, bevel=0))

# ---- Ore hopper with scrap and molten slag ----
HY = 0.5
for sx in A.SIDES:
    body.append(hull_from(f'hwall{sx}', [(sx * 0.4, HY + 0.24, 0.78), (sx * 0.4, HY - 0.24, 0.78), (sx * 0.46, HY + 0.26, 1.02), (sx * 0.46, HY - 0.26, 1.02),
                                         (sx * 0.36, HY + 0.24, 0.78), (sx * 0.36, HY - 0.24, 0.78), (sx * 0.4, HY + 0.26, 1.02), (sx * 0.4, HY - 0.26, 1.02)], 'team', bevel=0.008))
for sy in (-1, 1):
    body.append(hull_from(f'hend{sy}', [(0.4, HY + sy * 0.24, 0.78), (-0.4, HY + sy * 0.24, 0.78), (0.46, HY + sy * 0.27, 1.02), (-0.46, HY + sy * 0.27, 1.02),
                                        (0.4, HY + sy * 0.2, 0.78), (-0.4, HY + sy * 0.2, 0.78), (0.46, HY + sy * 0.23, 1.02), (-0.46, HY + sy * 0.23, 1.02)], 'team_dark', bevel=0.008))
body.append(box('hrim', (0, HY, 1.03), (0.96, 0.56, 0.02), 'hull_dark', 0.004, 1))
body.append(box('hrimhole', (0, HY, 1.035), (0.84, 0.44, 0.02), 'mech', 0, 1))
body.append(box('slag', (0, HY, 0.93), (0.8, 0.42, 0.04), 'glow_dim', 0, 1))
for i, (x, y, rx, rz, s) in enumerate(((-0.2, -0.05, 0.5, 0.3, 1.0), (0.18, 0.05, -0.4, 0.8, 0.9), (0.02, -0.1, 1.1, 0.2, 1.2),
                                       (-0.28, 0.1, 0.3, 1.4, 0.8), (0.27, -0.1, 0.9, 2.0, 1.0), (0.05, 0.12, 0.2, 2.6, 0.9))):
    c = (x, HY + y, 0.98)
    ch = box(f'scrap{i}', c, (0.16 * s, 0.12 * s, 0.07 * s), 'metal' if i % 2 else 'accent', 0.008, 1)
    A.xform([ch], c, (rx, 0.3, rz))
    body.append(ch)
body.append(cyl('scrapbeam', (-0.3, HY - 0.12, 1.0), (0.25, HY + 0.15, 1.06), 0.03, 'hull_dark', 6, bevel=0))
body += rivets('hrv', (-0.44, HY + 0.27, 1.02), (0.44, HY + 0.27, 1.02), 6, 'metal', 0.02)

# ---- Smokestacks at the back ----
for i, (x, y, h) in enumerate(((-0.4, -0.72, 0.62), (0.4, -0.72, 0.62), (-0.18, -0.94, 0.48), (0.18, -0.94, 0.48))):
    body.append(cyl(f'stbase{i}', (x, y, 0.78), (x, y, 0.86), 0.11, 'hull_dark', 12, bevel=0.006))
    body.append(cyl(f'stack{i}', (x, y, 0.86), (x, y, 0.78 + h), 0.075, 'metal', 12, r2=0.065, bevel=0.004))
    body.append(cyl(f'stband{i}', (x, y, 0.78 + h * 0.45), (x, y, 0.78 + h * 0.55), 0.08, 'team_dark', 12, bevel=0))
    body.append(cyl(f'strim{i}', (x, y, 0.78 + h - 0.03), (x, y, 0.78 + h + 0.02), 0.078, 'mech', 12, bevel=0))
    body.append(cyl(f'stglow{i}', (x, y, 0.78 + h + 0.015), (x, y, 0.78 + h + 0.022), 0.06, 'glow', 12, bevel=0))
body += rivets('rr', (-0.5, -0.45, 0.785), (0.5, -0.45, 0.785), 6, 'metal', 0.022)
body.append(box('engdeck', (0, -0.83, 0.8), (0.3, 0.2, 0.04), 'mech', 0.006, 1))
for i in range(4):
    body.append(box(f'grate{i}', (0, -0.76 - i * 0.045, 0.825), (0.26, 0.018, 0.012), 'hull_dark', 0, 1))
# Pipes running from the hopper to the stacks.
for sx in A.SIDES:
    body.append(cyl(f'pipe{sx}', (sx * 0.5, 0.2, 0.84), (sx * 0.5, -0.6, 0.84), 0.035, 'accent', 8, bevel=0))
    body.append(cyl(f'pipeb{sx}', (sx * 0.5, -0.6, 0.84), (sx * 0.42, -0.7, 0.9), 0.035, 'accent', 8, bevel=0))

# ---- Point-defence lasers on the front corners ----
for sx in A.SIDES:
    body.append(box(f'pdbox{sx}', (sx * 0.48, 0.74, 0.86), (0.16, 0.18, 0.14), 'hull_dark', 0.014, 1))
    body.append(box(f'pdtop{sx}', (sx * 0.48, 0.74, 0.935), (0.14, 0.16, 0.012), 'team', 0.004, 1))
    body.append(cyl(f'pdgun{sx}', (sx * 0.48, 0.82, 0.86), (sx * 0.48, 1.04, 0.86), 0.025, 'metal', 8, bevel=0))
    body.append(cyl(f'pdlens{sx}', (sx * 0.48, 1.0, 0.86), (sx * 0.48, 1.06, 0.86), 0.035, 'mech', 8, bevel=0))
    body.append(sphere(f'pdglow{sx}', (sx * 0.48, 1.06, 0.86), 0.022, 'glow', 8, 4))

# ---- Slag cannon (turret) ----
TP = V((0, -0.12, 0.78))
P = lambda x, y, z: tuple(TP + V((x, y, z)))
tur = [cyl('race', P(0, 0, -0.01), P(0, 0, 0.08), 0.36, 'mech', 16, r2=0.32, bevel=0.008)]
tb = box('tbox', P(0, -0.06, 0.22), (0.54, 0.6, 0.28), 'team', 0.03, 2)
inset_panels(tb, 0.035, 0.01, lambda f: f.normal.z > 0.9 or abs(f.normal.x) > 0.9)
tur.append(tb)
tur.append(box('tface', P(0, 0.25, 0.2), (0.46, 0.04, 0.22), 'hull_dark', 0.012, 1))
tur.append(box('tbustle', P(0, -0.4, 0.2), (0.44, 0.12, 0.2), 'hull_dark', 0.016, 1))
tur += rivets('trv', P(-0.22, 0.2, 0.365), P(0.22, 0.2, 0.365), 5, 'metal', 0.02)
tur += rivets('trv2', P(-0.22, -0.32, 0.365), P(0.22, -0.32, 0.365), 5, 'metal', 0.02)
tur.append(cyl('cupola', P(-0.14, -0.15, 0.36), P(-0.14, -0.15, 0.42), 0.09, 'hull', 12, bevel=0.006))
tur.append(box('cupglow', P(-0.14, -0.06, 0.4), (0.08, 0.01, 0.02), 'glow', 0, 1))
for sx in A.SIDES:
    tur.append(cyl(f'feed{sx}', P(sx * 0.2, -0.38, 0.32), P(sx * 0.2, 0.1, 0.32), 0.04, 'accent', 8, bevel=0))
# Barrel: elevated 16 degrees, with a team band, muzzle glow and a glowing heat seam.
root = V(P(0, 0.2, 0.24))
d = V((0, math.cos(math.radians(16)), math.sin(math.radians(16))))
tur.append(cyl('barrel', root, root + d * 0.98, 0.11, 'metal', 14, r2=0.095, bevel=0.006))
tur.append(cyl('bband', root + d * 0.1, root + d * 0.3, 0.14, 'team_dark', 14, bevel=0.006))
tur.append(cyl('muzzle', root + d * 0.88, root + d * 1.06, 0.14, 'mech', 14, r2=0.13, bevel=0.006))
tur.append(cyl('muzzleglow', root + d * 1.055, root + d * 1.065, 0.1, 'glow', 14, bevel=0))
tur.append(A.beam('seam', root + d * 0.32 + V((0, 0, 0.1)), root + d * 0.86 + V((0, 0, 0.09)), 0.04, 0.02, 'glow_dim', bevel=0.003))
for k in range(3):
    t = 0.4 + k * 0.15
    tur.append(cyl(f'bring{k}', root + d * t, root + d * (t + 0.03), 0.112, 'mech', 14, bevel=0))

A.finish('crucible_experimental', [('Body', body, None), ('Turret', tur, tuple(TP))], 1024)

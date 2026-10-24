"""Lattice "Monolith" battleship. Run: python tools/models/lattice_battleship.py <out_dir>

A floating fortress in stepped ceramic slabs: a long needle-bowed hull with blue waterline
lights, a three-tier ziggurat superstructure with team roofs, a rangefinder bar and a tall
sensor mast, flank beam emitters for close defence, vertical launch cells, a fixed twin turret
facing aft, and a big triple-barrelled artillery turret forward (the turret part).
The waterline is z = 0: the hull dips below it.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A

kit.reset('lattice')
F = 'lattice'
HW, DECK, KEEL = 0.3, 0.14, -0.1
body, secs = A.ship_hull(F, HW, DECK, KEEL)
for sx in A.SIDES:
    body.append(box(f'wl{sx}', (sx * (HW * 0.985 + 0.004), -0.37, 0.07), (0.01, 0.98, 0.01), 'glow_dim', 0, 1))
body.append(box('boweye', (0, 1.13, DECK + 0.035), (0.03, 0.04, 0.015), 'glow', 0, 1))
body.append(box('foredeck', (0, 0.88, DECK + 0.005), (0.18, 0.3, 0.01), 'team_dark', 0.003, 1))


def slab(name, c, size, top='team', cap=0.03):
    x, y, z = c; sx_, sy_, sz_ = size
    z0, z1 = z - sz_ / 2, z + sz_ / 2
    pts = [(x + a * sx_ / 2, y + b * sy_ / 2, z0) for a in (-1, 1) for b in (-1, 1)]
    pts += [(x + a * sx_ / 2, y + b * sy_ / 2, z1 - cap) for a in (-1, 1) for b in (-1, 1)]
    pts += [(x + a * (sx_ / 2 - cap * 1.2), y + b * (sy_ / 2 - cap * 1.2), z1) for a in (-1, 1) for b in (-1, 1)]
    o = [hull_from(name, pts, 'hull', bevel=0.006)]
    if top:
        o.append(box(name + 'roof', (x, y, z1 + 0.004), (sx_ - cap * 3, sy_ - cap * 3, 0.01), top, 0.003, 1))
    return o

# ---- Ziggurat superstructure ----
body += slab('tier1', (0, -0.12, 0.22), (0.36, 0.66, 0.16))
body += slab('tier2', (0, -0.06, 0.36), (0.26, 0.38, 0.13))
body += slab('tier3', (0, -0.02, 0.48), (0.16, 0.18, 0.1), top='hull_dark')
body.append(hull_from('bridgeglass', [(0.07, 0.07, 0.46), (-0.07, 0.07, 0.46), (0.06, 0.065, 0.5), (-0.06, 0.065, 0.5),
                                      (0.07, 0.08, 0.46), (-0.07, 0.08, 0.46), (0.06, 0.075, 0.5), (-0.06, 0.075, 0.5)], 'glass', bevel=0.002))
body.append(box('rangefinder', (0, -0.04, 0.55), (0.34, 0.04, 0.035), 'hull_dark', 0.006, 1))
for sx in A.SIDES:
    body.append(box(f'rfeye{sx}', (sx * 0.17, -0.04, 0.55), (0.01, 0.045, 0.03), 'glow', 0, 1))
    body.append(box(f'strip{sx}', (sx * 0.182, -0.12, 0.22), (0.006, 0.54, 0.012), 'glow', 0, 1))
    body.append(box(f'strip2{sx}', (sx * 0.132, -0.06, 0.36), (0.006, 0.3, 0.01), 'glow', 0, 1))
    # Flank beam emitters in faceted sponsons.
    body.append(hull_from(f'spon{sx}', [(sx * 0.16, 0.18, DECK), (sx * 0.27, 0.18, DECK), (sx * 0.16, 0.0, DECK), (sx * 0.27, 0.02, DECK),
                                         (sx * 0.17, 0.15, 0.26), (sx * 0.24, 0.15, 0.25), (sx * 0.17, 0.02, 0.26), (sx * 0.24, 0.03, 0.25)], 'hull', bevel=0.006))
    body.append(cyl(f'emit{sx}', (sx * 0.22, 0.14, 0.22), (sx * 0.24, 0.34, 0.22), 0.018, 'metal', 8, bevel=0))
    body.append(cyl(f'emitlens{sx}', (sx * 0.24, 0.33, 0.22), (sx * 0.242, 0.37, 0.22), 0.025, 'mech', 8, bevel=0))
    body.append(sphere(f'emitglow{sx}', (sx * 0.242, 0.37, 0.22), 0.016, 'glow', 8, 4))
    # VLS cells along the flanks of tier one.
    for k in range(3):
        body.append(box(f'vls{sx}{k}', (sx * 0.22, -0.3 - k * 0.09, DECK + 0.012), (0.06, 0.07, 0.024), 'mech', 0.003, 1))
        body.append(box(f'vlsl{sx}{k}', (sx * 0.22, -0.3 - k * 0.09, DECK + 0.026), (0.012, 0.012, 0.004), 'glow', 0, 1))
# ---- Sensor mast ----
body.append(cyl('mast', (0, -0.04, 0.53), (0, -0.06, 0.9), 0.014, 'metal', 8, r2=0.009, bevel=0))
for sx in A.SIDES:
    body.append(cyl(f'mleg{sx}', (sx * 0.07, -0.08, 0.53), (0, -0.05, 0.74), 0.008, 'metal', 6, bevel=0))
body.append(box('yard', (0, -0.05, 0.78), (0.2, 0.016, 0.016), 'metal', 0.003, 1))
body.append(sphere('radome', (0, -0.05, 0.69), 0.035, 'hull', 12, 6))
body.append(sphere('masttip', (0, -0.06, 0.91), 0.02, 'glow', 8, 4))

# ---- Aft twin turret, fixed, facing back ----
AP = (0, -0.66, DECK)
aft = [cyl('arace', AP, (0, -0.66, DECK + 0.025), 0.14, 'mech', 14, bevel=0.004)]
aft.append(hull_from('ashell', [(-0.13, -0.66 + 0.14, DECK + 0.02), (0.13, -0.52, DECK + 0.02), (-0.14, -0.74, DECK + 0.02), (0.14, -0.74, DECK + 0.02),
                                (-0.06, -0.84, DECK + 0.03), (0.06, -0.84, DECK + 0.03), (-0.1, -0.53, DECK + 0.13), (0.1, -0.53, DECK + 0.13),
                                (-0.08, -0.72, DECK + 0.14), (0.08, -0.72, DECK + 0.14)], 'team', bevel=0.01))
for x in (-0.045, 0.045):
    aft += A.barrel(f'ab{x}', (x, -0.8, DECK + 0.07), (x, -1.12, DECK + 0.08), 0.018)
body += aft

# ---- Main triple artillery turret ----
PIV = (0, 0.5, DECK)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = [cyl('race', P(0, 0, -0.005), P(0, 0, 0.025), 0.18, 'mech', 16, bevel=0.004)]
shell = hull_from('tshell', [P(-0.17, -0.18, 0.02), P(0.17, -0.18, 0.02), P(-0.18, 0.1, 0.02), P(0.18, 0.1, 0.02),
                             P(-0.1, 0.22, 0.03), P(0.1, 0.22, 0.03), P(-0.13, -0.17, 0.15), P(0.13, -0.17, 0.15),
                             P(-0.11, 0.08, 0.16), P(0.11, 0.08, 0.16), P(-0.07, 0.16, 0.12), P(0.07, 0.16, 0.12)], 'team', bevel=0.012)
inset_panels(shell, 0.02, 0.006, lambda f: f.calc_area() > 0.01)
tur.append(shell)
tur.append(box('trf', P(0, -0.12, 0.17), (0.3, 0.04, 0.03), 'hull_dark', 0.005, 1))
for sx in A.SIDES:
    tur.append(box(f'trfeye{sx}', P(sx * 0.15, -0.12, 0.17), (0.008, 0.042, 0.022), 'glow', 0, 1))
tur.append(box('mantlet', P(0, 0.19, 0.075), (0.2, 0.05, 0.07), 'hull_dark', 0.008, 1))
for x in (-0.065, 0.0, 0.065):
    tur += A.barrel(f'gun{x}', P(x, 0.2, 0.075), P(x, 0.7, 0.085), 0.022)
    tur.append(cyl(f'sleeve{x}', P(x, 0.2, 0.075), P(x, 0.32, 0.077), 0.03, 'hull', 10, bevel=0.003))

A.finish('lattice_battleship', [('Body', body, None), ('Turret', tur, PIV)], 1024)

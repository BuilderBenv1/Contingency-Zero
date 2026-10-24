"""Lattice "Skiff" patrol boat. Run: python tools/models/lattice_patrol_boat.py <out_dir>

A fast, sleek boat: a needle-bowed hull with a team shoulder stripe and a blue waterline light,
a bevelled bridge slab with a wraparound screen, a sensor mast with a glowing tip, a two-cell AA
launcher aft, twin waterjets glowing at the stern, and a faceted gun turret forward (the turret
part). The waterline is z = 0: the hull dips below it.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A

kit.reset('lattice')
F = 'lattice'
HW, DECK, KEEL = 0.4, 0.24, -0.16
body, secs = A.ship_hull(F, HW, DECK, KEEL)

# Blue waterline lights along both sides, and a sensor eye on the bow.
for sx in A.SIDES:
    body.append(box(f'wl{sx}', (sx * (HW * 0.985 + 0.004), -0.36, 0.13), (0.01, 0.95, 0.014), 'glow_dim', 0, 1))
body.append(box('boweye', (0, 1.12, DECK + 0.02), (0.04, 0.05, 0.02), 'glow', 0, 1))
# Team deck plates fore and aft.
for y, ln, w in ((0.22, 0.3, 0.5), (-0.64, 0.52, 0.6)):
    p = box(f'deckplate{y}', (0, y, DECK + 0.006), (w, ln, 0.012), 'team', 0.004, 1)
    body.append(p)
# ---- Bridge slab ----
bridge = hull_from('bridge', [(0.22, 0.14, DECK), (-0.22, 0.14, DECK), (0.22, -0.4, DECK), (-0.22, -0.4, DECK),
                              (0.21, 0.06, 0.48), (-0.21, 0.06, 0.48), (0.21, -0.38, 0.5), (-0.21, -0.38, 0.5),
                              (0.15, 0.0, 0.54), (-0.15, 0.0, 0.54), (0.15, -0.32, 0.54), (-0.15, -0.32, 0.54)], 'team', bevel=0.012)
body.append(bridge)
body.append(box('broof', (0, -0.16, 0.548), (0.26, 0.28, 0.012), 'hull', 0.004, 1))
body.append(hull_from('screen', [(0.2, 0.128, 0.36), (-0.2, 0.128, 0.36), (0.19, 0.072, 0.46), (-0.19, 0.072, 0.46),
                                 (0.2, 0.142, 0.36), (-0.2, 0.142, 0.36), (0.19, 0.086, 0.46), (-0.19, 0.086, 0.46)], 'glass', bevel=0.003))
for sx in A.SIDES:
    body.append(box(f'sidewin{sx}', (sx * 0.216, -0.06, 0.42), (0.008, 0.2, 0.05), 'glass', 0, 1))
for sx in A.SIDES:
    body.append(box(f'bstrip{sx}', (sx * 0.222, -0.2, 0.32), (0.006, 0.36, 0.012), 'glow', 0, 1))
# ---- Mast: a slim tripod with a yard, radome and glowing tip ----
body.append(cyl('mast', (0, -0.22, 0.54), (0, -0.26, 0.98), 0.02, 'metal', 8, r2=0.012, bevel=0))
for sx in A.SIDES:
    body.append(cyl(f'mleg{sx}', (sx * 0.12, -0.3, 0.54), (0, -0.25, 0.8), 0.01, 'metal', 6, bevel=0))
body.append(box('yard', (0, -0.25, 0.82), (0.3, 0.025, 0.025), 'metal', 0.004, 1))
body.append(sphere('radome', (0, -0.245, 0.88), 0.045, 'hull', 12, 6))
body.append(sphere('masttip', (0, -0.26, 0.99), 0.03, 'glow', 8, 4))
# ---- AA launcher aft, facing back and up ----
body.append(cyl('aamount', (0, -0.72, DECK), (0, -0.72, DECK + 0.07), 0.13, 'hull_dark', 14, bevel=0.006))
body += A.missile_pod('aa', (0, -0.74, 0.37), (0.28, 0.24, 0.16), 45, 2, 1, 'team', tip='hull', back=True)
# ---- Stern: twin waterjets glowing blue, and a transom step ----
for sx in A.SIDES:
    body.append(cyl(f'jet{sx}', (sx * 0.18, -1.08, 0.04), (sx * 0.18, -1.2, 0.04), 0.07, 'mech', 12, r2=0.075, bevel=0.004))
    body.append(cyl(f'jetglow{sx}', (sx * 0.18, -1.19, 0.04), (sx * 0.18, -1.205, 0.04), 0.055, 'glow', 12, bevel=0))
    # Deck cleats and a life-raft canister.
    body.append(box(f'cleat{sx}', (sx * 0.28, 0.6, DECK + 0.02), (0.03, 0.08, 0.03), 'metal', 0.006, 1))
    body.append(cyl(f'raft{sx}', (sx * 0.3, -0.36, DECK + 0.05), (sx * 0.3, -0.56, DECK + 0.05), 0.05, 'hull', 10, bevel=0.006))

# ---- Turret: faceted wedge with a single long gun ----
PIV = (0, 0.56, DECK)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = []
tur.append(cyl('race', P(0, 0, -0.01), P(0, 0, 0.03), 0.17, 'mech', 16, bevel=0.004))
shell = hull_from('tshell', [P(-0.16, -0.16, 0.02), P(0.16, -0.16, 0.02), P(-0.17, 0.08, 0.02), P(0.17, 0.08, 0.02),
                             P(-0.08, 0.2, 0.04), P(0.08, 0.2, 0.04), P(-0.12, -0.15, 0.15), P(0.12, -0.15, 0.15),
                             P(-0.1, 0.06, 0.16), P(0.1, 0.06, 0.16), P(-0.05, 0.14, 0.12), P(0.05, 0.14, 0.12)], 'team', bevel=0.012)
inset_panels(shell, 0.02, 0.006, lambda f: f.calc_area() > 0.01)
tur.append(shell)
tur.append(box('tsensor', P(0.09, -0.04, 0.17), (0.05, 0.08, 0.04), 'hull_dark', 0.006, 1))
tur.append(box('teye', P(0.09, 0.0, 0.17), (0.035, 0.012, 0.018), 'glow', 0, 1))
tur.append(box('mantlet', P(0, 0.17, 0.08), (0.08, 0.06, 0.07), 'hull_dark', 0.01, 1))
tur += A.barrel('gun', P(0, 0.18, 0.08), P(0, 0.64, 0.085), 0.026)
tur.append(cyl('gunsleeve', P(0, 0.18, 0.08), P(0, 0.3, 0.08), 0.036, 'hull', 10, bevel=0.003))

A.finish('lattice_patrol_boat', [('Body', body, None), ('Turret', tur, PIV)], 512)

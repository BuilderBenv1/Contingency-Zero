"""Crucible "Leviathan" battleship. Run: python tools/models/crucible_battleship.py <out_dir>

A riveted iron fortress: a broad blunt hull with an armour belt, a stacked blockhouse
superstructure with team roofs and an orange-lit bridge slot, two hot smokestacks, a crane and
lifeboats, flank laser sponsons, a fixed twin turret facing aft, and a huge square triple
artillery turret forward (the turret part). The waterline is z = 0: the hull dips below it.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A

kit.reset('crucible')
F = 'crucible'
HW, DECK, KEEL = 0.34, 0.15, -0.1
body, secs = A.ship_hull(F, HW, DECK, KEEL)
for sx in A.SIDES:
    # Armour belt along the waterline, studded with rivets.
    body.append(box(f'belt{sx}', (sx * (HW + 0.01), -0.1, 0.05), (0.025, 1.6, 0.07), 'hull_dark', 0.006, 1))
    body += rivets(f'br{sx}', (sx * (HW + 0.024), 0.62, 0.05), (sx * (HW + 0.024), -0.82, 0.05), 12, 'metal', 0.012)
    body += rivets(f'dr{sx}', (sx * HW * 0.82, 0.85, DECK + 0.004), (sx * HW * 0.82, -1.0, DECK + 0.004), 12, 'metal', 0.012)
body.append(box('foredeck', (0, 0.88, DECK + 0.006), (0.3, 0.26, 0.012), 'team_dark', 0.004, 1))
body.append(box('anchor', (0.18, 1.1, 0.1), (0.02, 0.07, 0.08), 'mech', 0.005, 1))
body.append(box('anchor2', (-0.18, 1.1, 0.1), (0.02, 0.07, 0.08), 'mech', 0.005, 1))

# ---- Blockhouse superstructure ----
b1 = box('block1', (0, -0.14, 0.25), (0.42, 0.66, 0.2), 'hull', 0.02, 2)
inset_panels(b1, 0.03, 0.008, lambda f: abs(f.normal.x) > 0.9)
body.append(b1)
r1 = box('roof1', (0, -0.14, 0.355), (0.44, 0.68, 0.02), 'team', 0.006, 1)
inset_panels(r1, 0.03, 0.005, lambda f: f.normal.z > 0.9)
body.append(r1)
body.append(box('block2', (0, -0.02, 0.42), (0.3, 0.36, 0.12), 'hull_dark', 0.016, 1))
r2 = box('roof2', (0, -0.02, 0.49), (0.32, 0.38, 0.02), 'team', 0.006, 1)
body.append(r2)
body.append(box('bwin', (0, 0.162, 0.43), (0.24, 0.01, 0.035), 'glow_dim', 0, 1))
body.append(box('conn', (0, 0.06, 0.54), (0.14, 0.12, 0.08), 'hull', 0.012, 1))
body.append(box('connslot', (0, 0.122, 0.55), (0.1, 0.01, 0.02), 'glow', 0, 1))
body += rivets('rr1', (-0.2, 0.18, 0.36), (0.2, 0.18, 0.36), 6, 'metal', 0.014)
body += rivets('rr2', (-0.14, 0.15, 0.5), (0.14, 0.15, 0.5), 4, 'metal', 0.013)
# Smokestacks.
for k, y in enumerate((-0.28, -0.42)):
    body.append(cyl(f'stack{k}', (0, y, 0.36), (0, y, 0.68), 0.06, 'metal', 10, r2=0.052, bevel=0.004))
    body.append(cyl(f'stband{k}', (0, y, 0.48), (0, y, 0.53), 0.065, 'team_dark', 10, bevel=0))
    body.append(cyl(f'strim{k}', (0, y, 0.66), (0, y, 0.7), 0.063, 'mech', 10, bevel=0))
    body.append(cyl(f'stglow{k}', (0, y, 0.695), (0, y, 0.702), 0.048, 'glow', 10, bevel=0))
# Mast with yard and light.
body.append(cyl('mast', (0, 0.0, 0.58), (0, 0.0, 0.88), 0.013, 'metal', 6, bevel=0))
body.append(box('yard', (0, 0.0, 0.8), (0.22, 0.02, 0.02), 'metal', 0, 1))
body.append(sphere('masttip', (0, 0.0, 0.89), 0.02, 'glow', 8, 4))
for sx in A.SIDES:
    # Flank laser sponsons.
    body.append(box(f'spon{sx}', (sx * 0.25, 0.1, 0.21), (0.1, 0.14, 0.1), 'hull_dark', 0.01, 1))
    body.append(cyl(f'emit{sx}', (sx * 0.27, 0.16, 0.22), (sx * 0.28, 0.34, 0.22), 0.02, 'metal', 8, bevel=0))
    body.append(cyl(f'emitlens{sx}', (sx * 0.28, 0.32, 0.22), (sx * 0.282, 0.36, 0.22), 0.028, 'mech', 8, bevel=0))
    body.append(sphere(f'emitglow{sx}', (sx * 0.282, 0.36, 0.22), 0.018, 'glow', 8, 4))
    # Lifeboats.
    body.append(cyl(f'boat{sx}', (sx * 0.25, -0.3, 0.38), (sx * 0.25, -0.5, 0.38), 0.035, 'accent', 8, bevel=0.005))
# Crane behind the stacks.
body.append(cyl('cranepost', (0.12, -0.52, 0.36), (0.12, -0.52, 0.56), 0.025, 'accent', 8, bevel=0))
body.append(cyl('cranejib', (0.12, -0.52, 0.55), (0.25, -0.75, 0.5), 0.014, 'accent', 6, bevel=0))
body.append(cyl('cranecable', (0.25, -0.75, 0.5), (0.25, -0.75, 0.36), 0.004, 'metal', 4, bevel=0))

# ---- Aft twin turret, fixed, facing back ----
body.append(cyl('arace', (0, -0.68, DECK), (0, -0.68, DECK + 0.025), 0.16, 'mech', 12, bevel=0.004))
ab = box('abox', (0, -0.68, DECK + 0.08), (0.26, 0.28, 0.12), 'team', 0.016, 2)
inset_panels(ab, 0.025, 0.005, lambda f: f.normal.z > 0.9)
body.append(ab)
body.append(box('aface', (0, -0.83, DECK + 0.075), (0.22, 0.03, 0.1), 'hull_dark', 0.006, 1))
body += rivets('arv', (-0.1, -0.6, DECK + 0.145), (0.1, -0.6, DECK + 0.145), 3, 'metal', 0.014)
for x in (-0.05, 0.05):
    body += A.barrel(f'ab{x}', (x, -0.82, DECK + 0.075), (x, -1.1, DECK + 0.075), 0.022)

# ---- Main triple turret ----
PIV = (0, 0.5, DECK)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = [cyl('race', P(0, 0, -0.005), P(0, 0, 0.025), 0.2, 'mech', 12, bevel=0.004)]
tb = box('tbox', P(0, -0.02, 0.09), (0.32, 0.36, 0.14), 'team', 0.02, 2)
inset_panels(tb, 0.025, 0.006, lambda f: f.normal.z > 0.9)
tur.append(tb)
tur.append(box('tface', P(0, 0.165, 0.08), (0.3, 0.03, 0.12), 'hull_dark', 0.008, 1))
tur.append(box('tbustle', P(0, -0.22, 0.08), (0.26, 0.06, 0.1), 'hull_dark', 0.008, 1))
tur += rivets('trv', P(-0.12, 0.12, 0.165), P(0.12, 0.12, 0.165), 4, 'metal', 0.015)
tur += rivets('trv2', P(-0.12, -0.15, 0.165), P(0.12, -0.15, 0.165), 4, 'metal', 0.015)
tur.append(box('rf', P(0, -0.08, 0.18), (0.36, 0.04, 0.035), 'hull_dark', 0.006, 1))
tur.append(box('hatch', P(-0.08, -0.02, 0.17), (0.07, 0.07, 0.015), 'hull_dark', 0.004, 1))
for x in (-0.08, 0.0, 0.08):
    tur += A.barrel(f'gun{x}', P(x, 0.17, 0.08), P(x, 0.66, 0.08), 0.027)
    tur.append(cyl(f'sleeve{x}', P(x, 0.17, 0.08), P(x, 0.28, 0.08), 0.036, 'hull_dark', 10, bevel=0.003))

A.finish('crucible_battleship', [('Body', body, None), ('Turret', tur, PIV)], 1024)

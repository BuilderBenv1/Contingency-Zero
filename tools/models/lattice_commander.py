"""Lattice Commander. Run: python tools/models/lattice_commander.py <out_dir>

A tall, sleek command mech: reverse-jointed legs with team thigh plates, a narrow waist, a
broad V-shaped chest under team armour, angular team pauldrons, a visored head with a crest
fin, and a backpack reactor with twin sensor masts. Right arm: the Breach Cannon, a long
team-plated rail housing with glowing accelerator coils and a split muzzle. Left arm: a slim
laser with a three-pronged nanolathe. The upper body is the Turret (it twists to aim).
Matches the procedural model: hips at 0.8, turret pivot (0, -0.04, 0.88), arms at x = +-0.5.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, inset_panels
from bots_parts import lattice_leg, beam, obox, emitter, finish, add, lerp, rivet_row

kit.reset('lattice')

body = []
# ---- Legs: confident wide stance, left foot a touch forward ----
for sx in (-1, 1):
    hip = (sx * 0.2, -0.03, 0.8)
    foot = (sx * 0.34, 0.05 if sx < 0 else -0.05, 0.0)
    body += lattice_leg(f'leg{sx}', hip, foot, 0.14)
# Pelvis: a bevelled wedge with a groin plate and team hip skirts
body.append(hull_from('pelvis', [
    (-0.2, -0.16, 0.7), (0.2, -0.16, 0.7), (-0.16, 0.14, 0.72), (0.16, 0.14, 0.72),
    (-0.26, -0.2, 0.9), (0.26, -0.2, 0.9), (-0.24, 0.16, 0.9), (0.24, 0.16, 0.9)], 'hull_dark', 0.02, 2))
body.append(hull_from('groin', [
    (-0.1, 0.16, 0.62), (0.1, 0.16, 0.62), (-0.15, 0.2, 0.88), (0.15, 0.2, 0.88),
    (-0.08, 0.1, 0.64), (0.08, 0.1, 0.64), (-0.13, 0.12, 0.88), (0.13, 0.12, 0.88)], 'hull', 0.01, 1))
body.append(box('groinglow', (0, 0.205, 0.8), (0.1, 0.01, 0.025), 'glow', 0, 1))
for sx in (-1, 1):
    body.append(hull_from(f'tasset{sx}', [
        (sx * 0.22, -0.14, 0.92), (sx * 0.22, 0.14, 0.92), (sx * 0.34, -0.12, 0.9), (sx * 0.34, 0.12, 0.9),
        (sx * 0.27, -0.1, 0.66), (sx * 0.27, 0.1, 0.66), (sx * 0.36, -0.08, 0.68), (sx * 0.36, 0.08, 0.68)], 'team', 0.012, 1))

# ---- Upper body (turret), pivot at the waist ----
PIV = (0.0, -0.04, 0.88)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = []
# Waist swivel and a ribbed abdomen
tur.append(cyl('waist', P(0, 0, -0.04), P(0, 0, 0.08), 0.16, 'mech', 14))
tur.append(cyl('waistring', P(0, 0, 0.02), P(0, 0, 0.05), 0.175, 'glow_dim', 14, bevel=0))
tur.append(hull_from('abdomen', [P(-0.15, -0.12, 0.06), P(0.15, -0.12, 0.06), P(-0.13, 0.12, 0.06), P(0.13, 0.12, 0.06),
                                 P(-0.2, -0.15, 0.2), P(0.2, -0.15, 0.2), P(-0.18, 0.14, 0.2), P(0.18, 0.14, 0.2)], 'mech', 0.015, 1))
for k in range(3):
    tur.append(box(f'rib{k}', P(0, 0.135, 0.09 + k * 0.04), (0.26 - k * 0.0, 0.03, 0.02), 'metal', 0.005, 1))
# Chest: V-taper, wide at the shoulders, the front sloping back
chest = hull_from('chest', [
    P(-0.22, -0.16, 0.16), P(0.22, -0.16, 0.16), P(-0.2, 0.16, 0.16), P(0.2, 0.16, 0.16),
    P(-0.38, -0.2, 0.42), P(0.38, -0.2, 0.42), P(-0.36, 0.2, 0.38), P(0.36, 0.2, 0.38),
    P(-0.3, -0.18, 0.5), P(0.3, -0.18, 0.5), P(-0.26, 0.12, 0.5), P(0.26, 0.12, 0.5)], 'hull', 0.025, 2)
body_chest = chest
tur.append(chest)
# Team armour: two angled pectoral plates and a top deck, with a glowing core between
for sx in (-1, 1):
    tur.append(hull_from(f'pec{sx}', [
        P(sx * 0.03, 0.17, 0.2), P(sx * 0.21, 0.165, 0.2),
        P(sx * 0.03, 0.215, 0.38), P(sx * 0.35, 0.205, 0.39),
        P(sx * 0.03, 0.15, 0.48), P(sx * 0.26, 0.135, 0.5),
        P(sx * 0.03, 0.19, 0.38), P(sx * 0.2, 0.15, 0.22)], 'team', 0.01, 1))
tur.append(hull_from('core', [P(-0.035, 0.2, 0.26), P(0.035, 0.2, 0.26), P(0, 0.235, 0.34), P(0, 0.2, 0.42),
                              P(-0.03, 0.17, 0.34), P(0.03, 0.17, 0.34)], 'glow', 0, 1))
deck = hull_from('deck', [P(-0.3, -0.19, 0.5), P(0.3, -0.19, 0.5), P(-0.26, 0.13, 0.5), P(0.26, 0.13, 0.5),
                          P(-0.26, -0.16, 0.535), P(0.26, -0.16, 0.535), P(-0.22, 0.1, 0.535), P(0.22, 0.1, 0.535)], 'team', 0.01, 1)
tur.append(deck)
tur.append(box('deckline', P(0, -0.03, 0.54), (0.03, 0.26, 0.01), 'glow_dim', 0, 1))
# Collar and head: a wedge with a visor band and a team crest fin
tur.append(hull_from('collar', [P(-0.13, -0.08, 0.5), P(0.13, -0.08, 0.5), P(-0.11, 0.1, 0.5), P(0.11, 0.1, 0.5),
                                P(-0.08, -0.05, 0.58), P(0.08, -0.05, 0.58), P(-0.07, 0.07, 0.58), P(0.07, 0.07, 0.58)], 'mech', 0.01, 1))
H = lambda x, y, z: P(x * 1.3, 0.01 + y * 1.25, 0.56 + (z - 0.56) * 1.3)
head = hull_from('head', [
    H(-0.09, -0.1, 0.56), H(0.09, -0.1, 0.56), H(-0.08, 0.12, 0.58), H(0.08, 0.12, 0.58),
    H(-0.06, 0.17, 0.63), H(0.06, 0.17, 0.63),
    H(-0.08, -0.12, 0.69), H(0.08, -0.12, 0.69), H(-0.06, 0.08, 0.71), H(0.06, 0.08, 0.71)], 'hull', 0.015, 2)
tur.append(head)
tur.append(hull_from('visor', [H(-0.075, 0.125, 0.605), H(0.075, 0.125, 0.605), H(-0.05, 0.165, 0.635), H(0.05, 0.165, 0.635),
                               H(-0.068, 0.13, 0.65), H(0.068, 0.13, 0.65), H(-0.04, 0.16, 0.65), H(0.04, 0.16, 0.65)], 'glow', 0, 1))
tur.append(hull_from('crest', [H(-0.012, 0.08, 0.7), H(0.012, 0.08, 0.7), H(-0.012, -0.16, 0.69), H(0.012, -0.16, 0.69),
                               H(-0.008, -0.22, 0.84), H(0.008, -0.22, 0.84), H(-0.008, -0.1, 0.78), H(0.008, -0.1, 0.78)], 'team', 0.004, 1))
for sx in (-1, 1):
    tur.append(obox(f'ear{sx}', P(sx * 0.125, -0.02, 0.64), (0.03, 0.1, 0.05), 'mech', (0, 0, 0), 0.006))
    tur.append(box(f'eartip{sx}', P(sx * 0.143, 0.02, 0.64), (0.008, 0.03, 0.02), 'glow', 0, 1))

# Shoulders: joint, angular team pauldron with a glowing edge, upper arm down to the elbow
for sx in (-1, 1):
    sh = P(sx * 0.42, 0.0, 0.38)
    tur.append(cyl(f'shj{sx}', P(sx * 0.33, 0, 0.38), P(sx * 0.5, 0, 0.38), 0.09, 'mech', 12))
    pa = hull_from(f'pauldron{sx}', [
        P(sx * 0.32, -0.19, 0.42), P(sx * 0.32, 0.2, 0.42), P(sx * 0.3, -0.15, 0.55), P(sx * 0.3, 0.13, 0.55),
        P(sx * 0.5, -0.2, 0.52), P(sx * 0.5, 0.17, 0.52),
        P(sx * 0.64, -0.17, 0.4), P(sx * 0.64, 0.17, 0.4), P(sx * 0.62, -0.13, 0.3), P(sx * 0.62, 0.13, 0.3),
        P(sx * 0.42, -0.14, 0.3), P(sx * 0.42, 0.14, 0.3)], 'team', 0.018, 2)
    tur.append(pa)
    tur.append(beam(f'paulglow{sx}', P(sx * 0.635, -0.13, 0.36), P(sx * 0.635, 0.13, 0.36), 0.012, 0.025, 'glow', bevel=0))
    tur.append(hull_from(f'paulfin{sx}', [P(sx * 0.4, -0.12, 0.54), P(sx * 0.4, 0.08, 0.54), P(sx * 0.42, -0.14, 0.6),
                                         P(sx * 0.44, -0.04, 0.6), P(sx * 0.46, -0.12, 0.53), P(sx * 0.46, 0.08, 0.53)], 'hull_dark', 0.004, 1))
    elbow = P(sx * 0.5, 0.02, 0.1)
    tur.append(beam(f'uarm{sx}', P(sx * 0.48, 0, 0.36), elbow, 0.1, 0.11, 'mech'))
    tur.append(beam(f'uarmplate{sx}', P(sx * 0.49, 0.0, 0.3), P(sx * 0.5, 0.02, 0.15), 0.15, 0.16, 'hull', w2=0.13, d2=0.14))
    tur.append(sphere(f'elbow{sx}', elbow, 0.075, 'metal', 10, 6))

# Right arm: Breach Cannon. Forearm housing in team, then a rail accelerator with coils.
X, Z = 0.5, 0.06
C = lambda y, dz=0.0, dx=0.0: P(X + dx, y, Z + dz)
house = hull_from('dg_house', [
    C(-0.14, -0.1, -0.1), C(-0.14, -0.1, 0.1), C(-0.14, 0.1, -0.1), C(-0.14, 0.1, 0.1),
    C(0.34, -0.09, -0.09), C(0.34, -0.09, 0.09), C(0.34, 0.08, -0.08), C(0.34, 0.08, 0.08),
    C(-0.08, 0.14, -0.06), C(-0.08, 0.14, 0.06), C(0.28, 0.12, -0.05), C(0.28, 0.12, 0.05)], 'team', 0.016, 2)
inset_panels(house, 0.018, 0.006, lambda f: f.calc_area() > 0.012)
tur.append(house)
tur.append(box('dg_belly', C(0.1, -0.11), (0.14, 0.4, 0.04), 'hull_dark', 0.008, 1))
tur.append(box('dg_vent', C(0.0, 0.135), (0.08, 0.16, 0.012), 'mech', 0.004, 1))
for k in range(4):
    tur.append(box(f'dg_slot{k}', C(-0.05 + k * 0.035, 0.142), (0.06, 0.012, 0.006), 'glow_dim', 0, 1))
# Twin rails above and below a glowing core, wrapped by accelerator coils
for sz in (-1, 1):
    tur.append(beam(f'dg_rail{sz}', C(0.3, sz * 0.055), C(0.8, sz * 0.05), 0.07, 0.035, 'hull', bevel=0.008))
    tur.append(beam(f'dg_railtip{sz}', C(0.8, sz * 0.05), C(0.88, sz * 0.03), 0.07, 0.035, 'hull_dark', w2=0.06, d2=0.03, bevel=0.006))
tur.append(cyl('dg_core', C(0.3), C(0.84), 0.028, 'glow', 10, bevel=0))
for k in range(4):
    y = 0.38 + k * 0.11
    tur.append(cyl(f'dg_coil{k}', C(y - 0.022), C(y + 0.022), 0.085, 'mech', 12, bevel=0.006))
    tur.append(cyl(f'dg_coilg{k}', C(y - 0.03), C(y - 0.022), 0.07, 'glow', 12, bevel=0))
tur.append(cyl('dg_muzring', C(0.82), C(0.86), 0.075, 'hull_dark', 14, bevel=0.006))
tur.append(cyl('dg_muzglow', C(0.86), C(0.87), 0.06, 'glow', 14, bevel=0))
tur.append(cyl('dg_spine', C(0.3, 0.0, 0.07), C(0.76, 0.0, 0.07), 0.016, 'metal', 6, bevel=0))
# Power cable from the backpack round to the cannon
tur.append(cyl('dg_cab0', P(0.22, -0.26, 0.32), P(0.5, -0.24, 0.22), 0.025, 'mech', 6, bevel=0))
tur.append(cyl('dg_cab1', P(0.5, -0.24, 0.22), P(0.56, -0.14, 0.1), 0.025, 'mech', 6, bevel=0))

# Left arm: slim forearm with a laser below and a three-pronged nanolathe at the front
X = -0.5
L = lambda y, dz=0.0, dx=0.0: P(X + dx, y, Z + dz)
fore = hull_from('lf_fore', [
    L(-0.1, -0.07, -0.07), L(-0.1, -0.07, 0.07), L(-0.1, 0.07, -0.07), L(-0.1, 0.07, 0.07),
    L(0.26, -0.06, -0.06), L(0.26, -0.06, 0.06), L(0.26, 0.06, -0.06), L(0.26, 0.06, 0.06),
    L(0.0, 0.1, -0.04), L(0.0, 0.1, 0.04), L(0.2, 0.09, -0.03), L(0.2, 0.09, 0.03)], 'hull', 0.014, 2)
tur.append(fore)
tur.append(box('lf_plate', L(0.06, 0.07), (0.1, 0.22, 0.04), 'team', 0.008, 1))
tur += emitter('lf_laser', L(0.2, -0.06), L(0.5, -0.06), 0.026)
tur.append(cyl('lf_hub', L(0.24, 0.02), L(0.32, 0.02), 0.06, 'mech', 10))
for k in range(3):
    a = math.radians(90 + k * 120)
    o = (math.cos(a) * 0.055, math.sin(a) * 0.055)
    tur.append(beam(f'lf_prong{k}', L(0.3, 0.02 + o[1], o[0]), L(0.46, 0.02 + o[1] * 0.5, o[0] * 0.5), 0.022, 0.022, 'metal', bevel=0.004))
tur.append(sphere('lf_orb', L(0.43, 0.02), 0.035, 'glow', 10, 6))

# Backpack: reactor housing with a glowing ring, cooling fins and twin sensor masts
pack = hull_from('pack', [P(-0.24, -0.18, 0.16), P(0.24, -0.18, 0.16), P(-0.26, -0.18, 0.5), P(0.26, -0.18, 0.5),
                          P(-0.2, -0.36, 0.2), P(0.2, -0.36, 0.2), P(-0.22, -0.34, 0.48), P(0.22, -0.34, 0.48)], 'hull_dark', 0.02, 2)
tur.append(pack)
tur.append(hull_from('packtop', [P(-0.24, -0.19, 0.5), P(0.24, -0.19, 0.5), P(-0.2, -0.34, 0.48), P(0.2, -0.34, 0.48),
                                 P(-0.2, -0.2, 0.53), P(0.2, -0.2, 0.53), P(-0.17, -0.31, 0.51), P(0.17, -0.31, 0.51)], 'team', 0.008, 1))
tur.append(cyl('reactor', P(0, -0.36, 0.33), P(0, -0.4, 0.33), 0.11, 'mech', 16))
tur.append(cyl('reactorg', P(0, -0.4, 0.33), P(0, -0.41, 0.33), 0.085, 'glow', 16, bevel=0))
tur.append(cyl('reactorc', P(0, -0.41, 0.33), P(0, -0.42, 0.33), 0.045, 'metal', 12, bevel=0))
for sx in (-1, 1):
    for k in range(4):
        tur.append(box(f'fin{sx}{k}', P(sx * 0.27, -0.22 - k * 0.04, 0.33), (0.04, 0.014, 0.24), 'metal', 0.003, 1))
    tur.append(cyl(f'mastb{sx}', P(sx * 0.16, -0.28, 0.5), P(sx * 0.16, -0.28, 0.56), 0.03, 'metal', 8, bevel=0))
    tur.append(cyl(f'mast{sx}', P(sx * 0.16, -0.28, 0.56), P(sx * 0.21, -0.36, 0.86), 0.013, 'mech', 6, bevel=0))
    tur.append(sphere(f'masttip{sx}', P(sx * 0.21, -0.36, 0.87), 0.028, 'glow', 8, 5))
tur += rivet_row('packr', P(-0.18, -0.355, 0.44), P(0.18, -0.355, 0.44), 5, 0.014)

finish('lattice_commander', body, tur, PIV, 1024)

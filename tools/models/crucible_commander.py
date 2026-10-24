"""Crucible Commander. Run: python tools/models/crucible_commander.py <out_dir>

A squat, furnace-hearted war-mech: stocky forward-kneed stomper legs, a hunched riveted
torso with a glowing furnace grate in its chest, a low sunken head between massive team
pauldrons, twin glowing smokestacks over a caged boiler on its back. Right arm: the Breach
Cannon, a fat banded cannon with cooling rings and a glowing bore. Left arm: a hydraulic
three-fingered nanolathe claw. The upper body is the Turret (it twists to aim).
Matches the procedural model: hips at 0.64, turret pivot (0, -0.05, 0.74), arms at x = +-0.57.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, inset_panels
from bots_parts import crucible_leg, beam, obox, stack, finish, add, lerp, rivet_row

kit.reset('crucible')

body = []
# ---- Legs: wide planted stance, left foot forward ----
for sx in (-1, 1):
    hip = (sx * 0.3, -0.05, 0.64)
    foot = (sx * 0.4, 0.06 if sx < 0 else -0.06, 0.0)
    body += crucible_leg(f'leg{sx}', hip, foot, 0.17)
# Pelvis: a heavy block with a riveted belt and a team codpiece plate
body.append(box('pelvis', (0, -0.05, 0.67), (0.66, 0.44, 0.2), 'hull_dark', 0.025, 2))
body.append(box('belt', (0, -0.05, 0.74), (0.7, 0.48, 0.05), 'mech', 0.01, 1))
body.append(hull_from('cod', [(-0.16, 0.18, 0.56), (0.16, 0.18, 0.56), (-0.2, 0.22, 0.76), (0.2, 0.22, 0.76),
                              (-0.14, 0.12, 0.56), (0.14, 0.12, 0.56), (-0.2, 0.14, 0.76), (0.2, 0.14, 0.76)], 'team', 0.012, 1))
body += rivet_row('codr', (-0.14, 0.215, 0.72), (0.14, 0.215, 0.72), 4, 0.016)
for sx in (-1, 1):
    body.append(box(f'hipguard{sx}', (sx * 0.4, -0.05, 0.7), (0.06, 0.34, 0.2), 'hull', 0.012, 1))

# ---- Upper body (turret), pivot at the waist ----
PIV = (0.0, -0.05, 0.74)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = []
tur.append(cyl('waist', P(0, 0, -0.03), P(0, 0, 0.09), 0.25, 'mech', 12))
tur.append(cyl('waistband', P(0, 0, 0.03), P(0, 0, 0.06), 0.26, 'accent', 12, bevel=0))
# Torso: a hunched barrel-chested block, leaning forward
torso = hull_from('torso', [
    P(-0.34, -0.24, 0.07), P(0.34, -0.24, 0.07), P(-0.32, 0.24, 0.07), P(0.32, 0.24, 0.07),
    P(-0.44, -0.28, 0.32), P(0.44, -0.28, 0.32), P(-0.42, 0.3, 0.3), P(0.42, 0.3, 0.3),
    P(-0.42, -0.26, 0.5), P(0.42, -0.26, 0.5), P(-0.38, 0.22, 0.5), P(0.38, 0.22, 0.5)], 'hull', 0.03, 2)
tur.append(torso)
# Team armour: a big riveted top plate and an upper chest plate sloping forward
top = hull_from('topplate', [P(-0.39, -0.24, 0.5), P(0.39, -0.24, 0.5), P(-0.35, 0.2, 0.5), P(0.35, 0.2, 0.5),
                             P(-0.36, -0.21, 0.535), P(0.36, -0.21, 0.535), P(-0.32, 0.18, 0.535), P(0.32, 0.18, 0.535)], 'team', 0.01, 1)
tur.append(top)
chestp = hull_from('chestplate', [P(-0.38, 0.22, 0.49), P(0.38, 0.22, 0.49), P(-0.4, 0.305, 0.3), P(0.4, 0.305, 0.3),
                                  P(-0.38, 0.26, 0.49), P(0.38, 0.26, 0.49), P(-0.4, 0.34, 0.3), P(0.4, 0.34, 0.3)], 'team', 0.01, 1)
tur.append(chestp)
tur += rivet_row('chr', P(-0.34, 0.3, 0.45), P(0.34, 0.3, 0.45), 6, 0.018)
tur += rivet_row('topr0', P(-0.33, -0.2, 0.54), P(-0.33, 0.15, 0.54), 4, 0.016)
tur += rivet_row('topr1', P(0.33, -0.2, 0.54), P(0.33, 0.15, 0.54), 4, 0.016)
# Furnace grate in the belly: a dark frame with glowing bars behind
tur.append(box('grateframe', P(0, 0.31, 0.19), (0.4, 0.06, 0.18), 'mech', 0.012, 1))
tur.append(box('grateglow', P(0, 0.335, 0.19), (0.32, 0.02, 0.12), 'glow', 0, 1))
for k in range(5):
    tur.append(box(f'gratebar{k}', P(-0.12 + k * 0.06, 0.35, 0.19), (0.022, 0.03, 0.16), 'metal', 0.004, 1))
# Sunken head between the pauldrons, a wide visor slit and a jaw guard
tur.append(hull_from('head', [P(-0.15, 0.0, 0.46), P(0.15, 0.0, 0.46), P(-0.15, 0.3, 0.46), P(0.15, 0.3, 0.46),
                              P(-0.13, 0.02, 0.63), P(0.13, 0.02, 0.63), P(-0.12, 0.24, 0.62), P(0.12, 0.24, 0.62)], 'hull_dark', 0.02, 2))
tur.append(box('visor', P(0, 0.295, 0.56), (0.2, 0.03, 0.035), 'glow', 0, 1))
tur.append(box('brow', P(0, 0.29, 0.6), (0.26, 0.06, 0.035), 'mech', 0.008, 1))
tur.append(hull_from('jaw', [P(-0.12, 0.28, 0.46), P(0.12, 0.28, 0.46), P(-0.08, 0.34, 0.48), P(0.08, 0.34, 0.48),
                             P(-0.13, 0.28, 0.53), P(0.13, 0.28, 0.53), P(-0.09, 0.33, 0.53), P(0.09, 0.33, 0.53)], 'metal', 0.008, 1))
tur.append(box('headtop', P(0, 0.1, 0.645), (0.18, 0.18, 0.03), 'team', 0.008, 1))

# Massive pauldrons, riveted, with a heavy rim and the upper arm beneath
for sx in (-1, 1):
    tur.append(cyl(f'shj{sx}', P(sx * 0.38, 0, 0.38), P(sx * 0.58, 0, 0.38), 0.14, 'mech', 10))
    pa = hull_from(f'pauldron{sx}', [
        P(sx * 0.36, -0.28, 0.36), P(sx * 0.36, 0.28, 0.36), P(sx * 0.4, -0.25, 0.6), P(sx * 0.4, 0.25, 0.6),
        P(sx * 0.68, -0.27, 0.58), P(sx * 0.68, 0.27, 0.58), P(sx * 0.76, -0.28, 0.4), P(sx * 0.76, 0.28, 0.4),
        P(sx * 0.74, -0.26, 0.3), P(sx * 0.74, 0.26, 0.3)], 'team', 0.02, 2)
    inset_panels(pa, 0.03, 0.01, lambda f: f.normal.z > 0.5 and f.calc_area() > 0.05)
    tur.append(pa)
    tur.append(box(f'paulrim{sx}', P(sx * 0.72, 0, 0.31), (0.1, 0.58, 0.05), 'hull_dark', 0.01, 1))
    tur += rivet_row(f'paulr{sx}a', P(sx * 0.48, -0.2, 0.6), P(sx * 0.48, 0.2, 0.6), 4, 0.02)
    tur += rivet_row(f'paulr{sx}b', P(sx * 0.66, -0.2, 0.59), P(sx * 0.66, 0.2, 0.59), 4, 0.02)
    elbow = P(sx * 0.57, 0.02, 0.04)
    tur.append(beam(f'uarm{sx}', P(sx * 0.57, 0, 0.32), elbow, 0.15, 0.16, 'mech'))
    tur.append(beam(f'uarmplate{sx}', P(sx * 0.58, 0, 0.28), P(sx * 0.58, 0.02, 0.1), 0.2, 0.2, 'hull_dark'))
    tur.append(sphere(f'elbow{sx}', elbow, 0.1, 'metal', 10, 6))

# Back: a caged boiler under twin hot stacks, with feed pipes
tur.append(box('boiler', P(0, -0.36, 0.3), (0.48, 0.18, 0.36), 'hull_dark', 0.02, 2))
tur.append(box('boilerglow', P(0, -0.455, 0.27), (0.28, 0.02, 0.16), 'glow_dim', 0, 1))
for k in range(4):
    tur.append(box(f'cagev{k}', P(-0.15 + k * 0.1, -0.47, 0.27), (0.02, 0.03, 0.26), 'mech', 0.004, 1))
for k in range(2):
    tur.append(box(f'cageh{k}', P(0, -0.47, 0.18 + k * 0.18), (0.36, 0.03, 0.02), 'mech', 0.004, 1))
for sx in (-1, 1):
    tur += stack(f'stack{sx}', P(sx * 0.17, -0.32, 0.46), 0.07, 0.42, band='accent')
    tur.append(cyl(f'pipe{sx}', P(sx * 0.3, -0.3, 0.42), P(sx * 0.3, -0.2, 0.55), 0.03, 'accent', 6, bevel=0))
tur.append(cyl('pipeh', P(-0.3, -0.42, 0.44), P(0.3, -0.42, 0.44), 0.028, 'metal', 6, bevel=0))

# Right arm: Breach Cannon. A riveted forearm housing in team, then a fat banded cannon.
X, Z = 0.57, 0.02
C = lambda y, dz=0.0, dx=0.0: P(X + dx, y, Z + dz)
house = box('dg_house', C(0.1), (0.28, 0.54, 0.28), 'team', 0.025, 2)
inset_panels(house, 0.025, 0.008, lambda f: f.calc_area() > 0.05)
tur.append(house)
tur.append(box('dg_under', C(0.08, -0.15), (0.22, 0.42, 0.04), 'hull_dark', 0.008, 1))
tur += rivet_row('dg_r0', C(-0.12, 0.145, -0.1), C(0.32, 0.145, -0.1), 5, 0.017)
tur += rivet_row('dg_r1', C(-0.12, 0.145, 0.1), C(0.32, 0.145, 0.1), 5, 0.017)
tur.append(box('dg_feed', C(0.0, 0.0, 0.16), (0.05, 0.26, 0.14), 'accent', 0.01, 1))
tur.append(cyl('dg_breech', C(0.36), C(0.44), 0.13, 'mech', 12))
tur.append(cyl('dg_barrel', C(0.44), C(0.84), 0.095, 'metal', 12, r2=0.09))
for k in range(3):
    y = 0.5 + k * 0.1
    tur.append(cyl(f'dg_ring{k}', C(y), C(y + 0.035), 0.12, 'hull_dark', 12, bevel=0.006))
tur.append(cyl('dg_muzzle', C(0.82), C(0.92), 0.125, 'mech', 12, r2=0.135))
tur.append(cyl('dg_bore', C(0.92), C(0.925), 0.085, 'glow', 12, bevel=0))
tur.append(cyl('dg_heat', C(0.56), C(0.8), 0.1, 'glow_dim', 12, bevel=0))

# Left arm: a hydraulic claw nanolathe
X = -0.57
L = lambda y, dz=0.0, dx=0.0: P(X + dx, y, Z + dz)
tur.append(box('lf_fore', L(0.08), (0.24, 0.44, 0.24), 'hull_dark', 0.02, 2))
tur.append(box('lf_plate', L(0.08, 0.13), (0.2, 0.36, 0.03), 'team', 0.008, 1))
tur.append(cyl('lf_ram', L(-0.08, 0.0, -0.14), L(0.26, 0.0, -0.14), 0.028, 'metal', 6, bevel=0))
tur.append(cyl('lf_wrist', L(0.3), L(0.38), 0.1, 'mech', 10))
for k in range(3):
    a = math.radians(90 + k * 120)
    ox, oz = math.cos(a) * 0.075, math.sin(a) * 0.075
    k1 = L(0.38, oz, ox)
    k2 = L(0.5, oz * 1.4, ox * 1.4)
    k3 = L(0.6, oz * 0.4, ox * 0.4)
    tur.append(beam(f'lf_f{k}a', k1, k2, 0.045, 0.045, 'metal', bevel=0.006))
    tur.append(beam(f'lf_f{k}b', k2, k3, 0.04, 0.04, 'accent', w2=0.025, d2=0.025, bevel=0.005))
tur.append(sphere('lf_orb', L(0.52), 0.05, 'glow', 10, 6))

finish('crucible_commander', body, tur, PIV, 1024)

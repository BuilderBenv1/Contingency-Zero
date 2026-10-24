"""Lattice "Skywarden" experimental walker. Run: python tools/models/lattice_experimental.py <out_dir>

A towering two-legged walker. Three-jointed legs with team thigh armour, glowing knee eyes,
hydraulic rams and splayed three-toed feet carry a heavy pelvis. The upper body is the turret
part: a broad faceted torso with a glowing visor, shoulder AA missile banks, swept antenna vanes,
heat-sink fins down the back, and the giant beam lance along its spine with three glowing focus
coils and a lens that cuts a lane across the battlefield.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
from mathutils import Vector
import airsea_parts as A

kit.reset('lattice')
body = []
V = Vector

# ---- Legs ----
for sx in A.SIDES:
    H, K = V((sx * 0.34, -0.12, 1.3)), V((sx * 0.42, 0.24, 0.86))
    N, Ft = V((sx * 0.44, -0.28, 0.4)), V((sx * 0.46, -0.04, 0.05))
    # Hip actuator drum.
    body.append(cyl(f'hip{sx}', (sx * 0.2, H.y, H.z), (sx * 0.48, H.y, H.z), 0.13, 'mech', 16, bevel=0.01))
    body.append(cyl(f'hipcap{sx}', (sx * 0.48, H.y, H.z), (sx * 0.5, H.y, H.z), 0.1, 'hull', 16, bevel=0.004))
    body.append(cyl(f'hipglow{sx}', (sx * 0.5, H.y, H.z), (sx * 0.505, H.y, H.z), 0.05, 'glow', 12, bevel=0))
    # Thigh: core and team armour plate.
    body.append(A.beam(f'thighcore{sx}', H, K, 0.1, 0.12, 'mech'))
    th = A.beam(f'thigh{sx}', H.lerp(K, 0.1), H.lerp(K, 0.84), 0.19, 0.24, 'team', bevel=0.02, taper=0.8)
    inset_panels(th, 0.025, 0.008, lambda f: f.calc_area() > 0.02)
    body.append(th)
    body.append(A.beam(f'thighrib{sx}', H.lerp(K, 0.2) + V((sx * 0.1, 0, 0)), H.lerp(K, 0.75) + V((sx * 0.09, 0, 0)), 0.03, 0.1, 'hull'))
    # Knee joint with a glowing eye.
    body.append(cyl(f'knee{sx}', K - V((0.1, 0, 0)), K + V((0.1, 0, 0)), 0.1, 'mech', 14, bevel=0.008))
    body.append(hull_from(f'kneecap{sx}', [tuple(K + V(p)) for p in ((-0.09, 0.06, 0.08), (0.09, 0.06, 0.08), (-0.09, 0.06, -0.1), (0.09, 0.06, -0.1),
                                                                     (-0.06, 0.16, 0.0), (0.06, 0.16, 0.0), (-0.06, 0.13, -0.08), (0.06, 0.13, -0.08))], 'hull', bevel=0.01))
    body.append(box(f'kneeeye{sx}', tuple(K + V((0, 0.165, -0.01))), (0.07, 0.012, 0.035), 'glow', 0, 1))
    # Shin: metal core, grey armour, a hydraulic ram from thigh to shin.
    body.append(A.beam(f'shincore{sx}', K, N, 0.08, 0.1, 'metal'))
    sh = A.beam(f'shin{sx}', K.lerp(N, 0.12), K.lerp(N, 0.72), 0.15, 0.17, 'hull', bevel=0.016, taper=0.75)
    inset_panels(sh, 0.02, 0.006, lambda f: f.calc_area() > 0.02)
    body.append(sh)
    body.append(cyl(f'ram{sx}', H.lerp(K, 0.45) + V((0, -0.12, 0)), K.lerp(N, 0.4) + V((0, -0.06, 0)), 0.03, 'metal', 8, bevel=0))
    body.append(cyl(f'ramsleeve{sx}', H.lerp(K, 0.45) + V((0, -0.12, 0)), (H.lerp(K, 0.45) + V((0, -0.12, 0))).lerp(K.lerp(N, 0.4) + V((0, -0.06, 0)), 0.45), 0.045, 'mech', 8, bevel=0))
    # Ankle and foot strut.
    body.append(cyl(f'ankle{sx}', N - V((0.08, 0, 0)), N + V((0.08, 0, 0)), 0.075, 'mech', 12, bevel=0.006))
    body.append(A.beam(f'footcore{sx}', N, Ft + V((0, 0, 0.06)), 0.07, 0.09, 'metal'))
    body.append(A.beam(f'footarm{sx}', N.lerp(Ft, 0.12), N.lerp(Ft, 0.6), 0.12, 0.13, 'hull', bevel=0.012, taper=0.8))
    # Splayed three-toed foot and a heel spur.
    body.append(cyl(f'footpad{sx}', Ft + V((0, 0, -0.04)), Ft + V((0, 0, 0.05)), 0.11, 'hull_dark', 12, bevel=0.008))
    for a in (-0.55, 0.0, 0.55):
        toe = [hull_from(f'toe{sx}{a}', [(Ft.x - 0.07, Ft.y + 0.04, 0.0), (Ft.x + 0.07, Ft.y + 0.04, 0.0), (Ft.x - 0.06, Ft.y + 0.04, 0.09),
                                         (Ft.x + 0.06, Ft.y + 0.04, 0.09), (Ft.x - 0.035, Ft.y + 0.34, 0.0), (Ft.x + 0.035, Ft.y + 0.34, 0.0),
                                         (Ft.x - 0.025, Ft.y + 0.3, 0.05), (Ft.x + 0.025, Ft.y + 0.3, 0.05)], 'hull_dark', bevel=0.008),
               box(f'toeplate{sx}{a}', (Ft.x, Ft.y + 0.17, 0.088), (0.09, 0.18, 0.012), 'hull', 0.003, 1),
               hull_from(f'claw{sx}{a}', [(Ft.x - 0.025, Ft.y + 0.33, 0.0), (Ft.x + 0.025, Ft.y + 0.33, 0.0), (Ft.x, Ft.y + 0.33, 0.05),
                                          (Ft.x, Ft.y + 0.42, 0.0)], 'mech', bevel=0.004)]
        A.xform(toe, (Ft.x, Ft.y, 0), (0, 0, -a))
        body += toe
    body.append(hull_from(f'heel{sx}', [(Ft.x - 0.05, Ft.y - 0.05, 0.0), (Ft.x + 0.05, Ft.y - 0.05, 0.0), (Ft.x - 0.04, Ft.y - 0.05, 0.08),
                                        (Ft.x + 0.04, Ft.y - 0.05, 0.08), (Ft.x, Ft.y - 0.24, 0.0), (Ft.x, Ft.y - 0.2, 0.04)], 'hull_dark', bevel=0.006))

# ---- Pelvis ----
pel = hull_from('pelvis', [(-0.31, 0.12, 1.22), (0.31, 0.12, 1.22), (-0.31, -0.32, 1.22), (0.31, -0.32, 1.22),
                           (-0.27, 0.1, 1.42), (0.27, 0.1, 1.42), (-0.27, -0.3, 1.42), (0.27, -0.3, 1.42),
                           (-0.15, 0.2, 1.3), (0.15, 0.2, 1.3)], 'hull_dark', bevel=0.02)
body.append(pel)
body.append(box('codplate', (0, 0.17, 1.28), (0.22, 0.04, 0.12), 'hull', 0.01, 1))
body.append(box('pelvislight', (0, 0.192, 1.28), (0.12, 0.008, 0.02), 'glow_dim', 0, 1))

# ---- Upper body (turret) ----
TP = V((0, -0.08, 1.4))
P = lambda x, y, z: tuple(TP + V((x, y, z)))
tur = [cyl('waist', P(0, 0, -0.09), P(0, 0, 0.02), 0.27, 'mech', 20, r2=0.23, bevel=0.006)]
secs = [A.hexring(0.46, 0.26, 0.16, 0.02, 0.18, 0.28), A.hexring(0.26, 0.46, 0.34, -0.02, 0.3, 0.46),
        A.hexring(-0.3, 0.46, 0.34, -0.02, 0.3, 0.46), A.hexring(-0.5, 0.3, 0.2, 0.02, 0.24, 0.36)]
secs = [[tuple(TP + V(p)) for p in r] for r in secs]
band = {0: 'mech', 1: 'hull', 5: 'hull', 2: 'hull', 4: 'hull', 3: 'team'}
torso = A.loft('torso', secs, 'hull', lambda j: band[j], cap0='hull_dark', cap1='hull_dark', bevel=0.02)
inset_panels(torso, 0.03, 0.01, lambda f: f.calc_area() > 0.05)
tur.append(torso)
# Visor and sensor cluster.
tur.append(box('visor', P(0, 0.465, 0.18), (0.3, 0.02, 0.05), 'glow', 0, 1))
for sx in A.SIDES:
    tur.append(sphere(f'sens{sx}', P(sx * 0.18, 0.4, 0.3), 0.035, 'glow', 10, 6))
    tur.append(box(f'brow{sx}', P(sx * 0.18, 0.42, 0.34), (0.12, 0.06, 0.025), 'hull_dark', 0.006, 1))
# Spine slab carrying the beam lance.
spine = hull_from('spine', [P(-0.15, 0.21, 0.46), P(0.15, 0.21, 0.46), P(-0.15, -0.45, 0.46), P(0.15, -0.45, 0.46),
                            P(-0.12, 0.18, 0.6), P(0.12, 0.18, 0.6), P(-0.12, -0.42, 0.6), P(0.12, -0.42, 0.6)], 'hull', bevel=0.012)
tur.append(spine)
tur.append(box('spineplate', P(0, -0.12, 0.605), (0.18, 0.48, 0.012), 'team', 0.004, 1))
tur.append(cyl('lance', P(0, -0.2, 0.62), P(0, 1.08, 0.62), 0.09, 'metal', 16, r2=0.075, bevel=0.006))
for k, y in enumerate((0.32, 0.56, 0.8)):
    tur.append(cyl(f'coil{k}', P(0, y - 0.05, 0.62), P(0, y + 0.05, 0.62), 0.12, 'glow', 16, bevel=0))
    tur.append(cyl(f'coilcap{k}a', P(0, y - 0.07, 0.62), P(0, y - 0.05, 0.62), 0.13, 'hull', 16, bevel=0))
    tur.append(cyl(f'coilcap{k}b', P(0, y + 0.05, 0.62), P(0, y + 0.07, 0.62), 0.13, 'hull', 16, bevel=0))
    tur.append(A.beam(f'coilstrut{k}', P(0, y, 0.55), P(0, y - 0.1, 0.48), 0.05, 0.03, 'mech', bevel=0.004))
tur.append(cyl('muzzle', P(0, 1.02, 0.62), P(0, 1.2, 0.62), 0.13, 'mech', 16, r2=0.1, bevel=0.006))
tur.append(cyl('muzzlering', P(0, 1.18, 0.62), P(0, 1.21, 0.62), 0.11, 'glow', 16, bevel=0))
tur.append(sphere('lens', P(0, 1.22, 0.62), 0.07, 'glow', 14, 8))
for sx in A.SIDES:
    tur.append(A.beam(f'rail{sx}', P(sx * 0.1, 0.0, 0.62), P(sx * 0.1, 1.0, 0.62), 0.025, 0.06, 'hull_dark', bevel=0.004))
# Shoulders: team pauldrons with AA missile banks on top, antenna vanes behind.
for sx in A.SIDES:
    pd = hull_from(f'pauldron{sx}', [P(sx * 0.38, 0.19, 0.13), P(sx * 0.62, 0.19, 0.13), P(sx * 0.38, -0.31, 0.13), P(sx * 0.62, -0.31, 0.13),
                                     P(sx * 0.41, 0.13, 0.29), P(sx * 0.59, 0.13, 0.29), P(sx * 0.41, -0.25, 0.29), P(sx * 0.59, -0.25, 0.29)], 'team', bevel=0.016)
    inset_panels(pd, 0.025, 0.006, lambda f: f.normal.z > 0.5)
    tur.append(pd)
    tur += A.missile_pod(f'aa{sx}', P(sx * 0.53, -0.06, 0.42), (0.22, 0.42, 0.24), 50, 2, 2, 'hull', tip='hull_dark')
    tur.append(A.beam(f'vane{sx}', P(sx * 0.2, -0.36, 0.44), P(sx * 0.32, -0.56, 0.88), 0.035, 0.2, 'team', bevel=0.006, taper=0.5))
    tur.append(sphere(f'vanetip{sx}', P(sx * 0.32, -0.56, 0.9), 0.035, 'glow', 8, 4))
    tur.append(box(f'flankstrip{sx}', P(sx * 0.462, -0.02, 0.22), (0.008, 0.46, 0.02), 'glow', 0, 1))
# Heat-sink fins down the back.
for k in range(5):
    y = -0.3 - k * 0.05
    tur.append(box(f'fin{k}', P(0, y, 0.34), (0.42 - k * 0.04, 0.018, 0.12), 'hull_dark', 0.003, 1))
    if k < 4:
        tur.append(box(f'fing{k}', P(0, y - 0.025, 0.33), (0.36 - k * 0.04, 0.02, 0.08), 'glow_dim', 0, 1))

A.finish('lattice_experimental', [('Body', body, None), ('Turret', tur, tuple(TP))], 1024)

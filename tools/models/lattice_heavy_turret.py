"""Lattice "Bastion" heavy cannon emplacement. Run: python <this> <out_dir>

An octagonal sloped keep with panelled walls, glowing embrasure lights and a team rampart ring,
carrying a traverse ring and a wedge-shaped armoured head (Turret: yaws to aim) with a team
glacis, a long cannon with a team sleeve, a fume collar and a slotted muzzle brake, a sensor eye
and rear vents. Footprint 2 (16 units): kept inside 15 across. Pivot at 3.0 as the procedural model.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('lattice')
body = []
P8 = math.pi / 8
oct_ = lambda r, z: [(math.cos(P8 + k * math.pi / 4) * r, math.sin(P8 + k * math.pi / 4) * r, z) for k in range(8)]
keep = hull_from('keep', oct_(7.3, 0.0) + oct_(5.75, 3.0), 'hull', 0.08)
inset_panels(keep, 0.22, 0.06, lambda f: abs(f.normal.z) < 0.9)
body.append(keep)
body.append(ep.ring('rampart', (0, 0), 5.8, 4.1, 3.0, 3.18, 'team', 8, phase=P8, bevel=0.03))
body.append(ep.ngon('deck', (0, 0), 4.15, 4.15, 2.9, 3.1, 8, 'hull_dark'))
for k in range(8):
    a = k * math.pi / 4
    # Embrasure lights between wall panels and a buttress at each corner.
    body.append(box(f'emb{k}', (0, 0, 0), (1.2, 0.14, 0.2), 'glow', 0, 1))
    body[-1].data.transform(Matrix.Translation((math.cos(a) * 6.45, math.sin(a) * 6.45, 1.55)) @ Matrix.Rotation(a + math.pi / 2, 4, 'Z') @ Matrix.Rotation(-0.48, 4, 'X'))
    b = a + P8
    body.append(ep.beam(f'butt{k}', (math.cos(b) * 7.4, math.sin(b) * 7.4, 0.0), (math.cos(b) * 5.9, math.sin(b) * 5.9, 2.9), 0.5, 0.4, 'hull_dark', 0.03))
body.append(box('gate', (0, -6.3, 0.9), (2.0, 0.8, 1.8), 'hull_dark', 0.05))
body.append(box('gatelamp', (0, -6.72, 1.95), (1.4, 0.05, 0.1), 'glow', 0, 1))

PIV = (0, 0, 3.0)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = []
tur.append(cyl('race', P(0, 0, 0), P(0, 0, 0.4), 3.0, 'mech', 24, bevel=0.03))
head = hull_from('head', [P(-2.6, -2.5, 0.35), P(2.6, -2.5, 0.35), P(-2.9, 1.0, 0.35), P(2.9, 1.0, 0.35), P(-1.4, 2.6, 0.5), P(1.4, 2.6, 0.5),
                          P(-2.2, -2.7, 2.1), P(2.2, -2.7, 2.1), P(-2.3, 0.6, 2.6), P(2.3, 0.6, 2.6), P(-1.0, 2.1, 2.1), P(1.0, 2.1, 2.1)], 'hull', 0.08)
inset_panels(head, 0.16, 0.05, lambda f: abs(f.normal.z) < 0.6 and f.calc_area() > 1.0)
tur.append(head)
glacis = hull_from('glacis', [P(-2.2, -2.62, 2.13), P(2.2, -2.62, 2.13), P(-2.3, 0.58, 2.63), P(2.3, 0.58, 2.63), P(-1.0, 2.05, 2.13), P(1.0, 2.05, 2.13),
                              P(-2.0, -2.45, 2.27), P(2.0, -2.45, 2.27), P(-2.1, 0.5, 2.77), P(2.1, 0.5, 2.77), P(-0.85, 1.9, 2.27), P(0.85, 1.9, 2.27)], 'team', 0.03)
tur.append(glacis)
for sx in (-1, 1):
    tur.append(box(f'skirt{sx}', P(sx * 2.85, -0.6, 1.1), (0.2, 3.2, 1.2), 'team_dark', 0.03))
# Gun.
tur.append(box('mantlet', P(0, 2.45, 1.25), (1.7, 0.8, 1.3), 'hull_dark', 0.08))
tur.append(cyl('sleeve', P(0, 2.7, 1.25), P(0, 4.0, 1.25), 0.52, 'team', 14, r2=0.48, bevel=0.03))
tur.append(cyl('barrel', P(0, 4.0, 1.25), P(0, 6.6, 1.3), 0.34, 'metal', 14, bevel=0.02))
tur.append(cyl('fume', P(0, 4.6, 1.27), P(0, 5.3, 1.28), 0.46, 'hull_dark', 14, bevel=0.02))
for k in range(3):
    tur.append(cyl(f'gring{k}', P(0, 4.15 + k * 0.15, 1.26), P(0, 4.2 + k * 0.15, 1.26), 0.4, 'glow', 14, bevel=0))
tur.append(box('brake', P(0, 6.85, 1.3), (0.95, 0.6, 0.75), 'mech', 0.06))
for sx in (-1, 1):
    tur.append(box(f'bslot{sx}', P(sx * 0.4, 6.85, 1.3), (0.2, 0.35, 0.36), 'tread', 0.01, 1))
# Sensor eye, vents, aerial.
tur.append(box('eyebox', P(1.45, 1.4, 2.6), (0.8, 0.6, 0.4), 'hull_dark', 0.03))
tur.append(box('eye', P(1.45, 1.71, 2.6), (0.6, 0.04, 0.16), 'glow', 0, 1))
for i in range(4):
    tur.append(box(f'vent{i}', P(-0.9 + i * 0.6, -2.62, 1.2), (0.4, 0.1, 0.9), 'mech', 0.01, 1))
tur.append(box('ventglow', P(0, -2.68, 0.6), (2.2, 0.04, 0.12), 'glow_dim', 0, 1))
tur.append(ep.rod('aerial', P(-1.6, -1.6, 2.4), P(-1.7, -2.0, 4.0), 0.04, 'mech', 5))

B = kit.join('Body', body)
T = kit.join('Turret', tur, parent_origin=PIV)
ep.finish('lattice_heavy_turret', [B, T], 1024, bake_scale=0.3, sizes={'turret': 512})

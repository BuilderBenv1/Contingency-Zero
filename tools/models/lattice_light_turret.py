"""Lattice "Sentinel" light laser tower. Run: python <this> <out_dir>

A bevelled hex plinth with a team ring and lit trim, a slim panelled pillar with a glowing
collar, and an angular sensor head (Turret: yaws to aim) with a team-painted brow, a long
ribbed laser emitter with a glowing lens, a sensor eye and heat fins.
Footprint 1 (8 units): kept inside 7 across. Turret pivot at 3.8 as the procedural model.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('lattice')
body = []
body.append(ep.ngon('plinth', (0, 0), 3.0, 2.3, 0.0, 1.3, 6, 'hull', phase=0, bevel=0.06, segs=2))
body.append(ep.ring('plinthteam', (0, 0), 2.32, 1.35, 1.3, 1.42, 'team', 6, bevel=0.02))
body.append(ep.ring('plinthglow', (0, 0), 2.86, 2.62, 0.35, 0.45, 'glow', 6, phase=0))
for k in range(3):
    a = k * 2 * math.pi / 3 + math.pi / 6
    # Fins brace the pillar.
    c, s = math.cos(a), math.sin(a)
    body.append(hull_from(f'fin{k}', [(c * 1.0 - s * 0.1, s * 1.0 + c * 0.1, 1.4), (c * 1.0 + s * 0.1, s * 1.0 - c * 0.1, 1.4),
                                      (c * 2.1 - s * 0.1, s * 2.1 + c * 0.1, 1.4), (c * 2.1 + s * 0.1, s * 2.1 - c * 0.1, 1.4),
                                      (c * 0.95 - s * 0.08, s * 0.95 + c * 0.08, 2.7), (c * 0.95 + s * 0.08, s * 0.95 - c * 0.08, 2.7)], 'hull_dark', 0.02))
pil = hull_from('pillar', [(math.cos(k * math.pi / 3) * 1.25, math.sin(k * math.pi / 3) * 1.25, 1.4) for k in range(6)] +
                [(math.cos(k * math.pi / 3) * 0.95, math.sin(k * math.pi / 3) * 0.95, 3.5) for k in range(6)], 'hull', 0.04)
inset_panels(pil, 0.08, 0.03, lambda f: abs(f.normal.z) < 0.5)
body.append(pil)
body.append(ep.ngon('collar', (0, 0), 1.12, 1.08, 2.55, 2.8, 6, 'glow', phase=0, bevel=0))
body.append(ep.ngon('bearing', (0, 0), 1.05, 0.95, 3.5, 3.8, 6, 'mech', phase=0))
body.append(box('hatch', (0, -1.05, 1.95), (0.6, 0.1, 0.7), 'hull_dark', 0.02, 1))

PIV = (0, 0, 3.8)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = []
head = hull_from('head', [P(-1.05, -1.0, 0.0), P(1.05, -1.0, 0.0), P(-1.15, 0.5, 0.0), P(1.15, 0.5, 0.0), P(-0.55, 1.25, 0.15), P(0.55, 1.25, 0.15),
                          P(-0.8, -1.15, 0.8), P(0.8, -1.15, 0.8), P(-0.85, 0.35, 1.15), P(0.85, 0.35, 1.15), P(-0.35, 1.0, 0.95), P(0.35, 1.0, 0.95)], 'hull', 0.05)
tur.append(head)
brow = hull_from('brow', [P(-0.82, -1.1, 0.82), P(0.82, -1.1, 0.82), P(-0.86, 0.33, 1.17), P(0.86, 0.33, 1.17), P(-0.38, 0.95, 0.97), P(0.38, 0.95, 0.97),
                          P(-0.7, -1.0, 0.92), P(0.7, -1.0, 0.92), P(-0.72, 0.3, 1.26), P(0.72, 0.3, 1.26), P(-0.3, 0.85, 1.06), P(0.3, 0.85, 1.06)], 'team', 0.02)
tur.append(brow)
for sx in (-1, 1):
    tur.append(box(f'cheek{sx}', P(sx * 1.12, -0.1, 0.45), (0.12, 1.3, 0.55), 'team_dark', 0.02))
    for i in range(3):
        tur.append(box(f'hfin{sx}{i}', P(sx * 0.4, -1.2 - i * 0.18, 0.5), (0.5, 0.06, 0.6), 'mech', 0.01, 1))
# Emitter: housing, ribbed barrel, focusing rings and a lens.
tur.append(cyl('emhouse', P(0, 0.9, 0.55), P(0, 1.5, 0.55), 0.32, 'mech', 10, bevel=0.02))
tur.append(cyl('embarrel', P(0, 1.5, 0.55), P(0, 3.2, 0.55), 0.16, 'metal', 10, bevel=0.01))
for i in range(4):
    y = 1.65 + i * 0.36
    tur.append(cyl(f'emring{i}', P(0, y, 0.55), P(0, y + 0.1, 0.55), 0.24, 'glow' if i == 3 else 'hull', 10, bevel=0))
tur.append(cyl('emtip', P(0, 3.15, 0.55), P(0, 3.4, 0.55), 0.22, 'hull_dark', 10, bevel=0.01))
tur.append(cyl('lens', P(0, 3.4, 0.55), P(0, 3.43, 0.55), 0.15, 'glow', 10, bevel=0))
# Sensor eye and aerial.
tur.append(box('eyebox', P(0.65, 0.75, 0.9), (0.4, 0.3, 0.24), 'hull_dark', 0.02))
tur.append(box('eye', P(0.65, 0.91, 0.9), (0.28, 0.03, 0.1), 'glow', 0, 1))
tur.append(ep.rod('aerial', P(-0.55, -0.8, 1.0), P(-0.6, -1.0, 2.0), 0.025, 'mech', 5))

B = kit.join('Body', body)
T = kit.join('Turret', tur, parent_origin=PIV)
ep.finish('lattice_light_turret', [B, T], 512, bake_scale=0.5)

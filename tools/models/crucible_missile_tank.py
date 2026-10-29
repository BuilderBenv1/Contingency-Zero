"""Crucible "Hailstorm" rocket tank. Run: python tools/models/crucible_missile_tank.py <out_dir>

A tracked carrier with a tall, slab-sided armoured hull and a team skirt, carrying a turret with
two big angled launcher boxes either side of a riveted armoured cab, a folding radar dish on a
mast for spotting aircraft, and reload racks on the engine deck. Tracked and boxy where the
Flak Truck is a wheeled truck, and with launchers, not barrels, where the Anvil has a gun.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import vehicles_parts as vp
from bots_parts import missile_pod

kit.reset('crucible')
body = vp.crucible_tracks(0.5, 0.84, -1.06, 1.04)
hull = hull_from('hull', [(-0.5, 1.12, 0.34), (0.5, 1.12, 0.34), (-0.52, -1.08, 0.34), (0.52, -1.08, 0.34),
                          (-0.5, 0.92, 0.66), (0.5, 0.92, 0.66), (-0.52, -1.06, 0.7), (0.52, -1.06, 0.7)], 'hull', 0.03, 2)
inset_panels(hull, 0.03, 0.01, lambda f: abs(f.normal.x) > 0.8 and f.calc_area() > 0.1)
body.append(hull)
body.append(hull_from('glacis', [(-0.46, 1.13, 0.36), (0.46, 1.13, 0.36), (-0.46, 0.93, 0.67), (0.46, 0.93, 0.67),
                                 (-0.46, 1.1, 0.36), (0.46, 1.1, 0.36), (-0.46, 0.9, 0.67), (0.46, 0.9, 0.67)], 'team_dark', 0.01, 1))
body += rivets('grv', (-0.38, 1.06, 0.48), (0.38, 1.06, 0.48), 6, 'metal', 0.018)
for sx in (-1, 1):
    body.append(cyl(f'lamp{sx}', (sx * 0.36, 1.0, 0.6), (sx * 0.36, 1.06, 0.62), 0.04, 'glow', 8, bevel=0))
# Deck: reload racks of spare rockets either side at the back, engine grille, stacks
for sx in (-1, 1):
    body.append(box(f'rack{sx}', (sx * 0.32, -0.8, 0.76), (0.3, 0.42, 0.12), 'hull_dark', 0.012, 1))
    for k in range(3):
        body.append(cyl(f'spare{sx}{k}', (sx * (0.22 + k * 0.1), -0.98, 0.84), (sx * (0.22 + k * 0.1), -0.6, 0.84), 0.035, 'accent', 8, r2=0.02))
    body += vp.stack(f'stack{sx}', (sx * 0.38, -0.4, 0.7), 0.055, 0.32)
body += vp.grille('grille', (0, -0.82, 0.71), 0.24, 0.4, 5)
body.append(cyl('tring', (0, 0.14, 0.68), (0, 0.14, 0.72), 0.42, 'mech', 16, bevel=0.006))

PIV = (0.0, 0.14, 0.7)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = [cyl('ring', P(0, 0, 0), P(0, 0, 0.08), 0.42, 'hull_dark', 12, r2=0.4, bevel=0.012)]
cab = hull_from('cab', [P(-0.2, 0.36, 0.06), P(0.2, 0.36, 0.06), P(-0.24, -0.36, 0.06), P(0.24, -0.36, 0.06),
                        P(-0.18, 0.3, 0.32), P(0.18, 0.3, 0.32), P(-0.22, -0.34, 0.36), P(0.22, -0.34, 0.36)], 'hull', 0.02, 2)
tur.append(cab)
tur.append(box('cabtop', P(0, -0.02, 0.37), (0.4, 0.6, 0.025), 'team', 0.008, 1))
tur.append(box('slit', P(0, 0.33, 0.24), (0.24, 0.02, 0.026), 'glow', 0, 1))
tur += rivets('cabrv', P(-0.16, 0.34, 0.12), P(0.16, 0.34, 0.12), 4, 'metal', 0.015)
for sx in (-1, 1):
    tur.append(box(f'arm{sx}', P(sx * 0.27, 0.0, 0.2), (0.1, 0.3, 0.12), 'mech', 0.01, 1))
    tur += missile_pod(f'pod{sx}', P(sx * 0.48, 0.04, 0.32), (0.32, 0.62, 0.3), 32, 3, 3, 'team', straps=2)
# Radar mast with a small folding dish
tur.append(cyl('mast', P(0.0, -0.26, 0.37), P(0.0, -0.26, 0.72), 0.025, 'mech', 8, bevel=0))
tur.append(hull_from('dish', [P(-0.16, -0.22, 0.66), P(0.16, -0.22, 0.66), P(-0.16, -0.22, 0.82), P(0.16, -0.22, 0.82),
                              P(-0.12, -0.28, 0.69), P(0.12, -0.28, 0.69), P(-0.12, -0.28, 0.79), P(0.12, -0.28, 0.79)], 'metal', 0.006, 1))
tur.append(box('dishg', P(0, -0.21, 0.74), (0.1, 0.01, 0.03), 'glow_dim', 0, 1))

vp.finish('crucible_missile_tank', body, tur, PIV)

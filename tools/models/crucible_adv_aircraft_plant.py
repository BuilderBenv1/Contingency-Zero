"""Crucible Great Hangar (advanced aircraft plant). Run: python tools/models/crucible_adv_aircraft_plant.py <out_dir>

A refit of the Hangar, built from the Hangar itself (crucible_aircraft_plant.py) with the
upgrade bolted on: a tall team-painted launch arch straddling the pad with lights along it and
a nanolathe hanging from its crown, armour belts round the barrel roof, a radar dish on the
ridge, and two extra stacks at the back.
"""
import math, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import kit
import factories_parts as fp
from kit import box, hull_from, cyl, sphere, inset_panels
from factories_parts import studs, beam

_cap, _real = {}, fp.finish
fp.finish = lambda uid, out, body, **k: _cap.update(body=body)
exec(compile(open(os.path.join(HERE, 'crucible_aircraft_plant.py')).read(), 'crucible_aircraft_plant.py', 'exec'),
     {'__file__': os.path.join(HERE, 'crucible_aircraft_plant.py'), '__name__': 'refit_base'})
fp.finish = _real
body = _cap['body']
F = 'crucible'
OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else '.'

# Launch arch over the pad (pad at y -2.6): square-section legs and a crown girder
AYC = -2.6
for sx in (-1, 1):
    body.append(box(f'aleg{sx}', (sx * 8.4, AYC, 6.0), (1.5, 1.5, 11.0), 'hull_dark', 0.05, 1))
    body.append(box(f'afoot{sx}', (sx * 8.4, AYC, 0.9), (2.6, 2.6, 0.8), 'hull', 0.04, 1))
    for k in range(4):
        body.append(box(f'alamp{sx}{k}', (sx * 7.6, AYC, 2.5 + k * 2.4), (0.1, 0.6, 0.6), 'glow', 0, 1))
    body.append(beam(f'astrut{sx}', (sx * 8.4, AYC, 9.0), (sx * 4.5, AYC, 11.6), 0.9, 0.9, 'hull_dark'))
crown = box('crown', (0, AYC, 12.0), (18.6, 1.8, 1.4), 'team', 0.05, 1)
body.append(crown)
body += fp.hazard_strip('chz', -8.4, 8.4, AYC - 0.95, 12.0, 0.06, 0.5, 16)
body += studs('cr', (-8.6, AYC, 12.75), (8.6, AYC, 12.75), 12, 'metal', 0.14)
body.append(box('cpod', (0, AYC, 11.0), (2.4, 2.2, 0.8), 'hull_dark', 0.05, 1))
body += fp.nano_arm('carm', (0, AYC, 10.6), (0, AYC, 6.0), 2.6, F, hang=True)
# Armour belts round the barrel roof
for y in (4.3, 9.1):
    pts = []
    for i in range(11):
        a = math.pi * i / 10
        pts.append((math.cos(a) * 10.95, 0.5 + math.sin(a) * 9.35))
    for i in range(10):
        (x0, z0), (x1, z1) = pts[i], pts[i + 1]
        body.append(beam(f'belt{y}_{i}', (x0, y, z0), (x1, y, z1), 0.8, 0.35, 'accent', 0.02, up=(0, 1, 0)))
# Radar dish on the ridge
body.append(cyl('rmast', (0, 8.0, 9.8), (0, 8.0, 12.2), 0.25, 'mech', 8, bevel=0))
body.append(hull_from('dish', [(-1.8, 8.4, 11.6), (1.8, 8.4, 11.6), (-1.8, 8.4, 13.4), (1.8, 8.4, 13.4),
                               (-1.2, 7.7, 11.9), (1.2, 7.7, 11.9), (-1.2, 7.7, 13.1), (1.2, 7.7, 13.1)], 'metal', 0.04, 1))
body.append(box('dishg', (0, 8.5, 12.5), (0.8, 0.06, 0.3), 'glow_dim', 0, 1))
# Two extra stacks at the back corners
for sx in (-1, 1):
    body += fp.stack(f'bstack{sx}', (sx * 9.6, 10.3, 0.5), 0.75, 9.0)

fp.finish('crucible_adv_aircraft_plant', OUT, body)

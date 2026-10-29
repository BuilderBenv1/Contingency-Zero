"""Crucible Great Foundry (advanced bot lab). Run: python tools/models/crucible_adv_bot_lab.py <out_dir>

A refit of the Foundry, so it is built from the Foundry itself (crucible_bot_lab.py) with the
upgrade bolted on: armour slabs and buttress plates over the hall walls, a tall central chimney
between the furnace stacks, glowing tier chevrons on the ridge ends, and a heavy portal gantry
standing on the hall roofs across the bay whose trolley hangs a big nanolathe over the pad.
"""
import math, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import kit
import factories_parts as fp
from kit import box, hull_from, cyl, sphere, inset_panels
from factories_parts import studs

# Build the Foundry and keep its parts instead of exporting them.
_cap, _real = {}, fp.finish
fp.finish = lambda uid, out, body, **k: _cap.update(body=body)
exec(compile(open(os.path.join(HERE, 'crucible_bot_lab.py')).read(), 'crucible_bot_lab.py', 'exec'),
     {'__file__': os.path.join(HERE, 'crucible_bot_lab.py'), '__name__': 'refit_base'})
fp.finish = _real
body = _cap['body']
F = 'crucible'
OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else '.'

for sx in (-1, 1):
    # Armour slabs over the outer wall between the buttresses
    for k in range(3):
        y = -5.6 + k * 5.6
        slab = box(f'arm{sx}{k}', (sx * 10.95, y, 3.6), (0.5, 4.2, 5.4), 'team_dark', 0.06, 1)
        body.append(slab)
        body += studs(f'armr{sx}{k}', (sx * 11.22, y - 1.7, 6.0), (sx * 11.22, y + 1.7, 6.0), 4, 'metal', 0.13)
        body += studs(f'armr2{sx}{k}', (sx * 11.22, y - 1.7, 1.2), (sx * 11.22, y + 1.7, 1.2), 4, 'metal', 0.13)
    # Glowing tier chevrons on the ridge front
    for k in range(2):
        body.append(hull_from(f'chev{sx}{k}', [(sx * 8.1 - 0.9, -11.25, 8.6 + k * 0.6), (sx * 8.1, -11.25, 9.0 + k * 0.6), (sx * 8.1 + 0.9, -11.25, 8.6 + k * 0.6),
                                                (sx * 8.1 - 0.9, -11.35, 8.6 + k * 0.6), (sx * 8.1, -11.35, 9.0 + k * 0.6), (sx * 8.1 + 0.9, -11.35, 8.6 + k * 0.6),
                                                (sx * 8.1 - 0.9, -11.25, 8.75 + k * 0.6), (sx * 8.1, -11.25, 9.15 + k * 0.6), (sx * 8.1 + 0.9, -11.25, 8.75 + k * 0.6)], 'glow', 0, 1))
    # Gantry legs on the hall ridges
    body.append(box(f'gleg{sx}', (sx * 8.1, -3.0, 11.7), (1.4, 1.4, 3.4), 'hull_dark', 0.05, 1))
    body.append(box(f'gfoot{sx}', (sx * 8.1, -3.0, 10.25), (2.2, 2.2, 0.4), 'hull', 0.04, 1))
    for k in range(2):
        body.append(box(f'gband{sx}{k}', (sx * 8.1, -3.0, 11.0 + k * 1.4), (1.55, 1.55, 0.25), 'accent', 0.02, 1))
# The portal girder and trolley with a hanging nanolathe
g = box('girder', (0, -3.0, 13.8), (19.4, 1.8, 1.2), 'team', 0.05, 1)
body.append(g)
body.append(box('girderb', (0, -3.0, 13.15), (19.0, 1.6, 0.15), 'hull_dark', 0.02, 1))
body += fp.hazard_strip('ghz', -9.0, 9.0, -3.95, 13.8, 0.06, 0.5, 16)
body += studs('gr', (-9.0, -3.0, 14.45), (9.0, -3.0, 14.45), 12, 'metal', 0.14)
body.append(box('trolley', (0, -3.0, 12.6), (2.6, 2.4, 1.0), 'hull_dark', 0.05, 1))
body.append(box('trolleyg', (0, -4.22, 12.6), (1.8, 0.06, 0.3), 'glow', 0, 1))
body += fp.nano_arm('garm', (0, -3.0, 12.1), (0, 0, 1.0), 3.4, F, hang=True)
# Central chimney between the furnace stacks
body += fp.stack('chimney', (0, 9.4, 10.8), 0.95, 7.2)
for k in range(3):
    body.append(cyl(f'chband{k}', (0, 9.4, 12.0 + k * 1.8), (0, 9.4, 12.3 + k * 1.8), 1.02, 'hull_dark', 12, bevel=0))

fp.finish('crucible_adv_bot_lab', OUT, body)

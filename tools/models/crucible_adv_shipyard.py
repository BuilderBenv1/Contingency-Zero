"""Crucible Graving Dock (advanced shipyard). Run: python tools/models/crucible_adv_shipyard.py <out_dir>

A refit of the Drydock, built from the Drydock itself (crucible_shipyard.py) with the upgrade
bolted on: a tall hammerhead crane tower on the right-hand pier with its jib reaching across the
basin, a third girder over the lock gates on armoured pillars, two more hot stacks on the
caisson, armour plating along the pier faces and floodlight masts at the gate ends.
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
exec(compile(open(os.path.join(HERE, 'crucible_shipyard.py')).read(), 'crucible_shipyard.py', 'exec'),
     {'__file__': os.path.join(HERE, 'crucible_shipyard.py'), '__name__': 'refit_base'})
fp.finish = _real
body = _cap['body']
F = 'crucible'
OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else '.'
PI, PO, PT = 6.6, 11.3, 2.6

# Hammerhead crane tower on the right pier
TX, TY = 9.0, 1.2
tw = box('tower', (TX, TY, PT + 7.0), (2.2, 2.2, 14.0), 'hull_dark', 0.05, 1)
body.append(tw)
for k in range(5):
    body.append(box(f'tband{k}', (TX, TY, PT + 1.5 + k * 2.8), (2.35, 2.35, 0.25), 'accent', 0.02, 1))
body.append(box('tcab', (TX, TY, PT + 14.6), (3.0, 3.0, 1.4), 'hull', 0.04, 1))
body.append(box('tcabw', (TX - 1.52, TY, PT + 14.7), (0.06, 2.4, 0.5), 'glow_dim', 0, 1))
jib = box('jib', (TX - 7.0, TY, PT + 15.6), (18.6, 1.3, 1.0), 'team', 0.04, 1)
body.append(jib)
body.append(box('cjib', (TX + 1.6, TY, PT + 15.6), (1.6, 1.3, 1.0), 'hull_dark', 0.04, 1))
body.append(box('cweight', (TX + 1.7, TY, PT + 14.4), (1.2, 1.4, 1.6), 'accent', 0.04, 1))
body += fp.hazard_strip('jhz', TX - 15.8, TX + 0.6, TY - 0.7, PT + 15.6, 0.06, 0.4, 14)
body.append(box('jtrolley', (-2.0, TY, PT + 14.8), (1.6, 1.6, 0.7), 'hull_dark', 0.04, 1))
body += fp.nano_arm('jarm', (-2.0, TY, PT + 14.4), (0, 0, 1.0), 3.0, F, hang=True)
# Third girder over the lock gates on armoured pillars
for sx in (-1, 1):
    body.append(box(f'gpil{sx}', (sx * 9.0, -9.6, (PT + 8.8) / 2), (1.4, 1.4, 8.8 - PT), 'hull', 0.05, 1))
    body.append(box(f'gpilb{sx}', (sx * 9.0, -9.6, 5.6), (1.55, 1.55, 0.3), 'accent', 0.02, 1))
    # Floodlight masts at the gate ends
    body.append(cyl(f'flm{sx}', (sx * 10.6, -10.8, PT), (sx * 10.6, -10.8, PT + 6.5), 0.14, 'mech', 6, bevel=0))
    body.append(box(f'fl{sx}', (sx * 10.6, -11.0, PT + 6.5), (1.0, 0.3, 0.5), 'hull_dark', 0.02, 1))
    body.append(box(f'flg{sx}', (sx * 10.6, -11.17, PT + 6.5), (0.8, 0.04, 0.35), 'glow', 0, 1))
    # Armour plating along the inner pier faces
    for k in range(3):
        body.append(box(f'parm{sx}{k}', (sx * (PI - 0.05), -4.0 + k * 5.0, 1.8), (0.2, 4.0, 1.4), 'team_dark', 0.03, 1))
g = box('girderg', (0, -9.6, 9.4), (22.4, 1.5, 1.2), 'hull_dark', 0.05, 1)
body.append(g)
body.append(box('girdergt', (0, -9.6, 10.06), (22.0, 1.3, 0.12), 'team', 0.02, 1))
body += fp.hazard_strip('ghz', -10.0, 10.0, -10.4, 9.4, 0.06, 0.5, 16)
# Two more stacks on the caisson
for sx in (-1, 1):
    body += fp.stack(f'xstack{sx}', (sx * 1.4, 10.4, 4.6), 0.8, 5.4)

fp.finish('crucible_adv_shipyard', OUT, body)

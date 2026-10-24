"""Lattice Vehicle Plant. Run: python tools/models/lattice_vehicle_plant.py <out_dir>

A low, wide plant: two long bevelled wings with team decks slope down towards an open bay, and
a gabled assembly hall closes the back. A gantry crane rides rails along the wing tops over
the round build pad, its trolley hanging a nanolathe down at the frame, with two more arms on
the wings. Glowing guide rails and chevrons lead vehicles out of the door (-y).
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels
import factories_parts as fp
from factories_parts import slab, beam, ring

OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else '.'
kit.reset('lattice')
F = 'lattice'
body = []

# ---- Floor, pad and the roll-out apron ----
body.append(slab('floor', (0, 0, 0.2), (22.8, 22.8, 0.4), 'hull_dark', 'hull_dark', chamfer=0.3, bevel=0.05))
body += fp.pad_round('pad', (0, 0, 0.4), 4.9, F)
body.append(box('door', (0, -11.0, 0.45), (12.0, 0.35, 0.1), 'glow', 0, 1))
body += fp.chevrons('chev', -9.6, 3.6, 0.42, 5, 'glow_dim', 0.55)
for sx in (-1, 1):
    body.append(box(f'guide{sx}', (sx * 4.4, -7.6, 0.44), (0.18, 6.0, 0.06), 'glow', 0, 1))
    body.append(box(f'guideb{sx}', (sx * 4.4, -7.6, 0.42), (0.5, 6.2, 0.05), 'metal', 0, 1))

# ---- Wings: long bevelled slabs, sloping in towards the bay ----
WI, WO = 6.2, 11.2
for sx in (-1, 1):
    xi, xo = sx * WI, sx * WO
    pts = [(xi, -10.6, 0.4), (xo, -10.6, 0.4), (xi, 11.0, 0.4), (xo, 11.0, 0.4),
           (xi, -9.0, 4.4), (xo, -8.2, 6.0), (xi, -10.6, 2.6), (xo, -10.6, 3.6),
           (xi, 11.0, 4.4), (xo, 11.0, 6.0), (xi + sx * 0.6, 10.6, 4.9), (xo - sx * 0.3, 10.6, 6.2),
           (xi + sx * 0.6, -8.4, 4.9), (xo - sx * 0.3, -7.8, 6.2)]
    w = hull_from(f'wing{sx}', pts, 'hull', bevel=0.1, segs=2)
    fp.two_tone(w, 'hull', 'team', 0.5)
    inset_panels(w, 0.2, 0.05, lambda f: f.calc_area() > 8 and abs(f.normal.z) < 0.5)
    body.append(w)
    # Crane rails along the deck.
    body.append(box(f'rail{sx}', (sx * 8.9, -3.0, 6.1), (0.35, 12.8, 0.25), 'metal', 0.03, 1))
    body.append(box(f'railbed{sx}', (sx * 8.9, -3.0, 5.85), (0.8, 13.2, 0.3), 'hull_dark', 0.03, 1))
    for k in range(2):
        body.append(box(f'stop{sx}{k}', (sx * 8.9, -9.2 + k * 12.4, 6.25), (0.8, 0.4, 0.5), 'accent', 0.03, 1))
    # Long glow seam and a sensor slit on the inner face.
    body.append(box(f'seam{sx}', (xi - sx * 0.02, 0.0, 2.2), (0.1, 16.0, 0.16), 'glow', 0, 1))
    body.append(box(f'seam2{sx}', (xi - sx * 0.02, 0.0, 3.0), (0.1, 12.0, 0.08), 'glow_dim', 0, 1))
    # Bay-side plinth and the wing arm.
    body.append(slab(f'plinth{sx}', (sx * 5.85, 0.2, 0.75), (0.7, 20.6, 0.7), 'hull_dark', 'metal', chamfer=0.2, bevel=0.04))
    body.append(box(f'armmount{sx}', (sx * 6.25, -3.2, 4.35), (1.2, 1.6, 0.4), 'mech', 0.04, 1))
    body += fp.nano_arm(f'arm{sx}', (sx * 6.25, -3.2, 4.55), (0, 0, 1.0), 2.8, F)
    # Outer face: louvres and windows.
    for i in range(6):
        body.append(box(f'louv{sx}{i}', (xo + sx * 0.02, -6.6 + i * 3.2, 3.2), (0.1, 2.0, 0.5), 'glass', 0.02, 1))
    body.append(box(f'outg{sx}', (xo + sx * 0.03, 1.0, 2.6), (0.08, 17.0, 0.08), 'glow_dim', 0, 1))
    # Deck detail: a raised hatch strip and vents between the rail and the inner edge.
    for k in range(4):
        body.append(box(f'deckv{sx}{k}', (sx * 7.3, -5.4 + k * 3.6, 5.05), (0.9, 1.8, 0.12), 'hull_dark', 0.02, 1))
    body.append(box(f'deckh{sx}', (sx * 10.2, 6.0, 6.0), (1.0, 3.0, 0.14), 'hull_dark', 0.02, 1))

# ---- Gantry crane: a bridge riding the rails over the pad, trolley and hanging arm ----
GY = -0.8
for sx in (-1, 1):
    body.append(slab(f'truck{sx}', (sx * 8.9, GY, 6.7), (1.6, 2.6, 1.0), 'hull_dark', 'hull', chamfer=0.2, bevel=0.04))
    for dy in (-0.8, 0.8):
        body.append(cyl(f'tw{sx}{dy}', (sx * 8.9 - 0.85, GY + dy, 6.45), (sx * 8.9 + 0.85, GY + dy, 6.45), 0.3, 'mech', 10, bevel=0))
bridge = slab('bridge', (0, GY, 7.6), (19.4, 1.8, 0.9), 'hull', 'team', chamfer=0.25, bevel=0.05)
body.append(bridge)
body.append(box('bridgeg', (0, GY - 0.92, 7.5), (15.0, 0.06, 0.12), 'glow', 0, 1))
body.append(slab('trolley', (0, GY, 7.0), (2.4, 2.6, 1.2), 'hull_dark', 'team', chamfer=0.3, bevel=0.05))
body.append(box('trolleyg', (0, GY - 1.32, 7.0), (1.6, 0.06, 0.2), 'glow', 0, 1))
body += fp.nano_arm('armc', (0, GY, 6.4), (0, 0.2, 0.6), 2.4, F, hang=True)

# ---- Rear assembly hall: gabled team roof, glazed front, sensor dome ----
HY0, HY1 = 3.6, 11.2
hall_pts = [(x, y, z) for x in (-11.2, 11.2) for y, z in ((HY0, 0.4), (HY1, 0.4), (HY0, 6.6), (HY1, 6.6))]
hall_pts += [(x, y, 9.4) for x in (-10.6, 10.6) for y in (6.6, 8.2)]
hall_pts += [(x, y, 7.4) for x in (-11.2, 11.2) for y in (HY0 + 0.3, HY1 - 0.3)]
hall = hull_from('hall', hall_pts, 'hull', bevel=0.12, segs=2)
fp.two_tone(hall, 'hull', 'team', 0.3)
inset_panels(hall, 0.25, 0.06, lambda f: f.calc_area() > 10)
body.append(hall)
body.append(box('ridge', (0, 7.4, 9.45), (21.0, 1.7, 0.2), 'hull_dark', 0.03, 1))
for k in range(5):
    x = -8.0 + k * 4.0
    body.append(box(f'sky{k}', (x, 5.6, 8.15), (2.2, 1.6, 0.1), 'glass', 0.02, 1))
    body.append(box(f'skyf{k}', (x, 5.6, 8.22), (2.4, 0.12, 0.06), 'hull', 0, 1))
# Glazed front and the inner bay mouth.
body.append(box('mouth', (0, HY0 - 0.04, 3.0), (11.2, 0.1, 5.0), 'mech', 0, 1))
body.append(box('mouthg', (0, HY0 - 0.08, 5.3), (11.2, 0.08, 0.2), 'glow', 0, 1))
for k in range(4):
    body.append(box(f'mouthr{k}', (-4.2 + k * 2.8, HY0 - 0.08, 2.4), (1.8, 0.06, 3.6), 'hull_dark', 0, 1))
for sx in (-1, 1):
    for k in range(2):
        body.append(box(f'glz{sx}{k}', (sx * (7.0 + k * 2.4), HY0 - 0.04, 4.8), (2.0, 0.1, 1.2), 'glass', 0.02, 1))
body.append(sphere('dome', (6.5, 8.6, 9.6), 0.9, 'glass', 12, 6))
body.append(ring('domer', (6.5, 8.6, 9.4), 1.1, 0.85, 0.2, 'glow', 16))
body.append(cyl('mast', (-6.5, 8.0, 9.4), (-6.5, 8.0, 12.4), 0.15, 'metal', 8, r2=0.08, bevel=0))
body.append(sphere('mastorb', (-6.5, 8.0, 12.5), 0.3, 'glow', 10, 6))

fp.finish('lattice_vehicle_plant', OUT, body)

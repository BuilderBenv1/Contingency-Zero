"""Crucible Foundry (bot lab). Run: python tools/models/crucible_bot_lab.py <out_dir>

Two heavy riveted halls with team-painted ridge roofs flank an open bay. A square build pad
with glowing edge channels and a hazard border sits at the centre, worked by two hydraulic
nanolathe arms from the hall walls and a third from the furnace block at the back, whose twin
stacks glow at the mouth. A riveted lintel with hazard stripes and work lamps spans the door.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels
import factories_parts as fp
from factories_parts import slab, beam, studs

OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else '.'
kit.reset('crucible')
F = 'crucible'
body = []

# ---- Floor: thick slab with plate seams, the square pad, door threshold ----
body.append(box('floor', (0, 0, 0.25), (22.8, 22.8, 0.5), 'hull_dark', 0.06, 1))
for i in range(5):
    body.append(box(f'seam{i}', (0, -8.0 + i * 4.0, 0.505), (11.0, 0.12, 0.02), 'mech', 0, 1))
body += fp.pad_square('pad', (0, 0, 0.5), 4.2)
body.append(box('door', (0, -11.05, 0.55), (11.0, 0.35, 0.1), 'glow', 0, 1))
body += fp.hazard_strip('dhz', -5.5, 5.5, -10.55, 0.56, 0.6, 0.06, 12)

# ---- Side halls: riveted blocks with buttresses, pipes and team ridge roofs ----
for sx in (-1, 1):
    xi, xo, xc = sx * 5.6, sx * 10.6, sx * 8.1
    hall = box(f'hall{sx}', (xc, 0, 4.15), (5.0, 22.4, 7.3), 'hull', 0.08, 1)
    inset_panels(hall, 0.3, 0.08, lambda f: abs(f.normal.z) < 0.5 and f.calc_area() > 20)
    body.append(hall)
    # Ridge roof: sloping team plates up to a dark ridge vent with furnace-glow slits.
    roof = hull_from(f'roof{sx}', [(x, y, z) for y in (-11.2, 11.2) for x, z in
                                    ((xi, 7.8), (xo, 7.8), (xi + sx * 0.1, 8.2), (xo - sx * 0.1, 8.2),
                                     (xc - 0.9, 9.9), (xc + 0.9, 9.9))], 'team', bevel=0.06, segs=1)
    fp.two_tone(roof, 'hull_dark', 'team', 0.3)
    body.append(roof)
    body.append(box(f'ridge{sx}', (xc, 0, 10.05), (1.9, 22.0, 0.4), 'hull_dark', 0.05, 1))
    for k in range(5):
        body.append(box(f'vent{sx}{k}', (xc, -8.0 + k * 4.0, 10.27), (1.0, 2.4, 0.08), 'glow_dim', 0, 1))
        body.append(box(f'ventc{sx}{k}', (xc, -8.0 + k * 4.0, 10.36), (1.3, 0.2, 0.14), 'mech', 0, 1))
    # Roof seams: dark straps across the slopes, riveted.
    for k in range(4):
        y = -8.4 + k * 5.6
        body.append(hull_from(f'strap{sx}{k}', [(x, y + dy, z) for dy in (-0.2, 0.2) for x, z in
                                                  ((xi, 8.25), (xo - sx * 0.1, 8.25), (xc - 0.85, 10.0), (xc + 0.85, 10.0),
                                                   (xi, 8.05), (xo - sx * 0.1, 8.05))], 'hull_dark', bevel=0.02))
    body += studs(f'rv{sx}a', (xi + sx * 0.25, -10.6, 7.85), (xi + sx * 0.25, 10.6, 7.85), 9, 'metal', 0.16)
    body += studs(f'rv{sx}b', (xo - sx * 0.25, -10.6, 7.85), (xo - sx * 0.25, 10.6, 7.85), 9, 'metal', 0.16)
    # Buttresses down the outer wall.
    for k in range(4):
        y = -8.4 + k * 5.6
        body.append(hull_from(f'butt{sx}{k}', [(xo, y + dy, z) for dy in (-0.5, 0.5) for z in (0.5, 7.2)] +
                                              [(xo + sx * 0.8, y + dy, z) for dy in (-0.5, 0.5) for z in (0.5, 2.2)], 'hull_dark', bevel=0.04))
    # Front face: team plate, a riveted frame and a hatch.
    body.append(box(f'frontplate{sx}', (xc, -11.25, 2.0), (4.2, 0.15, 2.6), 'team', 0.04, 1))
    body.append(box(f'fronthatch{sx}', (xc, -11.28, 5.2), (2.0, 0.12, 2.6), 'hull_dark', 0.04, 1))
    body.append(box(f'frontlamp{sx}', (xc, -11.34, 6.7), (1.6, 0.1, 0.25), 'glow', 0, 1))
    # Inner wall: a coolant pipe run and a row of furnace vents facing the pad.
    body += fp.pipe_run(f'pipe{sx}', [(xi - sx * 0.35, -10.5, 6.6), (xi - sx * 0.35, 9.0, 6.6), (xi - sx * 0.35, 9.0, 0.6)], 0.22)
    for k in range(3):
        body.append(box(f'ivent{sx}{k}', (xi - sx * 0.04, -6.5 + k * 1.2, 2.2), (0.1, 0.8, 1.6), 'mech', 0, 1))
        body.append(box(f'iventg{sx}{k}', (xi - sx * 0.06, -6.5 + k * 1.2, 2.2), (0.06, 0.5, 1.3), 'glow_dim', 0, 1))
    # Hydraulic nanolathe arm on a riveted bracket.
    body.append(box(f'armbr{sx}', (xi - sx * 0.4, -1.0, 2.9), (0.8, 2.0, 0.6), 'hull_dark', 0.05, 1))
    body += fp.nano_arm(f'arm{sx}', (xi - sx * 0.6, -1.0, 3.2), (0, 0, 1.0), 3.0, F)

# ---- Furnace block at the back: tall, team-topped, with twin glowing stacks ----
fb = box('furnace', (0, 8.6, 5.65), (11.2, 5.0, 10.3), 'hull', 0.08, 1)
inset_panels(fb, 0.3, 0.08, lambda f: abs(f.normal.z) < 0.5 and f.calc_area() > 20)
body.append(fb)
body.append(box('furntop', (0, 8.6, 10.85), (11.0, 4.8, 0.1), 'team', 0.03, 1))
# Furnace mouth facing the pad: dark recess, glowing slot, a heavy riveted lintel.
body.append(box('mouth', (0, 6.05, 3.0), (6.4, 0.1, 4.2), 'mech', 0, 1))
body.append(box('mouthg', (0, 6.0, 1.6), (5.4, 0.1, 0.9), 'glow', 0, 1))
body.append(box('mouthg2', (0, 6.0, 2.6), (5.4, 0.1, 0.25), 'glow_dim', 0, 1))
body.append(box('mlintel', (0, 5.9, 5.4), (7.0, 0.5, 0.6), 'hull_dark', 0.04, 1))
body += studs('mlr', (-3.2, 5.62, 5.4), (3.2, 5.62, 5.4), 7, 'metal', 0.14)
for sx in (-1, 1):
    body += fp.stack(f'stack{sx}', (sx * 2.8, 8.6, 10.8), 1.0, 5.0)
    body.append(box(f'cage{sx}', (sx * 2.8, 8.6, 13.5), (2.6, 2.6, 0.12), 'mech', 0, 1))
    for cx in (-1, 1):
        for cy in (-1, 1):
            body.append(cyl(f'cpost{sx}{cx}{cy}', (sx * 2.8 + cx * 1.25, 8.6 + cy * 1.25, 10.9), (sx * 2.8 + cx * 1.25, 8.6 + cy * 1.25, 13.5), 0.07, 'mech', 5, bevel=0))
# Roof catwalk railing and a tank on the furnace top.
body.append(cyl('tank', (-0.0, 10.4, 11.4), (0.0, 7.0, 11.4), 0.7, 'accent', 12, bevel=0.04))
for y in (7.6, 9.8):
    body.append(box(f'saddle{y}', (0, y, 11.0), (1.6, 0.3, 0.5), 'hull_dark', 0.02, 1))
body += studs('ftr', (-5.2, 6.4, 10.9), (5.2, 6.4, 10.9), 9, 'metal', 0.15)
# Back-wall arm reaching over the pad.
body += fp.nano_arm('armb', (0, 5.6, 6.0), (0, 0, 1.0), 2.6, F)
body.append(box('armbm', (0, 5.8, 5.85), (1.2, 0.8, 0.4), 'hull_dark', 0.04, 1))

# ---- Front lintel over the door: riveted, team-topped, hazard stripes and work lamps ----
body.append(box('lintel', (0, -10.3, 7.7), (11.4, 1.8, 1.6), 'hull_dark', 0.06, 1))
body.append(box('linteltop', (0, -10.3, 8.55), (11.2, 1.6, 0.12), 'team', 0.02, 1))
body += fp.hazard_strip('lhz', -5.6, 5.6, -11.22, 7.4, 0.06, 0.6, 12)
body += studs('lr', (-5.2, -10.3, 8.62), (5.2, -10.3, 8.62), 9, 'metal', 0.15)
for x in (-3.5, 0.0, 3.5):
    body.append(box(f'lamph{x}', (x, -10.3, 6.75), (1.2, 0.8, 0.3), 'mech', 0.03, 1))
    body.append(box(f'lamp{x}', (x, -10.3, 6.58), (1.0, 0.6, 0.06), 'glow', 0, 1))

fp.finish('crucible_bot_lab', OUT, body)

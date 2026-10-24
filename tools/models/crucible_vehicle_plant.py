"""Crucible Tread Works (vehicle plant). Run: python tools/models/crucible_vehicle_plant.py <out_dir>

A classic works: thick riveted side walls with team tops, a sawtooth roof over the back half
whose team slopes face the door (its lit glazing faces north), and one big stack at the corner. The square build
pad sits in the open bay, worked by four hydraulic nanolathe arms on the walls, and a roller
conveyor carries finished vehicles under a hazard-striped lintel out of the door (-y).
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, inset_panels
import factories_parts as fp
from factories_parts import slab, beam, studs

OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else '.'
kit.reset('crucible')
F = 'crucible'
body = []
WH = 7.4                       # wall top

# ---- Floor, pad, conveyor ----
body.append(box('floor', (0, 0, 0.25), (22.8, 22.8, 0.5), 'hull_dark', 0.06, 1))
body += fp.pad_square('pad', (0, 0.6, 0.5), 4.4)
body.append(box('door', (0, -11.05, 0.55), (15.0, 0.35, 0.1), 'glow', 0, 1))
for sx in (-1, 1):
    body.append(box(f'convrail{sx}', (sx * 2.9, -7.9, 0.75), (0.35, 6.2, 0.5), 'hull_dark', 0.03, 1))
    body.append(box(f'convlamp{sx}', (sx * 2.9, -7.9, 1.01), (0.12, 5.6, 0.04), 'glow_dim', 0, 1))
for i in range(13):
    body.append(cyl(f'roller{i}', (-2.7, -5.2 - i * 0.45, 0.72), (2.7, -5.2 - i * 0.45, 0.72), 0.18, 'metal' if i % 2 else 'tread', 8, bevel=0))
body.append(box('convbed', (0, -7.9, 0.55), (5.6, 6.2, 0.1), 'mech', 0, 1))

# ---- Side walls: thick, riveted, team-topped, with pilasters on the bay side ----
for sx in (-1, 1):
    xi, xo, xc = sx * 8.4, sx * 11.4, sx * 9.9
    w = box(f'wall{sx}', (xc, 0, WH / 2 + 0.25), (3.0, 22.8, WH - 0.5), 'hull', 0.08, 1)
    inset_panels(w, 0.3, 0.08, lambda f: abs(f.normal.z) < 0.5 and f.calc_area() > 15)
    body.append(w)
    body.append(box(f'walltop{sx}', (xc, -4.6, WH + 0.06), (2.8, 13.4, 0.12), 'team', 0.02, 1))
    body += studs(f'rv{sx}a', (xc - 1.25, -10.8, WH + 0.02), (xc - 1.25, 1.6, WH + 0.02), 7, 'metal', 0.15)
    body += studs(f'rv{sx}b', (xc + 1.25, -10.8, WH + 0.02), (xc + 1.25, 1.6, WH + 0.02), 7, 'metal', 0.15)
    for k in range(4):
        y = -8.0 + k * 4.6
        body.append(box(f'pil{sx}{k}', (xi - sx * 0.3, y, WH / 2), (0.6, 0.9, WH - 0.6), 'hull_dark', 0.04, 1))
    body.append(box(f'wfront{sx}', (xc, -11.43, 2.2), (2.4, 0.1, 2.8), 'team', 0.03, 1))
    body.append(box(f'wlamp{sx}', (xc, -11.45, 4.6), (1.6, 0.1, 0.3), 'glow', 0, 1))
    # Two hydraulic arms per wall, aimed at the pad.
    for k, y in enumerate((-2.6, 2.8)):
        body.append(box(f'armbr{sx}{k}', (xi - sx * 0.45, y, 3.0 + k * 0.6), (0.9, 1.8, 0.6), 'hull_dark', 0.05, 1))
        body += fp.nano_arm(f'arm{sx}{k}', (xi - sx * 0.7, y, 3.3 + k * 0.6), (0, 0.6, 1.0), 2.7, F)
    # Outside: a pipe run along the wall.
    body += fp.pipe_run(f'pipe{sx}', [(xo - sx * 0.35, -10.8, 1.6), (xo - sx * 0.35, 10.6, 1.6)], 0.2)

# ---- Sawtooth roof over the back half: team slopes towards the door, lit glazing to the north ----
RY0, RY1, TEETH = 1.2, 11.4, 3
body.append(box('roofbase', (0, (RY0 + RY1) / 2, WH + 0.2), (22.8, RY1 - RY0, 0.4), 'hull_dark', 0.05, 1))
span = (RY1 - RY0) / TEETH
for i in range(TEETH):
    y0 = RY0 + i * span; y1 = y0 + span
    # Slope faces the door (team, seen from the RTS camera); the glazing faces north.
    t = prism_x(f'tooth{i}', [(y0, WH + 0.4), (y1, WH + 0.4), (y1, WH + 3.1)], -11.2, 11.2, 'hull_dark', 0.05, 1)
    fp.two_tone(t, 'hull_dark', 'team', 0.3)
    body.append(t)
    for k in range(8):
        x = -9.45 + k * 2.7
        body.append(box(f'pane{i}{k}', (x, y1 + 0.03, WH + 1.75), (2.3, 0.08, 2.1), 'glow_dim', 0, 1))
    body.append(box(f'ridgel{i}', (0, y1 - 0.12, WH + 3.13), (21.6, 0.2, 0.06), 'glow_dim', 0, 1))
    body.append(box(f'gutter{i}', (0, y0 + 0.15, WH + 0.55), (22.4, 0.3, 0.3), 'mech', 0, 1))
    # Riveted ridge and dark seams down the slopes.
    body += studs(f'rr{i}', (-10.5, y1 - 0.25, WH + 3.0), (10.5, y1 - 0.25, WH + 3.0), 8, 'metal', 0.13)
    for k in range(3):
        x = -5.4 + k * 5.4
        body.append(hull_from(f'tseam{i}{k}', [(x + dx, y, z) for dx in (-0.12, 0.12) for y, z in
                                                ((y0 + 0.05, WH + 0.42), (y1 - 0.05, WH + 3.12), (y1 - 0.05, WH + 2.9), (y0 + 0.3, WH + 0.42))], 'hull_dark', bevel=0))
# Back wall under the roof, facing the pad: dark recess with a furnace-glow slot.
body.append(box('backwall', (0, 10.2, WH / 2 + 0.25), (16.8, 2.4, WH - 0.5), 'hull', 0.06, 1))
body.append(box('backmouth', (0, 8.97, 3.0), (10.0, 0.1, 4.6), 'mech', 0, 1))
body.append(box('backglow', (0, 8.93, 1.4), (8.4, 0.08, 0.8), 'glow', 0, 1))
for k in range(4):
    body.append(box(f'shutter{k}', (-3.6 + k * 2.4, 8.92, 3.4), (2.1, 0.06, 2.4), 'hull_dark', 0, 1))

# ---- Big corner stack and a fuel tank on the opposite wall ----
body += fp.stack('stack', (9.9, 8.6, WH + 3.1), 1.15, 6.5)
body.append(box('stackbase', (9.9, 8.6, WH + 2.4), (2.8, 2.8, 1.6), 'hull_dark', 0.05, 1))
body += fp.pipe_run('stpipe', [(9.9, 7.2, WH + 3.6), (9.9, 5.2, WH + 3.6), (9.9, 5.2, WH + 0.4)], 0.28)
body.append(cyl('tank', (-9.9, -1.0, WH + 1.2), (-9.9, -7.6, WH + 1.2), 1.0, 'accent', 14, bevel=0.06))
for y in (-2.0, -6.6):
    body.append(box(f'saddle{y}', (-9.9, y, WH + 0.45), (2.4, 0.4, 0.8), 'hull_dark', 0.03, 1))
for y in (-1.6, -4.3, -7.0):
    body.append(cyl(f'tband{y}', (-9.9, y + 0.12, WH + 1.2), (-9.9, y - 0.12, WH + 1.2), 1.05, 'mech', 14, bevel=0))

# ---- Front lintel: heavy riveted beam with hazard stripes and work lamps ----
body.append(box('lintel', (0, -10.5, WH - 0.8), (17.0, 1.8, 1.8), 'hull_dark', 0.06, 1))
body.append(box('linteltop', (0, -10.5, WH + 0.16), (16.8, 1.6, 0.12), 'team', 0.02, 1))
body += fp.hazard_strip('lhz', -8.4, 8.4, -11.42, WH - 1.0, 0.06, 0.7, 16)
body += studs('lr', (-7.8, -10.5, WH + 0.22), (7.8, -10.5, WH + 0.22), 10, 'metal', 0.15)
for x in (-5.0, 0.0, 5.0):
    body.append(box(f'lamph{x}', (x, -10.4, WH - 1.85), (1.4, 0.9, 0.3), 'mech', 0.03, 1))
    body.append(box(f'lamp{x}', (x, -10.4, WH - 2.03), (1.2, 0.7, 0.06), 'glow', 0, 1))

fp.finish('crucible_vehicle_plant', OUT, body)

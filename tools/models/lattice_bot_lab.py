"""Lattice Bot Lab. Run: python tools/models/lattice_bot_lab.py <out_dir>

Two tall prow-fronted ceramic towers flank an open bay with a round, blue-ringed build pad at
the centre. Four slim nanolathe arms (two per tower, stacked) reach in from the towers' inner
faces, a bridge with a sensor eye spans the bay overhead, and antenna masts crown the towers.
The bay opens south (-y) where finished bots walk out.
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

# ---- Floor: bevelled slab, central round pad, door threshold and chevrons ----
body.append(slab('floor', (0, 0, 0.2), (22.8, 22.8, 0.4), 'hull_dark', 'hull_dark', chamfer=0.3, bevel=0.05))
body += fp.pad_round('pad', (0, 0, 0.4), 4.6, F)
body.append(box('door', (0, -11.0, 0.45), (10.0, 0.35, 0.1), 'glow', 0, 1))
body += fp.chevrons('chev', -9.4, 3.0, 0.42, 4, 'glow_dim', 0.5)
for sx in (-1, 1):
    # Walkway strips leading out of the bay.
    body.append(box(f'walk{sx}', (sx * 3.6, -7.8, 0.43), (0.25, 5.0, 0.05), 'metal', 0, 1))

# ---- Side towers: prow-fronted bevelled slabs, team tops ----
for sx in (-1, 1):
    xi, xo = sx * 5.6, sx * 11.2
    pts = []
    for x, xt in ((xi, sx * 6.1), (xo, sx * 10.7)):
        pts += [(x, -10.6, 0.4), (x, 11.0, 0.4), (x, -10.6, 2.6), (x, 11.0, 8.6),
                (xt, -7.4, 10.0), (xt, 10.4, 10.0), (xt, -9.0, 8.4)]
    t = hull_from(f'tower{sx}', pts, 'hull', bevel=0.12, segs=2)
    fp.two_tone(t, 'hull', 'team', 0.45)
    inset_panels(t, 0.22, 0.06, lambda f: f.calc_area() > 6)
    body.append(t)
    # Glowing seams on the inner face, and a team band across the prow.
    for k, (y0, y1) in enumerate(((-8.6, -2.0), (1.0, 8.0))):
        body.append(box(f'seam{sx}{k}', (xi - sx * 0.02, (y0 + y1) / 2, 5.6), (0.1, y1 - y0, 0.18), 'glow', 0, 1))
    body.append(box(f'eyeslit{sx}', (xi - sx * 0.02, -9.4, 3.4), (0.1, 1.4, 0.35), 'glow', 0, 1))
    # Windows down the outer face.
    for i in range(5):
        body.append(box(f'win{sx}{i}', (xo + sx * 0.02, -6.0 + i * 3.4, 6.2), (0.1, 2.2, 0.7), 'glass', 0.02, 1))
        body.append(box(f'winl{sx}{i}', (xo + sx * 0.03, -6.0 + i * 3.4, 5.75), (0.08, 2.2, 0.08), 'glow_dim', 0, 1))
    # Plinth under the tower's inner foot.
    body.append(slab(f'plinth{sx}', (sx * 5.3, 0.2, 0.75), (0.8, 20.6, 0.7), 'hull_dark', 'metal', chamfer=0.2, bevel=0.04))
    # Two nanolathe arms per tower, stacked, aimed at the pad centre.
    for i, (y, z) in enumerate(((-2.2, 2.0), (1.6, 5.4))):
        arm_base = (sx * 5.75, y, z)
        body.append(box(f'armmount{sx}{i}', (sx * 5.55, y, z - 0.15), (0.6, 1.4, 0.5), 'mech', 0.04, 1))
        body += fp.nano_arm(f'arm{sx}{i}', arm_base, (0, 0, 1.2 + i * 0.6), 3.2, F, reach=0.95)
    # Antenna mast with a glowing sensor orb at the back of each tower.
    body.append(cyl(f'mast{sx}', (sx * 8.6, 8.0, 9.6), (sx * 8.6, 8.0, 13.6), 0.2, 'metal', 8, r2=0.1, bevel=0))
    body.append(cyl(f'mastb{sx}', (sx * 8.6, 8.0, 9.7), (sx * 8.6, 8.0, 10.4), 0.45, 'mech', 8, r2=0.3, bevel=0.02))
    body.append(sphere(f'orb{sx}', (sx * 8.6, 8.0, 13.7), 0.36, 'glow', 10, 6))
    for k in range(3):
        body.append(box(f'fin{sx}{k}', (sx * 8.6, 8.0, 11.0 + k * 0.8), (1.2 - k * 0.3, 0.08, 0.08), 'mech', 0, 1))
    # Tower-top detail: a raised spine with ribs splitting the team deck into plates, a hatch
    # and heat-sink fins.
    body.append(slab(f'spine{sx}', (sx * 8.5, 1.5, 10.08), (0.7, 17.0, 0.2), 'hull', 'metal', chamfer=0.1, bevel=0.03))
    for k in range(6):
        body.append(box(f'rib{sx}{k}', (sx * 8.5, -6.4 + k * 3.2, 10.06), (4.2, 0.22, 0.14), 'hull', 0.03, 1))
    body.append(box(f'hatch{sx}', (sx * 7.4, 5.1, 10.06), (1.2, 1.6, 0.12), 'hull_dark', 0.03, 1))
    for k in range(5):
        body.append(box(f'hs{sx}{k}', (sx * 9.6, 4.3 + k * 0.4, 10.2), (1.0, 0.12, 0.36), 'mech', 0.01, 1))
    body.append(box(f'prowg{sx}', (sx * 8.5, -8.3, 9.25), (3.4, 0.14, 0.12), 'glow', 0, 1))
    # Sensor block on the tower top, ahead of the mast.
    body.append(slab(f'sens{sx}', (sx * 8.5, 0.5, 10.3), (2.2, 3.4, 0.6), 'hull_dark', 'hull', chamfer=0.25, bevel=0.04))
    body.append(box(f'sensg{sx}', (sx * 8.5, -1.22, 10.3), (1.4, 0.08, 0.2), 'glow', 0, 1))

# ---- Back block: tall slab closing the bay, glowing slot at the build height ----
bb = slab('back', (0, 8.8, 4.6), (11.4, 4.4, 8.4), 'hull', 'team', chamfer=0.5, bevel=0.1, segs=2)
inset_panels(bb, 0.25, 0.06, lambda f: f.calc_area() > 8)
body.append(bb)
body.append(box('backslot', (0, 6.58, 4.4), (6.0, 0.1, 0.3), 'glow', 0, 1))
body.append(box('backslot2', (0, 6.58, 2.2), (4.0, 0.1, 0.18), 'glow_dim', 0, 1))
for sx in (-1, 1):
    body.append(box(f'backpanel{sx}', (sx * 3.6, 6.5, 3.3), (1.6, 0.2, 4.8), 'hull_dark', 0.04, 1))
    body.append(box(f'backvent{sx}', (sx * 3.6, 6.38, 3.3), (1.0, 0.06, 3.6), 'mech', 0, 1))
# Rear faces: vertical fins and cooling vents on the back block and towers.
for k in range(6):
    x = -10.0 + k * 4.0
    if abs(x) < 3: continue
    body.append(slab(f'rfin{k}', (x, 11.15, 3.6), (0.4, 0.5, 6.4), 'hull_dark', 'hull', chamfer=0.1, bevel=0.03))
for sx in (-1, 1):
    for k in range(4):
        body.append(box(f'rvent{sx}{k}', (sx * 8.4, 11.04, 2.0 + k * 0.7), (2.4, 0.08, 0.3), 'mech', 0, 1))
# Rear service bay doors.
body.append(box('reardoor', (0, 11.02, 2.6), (5.0, 0.12, 4.4), 'hull_dark', 0.03, 1))
body.append(box('reardoorl', (0, 11.1, 4.9), (5.4, 0.1, 0.18), 'glow_dim', 0, 1))

# ---- Bridge across the bay: team-topped, with a sensor eye looking down at the pad ----
br = slab('bridge', (0, 3.0, 9.35), (11.6, 2.6, 1.1), 'hull_dark', 'team', chamfer=0.3, bevel=0.05)
body.append(br)
body.append(box('bridgeglow', (0, 3.0, 8.78), (9.0, 0.6, 0.06), 'glow', 0, 1))
body.append(hull_from('eyehood', [(-1.0, 1.6, 8.8), (1.0, 1.6, 8.8), (-1.0, 3.0, 8.8), (1.0, 3.0, 8.8),
                                  (-0.7, 1.8, 8.0), (0.7, 1.8, 8.0), (-0.7, 2.8, 8.0), (0.7, 2.8, 8.0)], 'hull', bevel=0.05))
body.append(sphere('eye', (0, 1.85, 8.2), 0.42, 'glow', 12, 6))
body.append(box('eyelid', (0, 1.6, 8.62), (1.6, 0.12, 0.2), 'mech', 0, 1))

fp.finish('lattice_bot_lab', OUT, body)

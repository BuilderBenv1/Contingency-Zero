"""Crucible "Broadside" cruise-missile ship. Run: python tools/models/crucible_missile_ship.py <out_dir>

A long, low, slab-sided hull whose deck is mostly two big armoured launch blocks, fore and aft,
each a grid of square cell hatches with glowing seams and a few hatches open with a missile nose
showing; a narrow island bridge offset to starboard with a radar mast and a hot stack, blast
deflector walls between the blocks and the island. No gun turret at all: it reads as a floating
missile magazine, unlike any gun ship. The waterline is z = 0.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A

kit.reset('crucible')
F = 'crucible'
HW, DECK, KEEL = 0.38, 0.16, -0.12
body, secs = A.ship_hull(F, HW, DECK, KEEL, bow=1.25, stern=-1.2)
for sx in A.SIDES:
    body += rivets(f'hr{sx}', (sx * (HW + 0.004), 0.95, 0.08), (sx * (HW + 0.004), -1.05, 0.08), 11, 'metal', 0.014)


def launcher(name, yc, rows, cols, opened):
    out = []
    w, l = 0.56, rows * 0.12 + 0.06
    blk = box(name, (-0.04, yc, DECK + 0.07), (w, l, 0.14), 'hull', 0.015, 2)
    inset_panels(blk, 0.02, 0.006, lambda f: abs(f.normal.z) < 0.5)
    out.append(blk)
    out.append(box(name + 'rim', (-0.04, yc, DECK + 0.145), (w + 0.02, l + 0.02, 0.012), 'team', 0.004, 1))
    for r in range(rows):
        for c in range(cols):
            x = -0.04 - w / 2 + (c + 0.5) * w / cols
            y = yc - l / 2 + 0.03 + (r + 0.5) * 0.12
            if (r, c) in opened:
                out.append(box(f'{name}h{r}{c}', (x, y, DECK + 0.16), (0.09, 0.09, 0.012), 'tread', 0, 1))
                out.append(cyl(f'{name}m{r}{c}', (x, y, DECK + 0.14), (x, y, DECK + 0.21), 0.03, 'accent', 8, r2=0.004, bevel=0))
                out.append(box(f'{name}lid{r}{c}', (x, y - 0.06, DECK + 0.2), (0.09, 0.01, 0.09), 'hull_dark', 0.003, 1))
            else:
                out.append(box(f'{name}h{r}{c}', (x, y, DECK + 0.157), (0.09, 0.09, 0.01), 'hull_dark', 0.003, 1))
    for r in range(rows + 1):
        y = yc - l / 2 + 0.03 + r * 0.12
        out.append(box(f'{name}s{r}', (-0.04, y, DECK + 0.155), (w - 0.04, 0.008, 0.006), 'glow_dim', 0, 1))
    return out


body += launcher('fwd', 0.62, 4, 4, {(1, 1), (2, 2)})
body += launcher('aft', -0.66, 4, 4, {(0, 3), (2, 0), (3, 2)})
# Island bridge offset to starboard
isl = box('island', (0.28, -0.02, 0.36), (0.14, 0.42, 0.4), 'hull', 0.015, 2)
body.append(isl)
body.append(box('islroof', (0.28, -0.02, 0.565), (0.16, 0.44, 0.02), 'team', 0.006, 1))
body.append(box('iwin', (0.28, 0.195, 0.48), (0.12, 0.01, 0.035), 'glow_dim', 0, 1))
body.append(box('iwin2', (0.205, -0.02, 0.48), (0.01, 0.32, 0.03), 'glow_dim', 0, 1))
body.append(cyl('mast', (0.28, 0.05, 0.57), (0.28, 0.05, 0.86), 0.012, 'metal', 6, bevel=0))
body.append(box('radar', (0.28, 0.05, 0.82), (0.22, 0.03, 0.05), 'metal', 0.004, 1))
body.append(sphere('masttip', (0.28, 0.05, 0.87), 0.018, 'glow', 8, 4))
body.append(cyl('stack', (0.28, -0.16, 0.56), (0.28, -0.16, 0.74), 0.045, 'metal', 10, r2=0.04, bevel=0.004))
body.append(cyl('stglow', (0.28, -0.16, 0.74), (0.28, -0.16, 0.746), 0.035, 'glow', 10, bevel=0))
# Blast deflectors between the blocks and the island
for y in (0.27, -0.31):
    body.append(hull_from(f'defl{y}', [(-0.32, y - 0.02, DECK), (0.18, y - 0.02, DECK), (-0.32, y + 0.02, DECK), (0.18, y + 0.02, DECK),
                                       (-0.3, y, DECK + 0.2), (0.16, y, DECK + 0.2)], 'hull_dark', 0.006, 1))
body.append(box('bowdeck', (0, 1.0, DECK + 0.006), (0.36, 0.16, 0.012), 'team_dark', 0.004, 1))
for sx in A.SIDES:
    body.append(box(f'bitt{sx}', (sx * 0.2, 1.06, DECK + 0.03), (0.04, 0.04, 0.05), 'metal', 0.006, 1))

A.finish('crucible_missile_ship', [('Body', body, None)], 512)

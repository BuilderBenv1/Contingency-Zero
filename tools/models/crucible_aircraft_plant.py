"""Crucible Hangar (aircraft plant). Run: python tools/models/crucible_aircraft_plant.py <out_dir>

A ribbed, riveted quonset hangar with a team-painted barrel roof fills the back; its great
doors face a square launch pad on the apron (frames are built in the air above it), lined
with landing lights. Two heavy nanolathe arms angle upwards from either side of the pad, a
square control tower with a lit cab stands at the front right and a stack at the front left.
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
AY0, AY1 = 2.0, 11.4          # hangar arch from front to back
AW, AH = 10.6, 9.0            # half width and height of the arch
N = 10


def arc(r_w, r_h, z0=0.5):
    return [(math.cos(math.pi * i / N) * r_w, z0 + math.sin(math.pi * i / N) * r_h) for i in range(N + 1)]


# ---- Floor and apron ----
body.append(box('floor', (0, 0, 0.25), (22.8, 22.8, 0.5), 'hull_dark', 0.06, 1))
body += fp.pad_square('pad', (0, -2.6, 0.5), 3.9)
body.append(box('door', (0, -11.05, 0.55), (8.0, 0.35, 0.1), 'glow', 0, 1))
for sx in (-1, 1):
    for i in range(5):
        y = -10.0 + i * 1.9
        body.append(box(f'll{sx}{i}', (sx * 5.6, y, 0.56), (0.45, 0.45, 0.1), 'glow', 0, 1))
        body.append(box(f'llb{sx}{i}', (sx * 5.6, y, 0.52), (0.7, 0.7, 0.06), 'mech', 0, 1))
    body.append(box(f'apronl{sx}', (sx * 4.5, -8.4, 0.52), (0.2, 5.6, 0.04), 'accent', 0, 1))
for k in range(4):
    body.append(box(f'aprond{k}', (0, -10.2 + k * 1.1, 0.52), (1.2, 0.5, 0.04), 'accent', 0, 1))

# ---- The quonset: barrel roof (team on top), ribs, ridge walk, riveted bands ----
pts = [(x, y, z) for y in (AY0, AY1) for x, z in arc(AW, AH)]
shell = hull_from('shell', pts, 'hull', bevel=0.06, segs=1)
fp.two_tone(shell, 'hull', 'team', 0.42)
body.append(shell)
ribs_y = [AY0 + 0.25, AY0 + 2.5, AY0 + 4.7, AY0 + 6.9, AY1 - 0.25]
outer = arc(AW + 0.18, AH + 0.18)
for k, y in enumerate(ribs_y):
    for i in range(N):
        (x0, z0), (x1, z1) = outer[i], outer[i + 1]
        body.append(beam(f'rib{k}_{i}', (x0, y, z0), (x1, y, z1), 0.32, 0.5, 'hull_dark', 0.02, up=(0, 1, 0)))
    if 0 < k < len(ribs_y) - 1:
        top = [(x, y, z) for x, z in arc(AW + 0.4, AH + 0.4)][3:8]
        body += studs(f'rv{k}', (top[0][0], y - 0.3, top[0][2]), (top[-1][0], y - 0.3, top[-1][2]), 5, 'metal', 0.13)
body.append(box('ridge', (0, (AY0 + AY1) / 2, AH + 0.62), (1.6, AY1 - AY0 - 0.2, 0.3), 'hull_dark', 0.03, 1))
for k in range(4):
    body.append(box(f'ridgev{k}', (0, AY0 + 1.4 + k * 2.2, AH + 0.8), (1.1, 1.2, 0.08), 'glow_dim', 0, 1))
# Side skylights low on the barrel.
for sx in (-1, 1):
    for k in range(4):
        y = AY0 + 1.4 + k * 2.2
        a = math.pi * 0.2 if sx > 0 else math.pi * 0.8
        x, z = math.cos(a) * (AW + 0.05), 0.5 + math.sin(a) * (AH + 0.05)
        body.append(box(f'sky{sx}{k}', (x, y, z), (0.55, 1.0, 0.55), 'glow_dim', 0, 1))

# ---- Hangar front: big door panels under a riveted arch frame, glowing lintel ----
front = arc(AW - 0.6, AH - 0.6)
for i in range(N):
    (x0, z0), (x1, z1) = front[i], front[i + 1]
    body.append(beam(f'frame{i}', (x0, AY0 - 0.15, z0), (x1, AY0 - 0.15, z1), 0.9, 0.5, 'hull_dark', 0.03, up=(0, 1, 0)))
for k in range(4):
    x = -6.0 + k * 4.0
    body.append(box(f'hdoor{k}', (x, AY0 - 0.12, 3.4), (3.8, 0.2, 5.8), 'mech' if k % 2 else 'hull_dark', 0.03, 1))
    for j in range(3):
        body.append(box(f'hdrib{k}{j}', (x, AY0 - 0.25, 1.6 + j * 1.8), (3.6, 0.08, 0.14), 'metal', 0, 1))
body.append(box('hdlintel', (0, AY0 - 0.3, 6.6), (15.6, 0.5, 0.7), 'hull_dark', 0.04, 1))
body.append(box('hdglow', (0, AY0 - 0.56, 6.35), (14.0, 0.06, 0.16), 'glow', 0, 1))
body += fp.hazard_strip('dhz', -7.6, 7.6, AY0 - 0.56, 6.85, 0.06, 0.24, 14)
body.append(box('emblem', (0, AY0 - 0.2, 8.0), (3.6, 0.2, 1.0), 'team', 0.03, 1))
# Back wall of the barrel: a vertical team plate and a vent.
body.append(box('backvent', (0, AY1 + 0.02, 3.0), (4.0, 0.1, 3.0), 'mech', 0, 1))

# ---- Control tower (front right): riveted block, lit cab, team roof, antenna ----
TX, TY = 9.0, -8.9
body.append(box('tower', (TX, TY, 4.5), (2.6, 2.6, 8.0), 'hull', 0.05, 1))
for k in range(3):
    body.append(box(f'tband{k}', (TX, TY, 2.0 + k * 2.4), (2.75, 2.75, 0.3), 'hull_dark', 0.02, 1))
body.append(box('cab', (TX, TY, 9.1), (3.4, 3.4, 1.6), 'hull_dark', 0.05, 1))
for sy in (-1, 1):
    body.append(box(f'cabwin{sy}', (TX, TY + sy * 1.72, 9.2), (2.8, 0.06, 0.7), 'glow_dim', 0, 1))
    body.append(box(f'cabwinx{sy}', (TX + sy * 1.72, TY, 9.2), (0.06, 2.8, 0.7), 'glow_dim', 0, 1))
body.append(box('cabroof', (TX, TY, 10.0), (3.8, 3.8, 0.3), 'team', 0.04, 1))
body += studs('cabr', (TX - 1.6, TY - 1.75, 10.15), (TX + 1.6, TY - 1.75, 10.15), 5, 'metal', 0.12)
body.append(cyl('ant', (TX + 1.0, TY + 1.0, 10.1), (TX + 1.0, TY + 1.0, 13.0), 0.08, 'mech', 6, bevel=0))
body.append(sphere('anttip', (TX + 1.0, TY + 1.0, 13.05), 0.18, 'glow', 8, 4))
for k in range(6):   # ladder
    body.append(box(f'rung{k}', (TX - 1.36, TY, 1.3 + k * 1.1), (0.08, 0.9, 0.08), 'metal', 0, 1))

# ---- Stack (front left) with a fuel tank beside it ----
body += fp.stack('stack', (-9.4, -9.0, 0.5), 0.85, 6.5)
body.append(cyl('fuel', (-9.6, -6.2, 1.5), (-9.6, -2.6, 1.5), 1.0, 'accent', 14, bevel=0.05))
for y in (-5.6, -3.2):
    body.append(box(f'fsad{y}', (-9.6, y, 0.75), (2.2, 0.4, 0.6), 'hull_dark', 0.02, 1))
body += fp.pipe_run('fpipe', [(-9.6, -6.2, 1.5), (-9.4, -7.6, 1.5), (-9.4, -8.3, 1.5)], 0.18)

# ---- Heavy arms either side of the pad, angled up at the build height above ----
for sx in (-1, 1):
    body.append(box(f'armbase{sx}', (sx * 6.6, -2.6, 0.9), (1.8, 1.8, 0.8), 'hull_dark', 0.05, 1))
    body += fp.nano_arm(f'arm{sx}', (sx * 6.6, -2.6, 1.3), (0, -2.6, 9.0), 3.0, F)

fp.finish('crucible_aircraft_plant', OUT, body)

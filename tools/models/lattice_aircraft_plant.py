"""Lattice Skyport (aircraft plant). Run: python tools/models/lattice_aircraft_plant.py <out_dir>

A raised octagonal landing deck on slim pylons, ringed in team paint with landing lights and a
glowing launch circle at the centre; a ramp drops to the door side (-y). Two nanolathe arms on
the deck rim angle upwards (aircraft frames are built in the air above the deck). A slim
control tower with a glazed cab carries a spinning radar dish (Rotor_Y), and a bevelled
hangar with a team roof closes the back right.
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
CY = -0.8                    # deck centre (y)
DZ = 3.0                     # deck top


def octo(r, z, rot=math.pi / 8, cy=CY):
    return [(math.cos(rot + i * math.pi / 4) * r, cy + math.sin(rot + i * math.pi / 4) * r, z) for i in range(8)]


# ---- Ground slab ----
body.append(slab('floor', (0, 0, 0.2), (22.8, 22.8, 0.4), 'hull_dark', 'hull_dark', chamfer=0.3, bevel=0.05))
body.append(box('door', (0, -11.0, 0.45), (7.0, 0.35, 0.1), 'glow', 0, 1))

# ---- The deck: a bevelled octagon on a recessed skirt and pylons ----
deck = hull_from('deck', octo(9.9, DZ - 0.7) + octo(9.6, DZ) + octo(10.0, DZ - 0.25), 'hull', bevel=0.08, segs=2)
fp.two_tone(deck, 'hull', 'hull_dark', 0.9)
body.append(deck)
body.append(hull_from('skirt', octo(8.6, 0.4) + octo(8.6, DZ - 0.7) + octo(7.6, 0.4), 'hull_dark', bevel=0.05))
for i in range(8):
    a = math.pi / 8 + i * math.pi / 4 + math.pi / 8
    if abs(math.sin(a) + 1) < 0.2: continue                     # leave the front (ramp) clear
    px, py = math.cos(a) * 9.0, CY + math.sin(a) * 9.0
    body.append(slab(f'pylon{i}', (px, py, (DZ - 0.6) / 2 + 0.2), (0.8, 0.8, DZ - 0.6), 'hull', 'hull', chamfer=0.1, bevel=0.03))
    body.append(box(f'pylong{i}', (px * 1.06, CY + (py - CY) * 1.06, 1.5), (0.3, 0.3, 0.9), 'glow_dim', 0, 1))
# Team ring with ribs, glowing launch circle, guide lines, rim lights.
body.append(ring('teamring', (0, CY, DZ), 8.7, 6.2, 0.08, 'team', 8, rot=math.pi / 8))
body.append(ring('teamring2', (0, CY, DZ), 5.2, 3.2, 0.06, 'team_dark', 8, rot=math.pi / 8))
body.append(cyl('core', (0, CY, DZ), (0, CY, DZ + 0.1), 3.0, 'mech', 24, bevel=0.02))
body.append(ring('launch', (0, CY, DZ + 0.06), 2.6, 2.2, 0.08, 'glow', 24))
body.append(cyl('launchc', (0, CY, DZ + 0.1), (0, CY, DZ + 0.16), 0.9, 'glow_dim', 16, bevel=0))
for i in range(8):
    a = i * math.pi / 4
    c, s = math.cos(a), math.sin(a)
    body.append(beam(f'spoke{i}', (c * 3.3, CY + s * 3.3, DZ + 0.07), (c * 5.1, CY + s * 5.1, DZ + 0.07), 0.25, 0.06, 'hull', 0))
    body.append(beam(f'tring{i}', (c * 6.3, CY + s * 6.3, DZ + 0.1), (c * 8.6, CY + s * 8.6, DZ + 0.1), 0.3, 0.06, 'hull', 0))
    a2 = a + math.pi / 8
    body.append(box(f'rim{i}', (math.cos(a2) * 9.2, CY + math.sin(a2) * 9.2, DZ + 0.05), (0.55, 0.55, 0.12), 'glow', 0, 1))
# Front ramp down to the door.
ramp = hull_from('ramp', [(x, y, z) for x in (-3.4, 3.4) for y, z in ((-8.6, DZ), (-11.3, 0.4), (-8.6, 0.4))], 'hull', bevel=0.05)
fp.two_tone(ramp, 'hull', 'hull_dark', 0.5)
body.append(ramp)
for sx in (-1, 1):
    body.append(beam(f'rampg{sx}', (sx * 3.2, -8.9, DZ + 0.02), (sx * 3.2, -11.2, 0.5), 0.15, 0.08, 'glow', 0))
for k in range(5):
    t = (k + 0.5) / 5
    body.append(beam(f'ramps{k}', (-2.6, -8.6 - 2.7 * t, DZ - (DZ - 0.4) * t + 0.03), (2.6, -8.6 - 2.7 * t, DZ - (DZ - 0.4) * t + 0.03), 0.15, 0.06, 'metal', 0))

# ---- Arms on the deck rim, angled up towards the air above the deck ----
for sx in (-1, 1):
    base = (sx * 7.2, CY + 2.4, DZ + 0.05)
    body.append(slab(f'armplinth{sx}', (base[0], base[1], DZ + 0.15), (1.6, 1.6, 0.3), 'hull_dark', 'hull', chamfer=0.15, bevel=0.03))
    body += fp.nano_arm(f'arm{sx}', (base[0], base[1], DZ + 0.3), (0, CY, DZ + 9.0), 3.0, F)

# ---- Control tower (back left) with glazed cab; the radar dish is the rotor ----
TX, TY = -8.6, 8.6
tower = hull_from('tower', [(TX + dx, TY + dy, z) for z, r in ((0.4, 1.9), (8.6, 1.2)) for dx, dy in
                            ((r, r * 0.42), (r * 0.42, r), (-r * 0.42, r), (-r, r * 0.42), (-r, -r * 0.42), (-r * 0.42, -r), (r * 0.42, -r), (r, -r * 0.42))], 'hull', bevel=0.06)
inset_panels(tower, 0.15, 0.04, lambda f: f.calc_area() > 4)
body.append(tower)
body.append(box('tstripe', (TX, TY - 1.5, 4.0), (0.6, 0.1, 5.0), 'glow_dim', 0, 1))
cab = hull_from('cab', [(TX + dx * r, TY + dy * r, z) for z, r in ((8.4, 1.4), (9.0, 2.2), (10.0, 2.0)) for dx, dy in
                        ((1, 0.42), (0.42, 1), (-0.42, 1), (-1, 0.42), (-1, -0.42), (-0.42, -1), (0.42, -1), (1, -0.42))], 'glass', bevel=0.04)
body.append(cab)
body.append(hull_from('cabroof', [(TX + dx * r, TY + dy * r, z) for z, r in ((10.0, 2.25), (10.5, 1.6)) for dx, dy in
                                  ((1, 0.42), (0.42, 1), (-0.42, 1), (-1, 0.42), (-1, -0.42), (-0.42, -1), (0.42, -1), (1, -0.42))], 'team', bevel=0.04))
body.append(ring('cabglow', (TX, TY, 8.95), 2.25, 2.0, 0.12, 'glow', 8, rot=math.pi / 8 - 0.0))
body.append(cyl('dishmount', (TX, TY, 10.5), (TX, TY, 11.2), 0.35, 'metal', 8, bevel=0.02))

# ---- Hangar block (back right): bevelled, team roof, glowing door facing the deck ----
hg = slab('hangar', (5.4, 8.9, 2.6), (11.4, 4.6, 4.4), 'hull', 'team', chamfer=0.7, bevel=0.08, segs=2)
inset_panels(hg, 0.22, 0.05, lambda f: f.calc_area() > 8)
body.append(hg)
body.append(box('hdoor', (5.4, 6.58, 1.9), (7.4, 0.1, 2.8), 'hull_dark', 0.02, 1))
body.append(box('hdoorg', (5.4, 6.55, 3.45), (7.4, 0.08, 0.18), 'glow', 0, 1))
for k in range(4):
    body.append(box(f'hdoorl{k}', (2.6 + k * 1.85, 6.54, 1.9), (0.08, 0.06, 2.6), 'mech', 0, 1))
for k in range(3):
    body.append(box(f'hvent{k}', (2.0 + k * 3.4, 8.9, 4.86), (2.2, 1.6, 0.14), 'hull_dark', 0.02, 1))
body.append(cyl('hmast', (10.2, 10.3, 4.8), (10.2, 10.3, 7.6), 0.12, 'metal', 6, r2=0.06, bevel=0))
body.append(sphere('hmorb', (10.2, 10.3, 7.7), 0.25, 'glow', 8, 5))

# ---- Rotor: a lattice radar dish spinning flat on the tower ----
RP = (TX, TY, 11.2)
rot = []
rot.append(cyl('rhub', (TX, TY, 11.2), (TX, TY, 11.6), 0.45, 'mech', 10, bevel=0.02))
rot.append(beam('rarm', (TX, TY, 11.6), (TX, TY + 0.6, 12.2), 0.3, 0.3, 'metal', 0.02))
dish = cyl('dish', (TX, TY + 0.5, 12.3), (TX, TY + 1.0, 12.6), 0.4, 'hull', 14, r2=1.7, bevel=0.03, cap_fill=True)
rot.append(dish)
rot.append(cyl('dishface', (TX, TY + 1.02, 12.62), (TX, TY + 1.06, 12.64), 1.5, 'team', 14, bevel=0))
rot.append(cyl('feed', (TX, TY + 1.0, 12.6), (TX, TY + 2.1, 13.2), 0.06, 'mech', 6, bevel=0))
rot.append(sphere('feedtip', (TX, TY + 2.15, 13.23), 0.18, 'glow', 8, 5))

fp.finish('lattice_aircraft_plant', OUT, body, rot, 'Rotor_Y', RP)

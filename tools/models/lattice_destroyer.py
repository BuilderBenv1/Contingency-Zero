"""Lattice "Reef" destroyer. Run: python tools/models/lattice_destroyer.py <out_dir>

A long, slim warship: a needle bow, a two-tier bevelled superstructure with team roofs and blue
light strips, a bridge screen, a lattice mast carrying a forward radar dish, a vertical launch
block amidships, a team-painted helipad on the stern, and a big faceted gun turret forward with
a long barrel (the turret part). The waterline is z = 0: the hull dips below it.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A

kit.reset('lattice')
F = 'lattice'
HW, DECK, KEEL = 0.32, 0.18, -0.12
body, secs = A.ship_hull(F, HW, DECK, KEEL)
for sx in A.SIDES:
    body.append(box(f'wl{sx}', (sx * (HW + 0.004), -0.2, 0.06), (0.01, 1.2, 0.012), 'glow_dim', 0, 1))
body.append(box('boweye', (0, 1.13, DECK + 0.03), (0.03, 0.04, 0.015), 'glow', 0, 1))
body.append(box('foredeck', (0, 0.88, DECK + 0.005), (0.2, 0.3, 0.01), 'team_dark', 0.003, 1))


def slab(name, c, size, top='team', cap=0.035):
    """Lattice armour slab: straight sides with a bevelled cap and a team roof plate."""
    x, y, z = c; sx_, sy_, sz_ = size
    z0, z1 = z - sz_ / 2, z + sz_ / 2
    pts = [(x + a * sx_ / 2, y + b * sy_ / 2, z0) for a in (-1, 1) for b in (-1, 1)]
    pts += [(x + a * sx_ / 2, y + b * sy_ / 2, z1 - cap) for a in (-1, 1) for b in (-1, 1)]
    pts += [(x + a * (sx_ / 2 - cap * 1.2), y + b * (sy_ / 2 - cap * 1.2), z1) for a in (-1, 1) for b in (-1, 1)]
    o = [hull_from(name, pts, 'hull', bevel=0.008)]
    plate = box(name + 'roof', (x, y, z1 + 0.004), (sx_ - cap * 3, sy_ - cap * 3, 0.01), top, 0.003, 1)
    o.append(plate)
    return o

# ---- Superstructure ----
body += slab('tier1', (0, -0.2, 0.28), (0.38, 0.74, 0.2))
body += slab('tier2', (0, -0.06, 0.43), (0.28, 0.36, 0.12))
body.append(hull_from('bridgeglass', [(0.13, 0.115, 0.42), (-0.13, 0.115, 0.42), (0.11, 0.105, 0.47), (-0.11, 0.105, 0.47),
                                      (0.13, 0.13, 0.42), (-0.13, 0.13, 0.42), (0.11, 0.12, 0.47), (-0.11, 0.12, 0.47)], 'glass', bevel=0.002))
for sx in A.SIDES:
    body.append(box(f'strip{sx}', (sx * 0.192, -0.2, 0.3), (0.006, 0.6, 0.014), 'glow', 0, 1))
    body.append(box(f'wing{sx}', (sx * 0.17, 0.05, 0.4), (0.08, 0.08, 0.02), 'hull', 0.004, 1))
    body.append(cyl(f'boat{sx}', (sx * 0.22, -0.3, 0.42), (sx * 0.22, -0.5, 0.42), 0.035, 'hull_dark', 10, bevel=0.004))
# ---- Mast and radar dish ----
body.append(cyl('mast', (0, -0.14, 0.49), (0, -0.18, 0.88), 0.016, 'metal', 8, r2=0.01, bevel=0))
for sx in A.SIDES:
    body.append(cyl(f'mleg{sx}', (sx * 0.1, -0.2, 0.49), (0, -0.16, 0.72), 0.009, 'metal', 6, bevel=0))
body.append(box('yard', (0, -0.17, 0.78), (0.24, 0.02, 0.02), 'metal', 0.003, 1))
body.append(cyl('dishback', (0, -0.15, 0.66), (0, -0.12, 0.66), 0.03, 'metal', 10, r2=0.09, bevel=0))
body.append(cyl('dish', (0, -0.12, 0.66), (0, -0.105, 0.66), 0.09, 'team', 14, bevel=0))
body.append(cyl('feed', (0, -0.105, 0.66), (0, -0.05, 0.66), 0.006, 'mech', 4, bevel=0))
body.append(sphere('feedglow', (0, -0.05, 0.66), 0.014, 'glow', 6, 4))
body.append(sphere('masttip', (0, -0.18, 0.89), 0.02, 'glow', 8, 4))
# ---- VLS block forward of the superstructure: a grid of cell hatches ----
body.append(box('vls', (0, 0.3, DECK + 0.025), (0.22, 0.2, 0.05), 'hull_dark', 0.006, 1))
for i in range(3):
    for j in range(2):
        body.append(box(f'cell{i}{j}', (-0.07 + i * 0.07, 0.25 + j * 0.1, DECK + 0.052), (0.055, 0.07, 0.008), 'mech', 0.002, 1))
        body.append(box(f'celll{i}{j}', (-0.07 + i * 0.07, 0.25 + j * 0.1, DECK + 0.057), (0.012, 0.012, 0.004), 'glow', 0, 1))
# ---- Stern helipad ----
pad = []
for k in range(6):
    a = k * math.pi / 3
pad_pts = [(math.cos(k * math.pi / 3 + math.pi / 6) * 0.2, -0.8 + math.sin(k * math.pi / 3 + math.pi / 6) * 0.2, z)
           for k in range(6) for z in (DECK, DECK + 0.02)]
body.append(hull_from('pad', pad_pts, 'team', bevel=0.004, segs=1))
body.append(A.ring('padring', (0, -0.8, DECK + 0.021), 0.12, 0.1, 0.004, 'hull', verts=20, bevel=0))
body.append(box('padH1', (0, -0.8, DECK + 0.022), (0.02, 0.1, 0.004), 'hull', 0, 1))
for k in range(6):
    a = k * math.pi / 3 + math.pi / 6
    body.append(sphere(f'padlt{k}', (math.cos(a) * 0.19, -0.8 + math.sin(a) * 0.19, DECK + 0.025), 0.01, 'glow', 6, 4))
body.append(box('hangar', (0, -0.6, DECK + 0.08), (0.24, 0.06, 0.16), 'hull_dark', 0.008, 1))

# ---- Main gun turret ----
PIV = (0, 0.58, DECK)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = [cyl('race', P(0, 0, -0.005), P(0, 0, 0.025), 0.16, 'mech', 16, bevel=0.004)]
shell = hull_from('tshell', [P(-0.15, -0.16, 0.02), P(0.15, -0.16, 0.02), P(-0.16, 0.08, 0.02), P(0.16, 0.08, 0.02),
                             P(-0.07, 0.2, 0.03), P(0.07, 0.2, 0.03), P(-0.11, -0.15, 0.14), P(0.11, -0.15, 0.14),
                             P(-0.09, 0.06, 0.15), P(0.09, 0.06, 0.15), P(-0.04, 0.14, 0.11), P(0.04, 0.14, 0.11)], 'team', bevel=0.012)
inset_panels(shell, 0.02, 0.006, lambda f: f.calc_area() > 0.01)
tur.append(shell)
tur.append(box('teye', P(-0.08, 0.04, 0.16), (0.04, 0.012, 0.015), 'glow', 0, 1))
tur.append(box('mantlet', P(0, 0.17, 0.07), (0.08, 0.06, 0.07), 'hull_dark', 0.01, 1))
tur += A.barrel('gun', P(0, 0.18, 0.07), P(0, 0.7, 0.08), 0.03)
tur.append(cyl('sleeve', P(0, 0.18, 0.07), P(0, 0.32, 0.072), 0.04, 'hull', 10, bevel=0.003))
for k in range(2):
    y = 0.4 + k * 0.1
    tur.append(cyl(f'coil{k}', P(0, y, 0.074), P(0, y + 0.02, 0.075), 0.036, 'glow_dim', 10, bevel=0))

A.finish('lattice_destroyer', [('Body', body, None), ('Turret', tur, PIV)], 512)

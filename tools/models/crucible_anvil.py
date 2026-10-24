"""Crucible "Anvil" main battle tank. Run: python tools/models/crucible_anvil.py <out_dir>

Heavy, industrial and riveted: a sloped glacis, team-painted side skirts over real tracks and
road wheels, an engine deck with twin smokestacks glowing at the tips, and a chamfered cast
turret with a long gun, muzzle brake, cupola and smoke dischargers.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels

OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else '.'
kit.reset()

body = []
# ---- Tracks: a dark band round the wheels, chunky cleats, road wheels, sprocket and idler ----
TR_IN, TR_OUT = 0.47, 0.80          # track spans x from TR_IN to TR_OUT on each side
Y0, Y1 = -1.02, 1.04                 # rear and front of the track run
R_END = 0.2                          # radius at the sprocket and idler
for sx in (-1, 1):
    xa, xb = sx * TR_IN, sx * TR_OUT
    x0, x1 = min(xa, xb), max(xa, xb)
    xc = (x0 + x1) / 2
    # The band: a stadium outline (two semicircles joined), extruded across the track width.
    pts = []
    for i in range(10):
        a = -math.pi / 2 + math.pi * i / 9
        pts.append((Y1 - R_END + math.cos(a) * R_END, 0.21 + math.sin(a) * R_END))
    for i in range(10):
        a = math.pi / 2 + math.pi * i / 9
        pts.append((Y0 + R_END + math.cos(a) * R_END, 0.21 + math.sin(a) * R_END))
    body.append(prism_x(f'band{sx}', pts, x0, x1, 'tread', bevel=0.01, segs=1))
    # Cleats along the bottom and top runs.
    n = 22
    for i in range(n):
        y = Y0 + R_END + (Y1 - Y0 - 2 * R_END) * i / (n - 1)
        body.append(box(f'cleat_b{sx}_{i}', (xc, y, 0.005), (x1 - x0 + 0.02, 0.045, 0.03), 'mech', 0.006, 1))
        body.append(box(f'cleat_t{sx}_{i}', (xc, y, 0.415), (x1 - x0 + 0.02, 0.045, 0.03), 'mech', 0.006, 1))
    # Road wheels, just inside the outer face so their rims show under the skirt.
    for i in range(6):
        y = -0.72 + i * 0.29
        body.append(cyl(f'wheel{sx}_{i}', (x0 - 0.01, y, 0.15), (x1 + 0.01, y, 0.15), 0.13, 'metal', 14, bevel=0.012))
        hx = x1 + 0.02 if sx > 0 else x0 - 0.02
        body.append(cyl(f'hub{sx}_{i}', (hx - sx * 0.03, y, 0.15), (hx, y, 0.15), 0.055, 'mech', 8, bevel=0))
    for y, r in ((Y1 - R_END, 0.17), (Y0 + R_END, 0.15)):
        body.append(cyl(f'end{sx}_{y}', (x0 - 0.015, y, 0.21), (x1 + 0.015, y, 0.21), r, 'mech', 12, bevel=0.01))
        # Sprocket teeth on the front drive wheel.
        if y > 0:
            for k in range(8):
                a = k * math.pi / 4
                body.append(box(f'tooth{sx}_{k}', (xc, y + math.cos(a) * 0.17, 0.21 + math.sin(a) * 0.17),
                                (x1 - x0 + 0.04, 0.04, 0.04), 'metal', 0.004, 1))

# ---- Hull: lower tub, sloped glacis, flat deck, raised engine deck at the back ----
HW = 0.47
body.append(box('tub', (0, 0.0, 0.28), (2 * HW, 2.0, 0.26), 'hull_dark', 0.02))
upper = prism_x('upper', [(1.12, 0.32), (0.72, 0.56), (-0.62, 0.56), (-0.98, 0.52), (-1.04, 0.36), (-1.04, 0.3), (1.12, 0.3)],
                -0.6, 0.6, 'team', bevel=0.025, segs=2)
inset_panels(upper, 0.03, 0.01, lambda f: f.normal.z > 0.3 and f.calc_area() > 0.05)
body.append(upper)
# Engine deck: a raised grille block with slats, and two smokestacks glowing at the top.
body.append(box('deck', (0, -0.78, 0.6), (0.82, 0.42, 0.08), 'hull_dark', 0.015))
for i in range(7):
    body.append(box(f'slat{i}', (0, -0.95 + i * 0.055, 0.645), (0.7, 0.025, 0.02), 'mech', 0.004, 1))
for sx in (-1, 1):
    body.append(cyl(f'stack{sx}', (sx * 0.3, -0.92, 0.55), (sx * 0.3, -0.92, 0.93), 0.065, 'metal', 10, r2=0.055))
    body.append(cyl(f'stackcap{sx}', (sx * 0.3, -0.92, 0.93), (sx * 0.3, -0.92, 0.96), 0.06, 'glow', 10, bevel=0))
    body.append(cyl(f'stackband{sx}', (sx * 0.3, -0.92, 0.72), (sx * 0.3, -0.92, 0.76), 0.075, 'accent', 10, bevel=0))
# Side skirts in team paint over the upper track run, with bolts along the edge.
for sx in (-1, 1):
    x = sx * (TR_OUT + 0.025)
    sk = prism_x(f'skirt{sx}', [(1.08, 0.46), (1.0, 0.28), (-0.98, 0.28), (-1.04, 0.46)], x - 0.022, x + 0.022, 'team', 0.01, 1)
    body.append(sk)
    # Toolboxes and a stowage bin on the hull sides, between the skirt and the deck.
    for k, (y, ln) in enumerate(((0.45, 0.32), (-0.05, 0.4), (-0.55, 0.26))):
        body.append(box(f'tool{sx}{k}', (sx * 0.53, y, 0.6), (0.12, ln, 0.1), 'accent' if k == 1 else 'hull_dark', 0.012, 1))
        body.append(box(f'latch{sx}{k}', (sx * 0.595, y, 0.6), (0.01, 0.04, 0.03), 'metal', 0, 1))
    body += rivets(f'skr{sx}_', (x + sx * 0.025, 0.95, 0.43), (x + sx * 0.025, -0.92, 0.43), 9, 'metal', 0.018)
# Front: headlights, tow hooks and a bolted strip across the glacis.
for sx in (-1, 1):
    body.append(cyl(f'lamp{sx}', (sx * 0.34, 1.0, 0.44), (sx * 0.34, 1.06, 0.44), 0.04, 'glow', 8, bevel=0))
    body.append(cyl(f'lamphood{sx}', (sx * 0.34, 0.97, 0.44), (sx * 0.34, 1.02, 0.44), 0.055, 'mech', 8, bevel=0))
    body.append(box(f'hook{sx}', (sx * 0.2, 1.13, 0.31), (0.05, 0.06, 0.07), 'metal', 0.01, 1))
body += rivets('glacis', (-0.42, 0.8, 0.535), (0.42, 0.8, 0.535), 7, 'metal', 0.02)
# Spare track links bolted across the glacis as extra armour, and a team-painted glacis plate.
for i in range(5):
    body.append(box(f'spare{i}', (-0.3 + i * 0.15, 0.93, 0.47), (0.13, 0.1, 0.035), 'tread', 0.008, 1))
body.append(prism_x('glacisplate', [(1.0, 0.39), (0.76, 0.55), (0.72, 0.565), (0.98, 0.405)], -0.4, 0.4, 'team', 0.006, 1))
# Rear: a jerrycan rack and a towing eye.
body.append(box('rack', (0.25, -1.08, 0.42), (0.3, 0.06, 0.14), 'accent', 0.01, 1))
body.append(box('eye', (-0.2, -1.08, 0.36), (0.08, 0.05, 0.06), 'metal', 0.01, 1))

# ---- Turret (pivot at the hull top, slightly behind centre) ----
PIV = (0.0, -0.08, 0.56)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = []
shell = hull_from('turret', [P(-0.42, -0.36, 0.0), P(0.42, -0.36, 0.0), P(0.44, 0.34, 0.0), P(-0.44, 0.34, 0.0),
                             P(0.3, 0.52, 0.06), P(-0.3, 0.52, 0.06),
                             P(-0.34, -0.42, 0.26), P(0.34, -0.42, 0.26), P(0.34, 0.26, 0.32), P(-0.34, 0.26, 0.32),
                             P(0.2, 0.44, 0.24), P(-0.2, 0.44, 0.24)], 'team', bevel=0.03, segs=2)
inset_panels(shell, 0.035, 0.012, lambda f: f.calc_area() > 0.04)
tur.append(shell)
tur.append(box('roof', P(0, -0.06, 0.33), (0.5, 0.5, 0.03), 'hull', 0.01, 1))
tur.append(box('bustle', P(0, -0.5, 0.15), (0.6, 0.18, 0.2), 'hull_dark', 0.02))
# Stowage basket on the bustle: a frame of rails with a tarp roll and crates in it.
for sx in (-1, 1):
    tur.append(cyl(f'rail{sx}', P(sx * 0.32, -0.42, 0.27), P(sx * 0.32, -0.7, 0.27), 0.012, 'mech', 6, bevel=0))
    tur.append(cyl(f'post{sx}', P(sx * 0.32, -0.7, 0.05), P(sx * 0.32, -0.7, 0.27), 0.012, 'mech', 6, bevel=0))
tur.append(cyl('rail_b', P(-0.32, -0.7, 0.27), P(0.32, -0.7, 0.27), 0.012, 'mech', 6, bevel=0))
tur.append(cyl('tarp', P(-0.26, -0.6, 0.31), P(0.05, -0.6, 0.31), 0.06, 'accent', 10))
tur.append(box('crate', P(0.18, -0.6, 0.3), (0.14, 0.14, 0.1), 'hull_dark', 0.01, 1))
# Side armour plates hung on the turret cheeks, in team paint.
for sx in (-1, 1):
    tur.append(box(f'cheek{sx}', P(sx * 0.46, 0.0, 0.14), (0.04, 0.5, 0.2), 'team_dark', 0.012, 1))
# Gun: mantlet, thick sleeve, fume extractor, long barrel and a slotted muzzle brake.
tur.append(box('mantlet', P(0, 0.56, 0.15), (0.3, 0.12, 0.2), 'hull_dark', 0.025))
tur.append(cyl('sleeve', P(0, 0.6, 0.15), P(0, 0.85, 0.15), 0.085, 'metal', 14))
tur.append(cyl('barrel', P(0, 0.85, 0.15), P(0, 1.42, 0.15), 0.058, 'metal', 14))
tur.append(cyl('fume', P(0, 1.0, 0.15), P(0, 1.14, 0.15), 0.08, 'hull_dark', 14))
tur.append(box('brake', P(0, 1.48, 0.15), (0.17, 0.13, 0.11), 'mech', 0.02))
for sx in (-1, 1):
    tur.append(box(f'slot{sx}', P(sx * 0.07, 1.48, 0.15), (0.045, 0.07, 0.05), 'tread', 0.005, 1))
# Commander's cupola with a hatch and vision blocks; a sight box; smoke dischargers; antenna.
tur.append(cyl('cupola', P(-0.17, -0.12, 0.33), P(-0.17, -0.12, 0.42), 0.12, 'hull', 12))
tur.append(cyl('hatch', P(-0.17, -0.12, 0.42), P(-0.17, -0.12, 0.45), 0.1, 'hull_dark', 12))
for k in range(5):
    a = k * 2 * math.pi / 5
    tur.append(box(f'vision{k}', P(-0.17 + math.cos(a) * 0.12, -0.12 + math.sin(a) * 0.12, 0.38), (0.035, 0.035, 0.03), 'glass', 0.004, 1))
tur.append(box('sight', P(0.2, 0.2, 0.36), (0.12, 0.16, 0.08), 'hull_dark', 0.012))
tur.append(box('sightglass', P(0.2, 0.285, 0.36), (0.08, 0.01, 0.04), 'glow', 0, 1))
for sx in (-1, 1):
    for k in range(3):
        a = P(sx * (0.36 + k * 0.03), 0.3 - k * 0.07, 0.2)
        b = P(sx * (0.44 + k * 0.03), 0.38 - k * 0.07, 0.27)
        tur.append(cyl(f'smoke{sx}{k}', a, b, 0.026, 'mech', 8, bevel=0))
tur.append(cyl('antenna', P(0.26, -0.42, 0.26), P(0.3, -0.5, 0.95), 0.008, 'mech', 5, bevel=0))
tur.append(cyl('antbase', P(0.26, -0.42, 0.22), P(0.26, -0.42, 0.3), 0.025, 'metal', 8, bevel=0))
tur += rivets('trv', P(-0.3, 0.42, 0.25), P(0.3, 0.42, 0.25), 6, 'metal', 0.018)

B = kit.join('Body', body)
T = kit.join('Turret', tur, parent_origin=PIV)
for o in (B, T):
    kit.paint_vertex_colours(o)
    kit.unwrap(o)
os.makedirs(OUT, exist_ok=True)
# Bake each part with the other present, so the turret shades the deck and vice versa.
kit.bake_detail(B, os.path.join(OUT, 'crucible_anvil_body.png'), 512, others=(T,))
kit.bake_detail(T, os.path.join(OUT, 'crucible_anvil_turret.png'), 512, others=(B,))
kit.export(os.path.join(OUT, 'crucible_anvil.glb'), [B, T])
print('tris', sum(len(p.vertices) - 2 for o in (B, T) for p in o.data.polygons))

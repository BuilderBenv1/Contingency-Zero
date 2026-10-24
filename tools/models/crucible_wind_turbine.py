"""Crucible Wind Turbine: an iron farm-windmill on a riveted girder pylon. Run: python <this> <out_dir>

A heavy base block with bollards, a tapering lattice pylon with a ladder and a team-plated
platform, a boxy riveted gearbox with a stack, and an eight-bladed wheel inside an iron rim
(Rotor_Z: faces forward, spins about the game's Z axis). Blades alternate team paint.
Footprint 1 (8 units): base within 7 across; the wheel turns above it.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('crucible')
body = []
HUB = (0, 1.5, 11.0)
# ---- Base: a footing block with team plate, rivets and bollards ----
body.append(box('base', (0, 0, 0.45), (5.6, 5.6, 0.9), 'hull_dark', 0.08))
body.append(box('baseplate', (0, 0, 0.95), (4.4, 4.4, 0.12), 'team', 0.03))
for sx in (-1, 1):
    body += ep.rivet_line(f'brv{sx}', (-2.0, sx * 2.55, 0.92), (2.0, sx * 2.55, 0.92), 6, 'metal', 0.06)
    body += ep.rivet_line(f'brh{sx}', (sx * 2.55, -2.0, 0.92), (sx * 2.55, 2.0, 0.92), 6, 'metal', 0.06)
    for sy in (-1, 1):
        body.append(cyl(f'bollard{sx}{sy}', (sx * 2.45, sy * 2.45, 0.9), (sx * 2.45, sy * 2.45, 1.35), 0.22, 'accent', 10, bevel=0.02))
        body.append(box(f'shoe{sx}{sy}', (sx * 1.55, sy * 1.55, 1.15), (0.7, 0.7, 0.4), 'hull', 0.04))
# Switch house on the base.
body.append(box('switch', (-1.2, -1.7, 1.55), (1.3, 0.9, 1.1), 'hull', 0.05))
body.append(box('switchroof', (-1.2, -1.7, 2.15), (1.45, 1.05, 0.12), 'team', 0.02))
body.append(box('switchlamp', (-0.8, -2.17, 1.8), (0.2, 0.04, 0.14), 'glow', 0, 1))
body += ep.pipe('cable', [(-0.6, -1.5, 1.2), (-0.2, -0.6, 1.1), (0, 0, 1.1)], 0.08, 'tread')
# ---- Pylon: tapering girder tower, a mid platform and a ladder ----
body += ep.tower_truss('pyl', 1.55, 0.6, 1.0, 10.4, 'hull', bays=6, leg=0.24, brace=0.09, brace_paint='mech')
pz = 5.6
ph = 1.55 + (0.6 - 1.55) * (pz - 1.0) / 9.4
for k in range(4):
    a = k * math.pi / 2
    ca, sa = round(math.cos(a)), round(math.sin(a))
    body.append(box(f'plat{k}', (ca * (ph + 0.25), sa * (ph + 0.25), pz), (0.62 if ca else 2 * ph + 1.1, 0.62 if sa else 2 * ph + 1.1, 0.14), 'team', 0.03))
r = ph + 0.55
body += ep.rail('prail', [(-r, -r, pz), (r, -r, pz), (r, r, pz), (-r, r, pz)], 0.55, 'accent', r=0.035, closed=True)
body += ep.ladder('lad', (0, -0.15, 1.0), 10.4, 'mech', w=0.4, normal=(0, -1, 0))
# ---- Gearbox: riveted box with team lid, cooling fins, a stack and a tail vane ----
gz = HUB[2]
body.append(box('yaw', (0, 0, 10.55), (1.4, 1.4, 0.3), 'mech', 0.03))
gb = box('gearbox', (0, 0.1, gz), (1.5, 2.6, 1.3), 'hull', 0.06)
body.append(gb)
body.append(box('lid', (0, 0.1, gz + 0.71), (1.3, 2.3, 0.14), 'team', 0.03))
for sx in (-1, 1):
    body += ep.rivet_line(f'gbr{sx}', (sx * 0.77, -1.0, gz + 0.5), (sx * 0.77, 1.2, gz + 0.5), 5, 'metal', 0.05)
    for i in range(4):
        body.append(box(f'gfin{sx}{i}', (sx * 0.8, -0.7 + i * 0.3, gz - 0.1), (0.1, 0.12, 0.8), 'mech', 0.01, 1))
body.append(cyl('bearing', (0, 1.35, gz), (0, HUB[1] - 0.1, gz), 0.42, 'mech', 12, bevel=0.02))
body += ep.stack('gstack', (0.4, -0.75, gz + 0.65), 0.15, 0.8)
body.append(ep.beam('vaneboom', (0, -1.1, gz + 0.1), (0, -2.6, gz + 0.3), 0.18, 0.18, 'mech', 0.01))
vane = hull_from('vane', [(0, -2.2, gz - 0.2), (0, -3.2, gz - 0.05), (0, -3.2, gz + 1.25), (0, -2.4, gz + 0.8),
                          (0.08, -2.2, gz - 0.2), (0.08, -3.2, gz - 0.05), (0.08, -3.2, gz + 1.25), (0.08, -2.4, gz + 0.8)], 'team', 0.02)
body.append(vane)
body.append(cyl('lamp', (-0.45, -0.8, gz + 0.78), (-0.45, -0.8, gz + 0.95), 0.09, 'glow', 8, bevel=0))

# ---- Rotor: eight-bladed iron wheel with a rim (spins about forward axis) ----
rot = []
X, Y, Z = HUB
toY = Matrix.Rotation(-math.pi / 2, 4, 'X')   # local +Z -> +Y (forward)
place = Matrix.Translation(Vector(HUB))
hub = cyl('hub', (X, Y, Z), (X, Y + 0.55, Z), 0.55, 'hull_dark', 12, bevel=0.02)
rot.append(hub)
rot.append(cyl('hubcap', (X, Y + 0.55, Z), (X, Y + 0.85, Z), 0.36, 'accent', 12, r2=0.18, bevel=0.01))
R_OUT = 3.55
rim = ep.ring('rim', (0, 0), R_OUT, R_OUT - 0.22, -0.12, 0.12, 'hull_dark', 32)
rim.data.transform(place @ Matrix.Translation((0, 0.3, 0)) @ toY)
rot.append(rim)
rim2 = ep.ring('rim2', (0, 0), 1.65, 1.5, -0.08, 0.08, 'mech', 24)
rim2.data.transform(place @ Matrix.Translation((0, 0.3, 0)) @ toY)
rot.append(rim2)
NB = 8
for i in range(NB):
    a = i * 2 * math.pi / NB
    m = place @ Matrix.Rotation(a, 4, 'Y')
    # Spoke from hub to rim, and a flat pitched paddle blade.
    sp = ep.beam(f'spoke{i}', (0, 0.3, 0.45), (0, 0.3, R_OUT - 0.1), 0.12, 0.12, 'mech', 0)
    sp.data.transform(m)
    rot.append(sp)
    bl = hull_from(f'blade{i}', [(-0.2, 0.25, 1.0), (0.45, 0.42, 1.0), (-0.2, 0.25, R_OUT - 0.2), (0.75, 0.5, R_OUT - 0.2),
                                 (-0.2, 0.3, 1.0), (0.45, 0.47, 1.0), (-0.2, 0.3, R_OUT - 0.2), (0.75, 0.55, R_OUT - 0.2)],
                 'team' if i % 2 == 0 else 'hull', 0.015, 1)
    bl.data.transform(m)
    rot.append(bl)
    for rr in (1.6, 2.6):
        rv = sphere(f'brv{i}{rr}', (0.1, 0.55, rr), 0.05, 'metal', 6, 3)
        rv.data.transform(m)
        rot.append(rv)

B = kit.join('Body', body)
R = kit.join('Rotor_Z', rot, parent_origin=HUB)
ep.finish('crucible_wind_turbine', [B, R], 512, bake_scale=0.5)

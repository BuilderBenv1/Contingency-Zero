"""Crucible "Flak Nest" anti-air missile pit. Run: python <this> <out_dir>

A riveted hex ring wall with a team-painted parapet round a sunken pit holding ammo crates,
and inside it a squat traverse base (Turret: yaws to aim, pivot low in the pit) with armoured
cheek plates and a big team box launcher of six tubes tilted up, glowing warheads showing.
Footprint 1 (8 units): kept inside 7 across. Turret pivot at 0.4 as the procedural model.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('crucible')
body = []
hexp = lambda r, z, ph=math.pi / 6: [(math.cos(ph + k * math.pi / 3) * r, math.sin(ph + k * math.pi / 3) * r, z) for k in range(6)]
# Ring wall: hex annulus, sloped outside, with a team parapet cap.
body.append(ep.ring('wall', (0, 0), 3.45, 2.3, 0.0, 1.8, 'hull', 6, phase=math.pi / 6, bevel=0.06, r_out2=3.0, r_in2=2.3))
body.append(ep.ring('parapet', (0, 0), 3.05, 2.2, 1.8, 2.05, 'team', 6, phase=math.pi / 6, bevel=0.03))
for k in range(6):
    a = k * math.pi / 3
    body += ep.rivet_line(f'pr{k}', (math.cos(a - 0.38) * 2.75, math.sin(a - 0.38) * 2.75, 2.07), (math.cos(a + 0.38) * 2.75, math.sin(a + 0.38) * 2.75, 2.07), 3, 'metal', 0.06)
    # Buttress blocks at the hex corners.
    b = a + math.pi / 6
    blk = box(f'butt{k}', (0, 0, 0), (0.7, 0.8, 1.2), 'hull_dark', 0.05)
    blk.data.transform(Matrix.Translation((math.cos(b) * 3.35, math.sin(b) * 3.35, 0.6)) @ Matrix.Rotation(b, 4, 'Z'))
    body.append(blk)
body.append(ep.ngon('floor', (0, 0), 2.35, 2.35, 0.0, 0.3, 6, 'mech', phase=math.pi / 6))
body.append(cyl('race', (0, 0, 0.3), (0, 0, 0.4), 1.35, 'metal', 16, bevel=0))
# Ammo crates and a lamp in the pit.
for k, (x, y) in enumerate(((-1.55, 0.9), (1.55, 0.9), (-1.5, -1.0))):
    body.append(box(f'crate{k}', (x, y, 0.55), (0.6, 0.45, 0.5), 'accent', 0.03))
    body.append(box(f'cratelid{k}', (x, y, 0.82), (0.62, 0.47, 0.06), 'hull_dark', 0.01, 1))
body.append(box('lamp', (1.6, -1.1, 1.3), (0.2, 0.2, 0.2), 'glow', 0.02, 1))
body.append(box('lampbox', (1.65, -1.15, 1.1), (0.3, 0.3, 0.25), 'mech', 0.02, 1))

PIV = (0, 0, 0.4)
P = lambda x, y, z: Vector((PIV[0] + x, PIV[1] + y, PIV[2] + z))
tur = []
tur.append(cyl('tring', tuple(P(0, 0, 0)), tuple(P(0, 0, 0.3)), 1.3, 'mech', 16, bevel=0.02))
tur.append(box('tbase', tuple(P(0, 0, 0.75)), (1.5, 1.5, 0.9), 'hull_dark', 0.05))
for sx in (-1, 1):
    ch = hull_from(f'cheek{sx}', [tuple(P(sx * 1.25 + dx, y, z)) for dx in (-0.14, 0.14) for (y, z) in ((-0.9, 0.3), (0.8, 0.3), (0.6, 2.4), (-0.6, 2.7))], 'hull', 0.03)
    tur.append(ch)
    tur += ep.rivet_line(f'chr{sx}', tuple(P(sx * 1.41, -0.6, 0.6)), tuple(P(sx * 1.41, 0.5, 0.6)), 3, 'metal', 0.06)
    tur.append(cyl(f'trun{sx}', tuple(P(sx * 1.1, 0, 1.9)), tuple(P(sx * 1.45, 0, 1.9)), 0.25, 'metal', 10, bevel=0))
PITCH = math.radians(52)
pod = []
pod.append(box('launcher', (0, 0, 0), (2.1, 2.1, 1.25), 'team', 0.06))
pod.append(box('lband', (0, -0.55, 0), (2.18, 0.25, 1.33), 'hull_dark', 0.02))
pod.append(box('lfront', (0, 1.0, 0), (1.9, 0.2, 1.05), 'mech', 0.02))
for ix in (-1, 0, 1):
    for iz in (-1, 1):
        pod.append(cyl(f'tube{ix}{iz}', (ix * 0.6, 0.9, iz * 0.27), (ix * 0.6, 1.15, iz * 0.27), 0.23, 'tread', 10, bevel=0))
        pod.append(cyl(f'war{ix}{iz}', (ix * 0.6, 0.95, iz * 0.27), (ix * 0.6, 1.17, iz * 0.27), 0.15, 'glow', 8, r2=0.05, bevel=0))
pod += ep.rivet_line('lr', (-0.9, -0.3, 0.64), (0.9, -0.3, 0.64), 5, 'metal', 0.06)
pod.append(box('lsight', (0.75, 0.2, 0.75), (0.35, 0.5, 0.25), 'hull_dark', 0.02))
xf = Matrix.Translation(P(0, 0.15, 1.9)) @ Matrix.Rotation(PITCH, 4, 'X')
for o in pod:
    o.data.transform(xf)
tur += pod

B = kit.join('Body', body)
T = kit.join('Turret', tur, parent_origin=PIV)
ep.finish('crucible_aa_turret', [B, T], 512, bake_scale=0.5)

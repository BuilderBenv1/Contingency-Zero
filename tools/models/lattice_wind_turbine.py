"""Lattice Wind Turbine: a slim tapering hex mast on a buttressed plinth, a sleek nacelle with a
sensor fin, and three long twisted blades with team-painted tips (Rotor_Z: faces forward and
spins about the game's Z axis). Run: python <this> <out_dir>
Footprint 1 (8 units): base within 7 across; the blades sweep above it.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('lattice')
body = []
HUB = (0, 1.05, 11.4)
# ---- Plinth: octagonal footing with a team ring and glowing trim ----
body.append(ep.ngon('plinth', (0, 0), 2.9, 2.45, 0.0, 0.75, 8, 'hull', bevel=0.06, segs=2))
body.append(ep.ring('plinthring', (0, 0), 2.42, 1.35, 0.75, 0.86, 'team', 8, phase=math.pi / 8, bevel=0.02))
body.append(ep.ring('plinthglow', (0, 0), 2.93, 2.7, 0.3, 0.38, 'glow', 8, phase=math.pi / 8))
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    body.append(box(f'vent{k}', (math.cos(a) * 2.6, math.sin(a) * 2.6, 0.55), (0.5, 0.5, 0.18), 'mech', 0.03))
# Three fins buttress the mast.
for k in range(3):
    a = k * 2 * math.pi / 3 - math.pi / 2
    c, s = math.cos(a), math.sin(a)
    pts = [(c * 0.4 - s * 0.12, s * 0.4 + c * 0.12, 0.8), (c * 0.4 + s * 0.12, s * 0.4 - c * 0.12, 0.8),
           (c * 2.0 - s * 0.12, s * 2.0 + c * 0.12, 0.8), (c * 2.0 + s * 0.12, s * 2.0 - c * 0.12, 0.8),
           (c * 0.38 - s * 0.1, s * 0.38 + c * 0.1, 3.4), (c * 0.38 + s * 0.1, s * 0.38 - c * 0.1, 3.4)]
    body.append(hull_from(f'fin{k}', pts, 'hull_dark', 0.03))
    body.append(box(f'finlight{k}', (c * 1.5, s * 1.5, 1.45), (0.08, 0.08, 0.08), 'glow', 0, 1))
# ---- Mast ----
body.append(ep.ngon('mast', (0, 0), 0.6, 0.3, 0.8, 11.0, 6, 'hull', phase=0, bevel=0.03))
band = ep.ngon('band', (0, 0), 0.53, 0.48, 4.6, 6.4, 6, 'team', phase=0, bevel=0.02)
body.append(band)
body.append(ep.ngon('glowband', (0, 0), 0.4, 0.39, 8.5, 8.75, 6, 'glow', phase=0, bevel=0))
body.append(ep.ngon('collar', (0, 0), 0.5, 0.42, 3.2, 3.45, 6, 'mech', phase=0))
body.append(box('hatch', (0, -0.52, 1.6), (0.42, 0.06, 0.9), 'hull_dark', 0.02, 1))
body.append(box('hatchlamp', (0, -0.56, 2.15), (0.2, 0.03, 0.06), 'glow', 0, 1))
# ---- Nacelle: a lofted pod, team spine, sensor fin and anemometer ----
nz = HUB[2]
pts = []
for y, w, h in ((-1.25, 0.18, 0.22), (-0.7, 0.42, 0.42), (0.3, 0.5, 0.5), (0.95, 0.42, 0.42)):
    for sx in (-1, 1):
        for sz in (-1, 1):
            pts.append((sx * w, y, nz + sz * h * (0.85 if sz < 0 else 1.0)))
nac = hull_from('nacelle', pts, 'hull', 0.04)
inset_panels(nac, 0.04, 0.015, lambda f: f.calc_area() > 0.25 and abs(f.normal.z) < 0.7)
body.append(nac)
spine = hull_from('spine', [(sx * w, y, nz + h) for y, w, h in ((-1.0, 0.16, 0.36), (-0.6, 0.3, 0.46), (0.3, 0.36, 0.53), (0.85, 0.28, 0.45))
                             for sx in (-1, 1)] + [(sx * w * 0.8, y, nz + h + 0.07) for y, w, h in ((-0.6, 0.3, 0.46), (0.3, 0.36, 0.53)) for sx in (-1, 1)],
                 'team', 0.02)
body.append(spine)
body.append(ep.ngon('yaw', (0, 0), 0.42, 0.42, 10.85, 11.05, 6, 'mech', phase=0))
fin = hull_from('tailfin', [(0, -0.9, nz + 0.3), (0, -1.6, nz + 0.35), (0, -1.65, nz + 1.0), (0, -1.2, nz + 0.95),
                            (0.05, -0.9, nz + 0.3), (0.05, -1.6, nz + 0.35), (0.05, -1.65, nz + 1.0), (0.05, -1.2, nz + 0.95)], 'hull_dark', 0.01)
body.append(fin)
body.append(box('fineye', (0.0, -1.62, nz + 0.85), (0.1, 0.08, 0.1), 'glow', 0, 1))
body.append(ep.rod('anemo', (0.25, -0.5, nz + 0.45), (0.25, -0.5, nz + 0.95), 0.025, 'mech', 5))
for k in range(3):
    a = k * 2 * math.pi / 3
    body.append(sphere(f'cup{k}', (0.25 + math.cos(a) * 0.18, -0.5 + math.sin(a) * 0.18, nz + 0.95), 0.05, 'metal', 6, 3))
body.append(box('nacglow', (0, 0.96, nz - 0.1), (0.5, 0.03, 0.08), 'glow', 0, 1))

# ---- Rotor (spins about the forward axis) ----
rot = []
X, Y, Z = HUB
rot.append(cyl('spinner', (X, Y - 0.05, Z), (X, Y + 0.85, Z), 0.5, 'hull', 16, r2=0.06, bevel=0.01))
rot.append(cyl('hubring', (X, Y - 0.25, Z), (X, Y + 0.0, Z), 0.56, 'mech', 16, bevel=0.01))
rot.append(cyl('hubglow', (X, Y + 0.0, Z), (X, Y + 0.04, Z), 0.5, 'glow', 16, bevel=0))


def blade(name, r0, r1, c0, c1, t0, t1, paint, pitch=0.3):
    """A tapered blade along local +Z from r0 to r1 (chord along X, thickness along Y), pitched."""
    pts = []
    for r, c, t in ((r0, c0, t0), (r1, c1, t1)):
        for sx in (-1, 1):
            for sy in (-1, 1):
                # Leading edge thicker than the trailing edge.
                pts.append((sx * c / 2 + (0.15 * c if sx > 0 else 0), sy * t / 2 * (1.0 if sx > 0 else 0.4), r))
    o = hull_from(name, pts, paint, 0.012, 1)
    o.data.transform(Matrix.Rotation(pitch, 4, 'Z'))
    return o


for i in range(3):
    ang = i * 2 * math.pi / 3
    m = Matrix.Translation(Vector(HUB)) @ Matrix.Rotation(ang, 4, 'Y')
    parts = [blade(f'blade{i}', 0.3, 3.4, 0.85, 0.42, 0.18, 0.09, 'hull', 0.32),
             blade(f'bladetip{i}', 3.35, 4.55, 0.45, 0.16, 0.1, 0.05, 'team', 0.28),
             blade(f'bladestripe{i}', 1.0, 2.4, 0.9, 0.62, 0.2, 0.15, 'hull_dark', 0.32),
             cyl(f'root{i}', (0, 0, 0.2), (0, 0, 0.75), 0.17, 'mech', 8, bevel=0)]
    for p in parts:
        p.data.transform(m)
        rot.append(p)

B = kit.join('Body', body)
R = kit.join('Rotor_Z', rot, parent_origin=HUB)
ep.finish('lattice_wind_turbine', [B, R], 512, bake_scale=0.5)

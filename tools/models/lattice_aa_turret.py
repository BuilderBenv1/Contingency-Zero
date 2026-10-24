"""Lattice "Spire" anti-air missile tower. Run: python <this> <out_dir>

A hex footing with a team top, a tall tapering needle with a team collar and glowing bands,
and a yoke (Turret: yaws to aim) carrying two team-painted missile pods tilted steeply up,
each with four tubes showing glowing warheads, and a tracking radar between them.
Footprint 1 (8 units): kept inside 7 across. Turret pivot at 5.3 as the procedural model.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('lattice')
body = []
body.append(ep.ngon('footing', (0, 0), 2.85, 2.2, 0.0, 0.9, 6, 'hull', phase=0, bevel=0.06, segs=2))
body.append(ep.ngon('footteam', (0, 0), 2.2, 1.7, 0.9, 1.05, 6, 'team', phase=0, bevel=0.03))
body.append(ep.ring('footglow', (0, 0), 2.7, 2.45, 0.3, 0.38, 'glow', 6, phase=0))
for k in range(6):
    a = k * math.pi / 3 + math.pi / 6
    body.append(box(f'vent{k}', (math.cos(a) * 2.2, math.sin(a) * 2.2, 0.6), (0.1, 0.1, 0.1), 'glow', 0, 1))
needle = hull_from('needle', [(math.cos(k * math.pi / 3) * 1.25, math.sin(k * math.pi / 3) * 1.25, 1.0) for k in range(6)] +
                   [(math.cos(k * math.pi / 3) * 0.5, math.sin(k * math.pi / 3) * 0.5, 5.1) for k in range(6)], 'hull', 0.04)
inset_panels(needle, 0.07, 0.025, lambda f: abs(f.normal.z) < 0.5)
body.append(needle)
body.append(ep.ngon('collar', (0, 0), 1.08, 0.98, 2.2, 3.0, 6, 'team', phase=0, bevel=0.03))
for z in (1.8, 3.9):
    r = 1.25 - (z - 1.0) / 4.1 * 0.75 + 0.04
    body.append(ep.ngon(f'band{z}', (0, 0), r, r - 0.02, z, z + 0.18, 6, 'glow', phase=0, bevel=0))
body.append(ep.ngon('bearing', (0, 0), 0.75, 0.68, 5.0, 5.3, 6, 'mech', phase=0))
# Three slim struts from the footing up the needle.
for k in range(3):
    a = k * 2 * math.pi / 3 - math.pi / 2
    body.append(ep.beam(f'strut{k}', (math.cos(a) * 2.0, math.sin(a) * 2.0, 1.0), (math.cos(a) * 0.75, math.sin(a) * 0.75, 3.6), 0.18, 0.14, 'mech', 0.01))

PIV = (0, 0, 5.3)
P = lambda x, y, z: Vector((PIV[0] + x, PIV[1] + y, PIV[2] + z))
tur = []
tur.append(ep.ngon('tbase', PIV[:2], 0.85, 0.65, PIV[2], PIV[2] + 0.45, 6, 'hull_dark', phase=0))
tur.append(box('yoke', tuple(P(0, 0, 0.6)), (2.0, 0.6, 0.35), 'hull', 0.04))
PITCH = math.radians(55)
for sx in (-1, 1):
    c = P(sx * 1.25, 0.05, 0.95)
    rot = Matrix.Translation(c) @ Matrix.Rotation(PITCH, 4, 'X')   # pod axis (+Y) tilts up
    pod = []
    pod.append(hull_from(f'pod{sx}', [(-0.5, -0.95, -0.5), (0.5, -0.95, -0.5), (-0.5, -0.95, 0.5), (0.5, -0.95, 0.5),
                                      (-0.45, 0.95, -0.45), (0.45, 0.95, -0.45), (-0.45, 0.95, 0.45), (0.45, 0.95, 0.45),
                                      (-0.3, 1.1, -0.3), (0.3, 1.1, -0.3), (-0.3, 1.1, 0.3), (0.3, 1.1, 0.3)], 'team', 0.04))
    pod.append(box(f'podband{sx}', (0, -0.3, 0), (1.06, 0.3, 1.06), 'hull_dark', 0.02))
    for ix in (-1, 1):
        for iz in (-1, 1):
            pod.append(cyl(f'tube{sx}{ix}{iz}', (ix * 0.22, 0.6, iz * 0.22), (ix * 0.22, 1.12, iz * 0.22), 0.17, 'mech', 8, bevel=0))
            pod.append(cyl(f'war{sx}{ix}{iz}', (ix * 0.22, 1.0, iz * 0.22), (ix * 0.22, 1.13, iz * 0.22), 0.11, 'glow', 8, r2=0.04, bevel=0))
    pod.append(box(f'podarm{sx}', (-sx * 0.55, 0, 0), (0.2, 0.6, 0.6), 'mech', 0.02))
    for o in pod:
        o.data.transform(rot)
    tur += pod
# Tracking radar between the pods.
tur.append(ep.rod('radmast', tuple(P(0, -0.15, 0.75)), tuple(P(0, -0.15, 1.45)), 0.08, 'mech'))
tur.append(ep.dish('raddish', tuple(P(0, 0.0, 1.55)), (0, 1, 0.35), 0.45, 0.15, 'hull', 0.04, 3, 12))
tur.append(sphere('radfeed', tuple(P(0, 0.35, 1.68)), 0.07, 'glow', 6, 4))

B = kit.join('Body', body)
T = kit.join('Turret', tur, parent_origin=PIV)
ep.finish('lattice_aa_turret', [B, T], 512, bake_scale=0.5)

"""Lattice Fusion Reactor: a faceted containment dome crowned by a caged star. Run: python <this> <out_dir>

A wide octagonal footing with a team ring, a twelve-sided faceted dome (team facets between
steel ribs), four tall field pylons on the diagonals feeding glowing struts into a crown where a
glowing core floats in four claws, and two glowing confinement rings with clamp blocks that spin
round the dome (Rotor_Y). Footprint 3 (24 units): kept inside 23 across.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('lattice')
body = []
P8 = math.pi / 8
N = 12
ringpts = lambda n, r, z, ph=0.0: [(math.cos(ph + k * 2 * math.pi / n) * r, math.sin(ph + k * 2 * math.pi / n) * r, z) for k in range(n)]
# ---- Footing ----
foot = hull_from('footing', ringpts(8, 11.3, 0.0, P8) + ringpts(8, 10.7, 1.0, P8), 'hull_dark', 0.1)
inset_panels(foot, 0.25, 0.06, lambda f: abs(f.normal.z) < 0.5)
body.append(foot)
body.append(ep.ring('footteam', (0, 0), 10.5, 8.3, 1.0, 1.14, 'team', 8, phase=P8, bevel=0.03))
body.append(ep.ring('footglow', (0, 0), 11.33, 11.0, 0.45, 0.55, 'glow', 8, phase=P8))
for k in range(8):
    a = k * math.pi / 4 + P8
    if k in (5, 6):  # leave the front clear for the access port
        continue
    body.append(box(f'vent{k}', (0, 0, 0), (2.4, 0.5, 0.18), 'mech', 0.03))
    body[-1].data.transform(Matrix.Translation((math.cos(a) * 9.3, math.sin(a) * 9.3, 1.18)) @ Matrix.Rotation(a + math.pi / 2, 4, 'Z'))
# ---- Dome: profile rings, facets in team, ribs in steel ----
prof = [(7.4, 1.0), (7.25, 2.6), (6.6, 4.4), (5.3, 6.1), (3.3, 7.4)]
dome = hull_from('dome', sum([ringpts(N, r, z) for r, z in prof], []), 'team', 0.06)
inset_panels(dome, 0.22, 0.07, lambda f: f.calc_area() > 1.0)
body.append(dome)
for k in range(N):
    a = k * 2 * math.pi / N
    for (r0, z0), (r1, z1) in zip(prof[:-1], prof[1:]):
        body.append(ep.beam(f'rib{k}_{z0}', (math.cos(a) * (r0 + 0.05), math.sin(a) * (r0 + 0.05), z0), (math.cos(a) * (r1 + 0.05), math.sin(a) * (r1 + 0.05), z1), 0.38, 0.22, 'hull', 0.03))
body.append(ep.ngon('domebase', (0, 0), 7.75, 7.55, 1.0, 1.9, N, 'hull', phase=0.0, bevel=0.04))
# Crown collar and caged core.
body.append(ep.ngon('collar', (0, 0), 3.5, 2.6, 7.3, 8.0, N, 'hull', phase=0.0, bevel=0.04))
body.append(ep.ring('collarglow', (0, 0), 2.65, 2.2, 8.0, 8.08, 'glow', N))
body.append(sphere('core', (0, 0, 9.6), 1.75, 'glow', 14, 8))
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    ca, sa = math.cos(a), math.sin(a)
    body.append(ep.beam(f'claw{k}', (ca * 2.5, sa * 2.5, 7.9), (ca * 2.2, sa * 2.2, 10.2), 0.6, 0.55, 'team', 0.05, 2, w2=0.45, h2=0.4))
    body.append(ep.beam(f'clawtip{k}', (ca * 2.2, sa * 2.2, 10.2), (ca * 1.1, sa * 1.1, 11.4), 0.45, 0.4, 'hull', 0.04, 2, w2=0.25, h2=0.25))
    body.append(box(f'clawknuckle{k}', (ca * 2.2, sa * 2.2, 10.2), (0.7, 0.7, 0.7), 'mech', 0.05))
# ---- Field pylons on the diagonals ----
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    ca, sa = math.cos(a), math.sin(a)
    c = (ca * 9.9, sa * 9.9)
    body.append(ep.ngon(f'pfoot{k}', c, 2.0, 1.8, 1.0, 1.6, 6, 'hull_dark', phase=a))
    py = hull_from(f'pylon{k}', [(c[0] + math.cos(a + j * math.pi / 3) * 1.6, c[1] + math.sin(a + j * math.pi / 3) * 1.6, 1.6) for j in range(6)] +
                   [(c[0] + math.cos(a + j * math.pi / 3) * 0.85, c[1] + math.sin(a + j * math.pi / 3) * 0.85, 7.2) for j in range(6)], 'hull', 0.05)
    inset_panels(py, 0.12, 0.04, lambda f: abs(f.normal.z) < 0.5)
    body.append(py)
    body.append(ep.ngon(f'pband{k}', c, 1.35, 1.2, 3.2, 4.4, 6, 'team', phase=a, bevel=0.03))
    body.append(ep.ngon(f'pcap{k}', c, 0.9, 0.6, 7.2, 7.6, 6, 'mech', phase=a))
    body.append(sphere(f'porb{k}', (c[0], c[1], 8.05), 0.6, 'glow', 10, 6))
    for z in (2.2, 5.6):
        body.append(box(f'pslit{k}{z}', (0, 0, 0), (0.08, 0.9, 0.5), 'glow', 0, 1))
        body[-1].data.transform(Matrix.Translation((c[0] - ca * (1.6 - (z - 1.6) * 0.134 + 0.0), c[1] - sa * (1.6 - (z - 1.6) * 0.134), z)) @ Matrix.Rotation(a, 4, 'Z'))
    # Glowing feed strut into the crown, with a steel sleeve.
    body.append(ep.beam(f'feed{k}', (c[0] - ca * 0.5, c[1] - sa * 0.5, 7.0), (ca * 3.1, sa * 3.1, 8.0), 0.5, 0.5, 'metal', 0.03))
    body.append(ep.beam(f'feedg{k}', (c[0] - ca * 0.5, c[1] - sa * 0.5, 7.3), (ca * 3.1, sa * 3.1, 8.3), 0.18, 0.12, 'glow', 0))
# ---- Access port at the front ----
body.append(box('port', (0, -7.9, 1.9), (3.6, 1.6, 1.8), 'hull_dark', 0.06))
body.append(box('portdoor', (0, -8.72, 1.75), (2.6, 0.06, 1.3), 'mech', 0.02, 1))
body.append(box('portlamp', (0, -8.74, 2.6), (2.0, 0.04, 0.1), 'glow', 0, 1))
body.append(box('porttop', (0, -7.95, 2.86), (3.8, 1.8, 0.14), 'team', 0.02))

# ---- Rotor: two confinement rings with clamp blocks ----
PIV = (0, 0, 1.0)
rot = []
for j, (z, rin) in enumerate(((3.1, 7.3), (5.4, 6.25))):
    rot.append(ep.ring(f'cring{j}', (0, 0), rin + 0.75, rin + 0.15, z - 0.22, z + 0.22, 'glow', 32))
    rot.append(ep.ring(f'cringb{j}', (0, 0), rin + 0.85, rin + 0.68, z - 0.3, z + 0.3, 'hull', 32))
    for i in range(6):
        a = i * math.pi / 3 + j * math.pi / 6
        blk = box(f'clamp{j}{i}', (0, 0, 0), (0.9, 1.2, 0.95), 'mech', 0.05)
        blk.data.transform(Matrix.Translation((math.cos(a) * (rin + 0.5), math.sin(a) * (rin + 0.5), z)) @ Matrix.Rotation(a, 4, 'Z'))
        rot.append(blk)
        cap = box(f'clampcap{j}{i}', (0, 0, 0), (0.7, 1.0, 0.1), 'team', 0.02, 1)
        cap.data.transform(Matrix.Translation((math.cos(a) * (rin + 0.5), math.sin(a) * (rin + 0.5), z + 0.5)) @ Matrix.Rotation(a, 4, 'Z'))
        rot.append(cap)

B = kit.join('Body', body)
R = kit.join('Rotor_Y', rot, parent_origin=PIV)
ep.finish('lattice_fusion_reactor', [B, R], 1024, bake_scale=0.2, sizes={'rotor': 512})

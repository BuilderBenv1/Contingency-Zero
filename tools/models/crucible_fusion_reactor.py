"""Crucible Fusion Furnace: a hexagonal furnace block with four cooling stacks. Run: python <this> <out_dir>

A riveted apron; a hex furnace hall with glowing firing ports and buttresses, an upper hex drum
with team plates under a dark dome with a hot vent, four huge banded cooling stacks at the
corners braced to the drum, pipework, and a glowing confinement band with three heavy carriers
that spins round the drum (Rotor_Y). Footprint 3 (24 units): kept inside 23 across.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('crucible')
body = []
E = 11.2
hexpts = lambda r, z, ph=0.0: [(math.cos(ph + k * math.pi / 3) * r, math.sin(ph + k * math.pi / 3) * r, z) for k in range(6)]
# ---- Apron ----
body.append(box('apron', (0, 0, 0.4), (2 * E, 2 * E, 0.8), 'hull_dark', 0.1))
for s in (-1, 1):
    body += ep.rivet_line(f'arx{s}', (-E + 2.6, s * (E - 0.3), 0.8), (E - 2.6, s * (E - 0.3), 0.8), 12, 'metal', 0.1)
    body += ep.rivet_line(f'ary{s}', (s * (E - 0.3), -E + 2.6, 0.8), (s * (E - 0.3), E - 2.6, 0.8), 12, 'metal', 0.1)
# ---- Furnace hall ----
hall = hull_from('hall', hexpts(9.2, 0.8) + hexpts(8.5, 4.4), 'hull', 0.08)
inset_panels(hall, 0.3, 0.08, lambda f: abs(f.normal.z) < 0.5)
body.append(hall)
body.append(hull_from('hallroof', hexpts(8.6, 4.4) + hexpts(7.6, 4.8), 'team', 0.06))
for k in range(6):
    a = k * math.pi / 3 + math.pi / 6
    ca, sa = math.cos(a), math.sin(a)
    # Firing port on each face: dark surround and a glowing grate.
    fp = box(f'port{k}', (0, 0, 0), (2.6, 0.5, 1.5), 'mech', 0.05)
    fp.data.transform(Matrix.Translation((ca * 7.75, sa * 7.75, 2.3)) @ Matrix.Rotation(a + math.pi / 2, 4, 'Z'))
    body.append(fp)
    for j in range(4):
        g = box(f'grate{k}{j}', (0, 0, 0), (0.35, 0.1, 1.0), 'glow', 0, 1)
        g.data.transform(Matrix.Translation((ca * 8.02, sa * 8.02, 2.3)) @ Matrix.Rotation(a + math.pi / 2, 4, 'Z') @ Matrix.Translation((-0.8 + j * 0.53, 0, 0)))
        body.append(g)
    # Buttress at each corner of the hex.
    b = k * math.pi / 3
    body.append(ep.beam(f'butt{k}', (math.cos(b) * 9.6, math.sin(b) * 9.6, 0.8), (math.cos(b) * 8.6, math.sin(b) * 8.6, 4.2), 1.1, 0.8, 'hull_dark', 0.05))
    body += ep.rivet_line(f'hr{k}', (math.cos(b) * 8.55 + math.cos(b + 2.1) * 0.3, math.sin(b) * 8.55 + math.sin(b + 2.1) * 0.3, 4.85),
                          (math.cos(b + math.pi / 3) * 8.55 + math.cos(b - 1.05) * 0.3, math.sin(b + math.pi / 3) * 8.55 + math.sin(b - 1.05) * 0.3, 4.85), 6, 'metal', 0.09)
# ---- Upper drum, team plates, dome and hot vent ----
drum = hull_from('drum', hexpts(6.1, 4.8) + hexpts(5.4, 8.0), 'hull_dark', 0.08)
body.append(drum)
for k in range(6):
    a = k * math.pi / 3 + math.pi / 6
    pl = box(f'plate{k}', (0, 0, 0), (4.2, 0.25, 2.2), 'team', 0.04)
    pl.data.transform(Matrix.Translation((math.cos(a) * 5.38, math.sin(a) * 5.38, 6.6)) @ Matrix.Rotation(a + math.pi / 2, 4, 'Z') @ Matrix.Rotation(math.radians(-12), 4, 'X'))
    body.append(pl)
body.append(hull_from('dome', hexpts(5.5, 8.0) + hexpts(4.4, 9.3) + hexpts(2.4, 10.1), 'team', 0.06))
for k in range(6):
    b = k * math.pi / 3
    body.append(ep.beam(f'domerib{k}', (math.cos(b) * 5.5, math.sin(b) * 5.5, 8.05), (math.cos(b) * 2.4, math.sin(b) * 2.4, 10.15), 0.4, 0.25, 'mech', 0.02))
body.append(cyl('vent', (0, 0, 10.0), (0, 0, 10.9), 1.5, 'metal', 12, r2=1.2, bevel=0.03))
body.append(cyl('ventlip', (0, 0, 10.9), (0, 0, 11.2), 1.4, 'mech', 12, bevel=0.02))
body.append(cyl('ventglow', (0, 0, 11.2), (0, 0, 11.25), 1.05, 'glow', 12, bevel=0))
# ---- Cooling stacks at the corners ----
for sx in (-1, 1):
    for sy in (-1, 1):
        x, y = sx * 9.1, sy * 9.1
        body.append(box(f'stfoot{sx}{sy}', (x, y, 1.2), (3.6, 3.6, 0.8), 'hull_dark', 0.06))
        body += ep.rivet_ring(f'stfr{sx}{sy}', (x, y), 1.55, 1.62, 8, 'metal', 0.1)
        body.append(cyl(f'stack{sx}{sy}', (x, y, 1.6), (x, y, 10.4), 1.45, 'hull', 16, r2=1.15, bevel=0.04))
        for z in (3.6, 7.0):
            body.append(cyl(f'sthoop{sx}{sy}{z}', (x, y, z), (x, y, z + 0.3), 1.45 - (z - 1.6) / 8.8 * 0.3 + 0.1, 'mech', 16, bevel=0.01))
        body.append(cyl(f'stband{sx}{sy}', (x, y, 4.6), (x, y, 6.2), 1.45 - 0.1 + 0.08, 'team', 16, r2=1.27, bevel=0.02))
        body.append(cyl(f'stlip{sx}{sy}', (x, y, 10.3), (x, y, 10.8), 1.35, 'mech', 16, bevel=0.03))
        body.append(cyl(f'stglow{sx}{sy}', (x, y, 10.8), (x, y, 10.85), 1.05, 'glow', 16, bevel=0))
        # Brace and a feed pipe from the drum.
        body.append(ep.beam(f'stbrace{sx}{sy}', (x - sx * 1.1, y - sy * 1.1, 8.0), (sx * 4.2, sy * 4.2, 7.6), 0.6, 0.6, 'metal', 0.03))
        body.append(ep.rod(f'stpipe{sx}{sy}', (x - sx * 0.9, y - sy * 0.9, 3.0), (sx * 6.0, sy * 6.0, 3.0), 0.4, 'accent', 10))
        body.append(cyl(f'stflange{sx}{sy}', (sx * 6.6, sy * 6.6, 3.0), (sx * 6.3, sy * 6.3, 3.0), 0.6, 'mech', 10, bevel=0))
# ---- Rotor: glowing confinement band with three carriers round the drum ----
PIV = (0, 0, 4.4)
rot = []
rot.append(ep.ring('band', (0, 0), 7.0, 6.25, PIV[2] + 1.05, PIV[2] + 1.95, 'glow', 24))
rot.append(ep.ring('bandtop', (0, 0), 7.15, 6.2, PIV[2] + 1.95, PIV[2] + 2.2, 'mech', 24))
rot.append(ep.ring('bandbot', (0, 0), 7.15, 6.2, PIV[2] + 0.85, PIV[2] + 1.05, 'mech', 24))
for i in range(3):
    a = i * 2 * math.pi / 3 + math.pi / 6
    blk = box(f'carrier{i}', (0, 0, 0), (1.2, 2.4, 1.9), 'hull_dark', 0.06)
    blk.data.transform(Matrix.Translation((math.cos(a) * 6.95, math.sin(a) * 6.95, PIV[2] + 1.5)) @ Matrix.Rotation(a, 4, 'Z'))
    rot.append(blk)
    cap = box(f'carriercap{i}', (0, 0, 0), (1.0, 2.2, 0.2), 'team', 0.03)
    cap.data.transform(Matrix.Translation((math.cos(a) * 6.95, math.sin(a) * 6.95, PIV[2] + 2.5)) @ Matrix.Rotation(a, 4, 'Z'))
    rot.append(cap)
    for s in (-1, 1):
        rv = sphere(f'carrierrv{i}{s}', (0, 0, 0), 0.1, 'metal', 6, 3)
        rv.data.transform(Matrix.Translation((math.cos(a) * 6.95, math.sin(a) * 6.95, PIV[2] + 2.62)) @ Matrix.Rotation(a, 4, 'Z') @ Matrix.Translation((0, s * 0.8, 0)))
        rot.append(rv)

B = kit.join('Body', body)
R = kit.join('Rotor_Y', rot, parent_origin=PIV)
ep.finish('crucible_fusion_reactor', [B, R], 1024, bake_scale=0.2, sizes={'rotor': 512})

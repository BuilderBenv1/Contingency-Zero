"""Lattice Experimental Gantry. Run: python tools/models/lattice_experimental_gantry.py <out_dir>

Footprint 4 (32 units). Two stepped, three-tier pylons with team-painted terraces flank a vast
bay; a triple-ringed pad at the centre holds the experimental's nanoframe. Two pointed arches
spring from the pylon tops across the bay, each dropping a mast with a nanolathe arm, and four
more arms reach in from the pylons. A back block closes the bay; antennae with sensor orbs
crown the pylons. The bay opens to the door side (-y).
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
PI, PO = 10.6, 15.2              # pylon inner / outer x
Y0, Y1 = -13.8, 14.2             # pylon front / back

# ---- Floor, pad, door ----
body.append(slab('floor', (0, 0, 0.25), (30.8, 30.8, 0.5), 'hull_dark', 'hull_dark', chamfer=0.35, bevel=0.06))
body += fp.pad_round('pad', (0, 0, 0.5), 8.4, F, segs=32)
body.append(ring('ring2', (0, 0, 0.5), 9.8, 9.3, 0.1, 'glow_dim', 32))
body.append(ring('ring3', (0, 0, 0.64), 3.2, 2.8, 0.06, 'glow', 32))
body.append(box('door', (0, -15.0, 0.55), (19.0, 0.4, 0.1), 'glow', 0, 1))
body += fp.chevrons('chev', -13.6, 6.0, 0.52, 7, 'glow_dim', 0.7)

# ---- Pylons: three bevelled tiers with team terraces ----
TIERS = ((0.5, 7.0, 0.0), (7.0, 12.6, 0.7), (12.6, 17.0, 1.4))
for sx in (-1, 1):
    for t, (z0, z1, ins) in enumerate(TIERS):
        xi, xo = PI + ins, PO - ins
        y0 = Y0 + ins * 2.2
        s = slab(f'tier{sx}{t}', (sx * (xi + xo) / 2, (y0 + Y1 - ins) / 2, (z0 + z1) / 2), (xo - xi, Y1 - ins - y0, z1 - z0),
                 'hull', 'team', chamfer=0.45, bevel=0.1, segs=2)
        inset_panels(s, 0.25, 0.06, lambda f: f.calc_area() > 10 and abs(f.normal.z) < 0.5)
        body.append(s)
        # Glow band round the bay face at each tier's top, and a sloped prow plate.
        body.append(box(f'band{sx}{t}', (sx * (xi - 0.03), 0.0, z1 - 0.6), (0.1, (Y1 - y0) * 0.75, 0.16), 'glow', 0, 1))
        body.append(box(f'prow{sx}{t}', (sx * (xi + xo) / 2, y0 - 0.03, (z0 + z1) / 2), ((xo - xi) * 0.6, 0.1, (z1 - z0) * 0.6), 'team', 0.02, 1))
        body.append(box(f'prowg{sx}{t}', (sx * (xi + xo) / 2, y0 - 0.06, (z0 + z1) / 2), ((xo - xi) * 0.3, 0.06, 0.18), 'glow', 0, 1))
    # Long vertical seams on the bay face of the bottom tier.
    for k in range(3):
        body.append(box(f'vs{sx}{k}', (sx * (PI - 0.03), -8.0 + k * 8.0, 3.6), (0.1, 0.3, 5.0), 'glow_dim', 0, 1))
    # Terrace detail: vents and ribs on the tier tops.
    for k in range(5):
        body.append(box(f'vent{sx}{k}', (sx * (PO - 1.2), -9.0 + k * 4.4, 7.08), (1.0, 2.2, 0.12), 'hull_dark', 0.02, 1))
        body.append(box(f'vent2{sx}{k}', (sx * (PO - 1.6), -7.0 + k * 4.0, 12.68), (0.8, 1.8, 0.12), 'hull_dark', 0.02, 1))
    body.append(slab(f'spine{sx}', (sx * 12.9, 0.8, 17.1), (0.7, 20.0, 0.25), 'hull', 'metal', chamfer=0.1, bevel=0.03))
    # Antenna mast and sensor orb at the back of each pylon.
    body.append(cyl(f'mast{sx}', (sx * 12.9, 11.6, 17.0), (sx * 12.9, 11.6, 22.0), 0.25, 'metal', 8, r2=0.12, bevel=0))
    body.append(cyl(f'mastb{sx}', (sx * 12.9, 11.6, 17.0), (sx * 12.9, 11.6, 18.0), 0.6, 'mech', 8, r2=0.4, bevel=0.02))
    body.append(sphere(f'orb{sx}', (sx * 12.9, 11.6, 22.1), 0.45, 'glow', 10, 6))
    for k in range(3):
        body.append(box(f'fin{sx}{k}', (sx * 12.9, 11.6, 19.0 + k * 0.9), (1.6 - k * 0.4, 0.1, 0.1), 'mech', 0, 1))
    # Pylon arms: two per side, low and high.
    for i, (y, z) in enumerate(((-6.0, 3.0), (4.0, 7.6))):
        body.append(box(f'armm{sx}{i}', (sx * (PI - 0.1), y, z - 0.2), (0.8, 1.8, 0.5), 'mech', 0.04, 1))
        body += fp.nano_arm(f'arm{sx}{i}', (sx * (PI - 0.2), y, z), (0, 0, 2.0 + i * 1.5), 3.6, F)

# ---- Back block ----
bb = slab('back', (0, 12.6, 5.5), (21.0, 4.8, 10.0), 'hull', 'team', chamfer=0.6, bevel=0.1, segs=2)
inset_panels(bb, 0.3, 0.06, lambda f: f.calc_area() > 10)
body.append(bb)
body.append(box('bslot', (0, 10.18, 6.0), (14.0, 0.1, 0.3), 'glow', 0, 1))
body.append(box('bslot2', (0, 10.18, 3.0), (10.0, 0.1, 0.2), 'glow_dim', 0, 1))
for k in range(4):
    body.append(slab(f'bfin{k}', (-7.2 + k * 4.8, 10.05, 4.4), (0.5, 0.4, 7.0), 'hull_dark', 'hull', chamfer=0.1, bevel=0.03))

# ---- Two pointed arches across the bay, each dropping a mast and a hanging arm ----
N = 12
for g, gy in enumerate((-5.0, 4.4)):
    pts = []
    for i in range(N + 1):
        x = -12.6 + 25.2 * i / N
        z = 17.0 + 6.0 * (1 - abs(x) / 12.6) ** 0.65
        pts.append((x, z))
    for i in range(N):
        (x0, z0), (x1, z1) = pts[i], pts[i + 1]
        body.append(beam(f'arch{g}_{i}', (x0, gy, z0), (x1, gy, z1), 1.0, 1.4, 'hull', 0.05, up=(0, 1, 0)))
        body.append(beam(f'archc{g}_{i}', (x0, gy, z0 + 0.55), (x1, gy, z1 + 0.55), 0.2, 1.6, 'team', 0.02, up=(0, 1, 0)))
        body.append(beam(f'archg{g}_{i}', (x0, gy - 0.72, z0 - 0.1), (x1, gy - 0.72, z1 - 0.1), 0.15, 0.06, 'glow_dim', 0, up=(0, 1, 0)))
    for sx in (-1, 1):
        body.append(slab(f'springer{g}{sx}', (sx * 12.9, gy, 17.4), (2.4, 2.4, 0.8), 'hull_dark', 'hull', chamfer=0.2, bevel=0.04))
    # Mast down from the apex, an emitter collar, then the hanging arm.
    body.append(cyl(f'gmast{g}', (0, gy, 22.6), (0, gy, 14.6), 0.35, 'metal', 8, bevel=0.02))
    body.append(cyl(f'gcol{g}', (0, gy, 15.6), (0, gy, 14.4), 0.9, 'hull_dark', 10, r2=0.6, bevel=0.03))
    body.append(ring(f'gring{g}', (0, gy, 15.0), 1.0, 0.8, 0.2, 'glow', 12))
    body.append(sphere(f'apex{g}', (0, gy, 23.4), 0.5, 'glow', 10, 6))
    body += fp.nano_arm(f'garm{g}', (0, gy, 14.4), (0, 0, 3.0), 3.6, F, hang=True)

fp.finish('lattice_experimental_gantry', OUT, body, s=5.0)

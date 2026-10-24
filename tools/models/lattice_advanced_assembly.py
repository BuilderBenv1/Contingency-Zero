"""Lattice Advanced Assembly. Run: python tools/models/lattice_advanced_assembly.py <out_dir>

The tier 2 plant: faceted team-decked wings flank a wide bay with a large double-ringed pad,
guarded by four emitter pylons. At the back a tall tapering spire carries a floating glow
halo; a high arched gantry over the front of the bay hangs two nanolathe arms, and a third
arm reaches in from each wing. Finished units leave by the door side (-y).
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
WI, WO, WH = 7.0, 11.2, 7.4

# ---- Floor, big pad with an outer ring, emitter pylons, door ----
body.append(slab('floor', (0, 0, 0.2), (22.8, 22.8, 0.4), 'hull_dark', 'hull_dark', chamfer=0.3, bevel=0.05))
body += fp.pad_round('pad', (0, 0, 0.4), 5.2, F)
body.append(ring('outer', (0, 0, 0.4), 6.6, 6.2, 0.08, 'glow_dim', 32))
body.append(box('door', (0, -11.0, 0.45), (13.0, 0.35, 0.1), 'glow', 0, 1))
body += fp.chevrons('chev', -9.9, 4.0, 0.42, 5, 'glow_dim', 0.5)
for i in range(4):
    a = math.pi / 4 + i * math.pi / 2
    px, py = math.cos(a) * 6.4, math.sin(a) * 6.4
    body.append(hull_from(f'pyl{i}', [(px + dx, py + dy, z) for z, r in ((0.4, 0.55), (3.2, 0.22)) for dx, dy in
                                      ((r, 0), (0, r), (-r, 0), (0, -r))], 'hull', bevel=0.03))
    body.append(sphere(f'pylg{i}', (px, py, 3.35), 0.28, 'glow', 8, 5))
    body.append(box(f'pylb{i}', (px, py, 0.55), (1.3, 1.3, 0.3), 'hull_dark', 0.03, 1))

# ---- Wings: faceted, team decks, multiple glow seams on the bay face ----
for sx in (-1, 1):
    xi, xo = sx * WI, sx * WO
    pts = [(xi, -10.8, 0.4), (xo, -10.8, 0.4), (xi, 10.0, 0.4), (xo, 10.0, 0.4),
           (xi, -10.8, 3.2), (xo, -10.8, 4.2), (xi + sx * 0.4, -8.6, WH - 0.6), (xo - sx * 0.6, -7.6, WH),
           (xi + sx * 0.4, 10.0, WH - 0.6), (xo - sx * 0.6, 10.0, WH)]
    w = hull_from(f'wing{sx}', pts, 'hull', bevel=0.1, segs=2)
    fp.two_tone(w, 'hull', 'team', 0.5)
    inset_panels(w, 0.22, 0.05, lambda f: f.calc_area() > 8)
    body.append(w)
    for k in range(3):
        body.append(box(f'seam{sx}{k}', (xi - sx * 0.02, 0.6, 1.8 + k * 1.5), (0.1, 15.0 - k * 3.0, 0.12), 'glow' if k == 1 else 'glow_dim', 0, 1))
    # Deck: spine, ribs, a raised sensor block.
    body.append(slab(f'spine{sx}', (sx * 9.0, 1.2, WH + 0.05), (0.6, 16.6, 0.2), 'hull', 'metal', chamfer=0.1, bevel=0.03))
    for k in range(5):
        body.append(box(f'rib{sx}{k}', (sx * 9.0, -5.2 + k * 3.4, WH - 0.15), (3.6, 0.22, 0.3), 'hull', 0.03, 1))
    body.append(slab(f'sens{sx}', (sx * 9.0, 7.2, WH + 0.35), (2.4, 2.4, 0.7), 'hull_dark', 'hull', chamfer=0.3, bevel=0.03))
    body.append(box(f'sensg{sx}', (sx * 9.0, 5.98, WH + 0.35), (1.6, 0.08, 0.22), 'glow', 0, 1))
    # Wing arm.
    body.append(box(f'armm{sx}', (sx * (WI + 0.1), 2.6, 4.1), (1.0, 1.6, 0.4), 'mech', 0.04, 1))
    body += fp.nano_arm(f'arm{sx}', (sx * (WI - 0.05), 2.6, 4.3), (0, 0, 1.2), 3.0, F)
    # Outer face: vertical fins.
    for k in range(5):
        y = -6.0 + k * 3.6
        body.append(slab(f'fin{sx}{k}', (xo + sx * 0.12, y, 3.4), (0.3, 0.5, 5.4), 'hull_dark', 'hull', chamfer=0.08, bevel=0.02))

# ---- Back: flanking blocks and the tall spire with a floating halo ----
for sx in (-1, 1):
    fb = slab(f'flank{sx}', (sx * 4.6, 9.4, 3.1), (5.0, 4.0, 5.4), 'hull', 'team', chamfer=0.5, bevel=0.08)
    inset_panels(fb, 0.2, 0.05, lambda f: f.calc_area() > 6)
    body.append(fb)
    body.append(box(f'flankg{sx}', (sx * 4.6, 7.38, 3.6), (3.4, 0.1, 0.25), 'glow', 0, 1))
spire = hull_from('spire', [(x * s, 9.4 + y * s, z) for z, s in ((0.4, 1.0), (10.0, 0.62), (15.6, 0.22)) for x, y in
                            ((3.0, 1.0), (1.0, 2.0), (-1.0, 2.0), (-3.0, 1.0), (-3.0, -1.0), (-1.0, -2.0), (1.0, -2.0), (3.0, -1.0))], 'hull', bevel=0.1, segs=2)
fp.two_tone(spire, 'hull', 'team', 0.35)
inset_panels(spire, 0.18, 0.05, lambda f: f.calc_area() > 5)
body.append(spire)
body.append(beam('spireg2', (0, 7.4, 1.2), (0, 8.16, 9.6), 1.2, 0.12, 'hull_dark', 0))
body.append(beam('spireg', (0, 7.34, 1.4), (0, 8.1, 9.4), 0.3, 0.1, 'glow', 0))
body.append(ring('halo', (0, 9.4, 14.2), 2.05, 1.6, 0.3, 'glow', 16))
body.append(ring('halo2', (0, 9.4, 13.4), 1.6, 1.35, 0.15, 'glow_dim', 16))
for i in range(3):
    a = math.pi / 2 + i * 2 * math.pi / 3
    body.append(beam(f'halos{i}', (math.cos(a) * 0.5, 9.4 + math.sin(a) * 0.5, 13.2), (math.cos(a) * 1.7, 9.4 + math.sin(a) * 1.7, 14.2), 0.12, 0.12, 'metal', 0))
body.append(sphere('spiretip', (0, 9.4, 15.8), 0.4, 'glow', 10, 6))

# ---- Front gantry: arched bridge on angled legs from the wing decks, two hanging arms ----
GY = -6.4
for sx in (-1, 1):
    for dy in (-1.0, 1.0):
        body.append(beam(f'gleg{sx}{dy}', (sx * 9.2, GY + dy, WH - 0.3), (sx * 8.4, GY + dy * 0.35, 10.2), 0.6, 0.6, 'hull', 0.04))
    body.append(box(f'glegg{sx}', (sx * 8.8, GY - 1.0, 8.8), (0.12, 0.12, 2.0), 'glow', 0, 1))
gb = slab('gbridge', (0, GY, 10.7), (19.0, 1.8, 1.2), 'hull', 'team', chamfer=0.35, bevel=0.05)
body.append(gb)
for sx in (-1, 1):
    body.append(hull_from(f'gsoff{sx}', [(x, GY + dy, z) for dy in (-0.6, 0.6) for x, z in
                                         ((sx * 8.6, 10.1), (sx * 8.6, 8.9), (sx * 3.6, 10.1))], 'hull_dark', bevel=0.03))
    body.append(slab(f'gpod{sx}', (sx * 3.0, GY, 9.85), (1.8, 1.8, 0.5), 'hull_dark', 'hull', chamfer=0.1, bevel=0.03))
    body += fp.nano_arm(f'garm{sx}', (sx * 3.0, GY, 9.6), (sx * 0.6, -0.5, 1.2), 2.6, F, hang=True)
body.append(box('gglow', (0, GY - 0.92, 10.5), (14.0, 0.06, 0.14), 'glow', 0, 1))
body.append(sphere('geye', (0, GY - 0.7, 10.05), 0.35, 'glow', 10, 6))

fp.finish('lattice_advanced_assembly', OUT, body)

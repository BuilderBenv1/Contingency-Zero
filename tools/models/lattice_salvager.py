"""Lattice "Gleaner" salvager. Run: python tools/models/lattice_salvager.py <out_dir>

A wreck-stripper on short Lattice pods: two long jointed pincer arms reach ahead of the nose,
a downward-angled reclaim emitter glows between them, and a team-painted matter hopper with a
glowing sorting grid fills the back. No weapons.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import vehicles_parts as vp

kit.reset('lattice')
body = []
body += vp.lattice_pods(0.55, 0.3, -1.1, 0.9, 0.34, cleats=14)

def ring(y, hw, ht, z0, z1, z2):
    return [(-hw * 0.85, y, z0), (hw * 0.85, y, z0), (hw, y, z1), (-hw, y, z1), (ht, y, z2), (-ht, y, z2)]
hull = hull_from('hull', ring(0.92, 0.3, 0.2, 0.14, 0.24, 0.32) + ring(0.5, 0.42, 0.32, 0.12, 0.33, 0.44) +
                 ring(-0.88, 0.42, 0.32, 0.12, 0.33, 0.44) + ring(-1.08, 0.36, 0.26, 0.14, 0.3, 0.38), 'hull', 0.025, 2)
inset_panels(hull, 0.03, 0.01, lambda f: abs(f.normal.x) > 0.5 and f.calc_area() > 0.03)
body.append(hull)
body.append(box('belly', (0, -0.1, 0.1), (0.66, 1.8, 0.06), 'mech', 0.01, 1))
body += vp.lattice_eye('noseeye', (0, 0.93, 0.27), 0.28)

# ---- Emitter head: an angled block on the nose with a big glowing lens aimed down-forward ----
body.append(hull_from('ehead', [(-0.2, 0.74, 0.42), (0.2, 0.74, 0.42), (-0.2, 0.36, 0.44), (0.2, 0.36, 0.44),
                                (-0.16, 0.66, 0.64), (0.16, 0.66, 0.64), (-0.16, 0.4, 0.62), (0.16, 0.4, 0.62)], 'team', 0.02, 2))
lens_c = (0, 0.74, 0.56)
body.append(cyl('lensring', (0, 0.7, 0.6), (0, 0.76, 0.54), 0.15, 'mech', 16, r2=0.14, bevel=0.006))
body.append(cyl('lens', (0, 0.755, 0.545), (0, 0.775, 0.525), 0.115, 'glow_dim', 16, bevel=0))
body.append(sphere('lenscore', (0, 0.785, 0.515), 0.045, 'glow', 10, 6))
for k in range(3):
    a = k * 2 * math.pi / 3 + 0.5
    body.append(cyl(f'spoke{k}', (math.cos(a) * 0.12, 0.76 + math.sin(a) * 0.08, 0.54 + math.sin(a) * 0.08),
                    (math.cos(a) * 0.04, 0.84 + math.sin(a) * 0.03, 0.48 + math.sin(a) * 0.03), 0.01, 'metal', 5, bevel=0))

# ---- Pincer arms: shoulder on the hull side, upper arm forward, claw closing inwards ----
for sx in (-1, 1):
    sh = (sx * 0.34, 0.42, 0.46)
    el = (sx * 0.3, 1.0, 0.36)
    wr = (sx * 0.24, 1.26, 0.24)
    body.append(sphere(f'shoulder{sx}', sh, 0.08, 'mech', 10, 6))
    body.append(vp.tapered(f'upper{sx}', sh, el, 0.12, 0.1, 0.09, 0.08, 'team', 0.012))
    body.append(sphere(f'elbow{sx}', el, 0.06, 'mech', 10, 6))
    body.append(vp.tapered(f'fore{sx}', el, wr, 0.08, 0.07, 0.06, 0.06, 'hull', 0.01))
    body.append(cyl(f'piston{sx}', (sx * 0.38, 0.5, 0.42), (sx * 0.34, 0.92, 0.42), 0.016, 'metal', 6, bevel=0))
    # Claw: two fingers, one sweeping inwards, one down.
    body.append(box(f'wristb{sx}', wr, (0.09, 0.08, 0.09), 'hull_dark', 0.01, 1))
    body.append(vp.tapered(f'finger{sx}', wr, (sx * 0.08, 1.38, 0.2), 0.05, 0.06, 0.02, 0.03, 'metal', 0.006))
    body.append(vp.tapered(f'finger2{sx}', wr, (sx * 0.18, 1.36, 0.06), 0.05, 0.05, 0.02, 0.03, 'metal', 0.006))
    body.append(box(f'clawg{sx}', (sx * 0.24, 1.3, 0.24), (0.02, 0.02, 0.02), 'glow', 0, 1))

# ---- Matter hopper at the back ----
hop = hull_from('hopper', [(-0.42, 0.2, 0.42), (0.42, 0.2, 0.42), (-0.42, -0.98, 0.42), (0.42, -0.98, 0.42),
                           (-0.38, 0.14, 0.78), (0.38, 0.14, 0.78), (-0.38, -0.92, 0.78), (0.38, -0.92, 0.78)], 'team', 0.025, 2)
inset_panels(hop, 0.035, 0.012, lambda f: abs(f.normal.x) > 0.8)
body.append(hop)
body.append(box('rim', (0, -0.39, 0.79), (0.72, 1.0, 0.03), 'hull', 0.008, 1))
body.append(box('pit', (0, -0.39, 0.8), (0.62, 0.9, 0.02), 'mech', 0.004, 1))
for i in range(4):
    body.append(box(f'gridx{i}', (-0.21 + i * 0.14, -0.39, 0.815), (0.012, 0.86, 0.012), 'glow_dim', 0, 1))
for i in range(5):
    body.append(box(f'gridy{i}', (0, -0.75 + i * 0.18, 0.815), (0.6, 0.012, 0.012), 'glow_dim', 0, 1))
# Sorted matter blocks sitting in the pit.
for (x, y, s) in ((-0.15, -0.2, 0.1), (0.12, -0.5, 0.12), (0.18, -0.15, 0.08), (-0.1, -0.68, 0.09)):
    body.append(hull_from(f'shard{x}', [(x - s / 2, y - s / 2, 0.81), (x + s / 2, y - s / 3, 0.81), (x, y + s / 2, 0.81),
                                        (x + s * 0.1, y, 0.81 + s * 0.9)], 'metal', 0.006, 1))
for sx in (-1, 1):
    body.append(box(f'hopline{sx}', (sx * 0.405, -0.39, 0.62), (0.008, 0.9, 0.02), 'glow', 0, 1))
body.append(box('rearlight', (0, -1.0, 0.6), (0.3, 0.02, 0.03), 'glow_dim', 0, 1))

vp.finish('lattice_salvager', body)

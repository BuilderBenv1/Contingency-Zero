"""Lattice Metal Maker: a glowing transmutation crystal held in a four-armed cradle. Run: python <this> <out_dir>

A bevelled square plinth with team-inset deck panels and a lit conduit ring; four angular team
arms rise from the corners to clamp a containment ring, their emitter tips aimed at a floating
faceted crystal. Footprint 2 (16 units): kept inside 15 across.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('lattice')
body = []
S = 6.7
# ---- Plinth: chamfered square slab with team deck panels ----
pl = hull_from('plinth', [(sx * S, sy * (S - 1.2), 0) for sx in (-1, 1) for sy in (-1, 1)] +
               [(sx * (S - 1.2), sy * S, 0) for sx in (-1, 1) for sy in (-1, 1)] +
               [(sx * (S - 0.5), sy * (S - 1.5), 1.0) for sx in (-1, 1) for sy in (-1, 1)] +
               [(sx * (S - 1.5), sy * (S - 0.5), 1.0) for sx in (-1, 1) for sy in (-1, 1)], 'hull', 0.08, 2)
body.append(pl)
for sx in (-1, 1):
    for sy in (-1, 1):
        deck = hull_from(f'deck{sx}{sy}', [(sx * 0.6, sy * 0.6, 1.0), (sx * 4.7, sy * 0.6, 1.0), (sx * 0.6, sy * 4.7, 1.0), (sx * 3.6, sy * 3.6, 1.0),
                                          (sx * 0.6, sy * 0.6, 1.1), (sx * 4.7, sy * 0.6, 1.1), (sx * 0.6, sy * 4.7, 1.1), (sx * 3.6, sy * 3.6, 1.1)], 'team', 0.03)
        body.append(deck)
body.append(ep.ring('conduit', (0, 0), 3.4, 2.8, 1.0, 1.18, 'glow', 8, phase=math.pi / 8))
body.append(ep.ngon('well', (0, 0), 2.6, 2.2, 1.0, 1.35, 8, 'hull_dark'))
body.append(ep.ngon('lens', (0, 0), 1.6, 1.6, 1.35, 1.4, 8, 'glow', bevel=0))
for k in range(4):
    a = k * math.pi / 2
    body.append(box(f'cross{k}', (math.cos(a) * 4.9, math.sin(a) * 4.9, 1.06), (1.0 if k % 2 == 0 else 3.4, 3.4 if k % 2 == 0 else 1.0, 0.1), 'mech', 0.02))
    body.append(box(f'edgelight{k}', (math.cos(a) * 6.25, math.sin(a) * 6.25, 0.55), (0.12 if k % 2 == 0 else 2.6, 2.6 if k % 2 == 0 else 0.12, 0.12), 'glow', 0, 1))
# ---- Cradle arms ----
for k, (sx, sy) in enumerate(((-1, -1), (1, -1), (1, 1), (-1, 1))):
    foot = Vector((sx * 5.0, sy * 5.0, 1.0))
    elbow = Vector((sx * 4.3, sy * 4.3, 4.2))
    tip = Vector((sx * 2.55, sy * 2.55, 6.4))
    body.append(box(f'shoulder{k}', (foot.x, foot.y, 1.45), (1.5, 1.5, 0.9), 'hull_dark', 0.06))
    body.append(ep.beam(f'arma{k}', foot + Vector((0, 0, 0.6)), elbow, 1.0, 0.8, 'team', 0.06, 2, w2=0.85, h2=0.7))
    body.append(ep.beam(f'armb{k}', elbow, tip, 0.85, 0.7, 'team', 0.06, 2, w2=0.6, h2=0.55))
    body.append(box(f'elbow{k}', tuple(elbow), (0.9, 0.9, 0.9), 'mech', 0.06))
    body.append(ep.beam(f'armrib{k}', foot + Vector((sx * 0.4, sy * 0.4, 0.8)), elbow + Vector((sx * 0.4, sy * 0.4, 0)), 0.25, 0.25, 'hull', 0.02))
    # Emitter head aimed at the crystal.
    d = (Vector((0, 0, 4.2)) - tip).normalized()
    body.append(box(f'head{k}', tuple(tip), (0.8, 0.8, 0.6), 'hull', 0.05))
    body.append(cyl(f'emit{k}', tuple(tip), tuple(tip + d * 0.9), 0.24, 'mech', 8, r2=0.14, bevel=0))
    body.append(sphere(f'emitglow{k}', tuple(tip + d * 0.95), 0.14, 'glow', 6, 4))
    # Clamp from the arm to the containment ring.
    body.append(ep.beam(f'clamp{k}', elbow + Vector((-sx * 0.4, -sy * 0.4, 0)), (sx * 2.25, sy * 2.25, 4.1), 0.32, 0.3, 'hull_dark', 0.02))
# Containment ring round the crystal.
body.append(ep.ring('contain', (0, 0), 3.3, 2.8, 3.85, 4.35, 'hull', 8, phase=math.pi / 8, bevel=0.03))
body.append(ep.ring('containglow', (0, 0), 2.85, 2.7, 3.95, 4.25, 'glow_dim', 8, phase=math.pi / 8))
# ---- The crystal: a faceted, elongated gem, with a smaller shard below ----
gem = []
for k in range(6):
    a = k * math.pi / 3
    gem.append((math.cos(a) * 1.45, math.sin(a) * 1.45, 4.1))
    gem.append((math.cos(a + math.pi / 6) * 1.1, math.sin(a + math.pi / 6) * 1.1, 5.0))
gem += [(0, 0, 6.4), (0, 0, 2.3)]
body.append(hull_from('crystal', gem, 'glow', 0))
body.append(hull_from('shard', [(0.3, 0, 1.75), (-0.3, 0, 1.75), (0, 0.3, 1.75), (0, -0.3, 1.75), (0, 0, 2.1), (0, 0, 1.45)], 'glow', 0))

B = kit.join('Body', body)
ep.finish('lattice_metal_maker', [B], 1024, bake_scale=0.3)

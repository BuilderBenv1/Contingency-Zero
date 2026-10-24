"""Crucible Metal Maker: a molten crucible pot slung on trunnions. Run: python <this> <out_dir>

A riveted flaring pot brimming with glowing melt and floating slag, a team band and a team lip,
hung on trunnions between two A-frame stands, a pouring spout over an ingot mould, a hot stack,
and a control hut. Footprint 2 (16 units): kept inside 15 across.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('crucible')
body = []
E = 7.2
body.append(box('apron', (0, 0, 0.3), (2 * E, 2 * E, 0.6), 'hull_dark', 0.08))
for s in (-1, 1):
    body += ep.rivet_line(f'arx{s}', (-E + 0.4, s * (E - 0.25), 0.6), (E - 0.4, s * (E - 0.25), 0.6), 12, 'metal', 0.07)
    body += ep.rivet_line(f'ary{s}', (s * (E - 0.25), -E + 0.9, 0.6), (s * (E - 0.25), E - 0.9, 0.6), 10, 'metal', 0.07)
# ---- Fire pit under the pot ----
body.append(cyl('pit', (0, 0, 0.6), (0, 0, 0.9), 3.9, 'mech', 16, bevel=0.02))
body.append(cyl('pitglow', (0, 0, 0.9), (0, 0, 0.94), 3.3, 'glow_dim', 16, bevel=0))
# ---- The pot ----
Z0, Z1 = 1.1, 4.9
body.append(cyl('pot', (0, 0, Z0), (0, 0, Z1), 3.5, 'hull_dark', 20, r2=4.6, bevel=0.05))
body.append(cyl('potbase', (0, 0, Z0 - 0.15), (0, 0, Z0 + 0.4), 3.3, 'mech', 20, bevel=0.03))
body.append(cyl('band', (0, 0, 2.5), (0, 0, 3.6), 4.08, 'team', 20, r2=4.32, bevel=0.03))
body += ep.rivet_ring('bandr1', (0, 0), 4.1, 2.62, 18, 'metal', 0.08)
body += ep.rivet_ring('bandr2', (0, 0), 4.3, 3.48, 18, 'metal', 0.08)
body.append(ep.ring('lip', (0, 0), 4.95, 3.95, Z1, Z1 + 0.45, 'team', 20, bevel=0.03))
body.append(ep.ring('lipedge', (0, 0), 5.0, 4.75, Z1 - 0.2, Z1 + 0.05, 'mech', 20))
body += ep.rivet_ring('lipr', (0, 0), 4.5, Z1 + 0.47, 16, 'metal', 0.08)
# Melt with slag crust.
body.append(cyl('melt', (0, 0, Z1 - 0.3), (0, 0, Z1 + 0.15), 4.0, 'glow', 20, bevel=0))
for k, (x, y, r) in enumerate(((-1.6, 0.8, 0.7), (1.2, 1.7, 0.55), (0.6, -1.4, 0.8), (-0.5, -0.3, 0.45), (2.2, -0.2, 0.5), (-2.2, -1.6, 0.4))):
    body.append(hull_from(f'slag{k}', [(x + math.cos(a) * r, y + math.sin(a) * r * 0.8, Z1 + 0.12) for a in (0, 1.3, 2.5, 3.7, 5.0)] +
                          [(x + math.cos(a) * r * 0.6, y + math.sin(a) * r * 0.5, Z1 + 0.32) for a in (0.5, 2.0, 3.4, 4.8)], 'tread', 0.03, 1))
# Spout at the front, dribbling into an ingot mould.
body.append(hull_from('spout', [(-0.7, -4.2, Z1 - 0.4), (0.7, -4.2, Z1 - 0.4), (-0.7, -4.2, Z1 + 0.4), (0.7, -4.2, Z1 + 0.4),
                                (-0.35, -5.5, Z1 - 0.2), (0.35, -5.5, Z1 - 0.2), (-0.45, -5.5, Z1 + 0.2), (0.45, -5.5, Z1 + 0.2)], 'hull', 0.04))
body.append(box('spoutglow', (0, -5.0, Z1 + 0.18), (0.5, 1.2, 0.06), 'glow', 0, 1))
body.append(cyl('pour', (0, -5.45, Z1 - 0.2), (0, -5.55, 1.2), 0.13, 'glow', 8, bevel=0))
body.append(box('mould', (0, -5.6, 0.85), (2.6, 1.3, 0.5), 'mech', 0.04))
for j in range(3):
    body.append(box(f'mouldcav{j}', (-0.8 + j * 0.8, -5.6, 1.09), (0.55, 0.9, 0.04), 'glow' if j == 1 else 'glow_dim', 0, 1))
# ---- Trunnions and A-frame stands ----
for sx in (-1, 1):
    x = sx * 4.75
    body.append(cyl(f'trun{sx}', (sx * 4.2, 0, 3.6), (sx * 5.7, 0, 3.6), 0.55, 'metal', 12, bevel=0.03))
    body.append(cyl(f'trunhub{sx}', (sx * 5.7, 0, 3.6), (sx * 6.0, 0, 3.6), 0.75, 'mech', 12, bevel=0.03))
    for sy in (-1, 1):
        body.append(ep.beam(f'aleg{sx}{sy}', (sx * 5.85, sy * 2.6, 0.6), (sx * 5.85, sy * 0.35, 3.6), 0.6, 0.5, 'hull', 0.04))
    body.append(box(f'acap{sx}', (sx * 5.85, 0, 4.25), (1.0, 1.4, 0.6), 'team', 0.05))
    body.append(box(f'afoot{sx}', (sx * 5.85, 0, 0.75), (1.1, 5.8, 0.3), 'hull_dark', 0.04))
    body.append(box(f'abrace{sx}', (sx * 5.85, 0, 1.6), (0.3, 3.2, 0.3), 'mech', 0.02))
    body += ep.rivet_line(f'afr{sx}', (sx * 5.85, -2.6, 0.92), (sx * 5.85, 2.6, 0.92), 6, 'metal', 0.07)
# ---- Stack and control hut at the back ----
body += ep.stack('stack', (5.4, 5.4, 0.6), 0.6, 5.2)
body.append(box('hut', (-4.9, 5.2, 1.5), (3.4, 2.4, 1.8), 'hull', 0.06))
body.append(box('hutroof', (-4.9, 5.2, 2.5), (3.6, 2.6, 0.2), 'team', 0.03))
body.append(box('hutwin', (-4.9, 3.98, 1.7), (2.2, 0.05, 0.5), 'glass', 0, 1))
body.append(box('hutlamp', (-3.4, 3.98, 2.15), (0.3, 0.06, 0.18), 'glow', 0, 1))
body += ep.pipe('feed', [(-3.2, 5.0, 1.0), (-2.2, 3.4, 1.0), (-2.0, 3.0, 1.6)], 0.2, 'accent')

B = kit.join('Body', body)
ep.finish('crucible_metal_maker', [B], 1024, bake_scale=0.3)

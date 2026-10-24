"""Lattice Solar Collector: four tapered solar petals opening from a faceted hub. Run: python <this> <out_dir>

An octagonal hub with a team crown and a glowing focus crystal; four petals on the diagonals
tilt up and out, each a team-framed bed of dark cells with metal tracery and a lit outer edge,
held on struts. Footprint 2 (16 units): kept inside 15 across.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('lattice')
body = []
# ---- Hub ----
body.append(ep.ngon('foot', (0, 0), 2.6, 2.3, 0.0, 0.45, 8, 'hull_dark', bevel=0.05))
hub = hull_from('hub', [(math.cos(a) * 2.2, math.sin(a) * 2.2, 0.45) for a in [math.pi / 8 + k * math.pi / 4 for k in range(8)]] +
                [(math.cos(a) * 1.6, math.sin(a) * 1.6, 1.7) for a in [math.pi / 8 + k * math.pi / 4 for k in range(8)]], 'hull', 0.05)
inset_panels(hub, 0.08, 0.03, lambda f: abs(f.normal.z) < 0.9)
body.append(hub)
body.append(ep.ngon('crown', (0, 0), 1.6, 1.15, 1.7, 2.05, 8, 'team', bevel=0.03))
body.append(ep.ring('crownglow', (0, 0), 1.18, 0.95, 2.05, 2.12, 'glow', 8, phase=math.pi / 8))
body.append(ep.ngon('mount', (0, 0), 0.6, 0.45, 2.05, 2.5, 6, 'mech', phase=0))
# Focus crystal: an elongated octahedron, glowing, held by three prongs.
body.append(hull_from('crystal', [(0, 0, 2.45), (0, 0, 3.75), (0.38, 0, 3.0), (-0.38, 0, 3.0), (0, 0.38, 3.0), (0, -0.38, 3.0)], 'glow', 0))
for k in range(3):
    a = k * 2 * math.pi / 3 + math.pi / 2
    body.append(ep.beam(f'prong{k}', (math.cos(a) * 0.55, math.sin(a) * 0.55, 2.3), (math.cos(a) * 0.42, math.sin(a) * 0.42, 3.35), 0.12, 0.08, 'metal', 0.01))
for k in range(4):
    a = k * math.pi / 2
    body.append(box(f'hubvent{k}', (math.cos(a) * 1.95, math.sin(a) * 1.95, 0.85), (0.5 if k % 2 else 0.12, 0.12 if k % 2 else 0.5, 0.3), 'glow', 0, 1))

# ---- Petals ----
L, W0, W1 = 5.0, 2.3, 4.4
TILT = math.radians(20)


def petal(i, ang):
    out = []
    frame = hull_from(f'pframe{i}', [(-W0 / 2, 0, 0), (W0 / 2, 0, 0), (-W1 / 2, L, 0), (W1 / 2, L, 0),
                                      (-W0 / 2, 0, 0.26), (W0 / 2, 0, 0.26), (-W1 / 2, L, 0.26), (W1 / 2, L, 0.26)], 'team', 0.04)
    out.append(frame)
    m = 0.32
    cells = hull_from(f'pcell{i}', [(-W0 / 2 + m, m, 0.2), (W0 / 2 - m, m, 0.2), (-W1 / 2 + m, L - m, 0.2), (W1 / 2 - m, L - m, 0.2),
                                     (-W0 / 2 + m, m, 0.31), (W0 / 2 - m, m, 0.31), (-W1 / 2 + m, L - m, 0.31), (W1 / 2 - m, L - m, 0.31)], 'solar', 0)
    out.append(cells)
    # Tracery: a spine and cross bars.
    out.append(box(f'pspine{i}', (0, L / 2, 0.33), (0.08, L - 2 * m, 0.04), 'metal', 0, 1))
    for t in (0.28, 0.52, 0.76):
        y = m + (L - 2 * m) * t
        w = W0 + (W1 - W0) * (y / L) - 2 * m
        out.append(box(f'pbar{i}{t}', (0, y, 0.33), (w, 0.06, 0.04), 'metal', 0, 1))
    for sx in (-1, 1):
        out.append(ep.beam(f'prib{i}{sx}', (sx * W0 * 0.22, m, 0.33), (sx * W1 * 0.24, L - m, 0.33), 0.05, 0.04, 'metal', 0))
    # Lit outer lip and a dark keel underneath.
    out.append(box(f'plip{i}', (0, L - 0.05, 0.13), (W1 - 0.3, 0.12, 0.12), 'glow', 0, 1))
    out.append(ep.beam(f'pkeel{i}', (0, 0.2, -0.05), (0, L - 0.4, -0.05), 0.5, 0.3, 'hull_dark', 0.03))
    out.append(box(f'phinge{i}', (0, 0.0, 0.05), (W0 * 0.8, 0.4, 0.36), 'mech', 0.03))
    xf = Matrix.Rotation(ang, 4, 'Z') @ Matrix.Translation((0, 2.0, 1.15)) @ Matrix.Rotation(TILT, 4, 'X')
    for o in out:
        o.data.transform(xf)
    # Struts from the hub foot to the keel, and a ground foot under the outer end.
    keel_mid = xf @ Vector((0, L * 0.62, -0.18))
    keel_out = xf @ Vector((0, L * 0.9, -0.18))
    base = Matrix.Rotation(ang, 4, 'Z') @ Vector((0, 2.4, 0.4))
    gfoot = Matrix.Rotation(ang, 4, 'Z') @ Vector((0, L * 0.88 + 2.0, 0.0))
    out.append(ep.beam(f'pstrut{i}', base, keel_mid, 0.22, 0.22, 'metal', 0.02))
    out.append(ep.rod(f'pleg{i}', gfoot + Vector((0, 0, 0.35)), keel_out, 0.12, 'metal'))
    out.append(ep.ngon(f'pfoot{i}', (gfoot.x, gfoot.y), 0.45, 0.32, 0.0, 0.38, 6, 'hull_dark', phase=ang, bevel=0.03))
    return out


for i in range(4):
    body += petal(i, i * math.pi / 2 + math.pi / 4)

B = kit.join('Body', body)
ep.finish('lattice_solar_collector', [B], 1024, bake_scale=0.3)

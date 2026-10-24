"""Lattice Metal Extractor: a slim tripod drilling rig over the ore. Run: python <this> <out_dir>

Three bevelled legs on hexagonal feet straddle a blue-lit bore; a hex drill column rises through a
team-capped collar with a sensor eye to a spinning three-bladed head (Rotor_Y) with glowing tips.
A sloped processing pod sits between the front legs. Footprint 1 (8 units): within 7 across.
The deep version (lattice_deep_extractor) shares this build.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels
import economy_parts as ep


def pol(r, a, z):
    return (math.cos(a) * r, math.sin(a) * r, z)


def build(deep):
    kit.reset('lattice')
    body = []
    H = 6.3 if deep else 4.3          # rotor pivot height
    CZ = 3.7 if deep else 2.9         # collar height
    FEET = [math.radians(a) for a in (90, 210, 330)]
    # ---- Bore: a ring over the ore with blue light welling up ----
    body.append(ep.ring('bore', (0, 0), 1.35, 0.9, 0.0, 0.28, 'hull_dark', 6, phase=math.pi / 6, bevel=0.03))
    body.append(ep.ngon('boreglow', (0, 0), 0.92, 0.92, 0.01, 0.05, 6, 'glow', bevel=0))
    body.append(ep.ngon('casing', (0, 0), 0.62, 0.55, 0.0, 0.75, 6, 'mech', phase=0))
    # ---- Legs: bevelled slabs with a team-painted spine, on hex feet ----
    for i, a in enumerate(FEET):
        foot = pol(2.85, a, 0.42)
        top = pol(0.95, a, CZ - 0.05)
        body.append(ep.ngon(f'foot{i}', foot[:2], 0.72, 0.5, 0.0, 0.42, 6, 'hull_dark', phase=a, bevel=0.04))
        body.append(ep.ring(f'footglow{i}', foot[:2], 0.62, 0.52, 0.2, 0.26, 'glow', 6, phase=a))
        body.append(ep.beam(f'leg{i}', foot, top, 0.46, 0.34, 'hull', 0.05, 2, w2=0.34, h2=0.3))
        # Team spine on the upper face of the leg.
        d = (top[0] - foot[0], top[1] - foot[1], top[2] - foot[2])
        L = math.sqrt(sum(c * c for c in d))
        side, up = ep._frame(kit.Vector(d))
        off = up * 0.2
        p0 = kit.Vector(foot) + kit.Vector(d) * 0.12 + off
        p1 = kit.Vector(foot) + kit.Vector(d) * 0.92 + off
        body.append(ep.beam(f'spine{i}', p0, p1, 0.36, 0.08, 'team', 0.02, 1, w2=0.26))
        body.append(box(f'knee{i}', (foot[0], foot[1], 0.55), (0.42, 0.42, 0.3), 'mech', 0.04))
        # Hydraulic brace from foot to the column base.
        body.append(ep.rod(f'ram{i}', pol(2.2, a + 0.18, 0.45), pol(0.62, a + 0.25, 1.3), 0.07, 'metal'))
        body.append(ep.rod(f'ramc{i}', pol(2.2, a + 0.18, 0.45), pol(1.5, a + 0.21, 0.78), 0.11, 'mech'))
    # ---- Column ----
    if deep:
        # A glowing core inside a cage of six ribs.
        body.append(cyl('core', (0, 0, 0.6), (0, 0, H - 0.3), 0.3, 'glow', 6, bevel=0))
        for k in range(6):
            a = k * math.pi / 3
            body.append(ep.beam(f'rib{k}', pol(0.5, a, 0.6), pol(0.42, a, H - 0.3), 0.16, 0.16, 'metal', 0.02))
        for z in (1.4, 2.6, 4.8):
            body.append(ep.ring(f'cring{z}', (0, 0), 0.62, 0.36, z, z + 0.22, 'hull', 6, phase=0))
        # Core ring half way: a broad team-painted hex flange.
        fl = hull_from('flange', [pol(1.75, k * math.pi / 3, 2.05) for k in range(6)] + [pol(1.45, k * math.pi / 3, 2.4) for k in range(6)] +
                       [pol(0.7, k * math.pi / 3, 2.05) for k in range(6)], 'team', 0.04)
        inset_panels(fl, 0.08, 0.02, lambda f: f.normal.z > 0.5)
        body.append(fl)
        body.append(ep.ngon('flangeb', (0, 0), 1.6, 1.75, 1.8, 2.05, 6, 'hull_dark', phase=0))
        for k in range(6):
            body.append(box(f'flight{k}', pol(1.72, k * math.pi / 3 + math.pi / 6, 1.95), (0.22, 0.22, 0.12), 'glow', 0.01, 1))
    else:
        body.append(ep.ngon('column', (0, 0), 0.5, 0.4, 0.7, H - 0.3, 6, 'metal', phase=0))
        for z in (1.35, 2.15):
            body.append(ep.ring(f'seam{z}', (0, 0), 0.56, 0.4, z, z + 0.12, 'glow', 6, phase=0))
    # Collar where the legs meet: hex frustum, team cap with inset panels, and a sensor eye.
    col = hull_from('collar', [pol(1.35, k * math.pi / 3, CZ - 0.35) for k in range(6)] + [pol(1.05, k * math.pi / 3, CZ + 0.3) for k in range(6)], 'hull', 0.05)
    inset_panels(col, 0.07, 0.025, lambda f: abs(f.normal.z) < 0.6)
    body.append(col)
    for k in (0, 2):
        a = k * math.pi / 3 + math.pi / 6
        c = pol(1.2, a, CZ - 0.02)
        s = box(f'slit{k}', (0, 0, 0), (0.7, 0.05, 0.07), 'glow', 0, 1)
        s.data.transform(kit.Matrix.Translation(c) @ kit.Matrix.Rotation(a + math.pi / 2, 4, 'Z'))
        body.append(s)
    cap = ep.ngon('cap', (0, 0), 1.05, 0.82, CZ + 0.3, CZ + 0.48, 6, 'team', phase=0, bevel=0.03)
    body.append(cap)
    body.append(box('eyebox', (0, -1.22, CZ), (0.62, 0.3, 0.32), 'hull_dark', 0.04))
    body.append(box('eye', (0, -1.38, CZ), (0.44, 0.04, 0.12), 'glow', 0, 1))
    body.append(ep.rod('aerial', (0.7, 0.45, CZ + 0.3), (0.85, 0.55, CZ + 1.6), 0.025, 'mech', 5))
    body.append(sphere('aerialtip', (0.85, 0.55, CZ + 1.62), 0.06, 'glow', 6, 4))
    # Neck to the head bearing.
    body.append(ep.ngon('neck', (0, 0), 0.45, 0.38, CZ + 0.48, H - 0.25, 6, 'hull', phase=0))
    body.append(ep.ngon('bearing', (0, 0), 0.7, 0.62, H - 0.3, H, 6, 'mech', phase=0))
    # ---- Processing pod between the front legs, piped to the column ----
    py = -2.35
    pod = hull_from('pod', [(-0.85, py - 0.55, 0.0), (0.85, py - 0.55, 0.0), (-0.85, py + 0.55, 0.0), (0.85, py + 0.55, 0.0),
                            (-0.7, py - 0.35, 0.7), (0.7, py - 0.35, 0.7), (-0.75, py + 0.5, 1.15), (0.75, py + 0.5, 1.15)], 'hull', 0.05)
    body.append(pod)
    body.append(hull_from('podtop', [(-0.62, py - 0.28, 0.73), (0.62, py - 0.28, 0.73), (-0.66, py + 0.44, 1.17), (0.66, py + 0.44, 1.17),
                                     (-0.62, py - 0.28, 0.79), (0.62, py - 0.28, 0.79), (-0.66, py + 0.44, 1.23), (0.66, py + 0.44, 1.23)], 'team', 0.02))
    body.append(box('podslit', (0, py - 0.47, 0.42), (1.1, 0.12, 0.08), 'glow', 0, 1))
    body.append(box('podvent', (0, py - 0.45, 0.22), (1.2, 0.12, 0.12), 'mech', 0.02, 1))
    body += ep.pipe('podpipe', [(0.35, py + 0.45, 0.8), (0.35, -0.9, 1.0), (0.3, -0.48, 1.25)], 0.1, 'metal')
    body += ep.pipe('podpipe2', [(-0.35, py + 0.45, 0.8), (-0.35, -0.9, 1.0), (-0.3, -0.48, 1.25)], 0.1, 'metal')

    # ---- Rotor: three swept blades with team tops and glowing tips ----
    PIV = (0, 0, H)
    rot = []
    reach = 2.65 if deep else 2.2
    rot.append(ep.ngon('hub', (0, 0), 0.62, 0.45, H, H + 0.5, 6, 'hull', phase=0, bevel=0.03))
    rot.append(ep.ngon('hubglow', (0, 0), 0.32, 0.32, H + 0.5, H + 0.56, 6, 'glow', phase=0, bevel=0))
    for i in range(3):
        a = i * 2 * math.pi / 3 + math.pi / 2
        sw = 0.28  # sweep: tips trail the root
        root_w, tip_w = 0.6, 0.26
        pts = []
        for r, w, z0, z1, aa in ((0.35, root_w, 0.08, 0.42, a), (reach, tip_w, -0.12, 0.08, a - sw)):
            c = kit.Vector(pol(r, aa, H))
            perp = kit.Vector((-math.sin(aa), math.cos(aa), 0))
            for s in (-1, 1):
                for z in (z0, z1):
                    pts.append(tuple(c + perp * s * w / 2 + kit.Vector((0, 0, z))))
        blade = hull_from(f'blade{i}', pts, 'hull', 0.03)
        rot.append(blade)
        # Team top plate along the blade.
        tp = []
        for r, w, z, aa in ((0.5, root_w * 0.8, 0.44, a - 0.02), (reach - 0.2, tip_w * 0.8, 0.1, a - sw * 0.93)):
            c = kit.Vector(pol(r, aa, H))
            perp = kit.Vector((-math.sin(aa), math.cos(aa), 0))
            for s in (-1, 1):
                for dz in (0, 0.06):
                    tp.append(tuple(c + perp * s * w / 2 + kit.Vector((0, 0, z + dz))))
        rot.append(hull_from(f'bladetop{i}', tp, 'team', 0.01, 1))
        rot.append(sphere(f'tip{i}', pol(reach + 0.05, a - sw, H - 0.02), 0.2, 'glow', 8, 5))
        rot.append(ep.ngon(f'tipcup{i}', pol(reach + 0.05, a - sw, 0)[:2], 0.26, 0.22, H - 0.22, H - 0.05, 6, 'mech', bevel=0.01))
    if deep:
        rot.append(ep.ring('halo', (0, 0), reach + 0.32, reach + 0.12, H - 0.12, H + 0.04, 'hull', 18, bevel=0.01))
    B = kit.join('Body', body)
    R = kit.join('Rotor_Y', rot, parent_origin=PIV)
    return [B, R]


if __name__ == '__main__':
    ep.finish('lattice_metal_extractor', build(False), 512, bake_scale=0.5)

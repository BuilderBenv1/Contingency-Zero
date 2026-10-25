"""Trees for the four map themes: tree_<theme>_<0..2>.glb (+ _body.png).
Run: python tools/models/features/trees.py game/models/features [--only tree_temperate_0,...]

temperate: 0 a tiered fir, 1 a Scots pine (tall bare trunk, flat clumped crown), 2 a broadleaf oak.
desert:    0 a dead, twisted tree, 1 a saguaro-like cactus, 2 a scrubby clump palm.
arctic:    0 a tall snow-laden fir, 1 a squat spruce heavy with snow, 2 a pair of frosted pines.
lava:      0 a snapped, charred trunk, 1 a dead forked snag, 2 a split, leaning trunk; all with
           glowing embers in an ash mound at the roots and cracks glowing in the lower bark.
Sizes follow Features.Tree (roughly 4-7 tall). Faceted low-poly, painted light / dark per plane.
"""
import math, os, sys, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector
import nature_parts as N
from nature_parts import TAU, shade, with_top, lump, tube, tube_bm, bend_path, conifer_tier, from_bm, frond_bm, strip_bm

OUT = N.out_dir()
ONLY = N.only(sys.argv)


def roots(p, rng, r, paint, n=4, z=0.0):
    """Root flares: short tapering spurs splaying out from the trunk foot into the ground."""
    for i in range(n):
        a = rng.random() * TAU if i == 0 else a0 + i * TAU / n + rng.uniform(-0.3, 0.3)
        if i == 0: a0 = a
        d = Vector((math.cos(a), math.sin(a), 0))
        pts = [Vector((0, 0, z + r * 1.6)) + d * r * 0.3, Vector((0, 0, z + r * 0.4)) + d * r * 1.3, Vector((0, 0, z - r * 0.5)) + d * r * 2.4]
        p.append(tube(f'root{i}', pts, [r * 0.55, r * 0.35, r * 0.12], paint, sides=5, cap=False))


def bark_crack(name, pts, radii, ang, i0, i1, width, rng, paint='ember'):
    """A glowing crack running up a tube's bark between rings i0 and i1 at angle ang."""
    frames = N._frames([Vector(q) for q in pts])
    ps, ns = [], []
    for i in range(i0, i1 + 1):
        for t in ((0.0, 0.5) if i < i1 else (0.0,)):
            j = min(i + 1, len(pts) - 1)
            p = Vector(pts[i]).lerp(Vector(pts[j]), t)
            r = radii[i] + (radii[j] - radii[i]) * t
            _, side, up = frames[i]
            a = ang + rng.uniform(-0.25, 0.25)
            n = side * math.cos(a) + up * math.sin(a)
            ps.append(p + n * r * 0.97); ns.append(n)
    bm = bmesh.new()
    strip_bm(bm, ps, ns, width, lift=0.02)
    return from_bm(name, bm, paint)


def ash_mound(p, rng, r, embers=4, cinder=True):
    """A low ash mound round the roots with ember chunks glowing in it."""
    p.append(lump('ash', rng, (0, 0, -0.25), r, r * 0.9, 0.42, shade('l_ash', 'l_ash_dk', 'l_basalt'), n=14, floor=-0.12, jit=0.15))
    for i in range(embers):
        a = rng.random() * TAU; d = rng.uniform(0.45, 0.85) * r
        p.append(lump(f'ember{i}', rng, (math.cos(a) * d, math.sin(a) * d, 0.05), 0.2, 0.15, 0.12, 'ember', n=7, floor=-0.05, jit=0.3))
    if cinder:
        for i in range(3):
            a = rng.random() * TAU; d = rng.uniform(0.9, 1.2) * r
            p.append(lump(f'cinder{i}', rng, (math.cos(a) * d, math.sin(a) * d, 0.0), 0.25, 0.2, 0.16,
                          shade('l_char_lt', 'l_char', 'l_char'), n=8, floor=-0.08))


# ---- temperate --------------------------------------------------------------------------------

def tree_temperate_0(rng):
    """A tall fir: seven star-skirted tiers stepping in to a spire."""
    p = []
    p.append(tube('trunk', [(0, 0, -0.3), (0, 0, 1.2), (0.03, 0.02, 2.4)], [0.34, 0.27, 0.2], shade('bark', 'bark', 'bark_dk', rng), sides=7))
    roots(p, rng, 0.22, 'bark_dk')
    tiers = 7
    for i in range(tiers):
        t = i / (tiers - 1)
        r = 2.15 * (1 - t * 0.78)
        z = 1.05 + t * 4.15
        h = 1.25 if i < tiers - 1 else 1.7
        p.append(conifer_tier(f'tier{i}', rng, (rng.uniform(-0.06, 0.06), rng.uniform(-0.06, 0.06), z), r, h,
                              shade('t_pine_lt', 't_pine', 't_pine_dk', rng, under='t_pine_dk'),
                              n=10 - i // 2, droop=0.38 * (1 - t * 0.4), inner=0.72))
    return p


def tree_temperate_1(rng):
    """A Scots pine: a tall, slightly kinked bare trunk (red-brown up high) under a flat, clumped crown."""
    p = []
    trunk = [Vector((0, 0, -0.3)), Vector((0.05, 0.0, 1.5)), Vector((0.2, 0.1, 3.0)), Vector((0.12, 0.25, 4.3)), Vector((0.3, 0.3, 5.4))]
    p.append(tube('trunk', trunk, [0.32, 0.26, 0.21, 0.17, 0.12], shade('bark_red', 'bark', 'bark_dk', rng), sides=7))
    roots(p, rng, 0.2, 'bark_dk')
    # A couple of dead lower stubs.
    for i, (z, a) in enumerate(((2.2, 0.6), (3.0, 3.4))):
        d = Vector((math.cos(a), math.sin(a), 0.25))
        b = Vector((0.15, 0.08, z))
        p.append(tube(f'stub{i}', [b, b + d * 0.7], [0.07, 0.03], 'bark_dk', sides=4, tip=b + d * 0.85))
    # Branches up to the clumps, then the clumps themselves (flat, layered, facing the sky).
    clumps = [((-1.0, -0.55, 4.6), 1.15, 0.36), ((1.05, 0.25, 5.0), 1.25, 0.38), ((0.15, 1.15, 5.35), 1.05, 0.36),
              ((0.4, 0.3, 6.05), 1.0, 0.38), ((-0.65, 0.75, 4.25), 0.85, 0.32), ((-0.35, -0.2, 5.55), 0.8, 0.32)]
    for i, (c, r, hz) in enumerate(clumps):
        base = trunk[3 if i % 2 else 2].lerp(trunk[3 if i % 2 else 3], 0.0 if i % 2 else 0.6) if i < 5 else trunk[4]
        tip = Vector(c) - Vector((0, 0, hz * 0.4))
        mid = base.lerp(tip, 0.5) + Vector((0, 0, 0.25))
        p.append(tube(f'branch{i}', [base, mid, tip], [0.11, 0.08, 0.05], 'bark_red', sides=5, cap=False))
        pal = shade('t_pine_lt', 't_pine', 't_pine_dk', rng, under='t_pine_dk', bias=-0.05, spread=0.2)
        p.append(lump(f'clump{i}', rng, c, r, r * rng.uniform(0.8, 1.0), hz, pal, n=24, jit=0.3, rot=rng.random() * TAU, flat_top=0.7))
        # A smaller tuft on the clump's rim breaks up the outline from above.
        a = rng.random() * TAU
        p.append(lump(f'tuft{i}', rng, (c[0] + math.cos(a) * r * 0.85, c[1] + math.sin(a) * r * 0.85, c[2] - 0.1), r * 0.45, r * 0.4, hz * 0.7, pal, n=10))
    return p


def tree_temperate_2(rng):
    """A broadleaf oak: a stout trunk forking into three limbs under a big, lumpy round canopy."""
    p = []
    bark = shade('bark', 'bark', 'bark_dk', rng)
    trunk = [Vector((0, 0, -0.3)), Vector((0, 0, 1.0)), Vector((0.08, 0.05, 2.1))]
    p.append(tube('trunk', trunk, [0.46, 0.38, 0.32], bark, sides=8))
    roots(p, rng, 0.3, 'bark_dk', n=5)
    limbs = [Vector((-1.2, -0.6, 3.6)), Vector((1.1, -0.4, 3.8)), Vector((0.1, 1.2, 3.9))]
    for i, e in enumerate(limbs):
        m = trunk[-1].lerp(e, 0.5) + Vector((0, 0, 0.2))
        p.append(tube(f'limb{i}', [trunk[-1], m, e], [0.27, 0.19, 0.12], bark, sides=6, cap=False))
    pal = shade('t_leaf_lt', 't_leaf', 't_leaf_dk', rng, under='t_leaf_dk')
    blobs = [((0.0, 0.1, 4.55), 1.75, 1.2), ((-1.5, -0.7, 4.0), 1.25, 0.95), ((1.45, -0.55, 4.15), 1.3, 0.95),
             ((0.25, 1.55, 4.25), 1.25, 0.95), ((-1.1, 1.0, 4.7), 1.05, 0.85), ((1.05, 0.95, 4.9), 1.0, 0.85),
             ((0.1, -1.3, 4.5), 1.05, 0.8), ((0.2, 0.2, 5.6), 1.0, 0.75)]
    for i, (c, r, hz) in enumerate(blobs):
        p.append(lump(f'leaf{i}', rng, c, r, r * rng.uniform(0.85, 1.05), hz, pal, n=20, jit=0.2, rot=rng.random() * TAU))
    return p


# ---- desert -----------------------------------------------------------------------------------

def tree_desert_0(rng):
    """A dead, wind-twisted tree: a gnarled bleached trunk splitting into crooked, splayed limbs."""
    p = []
    wood = shade('d_wood_lt', 'd_wood', 'd_wood_dk', rng, bias=0.05)
    trunk = bend_path(rng, (0, 0, -0.3), (0.25, 0.1, 1), 2.6, 4, wander=0.3)
    p.append(tube('trunk', trunk, [0.42, 0.36, 0.3, 0.27, 0.24], wood, sides=7, phase=0.3))
    roots(p, rng, 0.26, 'd_wood_dk', n=4)
    top = trunk[-1]
    for i in range(4):
        a = i * TAU / 4 + rng.uniform(-0.4, 0.4)
        d = Vector((math.cos(a), math.sin(a), rng.uniform(0.5, 1.1)))
        start = trunk[-1 - (i % 2)]
        L_ = rng.uniform(1.8, 2.5)
        path = bend_path(rng, start, d, L_, 3, wander=0.4)
        p.append(tube(f'limb{i}', path, [0.24, 0.18, 0.12, 0.07], wood, sides=5, tip=path[-1] + (path[-1] - path[-2]).normalized() * 0.4))
        # One or two crooked twigs off each limb.
        for k in range(1 + i % 2):
            s = path[1 + k]
            b = (path[-1] - path[0]).normalized()
            d2 = Vector((b.x + rng.uniform(-0.9, 0.9), b.y + rng.uniform(-0.9, 0.9), rng.uniform(0.3, 0.9)))
            tw = bend_path(rng, s, d2, rng.uniform(0.8, 1.3), 2, wander=0.5)
            p.append(tube(f'twig{i}{k}', tw, [0.07, 0.045, 0.025], wood, sides=4, tip=tw[-1] + (tw[-1] - tw[-2]).normalized() * 0.3))
    # A fallen limb and a drift of sand banked against the foot.
    fl = [Vector((0.6, -0.7, 0.05)), Vector((1.3, -1.3, 0.12)), Vector((2.0, -1.6, 0.08))]
    p.append(tube('fallen', fl, [0.14, 0.11, 0.07], wood, sides=5, tip=Vector((2.3, -1.7, 0.06))))
    p.append(lump('sand', rng, (0.1, 0, -0.2), 1.1, 0.95, 0.5, shade('d_sand', 'd_sand', 'd_sand_dk', rng), n=12, floor=-0.1))
    return p


def tree_desert_1(rng):
    """A saguaro-like cactus: a ribbed column with three upturned arms and a few flowers on top."""
    p = []
    pal = shade('d_cactus_lt', 'd_cactus', 'd_cactus_dk', rng, spread=0.06)
    p.append(tube('column', [(0, 0, -0.3), (0, 0, 1.2), (0.02, 0, 2.8), (0.05, 0.02, 4.2), (0.06, 0.02, 4.75)],
                  [0.5, 0.5, 0.48, 0.44, 0.32], pal, sides=16, star=0.14, tip=(0.06, 0.02, 5.0)))
    arms = [(0.0, 1.5, 0.95, 3.4, 0.33), (math.pi * 1.05, 2.3, 0.85, 3.9, 0.3), (math.pi * 0.55, 2.9, 0.6, 3.9, 0.24)]
    for i, (a, z0, reach, z1, r) in enumerate(arms):
        d = Vector((math.cos(a), math.sin(a), 0))
        b = Vector((0, 0, z0))
        pts = [b + d * 0.3, b + d * (reach * 0.65) + Vector((0, 0, 0.05)), b + d * reach + Vector((0, 0, 0.45)),
               b + d * reach * 1.05 + Vector((0, 0, (z1 - z0) * 0.7)), b + d * reach * 1.05 + Vector((0, 0, z1 - z0))]
        tipp = pts[-1] + Vector((0, 0, r * 0.7))
        p.append(tube(f'arm{i}', pts, [r, r, r, r * 0.95, r * 0.8], pal, sides=12, star=0.14, tip=tipp))
        p.append(lump(f'flower_a{i}', rng, tipp + Vector((0, 0, -0.05)), 0.1, 0.1, 0.08, 'd_flower', n=6))
    for i in range(3):
        a = i * TAU / 3 + 0.4
        p.append(lump(f'flower{i}', rng, (0.06 + math.cos(a) * 0.18, 0.02 + math.sin(a) * 0.18, 4.92), 0.12, 0.12, 0.09, 'd_flower', n=6))
    # Sand banked round the foot, a couple of stones.
    p.append(lump('sand', rng, (0, 0, -0.15), 1.0, 0.9, 0.35, shade('d_sand', 'd_sand', 'd_sand_dk', rng), n=12, floor=-0.1))
    for i in range(2):
        a = rng.random() * TAU
        p.append(lump(f'stone{i}', rng, (math.cos(a) * 1.1, math.sin(a) * 1.1, 0), 0.3, 0.25, 0.22, shade('d_rock_lt', 'd_rock', 'd_rock_dk', rng), n=9, floor=-0.08))
    return p


def tree_desert_2(rng):
    """A scrubby clump palm: three ringed trunks of different heights, each with a crown of arching
    fronds and a skirt of dead ones hanging below."""
    p = []
    green = shade('d_frond_lt', 'd_frond', 'd_frond', rng, spread=0.15)
    trunks = [((0, 0), (0.25, 0.1), 4.2), ((0.5, -0.35), (0.9, -0.55), 3.0), ((-0.45, 0.3), (-0.95, 0.6), 1.9)]
    for ti, (b, lean, h) in enumerate(trunks):
        n = max(4, int(h * 2.2))
        pts, radii = [], []
        for i in range(n + 1):
            t = i / n
            pts.append(Vector((b[0] + lean[0] * t * t, b[1] + lean[1] * t * t, -0.2 + h * t)))
            radii.append((0.24 - 0.06 * t) * (1.08 if i % 2 else 0.92))
        p.append(tube(f'trunk{ti}', pts, radii, shade('d_wood', 'd_wood_dk', 'd_wood_dk', rng), sides=6))
        top = pts[-1]
        nf = 8 if ti == 0 else 7
        ph = rng.random() * TAU
        bm = bmesh.new()
        for k in range(nf):
            a = ph + k * TAU / nf + rng.uniform(-0.15, 0.15)
            d = Vector((math.cos(a), math.sin(a), rng.uniform(0.5, 0.9)))
            L_ = (2.1 if ti == 0 else 1.6 if ti == 1 else 1.2) * rng.uniform(0.85, 1.1)
            frond_bm(bm, top, d, L_, 0.3 * L_ / 2, droop=rng.uniform(0.7, 1.0), segs=3, fold=0.45)
        # A short upright spike of new growth in the middle.
        for k in range(3):
            a = ph + k * TAU / 3
            frond_bm(bm, top, Vector((math.cos(a) * 0.3, math.sin(a) * 0.3, 1.5)), 0.7, 0.12, droop=0.2, segs=2, fold=0.4)
        p.append(from_bm(f'crown{ti}', bm, green))
        bm = bmesh.new()
        for k in range(5):
            a = ph + (k + 0.5) * TAU / 5
            frond_bm(bm, top - Vector((0, 0, 0.15)), Vector((math.cos(a), math.sin(a), -0.6)), 0.9 * h / 4.2 + 0.4, 0.16, droop=0.6, segs=2, fold=0.3)
        p.append(from_bm(f'dead{ti}', bm, 'd_frond_dry'))
    # Dry scrub and sand round the base of the clump.
    p.append(lump('sand', rng, (0.05, 0, -0.2), 1.3, 1.1, 0.45, shade('d_sand', 'd_sand', 'd_sand_dk', rng), n=12, floor=-0.1))
    for i in range(3):
        a = rng.random() * TAU
        p.append(lump(f'scrub{i}', rng, (math.cos(a) * 0.9, math.sin(a) * 0.9, 0.1), 0.45, 0.4, 0.3, shade('d_scrub', 'd_scrub', 'd_scrub_dk', rng), n=9, floor=-0.05))
    return p


# ---- arctic -----------------------------------------------------------------------------------

def tree_arctic_0(rng):
    """A tall, snow-laden fir: snow lying on every tier, dark blue-green showing between."""
    p = []
    p.append(tube('trunk', [(0, 0, -0.3), (0, 0, 1.2), (0, 0.03, 2.4)], [0.33, 0.27, 0.2], shade('bark', 'bark_dk', 'bark_dk', rng), sides=7))
    roots(p, rng, 0.2, 'bark_dk')
    tiers = 7
    for i in range(tiers):
        t = i / (tiers - 1)
        r = 2.0 * (1 - t * 0.78)
        z = 1.0 + t * 4.4
        h = 1.3 if i < tiers - 1 else 1.7
        p += conifer_tier(f'tier{i}', rng, (rng.uniform(-0.05, 0.05), rng.uniform(-0.05, 0.05), z), r, h,
                          shade('a_pine_lt', 'a_pine', 'a_pine_dk', rng, under='a_pine_dk'), n=10 - i // 2, droop=0.42 * (1 - t * 0.4), inner=0.66,
                          cap=shade('snow', 'snow', 'snow_sh', rng, spread=0.05), cap_r=0.5)
    p.append(lump('drift', rng, (0, 0, -0.25), 1.6, 1.4, 0.5, shade('snow', 'snow', 'snow_sh', rng), n=12, floor=-0.1))
    return p


def tree_arctic_1(rng):
    """A squat, broad spruce bowed under heavy snow: wide tiers with snow pillows on the shoulders."""
    p = []
    p.append(tube('trunk', [(0, 0, -0.3), (0, 0, 1.4)], [0.36, 0.28], 'bark_dk', sides=7))
    tiers = 5
    for i in range(tiers):
        t = i / (tiers - 1)
        r = 2.55 * (1 - t * 0.7)
        z = 0.75 + t * 2.75
        h = 1.25 if i < tiers - 1 else 1.55
        p += conifer_tier(f'tier{i}', rng, (rng.uniform(-0.08, 0.08), rng.uniform(-0.08, 0.08), z), r, h,
                          shade('a_pine_lt', 'a_pine', 'a_pine_dk', rng, under='a_pine_dk'), n=11 - i, droop=0.5 * (1 - t * 0.3), inner=0.68,
                          cap=shade('snow', 'snow', 'snow_sh', rng, spread=0.05), cap_r=0.72, cap_lift=0.1)
        if i < tiers - 1:
            # Snow pillows heaped on the tier's shoulders.
            for k in range(3):
                a = rng.random() * TAU
                rr = r * rng.uniform(0.45, 0.6)
                p.append(lump(f'pillow{i}{k}', rng, (math.cos(a) * rr, math.sin(a) * rr, z + h * 0.32), r * 0.24, r * 0.2, 0.17,
                              shade('snow', 'snow', 'snow_sh', rng), n=8))
    p.append(lump('drift', rng, (0, 0, -0.25), 2.1, 1.9, 0.55, shade('snow', 'snow', 'snow_sh', rng), n=14, floor=-0.1))
    return p


def tree_arctic_2(rng):
    """A pair of frosted pines from one root: a tall slender one and a short one leaning away."""
    p = []
    for j, (bx, by, H, R, tiers, lean) in enumerate(((-0.35, 0.15, 6.4, 1.45, 6, (-0.03, 0.02)), (0.85, -0.45, 3.7, 1.25, 4, (0.08, -0.05)))):
        p.append(tube(f'trunk{j}', [(bx, by, -0.3), (bx + lean[0] * 6, by + lean[1] * 6, 1.6)], [0.26 if j == 0 else 0.2, 0.17], 'bark_dk', sides=6))
        for i in range(tiers):
            t = i / (tiers - 1)
            z = 1.0 + t * (H - 2.6)
            r = R * (1 - t * 0.75)
            h = 1.15 if i < tiers - 1 else 1.6
            c = (bx + lean[0] * z * 3, by + lean[1] * z * 3, z)
            p += conifer_tier(f'tier{j}{i}', rng, c, r, h, shade('a_pine_lt', 'a_pine', 'a_pine_dk', rng, under='a_pine_dk'), n=9 - i // 2,
                              droop=0.32, inner=0.62, tilt=lean, cap=shade('frost', 'frost', 'snow_sh', rng, spread=0.05), cap_r=0.35, cap_lift=0.05)
        # Snow caps on the very tops.
        top = (bx + lean[0] * (H - 1.6) * 3, by + lean[1] * (H - 1.6) * 3, H - 0.35)
    # A dead lower branch and the drift they share.
    b = Vector((-0.3, 0.1, 1.2))
    p.append(tube('deadbranch', [b, b + Vector((-0.9, -0.5, 0.3))], [0.06, 0.03], 'a_twig', sides=4, tip=b + Vector((-1.2, -0.65, 0.4))))
    p.append(lump('drift', rng, (0.2, -0.1, -0.25), 1.9, 1.5, 0.55, shade('snow', 'snow', 'snow_sh', rng), n=14, floor=-0.1, rot=0.5))
    return p


# ---- lava -------------------------------------------------------------------------------------

def char(rng):
    return shade('l_char_lt', 'l_char', 'l_char', rng, bias=-0.05)


def tree_lava_0(rng):
    """A snapped, charred trunk: splintered top, broken limb stubs, glowing cracks low in the bark."""
    p = []
    pts = [Vector((0, 0, -0.3)), Vector((0.05, 0.02, 1.2)), Vector((0.15, 0.05, 2.6)), Vector((0.25, 0.12, 3.9)), Vector((0.32, 0.15, 4.6))]
    radii = [0.58, 0.48, 0.42, 0.37, 0.34]
    p.append(tube('trunk', pts, radii, char(rng), sides=8, jag=(rng, 0.9)))
    roots(p, rng, 0.35, 'l_char', n=5)
    for i, ang in enumerate((0.4, 2.6, 4.4)):
        p.append(bark_crack(f'crack{i}', pts, radii, ang, 0, 1, 0.07, rng))
    # Broken limbs: one stub with a splintered end, one longer limb angled up.
    for i, (z, a, L_) in enumerate(((2.3, 0.9, 0.9), (3.3, 3.8, 1.6), (1.7, 5.2, 0.6))):
        b = Vector((0.1, 0.04, z))
        d = Vector((math.cos(a), math.sin(a), 0.7)).normalized()
        path = bend_path(rng, b, d, L_, 2, wander=0.2)
        p.append(tube(f'limb{i}', path, [0.17, 0.13, 0.1], char(rng), sides=5, jag=(rng, 0.25)))
    # The top that snapped off, lying in the ash.
    fl = [Vector((-0.6, -0.4, 0.1)), Vector((-1.6, -1.0, 0.2)), Vector((-2.4, -1.5, 0.15))]
    p.append(tube('fallen', fl, [0.3, 0.26, 0.2], char(rng), sides=6, jag=(rng, 0.4)))
    ash_mound(p, rng, 1.25, embers=5)
    return p


def tree_lava_1(rng):
    """A dead, forked snag: a tall charred trunk splitting into bare limbs, ash-grey at the tips."""
    p = []
    trunk = bend_path(rng, (0, 0, -0.3), (0, 0, 1), 3.3, 3, wander=0.12)
    radii = [0.46, 0.38, 0.32, 0.27]
    p.append(tube('trunk', trunk, radii, char(rng), sides=7))
    roots(p, rng, 0.3, 'l_char', n=4)
    for i, ang in enumerate((1.0, 3.7)):
        p.append(bark_crack(f'crack{i}', trunk, radii, ang, 0, 1, 0.06, rng))
    tipcol = shade('l_ash', 'l_ash_dk', 'l_char_lt', rng)
    for i in range(4):
        a = i * TAU / 4 + rng.uniform(-0.4, 0.4)
        d = Vector((math.cos(a) * 0.6, math.sin(a) * 0.6, 1))
        path = bend_path(rng, trunk[-1 - (i % 2)], d, rng.uniform(2.2, 3.0), 3, wander=0.3)
        p.append(tube(f'limb{i}', path[:3], [0.2, 0.14, 0.09], char(rng), sides=5, cap=False))
        p.append(tube(f'limbtip{i}', path[2:], [0.09, 0.05], tipcol, sides=5, tip=path[-1] + (path[-1] - path[-2]).normalized() * 0.4))
        s = path[1]
        d2 = Vector((d.x + rng.uniform(-1, 1), d.y + rng.uniform(-1, 1), 0.6))
        tw = bend_path(rng, s, d2, 1.0, 2, wander=0.4)
        p.append(tube(f'twig{i}', tw, [0.07, 0.04, 0.02], char(rng), sides=4, tip=tw[-1] + (tw[-1] - tw[-2]).normalized() * 0.25))
    ash_mound(p, rng, 1.1, embers=4)
    return p


def tree_lava_2(rng):
    """A trunk split down the middle: two charred halves leaning apart, one snapped, the split glowing."""
    p = []
    A = [Vector((-0.1, 0, -0.3)), Vector((-0.25, 0.05, 1.3)), Vector((-0.7, 0.2, 2.9)), Vector((-1.2, 0.35, 4.1)), Vector((-1.45, 0.45, 5.2))]
    B = [Vector((0.1, 0, -0.3)), Vector((0.3, -0.05, 1.2)), Vector((0.75, -0.2, 2.3)), Vector((1.05, -0.3, 3.0))]
    ra = [0.42, 0.36, 0.3, 0.24, 0.18]; rb = [0.42, 0.36, 0.3, 0.27]
    p.append(tube('halfA', A, ra, char(rng), sides=7, tip=A[-1] + Vector((-0.2, 0.05, 0.5))))
    p.append(tube('halfB', B, rb, char(rng), sides=7, jag=(rng, 0.6)))
    roots(p, rng, 0.33, 'l_char', n=5)
    # The split glows down the inner faces of both halves.
    for path, radii, ang in ((A, ra, 0.0), (B, rb, math.pi)):
        p.append(bark_crack(f'split{ang:.0f}', path, radii, ang + math.pi / 2, 0, 2, 0.09, rng))
        p.append(bark_crack(f'side{ang:.0f}', path, radii, ang + 2.2, 0, 1, 0.06, rng))
    for i, (z, a) in enumerate(((3.4, 1.4), (2.4, 4.0))):
        b = A[2].lerp(A[3], 0.5) if i == 0 else A[1].lerp(A[2], 0.6)
        d = Vector((math.cos(a), math.sin(a), 0.8))
        path = bend_path(rng, b, d, 1.4, 2, wander=0.3)
        p.append(tube(f'limb{i}', path, [0.12, 0.08, 0.05], char(rng), sides=5, tip=path[-1] + (path[-1] - path[-2]).normalized() * 0.3))
    ash_mound(p, rng, 1.3, embers=5)
    return p


BUILDS = [(f'tree_{t}_{v}', globals()[f'tree_{t}_{v}']) for t in ('temperate', 'desert', 'arctic', 'lava') for v in range(3)]

if __name__ == '__main__':
    for i, (name, fn) in enumerate(BUILDS):
        if ONLY and name not in ONLY:
            continue
        N.reset()
        parts = fn(random.Random(1000 + i * 37))
        snow = 'arctic' in name
        N.finish(name, parts, OUT, 512, bake_scale=0.4, grime=0.45 if snow else 0.9)

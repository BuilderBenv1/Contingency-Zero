"""Rocks for the four map themes: rock_<theme>_<0..2>.glb (+ _body.png).
Run: python tools/models/features/rocks.py game/models/features [--only rock_desert_1,...]

Variant 0 a single boulder, 1 a flat slab with a stone on it, 2 a cluster of three; each about
3-4 across and partly sunk. Builders reclaim rocks for metal, so every rock has a few cool
grey-blue metal-ore crystals poking out of it.
temperate: grey stone, moss on the tops, grass at the foot.
desert:    red-brown sandstone in strata, sand on the ledges and banked round the base.
arctic:    blue-grey rock with snow caps and a drift at the foot.
lava:      black basalt split by glowing cracks, ash and embers round the base.
"""
import math, os, sys, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nature_parts as N
import bpy, bmesh
from mathutils import Vector
from nature_parts import TAU, shade, with_top, lump, from_bm, hull_bm, blob_points, ore_cluster, cut_lines, crack_bm, blade_bm

OUT = N.out_dir()
ONLY = N.only(sys.argv)

THEMES = {
    'temperate': dict(lt='t_rock_lt', mid='t_rock', dk='t_rock_dk', top='t_moss', top_nz=0.8, top_chance=0.45),
    'desert':    dict(lt='d_rock_lt', mid='d_rock', dk='d_rock_dk', top='d_sand', top_nz=0.82, top_chance=0.9),
    'arctic':    dict(lt='a_rock_lt', mid='a_rock', dk='a_rock_dk', top='snow', top_nz=0.5, top_chance=1.0),
    'lava':      dict(lt='l_basalt_lt', mid='l_basalt', dk='l_basalt_dk', top=None),
}


def stone(theme, name, rng, c, rx, ry, rz, n=20, jit=0.26, rot=None, flat_top=None):
    """One faceted stone, painted for the theme: strata in the desert, snow caps in the arctic,
    moss in the temperate lands, glowing cracks in the lava fields (returned as extra parts)."""
    T = THEMES[theme]
    rot = rng.random() * TAU if rot is None else rot
    n = int(n * 1.8)
    bm = hull_bm(blob_points(rng, c, rx, ry, rz, n, jit, floor=-0.18, rot=rot, flat_top=flat_top), floor=-0.18)
    extra = []
    base = shade(T['lt'], T['mid'], T['dk'], rng, spread=0.1)
    if theme == 'desert':
        # Strata: slice into bands and alternate the tone a step, so the layering shows.
        zs = [c[2] - rz + k * 0.32 + rng.uniform(-0.05, 0.05) for k in range(1, 12)]
        zs = [z for z in zs if -0.1 < z < c[2] + rz * 0.95]
        for z in zs:
            cut_lines(bm, (0, 0, z), (rng.uniform(-0.05, 0.05), rng.uniform(-0.05, 0.05), 1))
        darker = {T['lt']: T['mid'], T['mid']: T['dk'], T['dk']: T['dk']}

        def strata(f):
            p = base(f)
            z = f.calc_center_median().z
            band = sum(1 for zz in zs if z > zz)
            return darker[p] if band % 2 else p
        paint = with_top(strata, T['top'], nz=T['top_nz'], rng=rng, chance=T['top_chance'])
    elif theme == 'arctic':
        # A cap of snow on everything facing up above the lower third, banked thicker on top.
        cut_lines(bm, (0, 0, c[2] + rz * 0.25), (0.1, 0.05, 1))
        paint = with_top(base, 'snow', nz=0.5, zmin=c[2] - rz * 0.1, rng=rng)
        paint = with_top(paint, 'snow_sh', nz=0.25, zmin=c[2] + rz * 0.35, rng=rng, chance=0.8)
    elif theme == 'lava':
        segs = []
        for k in range(2):
            a = rng.random() * TAU
            no = (math.cos(a), math.sin(a), rng.uniform(-0.4, 0.4))
            segs += cut_lines(bm, (c[0] + rng.uniform(-0.3, 0.3) * rx, c[1] + rng.uniform(-0.3, 0.3) * ry, c[2]), no, zmin=0.0, keep=0.75 if k == 0 else 0.45, rng=rng)
        extra.append(from_bm(name + '_cracks', crack_bm(segs, 0.05, rng), 'ember'))
        paint = base
    else:
        paint = with_top(base, T['top'], nz=T['top_nz'], rng=rng, chance=T['top_chance'])
    return [from_bm(name, bm, paint)] + extra


def ore(rng, c, rx, ry, rz, a, size=0.9, n=3):
    """Ore crystals breaking out of a stone's upper flank at angle a, jutting out and up."""
    d = Vector((math.cos(a), math.sin(a), 0))
    p = Vector(c) + Vector((d.x * rx * 0.55, d.y * ry * 0.55, rz * 0.45))
    return ore_cluster('ore', rng, p, n=n, size=size * 1.5, out=d + Vector((0, 0, 0.9)))


def foot(theme, p, rng, spots):
    """What gathers at the foot of a rock: grass, sand, snow or ash (and embers)."""
    for i, (x, y, r) in enumerate(spots):
        if theme == 'temperate':
            bm = bmesh.new()
            for k in range(6):
                a = rng.random() * TAU; d = rng.uniform(0.0, r * 0.5)
                b = Vector((x + math.cos(a) * d, y + math.sin(a) * d, -0.05))
                dd = Vector((math.cos(a) * 0.5, math.sin(a) * 0.5, 1))
                blade_bm(bm, b, dd, rng.uniform(0.35, 0.6), 0.05, bend=0.5, segs=2, flat=True)
            p.append(from_bm(f'grass{i}', bm, shade('t_grass_lt', 't_grass', 't_grass_dk', rng)))
        elif theme == 'desert':
            p.append(lump(f'sand{i}', rng, (x, y, -0.1), r, r * 0.8, 0.28, shade('d_sand', 'd_sand', 'd_sand_dk', rng), n=10, floor=-0.08))
        elif theme == 'arctic':
            p.append(lump(f'drift{i}', rng, (x, y, -0.1), r, r * 0.8, 0.32, shade('snow', 'snow', 'snow_sh', rng), n=10, floor=-0.08))
        else:
            p.append(lump(f'ash{i}', rng, (x, y, -0.1), r, r * 0.8, 0.22, shade('l_ash', 'l_ash_dk', 'l_ash_dk', rng), n=10, floor=-0.08))
            p.append(lump(f'ember{i}', rng, (x + r * 0.3, y, 0.0), 0.16, 0.13, 0.1, 'ember', n=7, floor=-0.05))


def build(theme, variant, rng):
    p = []
    if variant == 0:
        p += stone(theme, 'boulder', rng, (0, 0, 0.35), 1.65, 1.35, 1.25, n=28)
        p += stone(theme, 'shoulder', rng, (-0.9, 0.55, 0.15), 0.95, 0.85, 0.85, n=16)
        p += stone(theme, 'chip', rng, (1.45, -0.75, 0.0), 0.45, 0.38, 0.32, n=10)
        p.append(ore(rng, (0, 0, 0.35), 1.65, 1.35, 1.25, rng.uniform(-1.4, -0.4), 1.0, n=4))
        foot(theme, p, rng, [(1.2, 0.6, 0.6), (-1.3, -0.6, 0.55)])
    elif variant == 1:
        p += stone(theme, 'slab', rng, (0, 0, 0.05), 2.0, 1.45, 0.5, n=24, flat_top=0.8)
        p += stone(theme, 'stone', rng, (0.6, 0.35, 0.65), 0.75, 0.62, 0.62, n=16)
        p += stone(theme, 'pebble', rng, (-1.7, 0.9, 0.0), 0.4, 0.33, 0.28, n=9)
        p.append(ore(rng, (0, 0, 0.05), 2.0, 1.45, 0.5, rng.uniform(2.6, 3.6), 0.9, n=3))
        p.append(ore(rng, (0.6, 0.35, 0.6), 0.75, 0.62, 0.62, rng.uniform(-1.2, -0.6), 0.6, n=2))
        foot(theme, p, rng, [(1.6, -0.9, 0.5), (-1.2, -1.0, 0.5)])
    else:
        p += stone(theme, 'big', rng, (-0.7, -0.35, 0.25), 1.15, 1.0, 1.05, n=22)
        p += stone(theme, 'mid', rng, (0.9, -0.15, 0.15), 0.9, 0.8, 0.9, n=18)
        p += stone(theme, 'small', rng, (0.1, 1.0, 0.05), 0.75, 0.62, 0.62, n=16)
        p.append(ore(rng, (-0.7, -0.35, 0.25), 1.15, 1.0, 1.05, rng.uniform(-2.3, -1.6), 0.85, n=3))
        p.append(ore(rng, (0.9, -0.15, 0.15), 0.9, 0.8, 0.9, rng.uniform(-0.3, 0.3), 0.6, n=2))
        foot(theme, p, rng, [(0.15, 0.1, 0.55), (1.4, 0.9, 0.45), (-1.5, 0.6, 0.45)])
    return p


BUILDS = [(f'rock_{t}_{v}', t, v) for t in ('temperate', 'desert', 'arctic', 'lava') for v in range(3)]

if __name__ == '__main__':
    for i, (name, theme, v) in enumerate(BUILDS):
        if ONLY and name not in ONLY:
            continue
        N.reset()
        parts = build(theme, v, random.Random(2000 + i * 53))
        N.finish(name, parts, OUT, 512, bake_scale=0.6, grime=0.4 if theme == 'arctic' else 0.85)

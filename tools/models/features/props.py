"""Ground props, scattered by the thousand over the map: prop_<theme>_<kind>_<v>.glb (+ _body.png).
Run: python tools/models/features/props.py game/models/features [--only prop_temperate_grass_0,...]

Tiny in triangles (<= 120 each) and small (0.3-1.5 across, under 0.8 tall), coloured from the
theme's ground textures so they sit IN the ground. Also writes props.txt ("<theme> <name>" per line).
temperate: grass x2, bush, pebbles x2, fern
desert:    scrub x2, pebbles x2, sand-blasted stone, cactus sprout
arctic:    frozen grass, snow lump, ice shards, pebbles x2, frosted twig
lava:      ash pebbles x2, glowing cinder chunk, charred stump, cracked basalt chip, dead weed
"""
import math, os, sys, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nature_parts as N
import bpy, bmesh
import kit
from mathutils import Vector
from nature_parts import TAU, shade, with_top, lump, tube, tube_bm, from_bm, hull_bm, blob_points, blade_bm, frond_bm, crystal_bm, cut_lines, crack_bm, bend_path

OUT = N.out_dir()
ONLY = N.only(sys.argv)


def tuft(name, rng, c, n, h, w, pal, spread=0.35, bend=0.5, segs=2, lean=0.55):
    bm = bmesh.new()
    for k in range(n):
        a = rng.random() * TAU; d = rng.uniform(0, spread)
        b = Vector((c[0] + math.cos(a) * d, c[1] + math.sin(a) * d, -0.04))
        dd = Vector((math.cos(a) * lean * (0.3 + d / spread), math.sin(a) * lean * (0.3 + d / spread), 1))
        blade_bm(bm, b, dd, h * rng.uniform(0.6, 1.0), w, bend=bend, segs=segs, flat=True)
    return from_bm(name, bm, pal)


def pebbles(rng, spots, pal, n=7, top=None, top_paint=None):
    out = []
    for i, (x, y, r, h) in enumerate(spots):
        bm = hull_bm(blob_points(rng, (x, y, h * 0.25), r, r * rng.uniform(0.7, 0.95), h, n, 0.2, floor=-0.03, rot=rng.random() * TAU), floor=-0.03)
        paint = pal if top is None else with_top(pal, top_paint, nz=top, rng=rng)
        out.append(from_bm(f'pebble{i}', bm, paint))
    return out


def twigs(rng, c, n, length, pal, sides=3, up=(0.35, 0.9), segs=2, r=0.025):
    out = []
    for i in range(n):
        a = rng.random() * TAU
        d = Vector((math.cos(a), math.sin(a), rng.uniform(*up)))
        path = bend_path(rng, (c[0] + rng.uniform(-0.05, 0.05), c[1] + rng.uniform(-0.05, 0.05), -0.03), d, length * rng.uniform(0.7, 1.0), segs, wander=0.45)
        tipp = path[-1] + (path[-1] - path[-2]).normalized() * 0.08
        out.append(tube(f'twig{i}', path, [r * (1 - k / (segs + 1)) for k in range(segs + 1)], pal, sides=sides, cap=False, tip=tipp))
    return out


# ---- temperate --------------------------------------------------------------------------------

def prop_temperate_grass_0(rng):
    pal = shade('t_grass_lt', 't_grass', 't_grass_dk', rng, spread=0.2)
    return [tuft('tuft', rng, (0, 0), 10, 0.6, 0.045, pal, spread=0.3)]


def prop_temperate_grass_1(rng):
    pal = shade('t_grass_lt', 't_grass', 't_grass_dk', rng, spread=0.2)
    p = [tuft('tufta', rng, (-0.2, 0.05), 7, 0.45, 0.04, pal, spread=0.22, segs=1), tuft('tuftb', rng, (0.25, -0.1), 6, 0.38, 0.04, pal, spread=0.2, segs=1)]
    # Two seed heads standing above it.
    for i, (x, y) in enumerate(((-0.15, 0.1), (0.05, -0.05))):
        b = Vector((x, y, -0.03)); t = b + Vector((rng.uniform(-0.1, 0.1), rng.uniform(-0.1, 0.1), 0.72))
        p.append(tube(f'stem{i}', [b, t], [0.012, 0.01], 't_grass_dk', sides=3, cap=False))
        p.append(lump(f'seed{i}', rng, t + Vector((0, 0, 0.04)), 0.03, 0.03, 0.07, 'd_frond_dry', n=6))
    return p


def prop_temperate_bush_0(rng):
    pal = shade('t_leaf_lt', 't_leaf', 't_leaf_dk', rng, under='t_leaf_dk')
    p = [lump('b0', rng, (0, 0, 0.18), 0.45, 0.4, 0.32, pal, n=12, floor=-0.03),
         lump('b1', rng, (0.32, 0.18, 0.12), 0.3, 0.28, 0.24, pal, n=10, floor=-0.03),
         lump('b2', rng, (-0.28, 0.22, 0.1), 0.28, 0.25, 0.22, pal, n=9, floor=-0.03)]
    return p


def prop_temperate_pebbles_0(rng):
    pal = shade('t_rock_lt', 't_rock', 't_rock_dk', rng)
    return pebbles(rng, [(0, 0, 0.16, 0.1), (0.22, 0.1, 0.11, 0.08), (-0.15, 0.2, 0.09, 0.06), (0.05, -0.22, 0.08, 0.05)], pal)


def prop_temperate_pebbles_1(rng):
    pal = shade('t_rock_lt', 't_rock', 't_rock_dk', rng)
    p = pebbles(rng, [(0, 0, 0.3, 0.17), (0.38, -0.15, 0.13, 0.09), (-0.32, 0.25, 0.1, 0.07)], pal, n=9, top=0.85, top_paint='t_moss')
    p.append(tuft('grass', rng, (-0.25, -0.2), 4, 0.3, 0.035, shade('t_grass_lt', 't_grass', 't_grass_dk', rng), spread=0.08, segs=1))
    return p


def prop_temperate_fern_0(rng):
    pal = shade('t_leaf_lt', 't_leaf', 't_leaf_dk', rng, spread=0.1)
    bm = bmesh.new()
    ph = rng.random() * TAU
    for k in range(6):
        a = ph + k * TAU / 6 + rng.uniform(-0.2, 0.2)
        frond_bm(bm, Vector((0, 0, 0.02)), Vector((math.cos(a), math.sin(a), 1.3)), rng.uniform(0.55, 0.7), 0.1, droop=0.9, segs=2, fold=0.4)
    return [from_bm('fern', bm, pal)]


# ---- desert -----------------------------------------------------------------------------------

def prop_desert_scrub_0(rng):
    p = twigs(rng, (0, 0), 6, 0.55, shade('d_wood_lt', 'd_wood', 'd_wood_dk', rng), up=(0.4, 1.1))
    p.append(lump('base', rng, (0, 0, 0.05), 0.2, 0.18, 0.12, shade('d_scrub', 'd_scrub', 'd_scrub_dk', rng), n=8, floor=-0.03))
    return p


def prop_desert_scrub_1(rng):
    pal = shade('d_scrub', 'd_scrub', 'd_scrub_dk', rng, spread=0.2)
    p = [lump('s0', rng, (0, 0, 0.15), 0.38, 0.34, 0.24, pal, n=12, floor=-0.03, jit=0.3),
         lump('s1', rng, (0.3, -0.15, 0.08), 0.22, 0.2, 0.16, pal, n=8, floor=-0.03, jit=0.3)]
    p += twigs(rng, (0.0, 0.0), 3, 0.5, 'd_wood_dk', up=(0.6, 1.2), segs=1)
    return p


def prop_desert_pebbles_0(rng):
    pal = shade('d_rock_lt', 'd_rock', 'd_rock_dk', rng)
    return pebbles(rng, [(0, 0, 0.15, 0.09), (0.2, 0.14, 0.1, 0.07), (-0.18, 0.12, 0.08, 0.05), (0.08, -0.2, 0.09, 0.06), (-0.22, -0.12, 0.06, 0.04)], pal, n=7)


def prop_desert_pebbles_1(rng):
    pal = shade('d_rock_lt', 'd_rock', 'd_rock_dk', rng)
    p = pebbles(rng, [(0, 0, 0.26, 0.13), (0.3, 0.2, 0.12, 0.08), (-0.3, -0.12, 0.1, 0.06)], pal, n=9, top=0.85, top_paint='d_sand')
    p.append(lump('sand', rng, (-0.1, 0.25, -0.03), 0.3, 0.2, 0.06, 'd_sand', n=8, floor=-0.02))
    return p


def prop_desert_stone_0(rng):
    """A wind-scoured stone: smoothed and elongated, a tail of sand in its lee."""
    pal = shade('d_rock_lt', 'd_rock', 'd_rock_dk', rng)
    bm = hull_bm(blob_points(rng, (0, 0, 0.1), 0.55, 0.32, 0.36, 18, 0.12, floor=-0.05, rot=0.5), floor=-0.05)
    zs = [0.12, 0.28]
    for z in zs:
        cut_lines(bm, (0, 0, z), (0.05, 0, 1))
    darker = {'d_rock_lt': 'd_rock', 'd_rock': 'd_rock_dk', 'd_rock_dk': 'd_rock_dk'}
    paint = lambda f: darker[pal(f)] if sum(1 for z in zs if f.calc_center_median().z > z) == 1 else pal(f)
    p = [from_bm('stone', bm, paint)]
    p.append(lump('sand', rng, (-0.45, -0.35, -0.02), 0.45, 0.22, 0.09, shade('d_sand', 'd_sand', 'd_sand_dk', rng), n=9, floor=-0.03, rot=0.5))
    return p


def prop_desert_cactus_0(rng):
    """A cactus sprout: a ribbed barrel with a pup at its side and a flower on top."""
    pal = shade('d_cactus_lt', 'd_cactus', 'd_cactus_dk', rng, spread=0.06)
    p = [tube('barrel', [(0, 0, -0.05), (0, 0, 0.22), (0.01, 0, 0.42)], [0.17, 0.19, 0.15], pal, sides=10, star=0.18, tip=(0.01, 0, 0.5)),
         tube('pup', [(0.22, 0.08, -0.05), (0.23, 0.09, 0.1)], [0.09, 0.08], pal, sides=8, star=0.18, tip=(0.235, 0.09, 0.16)),
         lump('flower', rng, (0.01, 0, 0.51), 0.06, 0.06, 0.04, 'd_flower', n=6)]
    return p


# ---- arctic -----------------------------------------------------------------------------------

def prop_arctic_grass_0(rng):
    pal = shade('frost', 'a_grass', 'a_grass_dk', rng, spread=0.2)
    return [tuft('tuft', rng, (0, 0), 11, 0.5, 0.04, pal, spread=0.3, bend=0.8)]


def prop_arctic_snowlump_0(rng):
    p = [lump('lump', rng, (0, 0, 0.0), 0.55, 0.42, 0.32, shade('snow', 'snow', 'snow_sh', rng, spread=0.05), n=22, floor=-0.04, jit=0.15),
         lump('lump2', rng, (-0.35, 0.25, -0.02), 0.28, 0.24, 0.17, 'snow', n=10, floor=-0.04)]
    p += pebbles(rng, [(0.42, -0.2, 0.12, 0.1)], shade('a_rock_lt', 'a_rock', 'a_rock_dk', rng), n=7, top=0.7, top_paint='snow')
    return p


def prop_arctic_ice_0(rng):
    bm = bmesh.new()
    for i in range(5):
        a = rng.random() * TAU
        b = Vector((math.cos(a) * rng.uniform(0, 0.2), math.sin(a) * rng.uniform(0, 0.2), -0.05))
        d = Vector((math.cos(a) * 0.5, math.sin(a) * 0.5, 1))
        crystal_bm(bm, b, d, rng.uniform(0.3, 0.6) if i else 0.7, rng.uniform(0.06, 0.1), sides=4, rng=rng)
    bm.normal_update()
    p = [from_bm('ice', bm, shade('ice_lt', 'ice', 'a_rock', rng, bias=0.05, spread=0.2))]
    p.append(lump('snow', rng, (0, 0, -0.03), 0.35, 0.3, 0.07, 'snow', n=8, floor=-0.02))
    return p


def prop_arctic_pebbles_0(rng):
    pal = shade('a_rock_lt', 'a_rock', 'a_rock_dk', rng)
    return pebbles(rng, [(0, 0, 0.15, 0.1), (0.22, 0.12, 0.1, 0.07), (-0.2, 0.16, 0.08, 0.06), (0.06, -0.22, 0.09, 0.06)], pal, n=7, top=0.8, top_paint='snow')


def prop_arctic_pebbles_1(rng):
    pal = shade('a_rock_lt', 'a_rock', 'a_rock_dk', rng)
    p = pebbles(rng, [(0, 0, 0.28, 0.16), (0.34, 0.18, 0.12, 0.08), (-0.3, -0.18, 0.1, 0.07)], pal, n=9, top=0.6, top_paint='snow')
    p.append(lump('drift', rng, (-0.15, 0.3, -0.03), 0.3, 0.18, 0.08, 'snow', n=8, floor=-0.02))
    return p


def prop_arctic_twig_0(rng):
    """A frosted fallen twig: a forked stick lying on the snow, frost on its upper faces."""
    pal = with_top(shade('a_twig', 'a_twig', 'bark_dk', rng), 'frost', nz=0.4, rng=rng)
    path = [Vector((-0.6, -0.1, 0.03)), Vector((-0.1, 0.0, 0.05)), Vector((0.55, 0.15, 0.04))]
    p = [tube('stick', path, [0.035, 0.03, 0.02], pal, sides=4, tip=Vector((0.65, 0.17, 0.04)))]
    for i, (s, e) in enumerate(((path[1], Vector((0.25, 0.4, 0.12))), (Vector((0.2, 0.08, 0.05)), Vector((0.5, -0.2, 0.1))))):
        p.append(tube(f'fork{i}', [s, e], [0.02, 0.012], pal, sides=3, tip=e + (e - s).normalized() * 0.05))
    p.append(lump('snow', rng, (0.1, -0.05, -0.03), 0.3, 0.15, 0.07, 'snow', n=8, floor=-0.02))
    return p


# ---- lava -------------------------------------------------------------------------------------

def prop_lava_ash_0(rng):
    pal = shade('l_basalt_lt', 'l_basalt', 'l_basalt_dk', rng)
    return pebbles(rng, [(0, 0, 0.16, 0.1), (0.22, 0.12, 0.11, 0.07), (-0.2, 0.14, 0.08, 0.05), (0.06, -0.22, 0.09, 0.06)], pal, n=7, top=0.8, top_paint='l_ash')


def prop_lava_ash_1(rng):
    pal = shade('l_basalt_lt', 'l_basalt', 'l_basalt_dk', rng)
    p = pebbles(rng, [(0, 0, 0.26, 0.14), (0.3, -0.18, 0.12, 0.08), (-0.3, 0.16, 0.1, 0.06)], pal, n=9, top=0.75, top_paint='l_ash')
    p.append(lump('ash', rng, (0.1, 0.25, -0.03), 0.32, 0.2, 0.06, 'l_ash', n=8, floor=-0.02))
    return p


def prop_lava_cinder_0(rng):
    """A glowing cinder: a black clinker split by cracks of orange heat."""
    bm = hull_bm(blob_points(rng, (0, 0, 0.08), 0.32, 0.27, 0.22, 10, 0.25, floor=-0.04), floor=-0.04)
    segs = cut_lines(bm, (0.03, 0, 0.1), (1, 0.4, 0.2), zmin=0.0)
    p = [from_bm('cinder', bm, shade('l_char_lt', 'l_char', 'l_char', rng)),
         from_bm('cracks', crack_bm(segs, 0.04, rng, lift=0.008), 'ember')]
    p.append(lump('glow', rng, (0.3, 0.1, 0.0), 0.08, 0.07, 0.05, 'ember', n=6, floor=-0.02))
    return p


def prop_lava_stump_0(rng):
    """A charred stump with a splintered top and embers at its roots."""
    p = [tube('stump', [(0, 0, -0.05), (0, 0, 0.25), (0.02, 0.01, 0.42)], [0.2, 0.17, 0.15], shade('l_char_lt', 'l_char', 'l_char', rng), sides=7, jag=(rng, 0.2))]
    for i in range(3):
        a = i * TAU / 3 + 0.4
        d = Vector((math.cos(a), math.sin(a), 0))
        p.append(tube(f'root{i}', [d * 0.1 + Vector((0, 0, 0.12)), d * 0.32 + Vector((0, 0, -0.02))], [0.07, 0.03], 'l_char', sides=3, cap=False))
    p.append(lump('ember', rng, (0.22, -0.12, 0.0), 0.08, 0.06, 0.05, 'ember', n=6, floor=-0.02))
    return p


def prop_lava_chip_0(rng):
    """A cracked basalt chip: a hexagonal column piece lying tilted, glowing along its split."""
    a, b = Vector((-0.28, -0.05, 0.08)), Vector((0.3, 0.08, 0.16))
    bm = tube_bm([a, b], [0.2, 0.2], sides=6, phase=0.2)
    bmesh.ops.contextual_create(bm, geom=[v for v in bm.verts if (v.co - a).length < 0.25])
    bm.normal_update()
    segs = cut_lines(bm, (0.02, 0.0, 0.1), (1, 0.15, -0.1), zmin=0.0)
    p = [from_bm('chip', bm, shade('l_basalt_lt', 'l_basalt', 'l_basalt_dk', rng)),
         from_bm('crack', crack_bm(segs, 0.035, rng, lift=0.008), 'ember')]
    return p


def prop_lava_weed_0(rng):
    pal = shade('l_ash', 'l_ash_dk', 'l_char_lt', rng, spread=0.2)
    p = [tuft('weed', rng, (0, 0), 8, 0.5, 0.03, pal, spread=0.18, bend=0.9, lean=0.8)]
    p += twigs(rng, (0.05, 0.0), 2, 0.45, 'l_char_lt', up=(1.0, 2.0), segs=1, r=0.015)
    return p


THEMES = ('temperate', 'desert', 'arctic', 'lava')
BUILDS = [(name, name.split('_')[1], fn) for name, fn in list(globals().items()) if name.startswith('prop_') and callable(fn)]
BUILDS.sort(key=lambda b: (THEMES.index(b[1]), b[0]))

if __name__ == '__main__':
    with open(os.path.join(OUT, 'props.txt'), 'w') as f:
        for name, theme, _ in BUILDS:
            f.write(f'{theme} {name}\n')
    for i, (name, theme, fn) in enumerate(BUILDS):
        if ONLY and name not in ONLY:
            continue
        N.reset()
        parts = fn(random.Random(4000 + i * 29))
        n = N.finish(name, parts, OUT, 256, bake_scale=2.0, ao=(0.3, 0.7), grime=0.25 if theme == 'arctic' else 0.4)
        assert n <= 120, f'{name}: {n} tris'

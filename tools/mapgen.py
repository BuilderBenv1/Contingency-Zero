#!/usr/bin/env python3
"""
Generates the built-in skirmish maps in data/maps/ (needs numpy).

Each map is a deterministic function of its seed, so running this again gives byte-identical
files. Heights are corner heights in quarter-unit steps; one tile is 8 world units.
Maps are made fair by symmetry (point symmetry for 2 players, four-way for 4 players), start
areas are flattened so bases can be built, and every start is checked to be reachable by
vehicles from every other start.

    python3 tools/mapgen.py            # writes data/maps/*.json
    python3 tools/mapgen.py --preview  # also writes PNG previews next to this script's output
"""
import json
import math
import os
import sys
from collections import deque

import numpy as np

OUT = os.path.join(os.path.dirname(__file__), "..", "data", "maps")
TILE = 8
STEPS = 4  # height steps per world unit

# Passability limits, mirrored from sim/Map/Terrain.cs
VEHICLE_SLOPE = 15
BUILD_SLOPE = 10
WADE = 4
SHIP_DEPTH = 8


# ---- noise ---------------------------------------------------------------------------------------

def smooth_noise(rng, n_y, n_x, cells):
    """Value noise: random values on a coarse grid, smoothly interpolated."""
    gy, gx = cells + 2, cells + 2
    grid = rng.random((gy, gx))
    ys = np.linspace(0, cells, n_y)
    xs = np.linspace(0, cells, n_x)
    y0 = np.floor(ys).astype(int)
    x0 = np.floor(xs).astype(int)
    fy = ys - y0
    fx = xs - x0
    fy = fy * fy * (3 - 2 * fy)
    fx = fx * fx * (3 - 2 * fx)
    a = grid[y0][:, x0]
    b = grid[y0][:, x0 + 1]
    c = grid[y0 + 1][:, x0]
    d = grid[y0 + 1][:, x0 + 1]
    top = a + (b - a) * fx[None, :]
    bot = c + (d - c) * fx[None, :]
    return top + (bot - top) * fy[:, None]


def fbm(rng, n_y, n_x, base_cells, octaves=4, persistence=0.5):
    total = np.zeros((n_y, n_x))
    amp, norm = 1.0, 0.0
    cells = base_cells
    for _ in range(octaves):
        total += smooth_noise(rng, n_y, n_x, cells) * amp
        norm += amp
        amp *= persistence
        cells *= 2
    return total / norm


def symmetrize(h, mode):
    if mode == "point":
        return (h + h[::-1, ::-1]) / 2
    if mode == "quad":
        h = (h + h[:, ::-1]) / 2
        return (h + h[::-1, :]) / 2
    if mode == "mirror_x":
        return (h + h[:, ::-1]) / 2
    if mode == "d4":
        h = symmetrize(h, "quad")
        return (h + h.T) / 2
    return h


def fold_d4(a):
    """Eight-way symmetric copy of a square field made by folding, not averaging, so noise keeps its contrast."""
    i = np.arange(a.shape[0])
    f = np.minimum(i, a.shape[0] - 1 - i)
    fy, fx = np.meshgrid(f, f, indexing="ij")
    return a[np.minimum(fy, fx), np.maximum(fy, fx)]


def blur(h, passes=1):
    for _ in range(passes):
        p = np.pad(h, 1, mode="edge")
        h = (p[1:-1, 1:-1] * 4 + p[:-2, 1:-1] + p[2:, 1:-1] + p[1:-1, :-2] + p[1:-1, 2:]) / 8
    return h


# ---- terrain queries -----------------------------------------------------------------------------

def tile_stats(h, water):
    """Per-tile average height, slope (max-min of corners) and depth."""
    a, b, c, d = h[:-1, :-1], h[:-1, 1:], h[1:, :-1], h[1:, 1:]
    avg = (a + b + c + d) / 4
    hi = np.maximum(np.maximum(a, b), np.maximum(c, d))
    lo = np.minimum(np.minimum(a, b), np.minimum(c, d))
    return avg, hi - lo, water - avg


def vehicle_passable(h, water):
    avg, slope, depth = tile_stats(h, water)
    return (slope <= VEHICLE_SLOPE) & (depth <= WADE)


def connected(passable, starts):
    ty, tx = passable.shape
    sx, sy = starts[0]
    seen = np.zeros_like(passable, dtype=bool)
    q = deque([(sy, sx)])
    seen[sy, sx] = True
    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < ty and 0 <= nx < tx and not seen[ny, nx] and passable[ny, nx]:
                seen[ny, nx] = True
                q.append((ny, nx))
    return all(seen[y, x] for x, y in starts[1:])


def flatten_around(h, cx, cy, radius, target=None):
    """Flatten a circle of corners (tile units) to a target height, blending the edge."""
    ny, nx = h.shape
    yy, xx = np.mgrid[0:ny, 0:nx]
    dist = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    if target is None:
        target = h[dist <= radius].mean()
    w = np.clip((radius + 3 - dist) / 3, 0, 1)
    return h * (1 - w) + target * w


# ---- placement -----------------------------------------------------------------------------------

def sym_points(x, y, n_x, n_y, mode):
    """A point and its symmetric partners (in tile units)."""
    if mode == "point":
        return [(x, y), (n_x - 1 - x, n_y - 1 - y)]
    if mode == "quad":
        return [(x, y), (n_x - 1 - x, y), (x, n_y - 1 - y), (n_x - 1 - x, n_y - 1 - y)]
    if mode == "mirror_x":
        return [(x, y), (n_x - 1 - x, y)]
    if mode == "d4":
        # Four-way mirror plus the diagonal: eight players, all on congruent ground.
        out = []
        for px, py in ((x, y), (y, x)):
            for p in ((px, py), (n_x - 1 - px, py), (px, n_y - 1 - py), (n_x - 1 - px, n_y - 1 - py)):
                if p not in out:
                    out.append(p)
        return out
    return [(x, y)]


def good_spot(avg, slope, depth, x, y, taken, min_gap, allow_water=False):
    ty, tx = avg.shape
    if not (1 <= x < tx - 1 and 1 <= y < ty - 1):
        return False
    if slope[y, x] > BUILD_SLOPE:
        return False
    if depth[y, x] > 0 and not allow_water:
        return False
    for (ox, oy) in taken:
        if (ox - x) ** 2 + (oy - y) ** 2 < min_gap ** 2:
            return False
    return True


def place_metal(rng, h, water, starts, mode, extra, rich_centre=0, base_spots=5):
    avg, slope, depth = tile_stats(h, water)
    ty, tx = avg.shape
    spots = []  # (x, y, yield) in tile units
    taken = []
    # Base spots: a ring around the first start, mirrored to the others.
    s0 = starts[0]
    placed = 0
    tries = 0
    while placed < base_spots and tries < 2000:
        tries += 1
        ang = rng.random() * 2 * math.pi
        r = 5 + rng.random() * 5
        x = int(round(s0[0] + math.cos(ang) * r))
        y = int(round(s0[1] + math.sin(ang) * r))
        pts = sym_points(x, y, tx, ty, mode)
        if all(good_spot(avg, slope, depth, px, py, taken, 3) for px, py in pts) and \
                all(abs(px - sx) > 2 or abs(py - sy) > 2 for px, py in pts for sx, sy in starts):
            for p in pts:
                spots.append((p[0], p[1], 2.0))
                taken.append(p)
            placed += 1
    # Spread spots over the rest of the map.
    tries = 0
    added = 0
    while added < extra and tries < 6000:
        tries += 1
        x = int(rng.integers(2, tx - 2))
        y = int(rng.integers(2, ty - 2))
        pts = sym_points(x, y, tx, ty, mode)
        if any(min((px - sx) ** 2 + (py - sy) ** 2 for sx, sy in starts) < 12 ** 2 for px, py in pts):
            continue
        if all(good_spot(avg, slope, depth, px, py, taken, 6) for px, py in pts):
            for p in pts:
                spots.append((p[0], p[1], 2.5 if rng.random() < 0.3 else 2.0))
                taken.append(p)
            added += 1
    # Rich spots in the middle (contested).
    cx, cy = tx // 2, ty // 2
    for i in range(rich_centre):
        ang = i * 2 * math.pi / max(1, rich_centre)
        x = int(round(cx + math.cos(ang) * 3))
        y = int(round(cy + math.sin(ang) * 3))
        if good_spot(avg, slope, depth, x, y, taken, 2):
            spots.append((x, y, 4.0))
            taken.append((x, y))
    return spots, taken


def place_features(rng, h, water, theme, starts, taken, mode, tree_density, rocks, wreck_fields):
    avg, slope, depth = tile_stats(h, water)
    ty, tx = avg.shape
    feats = []
    forest = fbm(rng, ty, tx, 6, 3)
    forest = fold_d4(forest) if mode == "d4" else symmetrize(forest, mode)
    blocked = set()
    for (x, y) in taken:
        for dy in range(-1, 2):
            for dx in range(-1, 2):
                blocked.add((x + dx, y + dy))

    def near_start(x, y, r):
        return min((x - sx) ** 2 + (y - sy) ** 2 for sx, sy in starts) < r * r

    def add_sym(kind, x, y, metal, energy, jitter=True):
        for px, py in sym_points(x, y, tx, ty, mode):
            if (px, py) in blocked:
                continue
            ox = rng.random() * 4 + 2 if jitter else 4
            oy = rng.random() * 4 + 2 if jitter else 4
            feats.append(dict(kind=kind, x=round(px * TILE + ox, 2), y=round(py * TILE + oy, 2),
                              metal=metal, energy=energy))
            blocked.add((px, py))

    # Trees: in forest noise above a threshold, on dry moderate ground, away from bases.
    if tree_density > 0:
        threshold = 1 - tree_density
        for y in range(1, ty - 1):
            for x in range(1, tx - 1):
                if forest[y, x] < threshold or depth[y, x] > -2 or slope[y, x] > 14:
                    continue
                if near_start(x, y, 9):
                    continue
                if rng.random() < 0.55:
                    add_sym("tree", x, y, 0, 30 + int(rng.integers(0, 4)) * 10)
    # Rocks with a little metal.
    for _ in range(rocks):
        x = int(rng.integers(2, tx - 2))
        y = int(rng.integers(2, ty - 2))
        if depth[y, x] > 0 or near_start(x, y, 8):
            continue
        add_sym("rock", x, y, 20 + int(rng.integers(0, 5)) * 10, 0)
    # Ancient wreck fields: old battles worth stripping (a design pillar).
    for i in range(wreck_fields):
        cx = int(rng.integers(tx // 4, 3 * tx // 4))
        cy = int(rng.integers(ty // 4, 3 * ty // 4))
        for _ in range(7):
            x = cx + int(rng.integers(-3, 4))
            y = cy + int(rng.integers(-3, 4))
            if not (1 <= x < tx - 1 and 1 <= y < ty - 1) or depth[y, x] > 2 or near_start(x, y, 12):
                continue
            add_sym("wreck", x, y, 80 + int(rng.integers(0, 9)) * 20, 0)
    return feats


# ---- maps ----------------------------------------------------------------------------------------

def base_land(rng, n, base, amp, cells, octaves=4, mode="point"):
    h = base + fbm(rng, n + 1, n + 1, cells, octaves) * amp
    return symmetrize(h, mode)


def finish(name, theme, tiles, h, water, starts_tiles, mode, rng, *, description, metal_extra, rich=0,
           trees=0.25, rocks=20, wrecks=2, wind=(4, 18), base_spots=5, author="Contingency Zero"):
    # Flatten bases and make sure every start is reachable by vehicles.
    for (sx, sy) in starts_tiles:
        h = flatten_around(h, sx + 0.5, sy + 0.5, 7, target=max(water + 20, h[sy, sx]))
    h = symmetrize(h, mode)
    for _ in range(30):
        if connected(vehicle_passable(h, water), starts_tiles):
            break
        h = symmetrize(blur(h, 1), mode)
    else:
        raise RuntimeError(f"{name}: starts not connected for vehicles")

    hi = np.clip(np.round(h), 0, 1200).astype(int)
    spots, taken = place_metal(rng, hi.astype(float), water, starts_tiles, mode, metal_extra, rich, base_spots)
    feats = place_features(rng, hi.astype(float), water, theme, starts_tiles, taken, mode, trees, rocks, wrecks)

    data = dict(
        formatVersion=1, name=name, description=description, author=author, theme=theme,
        tilesX=tiles, tilesY=tiles, waterLevel=int(water), windMin=wind[0], windMax=wind[1],
        heights=[int(v) for v in hi.flatten()],
        metalSpots=[dict(x=x * TILE + 4, y=y * TILE + 4, **{"yield": yv}) for x, y, yv in spots],
        features=feats,
        startPositions=[dict(x=sx * TILE + 4, y=sy * TILE + 4) for sx, sy in starts_tiles],
    )
    return data


def proving_grounds():
    rng = np.random.default_rng(101)
    n = 64
    h = base_land(rng, n, 70, 50, 3, mode="point")
    starts = [(32, 9), (31, 54)]
    return finish("Proving Grounds", "temperate", n, h, 20, starts, "point", rng,
                  description="A small, open 1v1 map. Fast games, few places to hide.",
                  metal_extra=5, trees=0.22, rocks=10, wrecks=1)


def ashfall_plains():
    rng = np.random.default_rng(202)
    n = 96
    h = base_land(rng, n, 80, 90, 4, mode="point")
    # A few mesas: raised flat-topped blobs bots can climb and vehicles go round.
    for _ in range(3):
        cx, cy = rng.integers(20, 76, size=2)
        ny, nx = h.shape
        yy, xx = np.mgrid[0:ny, 0:nx]
        d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
        h += np.clip((7 - d) / 2, 0, 1) * 60
    h = symmetrize(blur(h, 1), "point")
    starts = [(18, 18), (77, 77)]
    return finish("Ashfall Plains", "desert", n, h, 10, starts, "point", rng,
                  description="Wide desert with a few mesas and the wrecks of an old war in the middle.",
                  metal_extra=10, rich=0, trees=0.0, rocks=40, wrecks=4, wind=(8, 24))


def twin_rivers():
    rng = np.random.default_rng(303)
    n = 112
    h = base_land(rng, n, 90, 60, 4, mode="point")
    ny, nx = h.shape
    yy, xx = np.mgrid[0:ny, 0:nx]
    water = 60
    # Two rivers running across the map, with shallow fords.
    for offset in (-26, 26):
        centre = xx - yy - offset + np.sin(yy / 9.0) * 4
        dist = np.abs(centre) / math.sqrt(2)
        ford = (np.abs(((xx + yy) % 36) - 18) < 5)
        depth = np.where(ford, 70, 115)
        h -= np.exp(-(dist / 3.2) ** 2) * depth
    h = symmetrize(h, "point")
    starts = [(14, 97), (97, 14)]
    return finish("Twin Rivers", "temperate", n, h, water, starts, "point", rng,
                  description="Two rivers split the map. Hold the fords, or build a navy.",
                  metal_extra=12, trees=0.35, rocks=15, wrecks=2, wind=(5, 15))


def crater_basin():
    rng = np.random.default_rng(404)
    n = 112
    h = base_land(rng, n, 120, 50, 3, mode="quad")
    ny, nx = h.shape
    yy, xx = np.mgrid[0:ny, 0:nx]
    d = np.sqrt((xx - nx / 2) ** 2 + (yy - ny / 2) ** 2)
    # Crater: a raised rim with a sunken floor and four breaches in the rim.
    rim = np.exp(-((d - 28) / 5) ** 2) * 70
    breach = (np.abs(xx - nx / 2) < 4) | (np.abs(yy - ny / 2) < 4)
    rim = np.where(breach, rim * 0.15, rim)
    floor = np.clip((28 - d) / 28, 0, 1) * -45
    h = h + rim + floor
    h = symmetrize(blur(h, 1), "quad")
    starts = [(14, 14), (97, 14), (14, 97), (97, 97)]
    return finish("Crater Basin", "lava", n, h, 0, starts, "quad", rng,
                  description="Four players round an old impact crater. The richest metal is in the bowl.",
                  metal_extra=6, rich=6, trees=0.0, rocks=30, wrecks=3, wind=(2, 10), base_spots=4)


def coastal_divide():
    rng = np.random.default_rng(505)
    n = 128
    h = base_land(rng, n, 90, 70, 4, mode="point")
    ny, nx = h.shape
    yy, xx = np.mgrid[0:ny, 0:nx]
    water = 70
    # A sea down the middle, with land bridges at the top and bottom and a few islands.
    sea = np.exp(-((xx - nx / 2) / 18) ** 2) * 130
    bridges = np.exp(-((yy - 14) / 7) ** 2) + np.exp(-((yy - (ny - 15)) / 7) ** 2)
    sea *= np.clip(1 - bridges * 1.1, 0, 1)
    h -= sea
    islands = fbm(rng, ny, nx, 10, 2)
    h += np.where((np.abs(xx - nx / 2) < 14) & (islands > 0.68), 90, 0)
    h = symmetrize(blur(h, 1), "point")
    starts = [(18, 64), (109, 63)]
    return finish("Saltwater Strait", "temperate", n, h, water, starts, "point", rng,
                  description="Two coasts face each other across open sea. Land bridges at either end.",
                  metal_extra=12, trees=0.3, rocks=15, wrecks=2, wind=(10, 26))


def frostline():
    rng = np.random.default_rng(606)
    n = 128
    h = base_land(rng, n, 140, 140, 5, mode="quad", octaves=5)
    h = symmetrize(blur(h, 2), "quad")
    starts = [(16, 16), (111, 16), (16, 111), (111, 111)]
    return finish("Frostline", "arctic", n, h, 30, starts, "quad", rng,
                  description="Frozen highlands for four. Rugged ground favours bots over vehicles.",
                  metal_extra=10, trees=0.25, rocks=25, wrecks=3, wind=(12, 30), base_spots=4)


# ---- second batch: shared tools -------------------------------------------------------------------
#
# The maps below build their terrain from canonical pieces (one base's plateau, one ramp, one island)
# that are copied by the map's symmetry, so every player gets exactly the same ground. Their checks
# mirror the sim's integer maths (sim/Map/Terrain.cs) and never blur the terrain to force a fix: a
# map that fails a check stops the run.

BOT_SLOPE = 26
AMPHIBIOUS_SLOPE = 20


def grid(n):
    yy, xx = np.mgrid[0:n + 1, 0:n + 1].astype(float)
    return xx, yy


def images(a, mode):
    """A canonical layer (over the corner grid) and its symmetric copies."""
    if mode == "point":
        return [a, a[::-1, ::-1]]
    if mode == "mirror_x":
        return [a, a[:, ::-1]]
    if mode == "quad":
        return [a, a[:, ::-1], a[::-1, :], a[::-1, ::-1]]
    if mode == "d4":
        return images(a, "quad") + images(a.T, "quad")
    return [a]


def sym_max(a, mode):
    return np.maximum.reduce(images(a, mode))


def sym_min(a, mode):
    return np.minimum.reduce(images(a, mode))


def sym_override(h, mask, level, mode):
    """Writes level into h wherever mask is set, for the canonical piece and all its copies."""
    for m, lv in zip(images(mask, mode), images(level, mode)):
        h = np.where(m, lv, h)
    return h


def smoothstep(t):
    t = np.clip(t, 0, 1)
    return t * t * (3 - 2 * t)


def corridor(xx, yy, a, b, width):
    """Corners within width/2 of the segment a-b, and how far along it they are (0 at a, 1 at b)."""
    vx, vy = b[0] - a[0], b[1] - a[1]
    u = ((xx - a[0]) * vx + (yy - a[1]) * vy) / (vx * vx + vy * vy)
    perp = np.hypot(xx - (a[0] + u * vx), yy - (a[1] + u * vy))
    return (u >= 0) & (u <= 1) & (perp <= width / 2), u


def seg_dist(xx, yy, a, b):
    vx, vy = b[0] - a[0], b[1] - a[1]
    u = np.clip(((xx - a[0]) * vx + (yy - a[1]) * vy) / (vx * vx + vy * vy), 0, 1)
    return np.hypot(xx - (a[0] + u * vx), yy - (a[1] + u * vy))


def checker(xx, yy):
    """1 on alternate corners: tiles over it all have the full step as their slope (rough ground)."""
    return ((xx.astype(int) + yy.astype(int)) % 2).astype(float)


def int_tile_stats(hi, water):
    """Tile height, slope and depth exactly as MapData computes them (integer average)."""
    a, b, c, d = hi[:-1, :-1], hi[:-1, 1:], hi[1:, :-1], hi[1:, 1:]
    avg = (a + b + c + d) // 4
    slope = np.maximum(np.maximum(a, b), np.maximum(c, d)) - np.minimum(np.minimum(a, b), np.minimum(c, d))
    return avg, slope, water - avg


def passable_for(hi, water, cls):
    _, slope, depth = int_tile_stats(hi, water)
    if cls == "bot":
        return (slope <= BOT_SLOPE) & (depth <= WADE)
    if cls == "vehicle":
        return (slope <= VEHICLE_SLOPE) & (depth <= WADE)
    if cls == "amphibious":
        return slope <= AMPHIBIOUS_SLOPE
    if cls == "hover":
        return slope <= VEHICLE_SLOPE
    if cls == "ship":
        return depth >= SHIP_DEPTH
    raise ValueError(cls)


def reach(passable, start):
    """Tiles reachable from start, moving like the sim's pathfinder (8 ways, no cutting corners)."""
    ty, tx = passable.shape
    seen = np.zeros_like(passable, dtype=bool)
    sx, sy = start
    if not passable[sy, sx]:
        return seen
    seen[sy, sx] = True
    q = deque([(sy, sx)])
    while q:
        y, x = q.popleft()
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                ny, nx = y + dy, x + dx
                if (dx == 0 and dy == 0) or not (0 <= ny < ty and 0 <= nx < tx):
                    continue
                if seen[ny, nx] or not passable[ny, nx]:
                    continue
                if dx and dy and not (passable[y, nx] and passable[ny, x]):
                    continue
                seen[ny, nx] = True
                q.append((ny, nx))
    return seen


def sym_tiles(points, tiles, mode):
    out = []
    for x, y in points:
        for p in sym_points(x, y, tiles, tiles, mode):
            if p not in out:
                out.append(p)
    return out


def place_metal2(rng, hi, water, canon_starts, starts, mode, base_spots, base_yield, extra, contested, home_reach):
    """Base spots in a ring round each canonical start, extras spread about, then hand-placed contested ones."""
    avg, slope, depth = int_tile_stats(hi, water)
    ty, tx = avg.shape
    spots, taken = [], []

    def add(pts, yv):
        for p in pts:
            spots.append((p[0], p[1], yv))
            taken.append(p)

    for s0 in canon_starts:
        placed = tries = 0
        while placed < base_spots and tries < 4000:
            tries += 1
            ang = rng.random() * 2 * math.pi
            r = 4.5 + rng.random() * 5.5
            x = int(round(s0[0] + math.cos(ang) * r))
            y = int(round(s0[1] + math.sin(ang) * r))
            pts = sym_points(x, y, tx, ty, mode)
            if not all(good_spot(avg, slope, depth, px, py, taken, 3) for px, py in pts):
                continue
            if not all(home_reach[py, px] for px, py in pts):
                continue
            if any(abs(px - sx) <= 2 and abs(py - sy) <= 2 for px, py in pts for sx, sy in starts):
                continue
            add(pts, base_yield)
            placed += 1
        if placed < base_spots:
            raise RuntimeError(f"only {placed} base spots fit round start {s0}")
    for (x, y, yv) in contested:
        pts = sym_points(x, y, tx, ty, mode)
        if not all(good_spot(avg, slope, depth, px, py, taken, 2) for px, py in pts):
            raise RuntimeError(f"contested metal at {(x, y)} is not on buildable ground")
        add(pts, yv)
    added = tries = 0
    while added < extra and tries < 8000:
        tries += 1
        x = int(rng.integers(2, tx - 2))
        y = int(rng.integers(2, ty - 2))
        pts = sym_points(x, y, tx, ty, mode)
        if any(min((px - sx) ** 2 + (py - sy) ** 2 for sx, sy in starts) < 12 ** 2 for px, py in pts):
            continue
        if all(good_spot(avg, slope, depth, px, py, taken, 6) for px, py in pts):
            add(pts, 2.5 if rng.random() < 0.3 else 2.0)
            added += 1
    return spots, taken


def add_features_sym(feats, rng, kind, x, y, metal, energy, tiles, mode, occupied):
    """One feature at tile (x, y) and its symmetric copies, skipping tiles already in use."""
    for px, py in sym_points(x, y, tiles, tiles, mode):
        if (px, py) in occupied or not (1 <= px < tiles - 1 and 1 <= py < tiles - 1):
            continue
        feats.append(dict(kind=kind, x=round(px * TILE + 2 + rng.random() * 4, 2),
                          y=round(py * TILE + 2 + rng.random() * 4, 2), metal=metal, energy=energy))
        occupied.add((px, py))


def finish2(name, theme, tiles, h, water, starts, mode, rng, *, description, need=("vehicle",), canon_starts=None,
            base_spots=5, base_yield=2.0, metal_extra=8, contested=(), trees=0.2, rocks=20, wrecks=2,
            wind=(5, 20), flatten_r=7, extra_features=None, author="Contingency Zero"):
    canon_starts = canon_starts or starts[:1]
    if sorted(sym_tiles(canon_starts, tiles, mode)) != sorted(starts):
        raise RuntimeError(f"{name}: start positions are not symmetric")
    for (sx, sy) in starts:
        centre = (h[sy, sx] + h[sy, sx + 1] + h[sy + 1, sx] + h[sy + 1, sx + 1]) / 4
        h = flatten_around(h, sx + 0.5, sy + 0.5, flatten_r, target=max(water + 20, centre))
    h = symmetrize(h, mode)
    hi = np.clip(np.round(h), 0, 1200).astype(int)

    # Every start must reach every other for each mover class the map promises.
    reached = {}
    for cls in ("vehicle", "bot", "amphibious", "ship"):
        seen = reach(passable_for(hi, water, cls), starts[0])
        reached[cls] = all(seen[y, x] for x, y in starts)
        if cls in need and not reached[cls]:
            raise RuntimeError(f"{name}: starts not connected for {cls}")
    home_reach = reach(passable_for(hi, water, need[0]), starts[0])

    spots, taken = place_metal2(rng, hi, water, canon_starts, starts, mode, base_spots, base_yield,
                                metal_extra, contested, home_reach)
    feats = place_features(rng, hi.astype(float), water, theme, starts, taken, mode, trees, rocks, wrecks)
    if extra_features:
        occupied = {(int(f["x"] // TILE), int(f["y"] // TILE)) for f in feats}
        occupied |= {(x + dx, y + dy) for x, y in taken for dx in (-1, 0, 1) for dy in (-1, 0, 1)}
        extra_features(feats, occupied, hi)

    # Fair, buildable starts.
    avg, slope, depth = int_tile_stats(hi, water)
    buildable = (slope <= BUILD_SLOPE) & (depth <= 0)
    ty, tx = avg.shape
    yy, xx = np.mgrid[0:ty, 0:tx]
    per_start = []
    for sx, sy in starts:
        near = (xx - sx) ** 2 + (yy - sy) ** 2 <= 36
        if buildable[near].sum() < 100:
            raise RuntimeError(f"{name}: start {(sx, sy)} has only {buildable[near].sum()} buildable tiles")
        mine = [yv for x, y, yv in spots if (x - sx) ** 2 + (y - sy) ** 2 <= 11 ** 2]
        if not 4 <= len(mine) <= 8:
            raise RuntimeError(f"{name}: start {(sx, sy)} has {len(mine)} base metal spots")
        per_start.append(len(mine))

    data = dict(
        formatVersion=1, name=name, description=description, author=author, theme=theme,
        tilesX=tiles, tilesY=tiles, waterLevel=int(water), windMin=wind[0], windMax=wind[1],
        heights=[int(v) for v in hi.flatten()],
        metalSpots=[dict(x=x * TILE + 4, y=y * TILE + 4, **{"yield": yv}) for x, y, yv in spots],
        features=feats,
        startPositions=[dict(x=sx * TILE + 4, y=sy * TILE + 4) for sx, sy in starts],
    )
    print(f"  {name}: reach " + ", ".join(f"{c}={'yes' if v else 'no'}" for c, v in reached.items())
          + f"; base spots per start {per_start}")
    return data


# ---- second batch: maps ---------------------------------------------------------------------------

def tablelands():
    """Mesas with cliff walls. Each base sits on its own tableland with one vehicle ramp and two
    steep bot ramps, so vehicles have a single way in and bots can come over the side."""
    rng = np.random.default_rng(707)
    n, mode = 128, "point"
    xx, yy = grid(n)
    base = 60 + symmetrize(fbm(rng, n + 1, n + 1, 4, 3), mode) * 24
    plat = np.zeros_like(base)
    home = ((np.abs(xx - 24.5) / 17) ** 4 + (np.abs(yy - 24.5) / 17) ** 4) < 1
    plat = np.maximum(plat, home * 96.0)
    plat = np.maximum(plat, (np.hypot(xx - 100.5, yy - 26.5) < 8) * 120.0)  # side mesa: bots only
    plat = np.maximum(plat, (np.hypot(xx - 64, yy - 64) < 11) * 56.0)       # the central table
    plat = sym_max(plat, mode)
    for top, bottom, width, height in (
            ((40.5, 24.5), (53.5, 24.5), 5, 96),   # home: the vehicle ramp
            ((24.5, 40.0), (24.5, 45.5), 3, 96),   # home: bot ramps
            ((36.0, 39.5), (36.0, 45.0), 3, 96),
            ((53.5, 64.0), (45.0, 64.0), 5, 56),   # centre: vehicle ramp
            ((64.0, 53.5), (64.0, 50.5), 3, 56),   # centre: bot ramp
            ((93.0, 26.5), (86.5, 26.5), 3, 120),  # side mesa: bot ramp
    ):
        mask, u = corridor(xx, yy, top, bottom, width)
        plat = sym_override(plat, mask, height * (1 - np.clip(u, 0, 1)), mode)
    h = base + plat
    starts = [(24, 24), (103, 103)]
    return finish2("Tablelands", "desert", n, h, 0, starts, mode, rng,
                   description="Cliff-walled mesas. One vehicle ramp into each base, bot ramps over the side, "
                               "and rich metal on the high tables only bots can climb.",
                   base_spots=6, metal_extra=10, trees=0.0, rocks=40, wrecks=2, wind=(6, 22),
                   contested=[(61, 63, 3.0), (65, 60, 3.0), (99, 25, 3.0), (102, 28, 3.0)])


def shoal_chain():
    """Island hopping. Deep water everywhere ships can sail; islands are joined by rough reefs that
    bots and amphibious units can wade but vehicles can't cross."""
    rng = np.random.default_rng(808)
    n, mode, water = 144, "point", 100
    xx, yy = grid(n)
    floor = water - 40 + symmetrize(fbm(rng, n + 1, n + 1, 5, 3), mode) * 10
    land_noise = symmetrize(fbm(rng, n + 1, n + 1, 6, 3), mode) * 16
    rise = np.zeros_like(floor)
    ragged = fbm(rng, n + 1, n + 1, 12, 2) * 8 - 4
    islands = [((28.5, 28.5), 24), ((66.5, 30.5), 14), ((30.5, 66.5), 14), ((72, 72), 16),
               ((114.5, 24.5), 12)]
    for (cx, cy), r in islands:
        rise = np.maximum(rise, smoothstep((r + ragged - np.hypot(xx - cx, yy - cy)) / 9))
    rise = sym_max(rise, mode)
    h = floor + rise * (water + 26 - floor) + rise * land_noise
    reef = water - 12 + 20 * checker(xx, yy)
    reef_mask = np.zeros_like(h, dtype=bool)
    for a, b in (((28.5, 28.5), (66.5, 30.5)), ((28.5, 28.5), (30.5, 66.5)),
                 ((66.5, 30.5), (72, 72)), ((30.5, 66.5), (72, 72))):
        m, _ = corridor(xx, yy, a, b, 3.2)
        reef_mask |= m
    reef_mask = np.logical_or.reduce(images(reef_mask, mode))
    h = np.where(reef_mask, np.maximum(h, reef), h)
    starts = [(28, 28), (115, 115)]
    return finish2("Shoal Chain", "temperate", n, h, water, starts, mode, rng,
                   description="Islands in deep water, linked by reefs bots and amphibious units can wade. "
                               "Vehicles can't leave home: build a navy or walk.",
                   need=("bot", "amphibious"), base_spots=6, metal_extra=8, trees=0.45, rocks=15, wrecks=1,
                   wind=(10, 28), flatten_r=6,
                   contested=[(70, 70, 4.0), (74, 70, 4.0), (114, 23, 3.0), (66, 30, 2.5)])


def boneyard():
    """An old battlefield. Thin metal in the ground, but the plain is littered with wrecks worth
    far more than the extractors: the side that reclaims fastest wins."""
    rng = np.random.default_rng(909)
    n, mode, water = 96, "point", 24
    xx, yy = grid(n)
    h = 64 + symmetrize(fbm(rng, n + 1, n + 1, 5, 4), mode) * 44
    # Lava-filled shell craters: too deep to wade, too small for ships.
    pits = np.zeros_like(h)
    for _ in range(9):
        cx, cy = rng.uniform(8, 88, size=2)
        if min(math.hypot(cx - 16.5, cy - 16.5), math.hypot(cx - 79.5, cy - 79.5)) < 16:
            continue
        pits = np.maximum(pits, smoothstep((3.2 - np.hypot(xx - cx, yy - cy)) / 2.2))
    pits = sym_max(pits, mode)
    h = h * (1 - pits) + (water - 6) * pits
    starts = [(16, 16), (79, 79)]

    def wreckage(feats, occupied, hi):
        avg, slope, depth = int_tile_stats(hi, water)

        def ok(x, y):
            return depth[y, x] <= 0 and min((x - sx) ** 2 + (y - sy) ** 2 for sx, sy in starts) >= 12 ** 2
        # Fallen giants in the middle: a few huge hulks.
        for x, y in ((44, 47), (49, 45), (46, 42)):
            if ok(x, y):
                add_features_sym(feats, rng, "wreck", x, y, 900, 0, n, mode, occupied)
        # Battle lines of tank and bot wrecks across the plain.
        for _ in range(14):
            cx, cy = int(rng.integers(10, 86)), int(rng.integers(10, 86))
            for _ in range(9):
                x, y = cx + int(rng.integers(-4, 5)), cy + int(rng.integers(-4, 5))
                if 1 <= x < n - 1 and 1 <= y < n - 1 and ok(x, y):
                    add_features_sym(feats, rng, "wreck", x, y, 100 + int(rng.integers(0, 11)) * 20, 0,
                                     n, mode, occupied)

    return finish2("The Boneyard", "lava", n, h, water, starts, mode, rng,
                   description="A scorched graveyard of old armies. Little metal in the ground, "
                               "a fortune in wrecks for whoever reclaims fastest.",
                   base_spots=4, metal_extra=4, trees=0.0, rocks=30, wrecks=0, wind=(3, 14),
                   extra_features=wreckage)


def glacier_gorge():
    """A winding ice canyon for 1v1: one wide main gorge and a narrow side cut, with high snowfields
    above that only bots reach, by goat trails."""
    rng = np.random.default_rng(1010)
    n, mode = 80, "point"
    xx, yy = grid(n)
    low = 110 + symmetrize(fbm(rng, n + 1, n + 1, 4, 3), mode) * 10
    high = 216 + symmetrize(fbm(rng, n + 1, n + 1, 5, 3), mode) * 18
    # How far each corner is outside the canyon floor (negative inside).
    e = np.hypot(xx - 12.5, yy - 12.5) - 12                     # home bowl
    e = np.minimum(e, np.hypot(xx - 40, yy - 40) - 8)            # central arena
    main = [(12.5, 12.5), (30, 13), (37, 25), (40, 40)]
    for a, b in zip(main, main[1:]):
        e = np.minimum(e, seg_dist(xx, yy, a, b) - 4.5)
    side = [(12.5, 12.5), (13, 32), (26, 44), (40, 40)]
    for a, b in zip(side, side[1:]):
        e = np.minimum(e, seg_dist(xx, yy, a, b) - 2.5)
    e = sym_min(e, mode)
    t = np.clip(e / 1.5, 0, 1)
    h = low + (high - low) * t
    # Goat trails: steep, straight climbs from the gorge floor to the snowfields.
    for top, bottom in (((30, 5.0), (30, 11.0)), ((22.5, 27.5), (17.0, 27.5)), ((49.5, 33.0), (43.5, 33.0))):
        mask, u = corridor(xx, yy, top, bottom, 2.2)
        u = np.clip(u, 0, 1)
        h = sym_override(h, mask, low + (high - low) * (1 - u), mode)
    starts = [(12, 12), (67, 67)]
    return finish2("Glacier Gorge", "arctic", n, h, 0, starts, mode, rng,
                   description="A tight 1v1 down a winding ice canyon. Hold the gorge, or send bots up the "
                               "goat trails to the snowfields above.",
                   base_spots=5, metal_extra=7, trees=0.15, rocks=14, wrecks=1, wind=(14, 30), flatten_r=6)


def grand_continent():
    """Eight kingdoms round an inland sea, split by mountain ranges with passes."""
    rng = np.random.default_rng(1111)
    n, mode, water = 192, "d4", 70
    xx, yy = grid(n)
    c = n / 2
    h = 100 + fold_d4(fbm(rng, n + 1, n + 1, 10, 4)) * 80
    edge = np.minimum(np.minimum(xx, n - xx), np.minimum(yy, n - yy))
    h -= smoothstep((14 - edge) / 12) * 80                       # the outer ocean
    r = np.hypot(xx - c, yy - c)
    lake = smoothstep((22 - r) / 6)
    h = h * (1 - lake) + 40 * lake                               # the inland sea
    h = np.where(r < 8, np.maximum(h, 100 - np.clip(r - 3, 0, 5) * 7), h)        # central isle
    # Causeways out to the isle along the axes, wadeable by everything.
    cause = (np.abs(yy - c) < 2.5) & (r < 24)
    cause = np.logical_or.reduce(images(cause, mode))
    h = np.where(cause & (r >= 7.5), np.maximum(h, water - 3 + np.clip(r - 14, 0, 10) * 2.5), h)
    # Mountain ranges along the mirror lines, with a pass in each.
    along = r
    rough = fold_d4(fbm(rng, n + 1, n + 1, 14, 2))
    spread = 2.0 + rough * 1.6
    ridge_ax = np.exp(-(np.abs(yy - c) / spread) ** 2)            # canonical range: the line y = c
    gate = ((along > 34) & ~((along > 54) & (along < 64))).astype(float)
    ridge_diag = np.exp(-(np.abs(xx - yy) / math.sqrt(2) / spread) ** 2)
    gate_d = ((along > 34) & ~((along > 70) & (along < 80))).astype(float)
    ridges = np.maximum(sym_max(ridge_ax * gate, mode), sym_max(ridge_diag * gate_d, mode))
    h = h + ridges * (60 + rough * 90)
    canon = [(26, 70)]
    all_s = sym_tiles(canon, n, mode)
    # Order: four spread out first (best for 2-4 players), then the other four.
    order = [(26, 70), (165, 121), (121, 26), (70, 165), (70, 26), (121, 165), (165, 70), (26, 121)]
    assert sorted(order) == sorted(all_s), all_s

    return finish2("Eight Crowns", "temperate", n, h, water, order, mode, rng,
                   description="Eight kingdoms round an inland sea, walled apart by mountains. Fight through "
                               "the passes, or wade the causeways to the rich isle in the middle.",
                   base_spots=5, metal_extra=5, trees=0.33, rocks=40, wrecks=3, wind=(6, 20),
                   contested=[(99, 99, 4.0)])


def ember_rift():
    """3v3 across a lava chasm: three wide land bridges, two broken ones only bots can scramble over."""
    rng = np.random.default_rng(1212)
    n, mode, water = 160, "mirror_x", 20
    xx, yy = grid(n)
    c = n / 2
    h = 150 + symmetrize(fbm(rng, n + 1, n + 1, 5, 4), mode) * 44
    d = np.abs(xx - c)
    half = 2.6 + fbm(rng, n + 1, 1, 6, 2)[:, :1].T.repeat(n + 1, 0).T * 5
    wall = np.clip((d - half) / 2, 0, 1)
    rift = 14 + (h - 14) * wall
    bridge = np.zeros_like(h, dtype=bool)
    for yb in (24, 80, 136):
        bridge |= np.abs(yy - yb) < 3.6
    broken = np.zeros_like(h, dtype=bool)
    for yb in (52, 108):
        broken |= np.abs(yy - yb) < 1.6
    h_deck = h.copy()
    h = np.where(bridge, h, rift)
    rough = (h_deck - 10) + 20 * checker(xx, yy)
    h = np.where(broken & (d < half + 3), rough, h)
    # Lava pools on each side.
    pools = np.zeros_like(h)
    for cx, cy in ((44, 52), (34, 104), (60, 136), (56, 14)):
        pools = np.maximum(pools, smoothstep((3.4 - np.hypot(xx - cx, yy - cy)) / 2.0))
    pools = sym_max(pools, mode)
    h = h * (1 - pools) + (water - 6) * pools
    starts = [(20, 26), (139, 26), (14, 80), (145, 80), (20, 134), (139, 134)]
    return finish2("Ember Rift", "lava", n, h, water, starts, mode, rng,
                   canon_starts=[(20, 26), (14, 80), (20, 134)],
                   description="Three against three across a lava chasm. Three land bridges for tanks, two "
                               "broken spans only bots can scramble over.",
                   base_spots=5, metal_extra=12, trees=0.15, rocks=30, wrecks=3, wind=(2, 12),
                   contested=[(74, 23, 3.0), (74, 79, 3.0), (74, 82, 3.0), (74, 136, 3.0)])


def frozen_mere():
    """Four bases on the shores of a frozen lake: the ice is shallow water everyone can drive over but
    nobody can build on, with islets of metal out on it and holes in the ice to steer round."""
    rng = np.random.default_rng(1313)
    n, mode, water = 112, "quad", 100
    xx, yy = grid(n)
    lake = 97 + np.round(symmetrize(fbm(rng, n + 1, n + 1, 8, 2), mode) * 2)
    land = 132 + symmetrize(fbm(rng, n + 1, n + 1, 5, 3), mode) * 24
    wob = symmetrize(fbm(rng, n + 1, n + 1, 6, 2), mode) * 6
    shore = smoothstep((25 + wob - np.hypot(xx - 16.5, yy - 16.5)) / 6)
    shore = np.maximum(shore, smoothstep((8 - np.hypot(xx - 36, yy - 36)) / 6))      # islets
    shore = np.maximum(shore, smoothstep((8 - np.hypot(xx - 56, yy - 20)) / 6))
    shore = np.maximum(shore, smoothstep((11 - np.hypot(xx - 56, yy - 56)) / 6))     # the centre isle
    shore = sym_max(shore, mode)
    h = lake + (land - lake) * shore
    holes = np.zeros_like(h, dtype=bool)
    for cx, cy in ((44, 24), (26, 46), (46, 46), (40, 33), (54, 40), (30, 54)):
        holes |= np.hypot(xx - cx, yy - cy) < 1.3
    holes = np.logical_or.reduce(images(holes, mode))
    h = np.where(holes & (shore < 0.05), water - 12, h)
    starts = [(16, 16), (95, 95), (95, 16), (16, 95)]
    return finish2("Frozen Mere", "arctic", n, h, water, starts, mode, rng,
                   description="Four shores round a frozen lake. The ice is open ground nobody can build on; "
                               "the metal is out on the islets. Mind the holes.",
                   base_spots=5, metal_extra=4, trees=0.3, rocks=20, wrecks=2, wind=(14, 32),
                   contested=[(35, 35, 2.5), (37, 36, 2.5), (55, 19, 3.0), (54, 54, 4.0)])


def sandriver_valley():
    """2v2: each team's bases sit on an escarpment above a river valley. The cliff edge is natural
    artillery ground; the river below has three fords and room for gunboats."""
    rng = np.random.default_rng(1414)
    n, mode, water = 144, "quad", 60
    xx, yy = grid(n)
    c = n / 2
    valley = 80 + symmetrize(fbm(rng, n + 1, n + 1, 6, 3), mode) * 12
    edge_y = 44 + 2.5 * np.cos(2 * math.pi * (xx - c) / 36)
    plat = (yy < edge_y) * 72.0
    plat = np.maximum(plat, (np.hypot(xx - 54, yy - 57) < 4.2) * 50.0)              # forward bluff
    plat = sym_max(plat, mode)
    for top, bottom, width, height in (
            ((40, edge_y[0, 40] - 1), (40, edge_y[0, 40] + 8), 6, 72),            # vehicle ramp
            ((12, edge_y[0, 12] - 1), (12, edge_y[0, 12] + 3.2), 3, 72),          # bot ramps
            ((72, edge_y[0, 72] - 1), (72, edge_y[0, 72] + 3.2), 3, 72),
            ((54, 53.6), (54, 50.6), 2.6, 50),                                    # bluff: bots only
    ):
        mask, u = corridor(xx, yy, top, bottom, width)
        plat = sym_override(plat, mask, height * (1 - np.clip(u, 0, 1)), mode)
    h = valley + plat
    dy = np.abs(yy - c)
    river = 40 + (h - 40) * np.clip((dy - 4) / 2, 0, 1)
    h = np.where(dy < 6, river, h)
    for xf in (24, 72, 120):
        ford = np.abs(xx - xf) < 3.2
        level = 57 + (80 - 57) * np.clip((dy - 2) / 4, 0, 1)
        h = np.where(ford & (dy < 6), level, h)
    starts = [(30, 18), (113, 125), (113, 18), (30, 125)]
    return finish2("Sandriver Valley", "desert", n, h, water, starts, mode, rng,
                   description="Two against two across a desert river. Bases on the escarpments, artillery on the "
                               "cliff edge, three fords and a channel deep enough for boats.",
                   base_spots=5, metal_extra=8, trees=0.08, rocks=30, wrecks=2, wind=(8, 22),
                   contested=[(54, 57, 3.0), (70, 63, 3.0), (26, 62, 2.5)])


MAPS = {
    "proving_grounds": proving_grounds,
    "ashfall_plains": ashfall_plains,
    "twin_rivers": twin_rivers,
    "crater_basin": crater_basin,
    "coastal_divide": coastal_divide,
    "frostline": frostline,
    "tablelands": tablelands,
    "shoal_chain": shoal_chain,
    "the_boneyard": boneyard,
    "glacier_gorge": glacier_gorge,
    "grand_continent": grand_continent,
    "ember_rift": ember_rift,
    "frozen_mere": frozen_mere,
    "sandriver_valley": sandriver_valley,
}


def preview(name, data, path):
    from PIL import Image
    n = data["tilesX"] + 1
    h = np.array(data["heights"]).reshape(n, n).astype(float)
    water = data["waterLevel"]
    img = np.zeros((n, n, 3), dtype=np.uint8)
    land = np.clip((h - h.min()) / max(1, h.max() - h.min()), 0, 1)
    img[..., 0] = (80 + land * 140).astype(np.uint8)
    img[..., 1] = (90 + land * 120).astype(np.uint8)
    img[..., 2] = (60 + land * 80).astype(np.uint8)
    wet = h < water
    img[wet] = [40, 80, 150]
    im = Image.fromarray(img).resize((n * 4, n * 4), Image.NEAREST)
    px = im.load()
    for s in data["metalSpots"]:
        x, y = int(s["x"] / TILE * 4), int(s["y"] / TILE * 4)
        for dx in range(-2, 3):
            for dy in range(-2, 3):
                if 0 <= x + dx < im.width and 0 <= y + dy < im.height:
                    px[x + dx, y + dy] = (255, 230, 0)
    for f in data["features"]:
        x, y = int(f["x"] / TILE * 4), int(f["y"] / TILE * 4)
        c = {"tree": (20, 120, 30), "rock": (130, 130, 130), "wreck": (200, 60, 60)}[f["kind"]]
        if 0 <= x < im.width and 0 <= y < im.height:
            px[x, y] = c
    for s in data["startPositions"]:
        x, y = int(s["x"] / TILE * 4), int(s["y"] / TILE * 4)
        for dx in range(-4, 5):
            for dy in range(-4, 5):
                if 0 <= x + dx < im.width and 0 <= y + dy < im.height:
                    px[x + dx, y + dy] = (255, 255, 255)
    im.save(path)


def main():
    os.makedirs(OUT, exist_ok=True)
    want_preview = "--preview" in sys.argv
    preview_dir = next((a.split("=", 1)[1] for a in sys.argv if a.startswith("--preview-dir=")), OUT)
    for key, fn in MAPS.items():
        data = fn()
        with open(os.path.join(OUT, f"{key}.json"), "w") as f:
            json.dump(data, f, separators=(",", ":"))
        spots = len(data["metalSpots"])
        print(f"{key}: {data['tilesX']}x{data['tilesY']} tiles, {spots} metal spots, "
              f"{len(data['features'])} features, {len(data['startPositions'])} starts")
        if want_preview:
            preview(key, data, os.path.join(preview_dir, f"{key}.png"))


if __name__ == "__main__":
    main()

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
    return h


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
    forest = symmetrize(forest, mode)
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
    return finish("Coastal Divide", "temperate", n, h, water, starts, "point", rng,
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


MAPS = {
    "proving_grounds": proving_grounds,
    "ashfall_plains": ashfall_plains,
    "twin_rivers": twin_rivers,
    "crater_basin": crater_basin,
    "coastal_divide": coastal_divide,
    "frostline": frostline,
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

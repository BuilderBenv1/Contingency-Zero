"""Builds game/textures/ from CC0 textures and writes game/src/View/TextureSet.g.cs.

Two sources, per theme and slot (shore, low, patch, high, rock):
- Plain images in tools/source_textures/<theme>/<slot>_color.{png,jpg} and
  <slot>_normal.{png,jpg} (Poly Haven downloads; see README credits).
- Otherwise ambientCG's (https://ambientcg.com, CC0 1.0), read from a local copy of
  github.com/PatrickJnr/cc0-texture-pack (Source 2 .vtex_c files), decoded with tools/vtex.py:
  python3 tools/textures.py <pack>/textures

The ambientCG pack is only needed for themes/slots without plain files; themes that were
not processed keep their existing averages in TextureSet.g.cs.

Ground textures are 1024 px with 512 px normal maps; five layers per theme.
"""
import os, sys, glob, re
sys.path.insert(0, os.path.dirname(__file__))
from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), '..')
OUT = os.path.join(ROOT, 'game', 'textures')

# theme -> (shore, low ground, patches, high ground, cliff rock), as ambientCG asset ids.
# Ground-scale textures (pebbles, grass, cracks), each with its normal map for lit relief.
GROUND = {
    'temperate': ('Ground044', 'Ground037', 'Ground024', 'Ground013', 'Rock030'),
    'desert':    ('Ground026', 'Ground025', 'Ground035', 'Ground028', 'Rock022'),
    'arctic':    ('Ice004', 'Snow004', 'Rock026', 'Gravel023', 'Rock027'),
    'lava':      ('Rocks016', 'Gravel024', 'Ground045', 'Rock014', 'Rock014'),
}
SLOTS = ('shore', 'low', 'patch', 'high', 'rock')
SIZE = 1024
def find(src, asset, kind='color'):
    import vtex
    hits = glob.glob(os.path.join(src, '*', asset.lower() + f'_{kind}_*.vtex_c'))
    if not hits: raise SystemExit(f'{asset} not found under {src}')
    return hits[0]

PLAIN = os.path.join(ROOT, 'tools', 'source_textures')

def has_plain(theme):
    return all(os.path.exists(os.path.join(PLAIN, theme, f'{slot}_{kind}.jpg'))
               or os.path.exists(os.path.join(PLAIN, theme, f'{slot}_{kind}.png'))
               for slot in SLOTS for kind in ('color', 'normal'))

def load(src, asset, size=SIZE, kind='color', theme=None, slot=None):
    # Plain downloaded image for this theme/slot takes priority over the ambientCG pack.
    if theme is not None:
        for ext in ('.png', '.jpg', '.jpeg'):
            plain = os.path.join(PLAIN, theme, f'{slot}_{kind}{ext}')
            if os.path.exists(plain):
                im = Image.open(plain).convert('RGB')
                return im.resize((size, size), Image.LANCZOS)
    import vtex
    return vtex.decode(find(src, asset, kind), size).resize((size, size), Image.LANCZOS)

def average(im):
    px = im.resize((1, 1), Image.BOX).getpixel((0, 0))
    return tuple(round(c / 255, 4) for c in px)

def existing_averages():
    """Theme -> average-colour line already in TextureSet.g.cs, for themes we don't rebuild."""
    cs = os.path.join(ROOT, 'game', 'src', 'View', 'TextureSet.g.cs')
    keep = {}
    if os.path.exists(cs):
        for m in re.finditer(r'\["(\w+)"\] = new\[\] \{ (.*?) \},', open(cs).read()):
            keep[m.group(1)] = m.group(2)
    return keep

def main(src):
    os.makedirs(OUT, exist_ok=True)
    lines = []
    keep = existing_averages()
    for theme, assets in GROUND.items():
        if not has_plain(theme) and not src:
            print(f'{theme}: no plain sources and no ambientCG pack given; keeping existing textures')
            if theme in keep:
                lines.append(f'        ["{theme}"] = new[] {{ {keep[theme]} }},')
            continue
        avgs = []
        for slot, asset in zip(SLOTS, assets):
            im = load(src, asset, theme=theme, slot=slot)
            im.save(os.path.join(OUT, f'{theme}_{slot}.png'))
            avgs.append(average(im))
            load(src, asset, 512, 'normal', theme, slot).save(os.path.join(OUT, f'{theme}_{slot}_n.png'))
        a = ', '.join(f'new Color({r}f, {g}f, {b}f)' for r, g, b in avgs)
        lines.append(f'        ["{theme}"] = new[] {{ {a} }},')

    # Hull detail, one RGB texture: R = plating (panels and rivets), G = paint wear
    # (where the paint has chipped through to rust), B = rust/grime streaks.
    hull = None
    if src:
        plates = load(src, 'MetalPlates001', 256).convert('L')
        paint = load(src, 'PaintedMetal010', 256)
        rust = load(src, 'Rust004', 256).convert('L')
        wear = Image.new('L', paint.size)
        wp = wear.load(); pp = paint.load()
        for y in range(paint.size[1]):
            for x in range(paint.size[0]):
                r, g, b = pp[x, y]
                wp[x, y] = 255 if r > b + 30 and r > 70 else 0
        Image.merge('RGB', (plates, wear, rust)).save(os.path.join(OUT, 'hull_detail.png'))
        hull = average(Image.merge('RGB', (plates, wear, rust)))
    else:
        m = re.search(r'HullAverage = new\((.*?)\)', open(os.path.join(ROOT, 'game', 'src', 'View', 'TextureSet.g.cs')).read())
        if m:
            hull = tuple(float(v) for v in m.group(1).replace('f', '').split(', '))
        print('no ambientCG pack given; keeping existing hull_detail.png')

    cs = os.path.join(ROOT, 'game', 'src', 'View', 'TextureSet.g.cs')
    with open(cs, 'w') as f:
        f.write('// Generated by tools/textures.py. Don\'t edit by hand.\n')
        f.write('using System.Collections.Generic;\nusing Godot;\n\nnamespace ContingencyZero.Game.View;\n\n')
        f.write('/// <summary>Average colour of each ground texture (shore, low, patch, high, rock) per theme, and of the hull detail map.</summary>\n')
        f.write('public static class TextureSet\n{\n')
        f.write('    public static readonly Dictionary<string, Color[]> GroundAverages = new()\n    {\n')
        f.write('\n'.join(lines) + '\n    };\n')
        f.write(f'    public static readonly Color HullAverage = new({hull[0]}f, {hull[1]}f, {hull[2]}f);\n}}\n')
    print('wrote', OUT, cs)

if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else None)

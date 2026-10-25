"""Paints over photo ground textures so they sit with the hand-built models:
python3 tools/stylise.py <theme> [<theme> ...]

For each layer in game/textures/<theme>_<slot>.png (and its _n.png normal map):
- Kuwahara filter: flattens fine photo speckle into larger painted patches but keeps edges;
- cavity shading from the normal map: hollows darken, ridges catch a little light, like the
  occlusion and worn edges baked into the units;
- a gentle saturation and contrast lift toward the models' bolder colour.
Originals are kept as <name>.photo.png the first time, so reruns start from the photo.
"""
import os, sys
import numpy as np
from PIL import Image, ImageEnhance, ImageFilter

ROOT = os.path.join(os.path.dirname(__file__), '..', 'game', 'textures')
SLOTS = ('shore', 'low', 'patch', 'high', 'rock')


def kuwahara(a, r):
    """Kuwahara filter on an HxWx3 float array (wrap-around, so tiles stay seamless)."""
    lum = a.mean(axis=2)
    best_mean = None; best_var = None
    for dy0, dx0 in ((-r, -r), (-r, 0), (0, -r), (0, 0)):
        s = np.zeros_like(a); s2 = np.zeros_like(lum); n = 0
        for dy in range(dy0, dy0 + r + 1):
            for dx in range(dx0, dx0 + r + 1):
                sh = np.roll(np.roll(a, dy, 0), dx, 1)
                s += sh; s2 += np.roll(np.roll(lum, dy, 0), dx, 1) ** 2; n += 1
        m = s / n
        var = s2 / n - m.mean(axis=2) ** 2
        if best_mean is None:
            best_mean, best_var = m, var
        else:
            pick = var < best_var
            best_mean[pick] = m[pick]; best_var[pick] = var[pick]
    return best_mean


def cavity(n_img, size):
    """Hollows and ridges from a tangent-space normal map, as -1..1 (negative = hollow)."""
    n = np.asarray(n_img.convert('RGB').resize((size, size), Image.BILINEAR), dtype=np.float32) / 127.5 - 1
    # Divergence of the normal's slope: concave where it converges.
    nx, ny = n[..., 0], n[..., 1]
    div = (np.roll(nx, -1, 1) - np.roll(nx, 1, 1)) + (np.roll(ny, 1, 0) - np.roll(ny, -1, 0))
    # Only the broad hollows: the raw divergence is fine-grained and reads as hatching.
    img = Image.fromarray(((div * 0.25 + 0.5).clip(0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(size / 128))
    blur = np.asarray(img, dtype=np.float32) / 255 * 4 - 2
    return (blur * 1.2).clip(-1, 1)


def stylise(path, npath):
    photo = path[:-4] + '.photo.png'
    if not os.path.exists(photo):
        os.replace(path, photo)
    im = Image.open(photo).convert('RGB')
    size = im.size[0]
    # Work at half size so the filter's patches are bigger, then come back up.
    small = np.asarray(im.resize((size // 2, size // 2), Image.LANCZOS), dtype=np.float32) / 255
    k = kuwahara(small, 3)
    k = kuwahara(k, 2)
    out = Image.fromarray((k.clip(0, 1) * 255).astype(np.uint8)).resize((size, size), Image.BICUBIC)
    a = np.asarray(out, dtype=np.float32) / 255
    if os.path.exists(npath):
        c = cavity(Image.open(npath), size)[..., None]
        a = a * np.where(c > 0, 1 + 0.18 * c, 1 + 0.55 * c)
    out = Image.fromarray((a.clip(0, 1) * 255).astype(np.uint8))
    out = ImageEnhance.Color(out).enhance(1.1)
    out = ImageEnhance.Contrast(out).enhance(1.12)
    out.save(path)


if __name__ == '__main__':
    for theme in sys.argv[1:]:
        for slot in SLOTS:
            p = os.path.join(ROOT, f'{theme}_{slot}.png')
            stylise(p, os.path.join(ROOT, f'{theme}_{slot}_n.png'))
            print('painted', p)

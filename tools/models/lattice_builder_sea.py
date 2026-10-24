"""Lattice "Tender" naval constructor. Run: python tools/models/lattice_builder_sea.py <out_dir>

A sleek work ship: a needle-bowed hull with a team shoulder stripe and blue waterline lights, an
aft bridge slab with a team roof and wraparound screen, a glowing nanolathe ring set into the
midships deck with a slim articulated nano crane reaching over the bow, a forward team work
platform with a second emitter, a sensor mast and twin glowing waterjets.
The waterline is z = 0: the hull dips below it.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
from mathutils import Vector
import airsea_parts as A

kit.reset('lattice')
F = 'lattice'
HW, DECK, KEEL = 0.4, 0.22, -0.14
body, secs = A.ship_hull(F, HW, DECK, KEEL)
V = Vector
for sx in A.SIDES:
    body.append(box(f'wl{sx}', (sx * (HW * 0.985 + 0.004), -0.36, 0.12), (0.01, 0.95, 0.014), 'glow_dim', 0, 1))
body.append(box('boweye', (0, 1.12, DECK + 0.025), (0.04, 0.05, 0.02), 'glow', 0, 1))

# ---- Aft bridge slab ----
body.append(hull_from('bridge', [(0.22, -0.4, DECK), (-0.22, -0.4, DECK), (0.22, -0.84, DECK), (-0.22, -0.84, DECK),
                                 (0.21, -0.46, 0.5), (-0.21, -0.46, 0.5), (0.21, -0.82, 0.5), (-0.21, -0.82, 0.5),
                                 (0.15, -0.5, 0.55), (-0.15, -0.5, 0.55), (0.15, -0.78, 0.55), (-0.15, -0.78, 0.55)], 'team', bevel=0.012))
body.append(box('broof', (0, -0.64, 0.558), (0.24, 0.22, 0.012), 'hull', 0.004, 1))
body.append(hull_from('screen', [(0.2, -0.413, 0.36), (-0.2, -0.413, 0.36), (0.19, -0.453, 0.47), (-0.19, -0.453, 0.47),
                                 (0.2, -0.398, 0.36), (-0.2, -0.398, 0.36), (0.19, -0.438, 0.47), (-0.19, -0.438, 0.47)], 'glass', bevel=0.003))
for sx in A.SIDES:
    body.append(box(f'bstrip{sx}', (sx * 0.222, -0.62, 0.32), (0.006, 0.36, 0.012), 'glow', 0, 1))
body.append(cyl('mast', (0, -0.7, 0.55), (0, -0.74, 0.96), 0.018, 'metal', 8, r2=0.011, bevel=0))
body.append(box('yard', (0, -0.73, 0.82), (0.26, 0.022, 0.022), 'metal', 0.003, 1))
body.append(sphere('masttip', (0, -0.74, 0.97), 0.028, 'glow', 8, 4))
for sx in A.SIDES:
    body.append(cyl(f'jet{sx}', (sx * 0.18, -1.08, 0.04), (sx * 0.18, -1.2, 0.04), 0.07, 'mech', 12, r2=0.075, bevel=0.004))
    body.append(cyl(f'jetglow{sx}', (sx * 0.18, -1.19, 0.04), (sx * 0.18, -1.205, 0.04), 0.055, 'glow', 12, bevel=0))
    body.append(cyl(f'raft{sx}', (sx * 0.3, -0.98, DECK + 0.05), (sx * 0.3, -0.86, DECK + 0.05), 0.045, 'hull', 10, bevel=0.006))

# ---- Midships nanolathe ring and crane ----
C = V((0, 0.06, DECK))
body.append(A.ring('nanoring', (0, 0.06, DECK + 0.02), 0.27, 0.2, 0.04, 'hull_dark', top='glow', verts=24))
body.append(cyl('nanopit', (0, 0.06, DECK + 0.002), (0, 0.06, DECK + 0.01), 0.2, 'glow_dim', 24, bevel=0))
body.append(cyl('turntable', (0, 0.06, DECK), (0, 0.06, DECK + 0.08), 0.13, 'hull', 16, r2=0.11, bevel=0.006))
sh, el, wr = C + V((0, 0.0, 0.1)), C + V((0, 0.24, 0.5)), C + V((0, 0.82, 0.4))
body.append(A.beam('upperarm', sh, el, 0.08, 0.1, 'team', bevel=0.012, taper=0.85))
body.append(cyl('elbow', el - V((0.06, 0, 0)), el + V((0.06, 0, 0)), 0.06, 'mech', 12, bevel=0.004))
body.append(A.beam('forearm', el, wr, 0.06, 0.07, 'hull', bevel=0.01, taper=0.8))
body.append(cyl('ram', sh + V((0, -0.04, 0.02)), (sh + V((0, -0.04, 0.02))).lerp(el, 0.5) + V((0, 0.04, -0.04)), 0.014, 'metal', 6, bevel=0))
body += A.nano_tip('tip', wr, wr + V((0, 0.14, -0.08)), 0.035)
for k in range(2):
    p = el.lerp(wr, 0.35 + k * 0.25)
    body.append(cyl(f'acoil{k}', p - V((0, 0.015, 0)), p + V((0, 0.015, 0)), 0.045, 'glow', 10, bevel=0))
# ---- Forward work platform with a second emitter ----
plat = hull_from('platform', [(0.17, 0.52, DECK), (-0.17, 0.52, DECK), (0.15, 0.82, DECK), (-0.15, 0.82, DECK),
                              (0.16, 0.54, DECK + 0.06), (-0.16, 0.54, DECK + 0.06), (0.13, 0.8, DECK + 0.06), (-0.13, 0.8, DECK + 0.06)], 'team', bevel=0.008)
body.append(plat)
body.append(cyl('fwdpost', (0, 0.74, DECK + 0.06), (0, 0.74, DECK + 0.16), 0.03, 'metal', 8, bevel=0))
body += A.nano_tip('ftip', (0, 0.74, DECK + 0.16), (0, 0.88, DECK + 0.2), 0.028)
for sx in A.SIDES:
    body.append(box(f'cleat{sx}', (sx * 0.28, 0.4, DECK + 0.02), (0.03, 0.08, 0.03), 'metal', 0.006, 1))
    body.append(box(f'locker{sx}', (sx * 0.26, -0.24, DECK + 0.05), (0.08, 0.2, 0.1), 'hull_dark', 0.008, 1))

A.finish('lattice_builder_sea', [('Body', body, None)], 512)

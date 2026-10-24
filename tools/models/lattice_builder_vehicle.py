"""Lattice "Pathfinder" builder vehicle. Run: python tools/models/lattice_builder_vehicle.py <out_dir>

A sleek survey-and-construct carrier on the Lattice track pods: a bevelled cab up front with a
wide glass canopy, a flat team-painted cargo deck behind it, a slim nanolathe crane mounted on a
turntable at the back, and a sensor mast with a blue tip.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import vehicles_parts as vp

kit.reset('lattice')
body = []
body += vp.lattice_pods(0.59, 0.3, -1.05, 1.1, 0.36)

def ring(y, hw, ht, z0, z1, z2):
    return [(-hw * 0.85, y, z0), (hw * 0.85, y, z0), (hw, y, z1), (-hw, y, z1), (ht, y, z2), (-ht, y, z2)]
hull = hull_from('hull', ring(1.06, 0.3, 0.2, 0.14, 0.24, 0.3) + ring(0.7, 0.44, 0.34, 0.12, 0.34, 0.45) +
                 ring(-0.82, 0.44, 0.34, 0.12, 0.34, 0.45) + ring(-1.02, 0.38, 0.28, 0.14, 0.3, 0.38), 'hull', 0.025, 2)
inset_panels(hull, 0.03, 0.01, lambda f: abs(f.normal.x) > 0.5 and f.calc_area() > 0.03)
body.append(hull)
body.append(box('belly', (0, 0.0, 0.1), (0.7, 1.9, 0.06), 'mech', 0.01, 1))

# ---- Cab: a faceted canopy with wraparound glass and a team roof ----
cab = hull_from('cab', [(-0.36, 0.86, 0.42), (0.36, 0.86, 0.42), (-0.4, 0.3, 0.44), (0.4, 0.3, 0.44),
                        (-0.26, 0.74, 0.6), (0.26, 0.74, 0.6), (-0.32, 0.36, 0.68), (0.32, 0.36, 0.68)], 'hull', 0.02, 2)
body.append(cab)
body.append(hull_from('canopy', [(-0.3, 0.885, 0.45), (0.3, 0.885, 0.45), (-0.24, 0.775, 0.585), (0.24, 0.775, 0.585),
                                 (-0.3, 0.86, 0.45), (0.3, 0.86, 0.45), (-0.24, 0.75, 0.585), (0.24, 0.75, 0.585)], 'glass', 0.004, 1))
for sx in (-1, 1):
    body.append(hull_from(f'sideglass{sx}', [(sx * 0.37, 0.78, 0.47), (sx * 0.37, 0.42, 0.47), (sx * 0.3, 0.72, 0.6), (sx * 0.33, 0.42, 0.64),
                                             (sx * 0.35, 0.78, 0.47), (sx * 0.35, 0.42, 0.47), (sx * 0.28, 0.72, 0.6), (sx * 0.31, 0.42, 0.64)],
                          'glass', 0.003, 1))
body.append(hull_from('cabroof', [(-0.27, 0.72, 0.615), (0.27, 0.72, 0.615), (-0.32, 0.36, 0.69), (0.32, 0.36, 0.69),
                                  (-0.24, 0.7, 0.635), (0.24, 0.7, 0.635), (-0.29, 0.38, 0.71), (0.29, 0.38, 0.71)], 'team', 0.006, 1))
body += vp.lattice_eye('noseeye', (0, 1.08, 0.27), 0.34)
for sx in (-1, 1):
    body.append(box(f'lamp{sx}', (sx * 0.24, 0.9, 0.41), (0.08, 0.02, 0.02), 'glow', 0, 1))
# Glacis team plate.
body.append(prism_x('glacis', [(1.02, 0.3), (0.86, 0.405), (0.82, 0.405), (0.98, 0.29)], -0.26, 0.26, 'team', 0.005, 1))

# ---- Cargo deck: team plate with tie-down rails and two stowed cells ----
body.append(hull_from('deck', [(-0.36, 0.28, 0.45), (0.36, 0.28, 0.45), (-0.36, -0.86, 0.45), (0.36, -0.86, 0.45),
                               (-0.33, 0.26, 0.48), (0.33, 0.26, 0.48), (-0.33, -0.84, 0.48), (0.33, -0.84, 0.48)], 'team', 0.006, 1))
for sx in (-1, 1):
    body.append(cyl(f'rail{sx}', (sx * 0.4, 0.24, 0.52), (sx * 0.4, -0.86, 0.52), 0.012, 'metal', 6, bevel=0))
    for y in (0.2, -0.3, -0.8):
        body.append(cyl(f'post{sx}{y}', (sx * 0.4, y, 0.45), (sx * 0.4, y, 0.52), 0.012, 'metal', 6, bevel=0))
for k, y in enumerate((0.12, -0.02)):
    body.append(box(f'cell{k}', (0.2, y, 0.53), (0.18, 0.12, 0.1), 'hull_dark', 0.012, 1))
    body.append(box(f'cellg{k}', (0.2, y, 0.585), (0.1, 0.06, 0.01), 'glow_dim', 0, 1))
# Rear heat-sink fins.
for i in range(5):
    body.append(box(f'fin{i}', (-0.16 + i * 0.08, -0.98, 0.42), (0.02, 0.06, 0.06), 'metal', 0.003, 1))

# ---- Nanolathe crane on a raised turntable at the back ----
body.append(cyl('turntable', (0, -0.4, 0.48), (0, -0.4, 0.54), 0.22, 'hull_dark', 16, bevel=0.008))
body.append(cyl('ttglow', (0, -0.4, 0.54), (0, -0.4, 0.55), 0.18, 'glow_dim', 16, bevel=0))
body += vp.nano_arm('arm', (0, -0.4, 0.54), 1.0, 0, 'lattice', reach=1.05)
# Sensor mast at the back right.
body.append(box('mastbase', (0.28, -0.72, 0.5), (0.08, 0.08, 0.06), 'hull_dark', 0.008, 1))
body.append(cyl('mast', (0.28, -0.72, 0.5), (0.3, -0.76, 1.02), 0.012, 'metal', 5, bevel=0))
body.append(sphere('masttip', (0.3, -0.76, 1.03), 0.03, 'glow', 8, 4))
for z in (0.7, 0.86):
    body.append(box(f'mastfin{z}', (0.29, -0.74, z), (0.08, 0.008, 0.02), 'hull', 0, 1))

vp.finish('lattice_builder_vehicle', body)

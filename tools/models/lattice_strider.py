"""Lattice "Strider" main battle tank. Run: python tools/models/lattice_strider.py <out_dir>

The Lattice answer to the Anvil: low and sleek on two faired track pods, a smooth sloped wedge
hull with a sensor strip across the nose, a glowing heat-sink deck at the back, and a flat
angular turret carrying a long, slim precision cannon in a team-painted shroud.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import vehicles_parts as vp

kit.reset('lattice')
body = []

# ---- Running gear: two faired pods on thin treads ----
body += vp.lattice_pods(0.6, 0.3, -1.05, 1.12, 0.38)

# ---- Hull: a chamfered wedge between the pods ----
def ring(y, hw, ht, z0, z1, z2):
    return [(-hw * 0.85, y, z0), (hw * 0.85, y, z0), (hw, y, z1), (-hw, y, z1), (ht, y, z2), (-ht, y, z2)]
pts = (ring(1.1, 0.3, 0.2, 0.14, 0.22, 0.28) + ring(0.6, 0.46, 0.34, 0.12, 0.34, 0.47) +
       ring(-0.72, 0.46, 0.34, 0.12, 0.34, 0.47) + ring(-1.0, 0.4, 0.28, 0.14, 0.3, 0.38))
hull = hull_from('hull', pts, 'hull', bevel=0.025, segs=2)
inset_panels(hull, 0.03, 0.01, lambda f: abs(f.normal.x) > 0.5 and f.calc_area() > 0.03)
body.append(hull)
body.append(box('belly', (0, 0.0, 0.1), (0.7, 1.9, 0.06), 'mech', 0.01, 1))
# Glacis: a team plate laid on the slope with a dark sensor visor across the nose.
gl = prism_x('glacis', [(1.04, 0.3), (0.64, 0.485), (0.58, 0.485), (0.98, 0.29)], -0.27, 0.27, 'team', 0.006, 1)
body.append(gl)
body.append(hull_from('visor', [(-0.24, 1.08, 0.2), (0.24, 1.08, 0.2), (-0.26, 1.13, 0.235), (0.26, 1.13, 0.235),
                                (-0.24, 1.08, 0.27), (0.24, 1.08, 0.27)], 'mech', 0.008, 1))
body.append(box('eye', (0, 1.135, 0.235), (0.36, 0.012, 0.026), 'glow', 0, 1))
for sx in (-1, 1):
    body.append(box(f'eyepod{sx}', (sx * 0.3, 1.095, 0.24), (0.05, 0.05, 0.05), 'hull_dark', 0.01, 1))
    body.append(box(f'eyelens{sx}', (sx * 0.3, 1.123, 0.24), (0.03, 0.006, 0.03), 'glow', 0, 1))
# Side bevel strips joining the hull to the pods (team on top, so the deck reads coloured).
for sx in (-1, 1):
    body.append(prism_x(f'sill{sx}', [(0.62, 0.4), (0.5, 0.44), (-0.7, 0.44), (-0.8, 0.4)],
                        sx * 0.36 - 0.06, sx * 0.36 + 0.06, 'team_dark', 0.006, 1))
# Rear deck: a heat-sink block with glowing fins, and a team plate over the engine bay.
body.append(box('sinkbase', (0, -0.82, 0.49), (0.5, 0.3, 0.05), 'hull_dark', 0.01, 1))
for i in range(6):
    x = -0.2 + i * 0.08
    body.append(box(f'fin{i}', (x, -0.82, 0.54), (0.02, 0.26, 0.07), 'metal', 0.004, 1))
    if i < 5:
        body.append(box(f'finglow{i}', (x + 0.04, -0.82, 0.52), (0.04, 0.22, 0.01), 'glow_dim', 0, 1))
body.append(prism_x('reartop', [(-0.62, 0.475), (-0.66, 0.475), (-0.98, 0.385), (-0.94, 0.385)], -0.24, 0.24, 'team', 0.004, 1))
# Rear lights and a towing bar.
for sx in (-1, 1):
    body.append(box(f'tail{sx}', (sx * 0.3, -1.01, 0.32), (0.08, 0.02, 0.03), 'glow_dim', 0, 1))
body.append(cyl('tow', (-0.15, -1.02, 0.2), (0.15, -1.02, 0.2), 0.02, 'metal', 6, bevel=0))
# Flush sensor domes either side of the turret ring.
for sx in (-1, 1):
    body.append(sphere(f'dome{sx}', (sx * 0.33, 0.42, 0.47), 0.045, 'glass', 10, 5))
    body.append(cyl(f'domering{sx}', (sx * 0.33, 0.42, 0.46), (sx * 0.33, 0.42, 0.48), 0.055, 'mech', 10, bevel=0))
# Turret ring.
body.append(cyl('tring', (0, -0.12, 0.46), (0, -0.12, 0.5), 0.3, 'mech', 20, bevel=0.006))

# ---- Turret: flat angular shell, pivot on the hull top ----
PIV = (0.0, -0.12, 0.46)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = []
shell = hull_from('tshell', [P(-0.16, 0.52, 0.04), P(0.16, 0.52, 0.04), P(-0.4, 0.2, 0.02), P(0.4, 0.2, 0.02),
                             P(-0.4, -0.38, 0.02), P(0.4, -0.38, 0.02), P(-0.3, -0.5, 0.04), P(0.3, -0.5, 0.04),
                             P(-0.1, 0.48, 0.14), P(0.1, 0.48, 0.14), P(-0.28, 0.16, 0.24), P(0.28, 0.16, 0.24),
                             P(-0.28, -0.36, 0.24), P(0.28, -0.36, 0.24), P(-0.22, -0.48, 0.18), P(0.22, -0.48, 0.18)],
                 'hull', bevel=0.022, segs=2)
inset_panels(shell, 0.025, 0.01, lambda f: f.normal.z < 0.7 and f.calc_area() > 0.02)
tur.append(shell)
# Team roof plate (chamfered) on top.
tur.append(hull_from('troof', [P(-0.25, 0.14, 0.24), P(0.25, 0.14, 0.24), P(-0.25, -0.34, 0.24), P(0.25, -0.34, 0.24),
                               P(-0.08, 0.44, 0.15), P(0.08, 0.44, 0.15),
                               P(-0.22, 0.12, 0.265), P(0.22, 0.12, 0.265), P(-0.22, -0.31, 0.265), P(0.22, -0.31, 0.265),
                               P(-0.06, 0.4, 0.17), P(0.06, 0.4, 0.17)], 'team', 0.008, 1))
# Cheek armour slabs, dark, angled.
for sx in (-1, 1):
    tur.append(hull_from(f'cheek{sx}', [P(sx * 0.4, 0.18, 0.03), P(sx * 0.44, 0.1, 0.05), P(sx * 0.44, -0.3, 0.05),
                                        P(sx * 0.4, -0.36, 0.03), P(sx * 0.4, 0.14, 0.16), P(sx * 0.4, -0.32, 0.16),
                                        P(sx * 0.35, 0.18, 0.03), P(sx * 0.35, -0.36, 0.03), P(sx * 0.35, 0.14, 0.17),
                                        P(sx * 0.35, -0.32, 0.17)], 'team_dark', 0.008, 1))
    for k in range(3):
        tur.append(box(f'cheekln{sx}{k}', P(sx * 0.425, 0.04 - k * 0.12, 0.1), (0.008, 0.07, 0.012), 'glow_dim', 0, 1))
# Gun: wedge mantlet, team shroud, slim barrel, and a slotted muzzle.
tur.append(hull_from('mantlet', [P(-0.13, 0.46, 0.04), P(0.13, 0.46, 0.04), P(-0.13, 0.46, 0.2), P(0.13, 0.46, 0.2),
                                 P(-0.09, 0.6, 0.07), P(0.09, 0.6, 0.07), P(-0.09, 0.6, 0.17), P(0.09, 0.6, 0.17)],
                     'hull_dark', 0.012, 1))
tur.append(vp.tapered('shroud', P(0, 0.58, 0.12), P(0, 0.98, 0.12), 0.13, 0.11, 0.1, 0.085, 'team', 0.01))
tur.append(box('shroudline', P(0, 0.78, 0.17), (0.02, 0.36, 0.01), 'glow_dim', 0, 1))
tur.append(cyl('barrel', P(0, 0.95, 0.12), P(0, 1.42, 0.12), 0.042, 'metal', 14))
tur.append(cyl('collar', P(0, 1.12, 0.12), P(0, 1.17, 0.12), 0.055, 'mech', 14, bevel=0.004))
tur.append(vp.tapered('muzzle', P(0, 1.4, 0.12), P(0, 1.53, 0.12), 0.11, 0.08, 0.095, 0.068, 'mech', 0.01))
for sx in (-1, 1):
    tur.append(box(f'mslot{sx}', P(sx * 0.052, 1.465, 0.12), (0.012, 0.05, 0.04), 'tread', 0, 1))
tur.append(cyl('bore', P(0, 1.525, 0.12), P(0, 1.54, 0.12), 0.03, 'glow', 10, bevel=0))
# Sensor eye on the turret face, gunner sight, a hatch, and a slim sensor mast.
tur += vp.lattice_eye('teye', P(0.22, 0.33, 0.17), 0.1)
tur.append(box('sight', P(-0.17, 0.02, 0.29), (0.1, 0.16, 0.06), 'hull_dark', 0.012, 1))
tur.append(box('sightlens', P(-0.17, 0.103, 0.29), (0.07, 0.006, 0.03), 'glow', 0, 1))
tur.append(cyl('hatch', P(0.12, -0.16, 0.265), P(0.12, -0.16, 0.285), 0.08, 'hull_dark', 12, bevel=0.004))
tur.append(cyl('mast', P(-0.2, -0.4, 0.2), P(-0.2, -0.44, 0.62), 0.01, 'mech', 5, bevel=0))
tur.append(sphere('masttip', P(-0.2, -0.44, 0.63), 0.022, 'glow', 8, 4))
tur.append(box('mastbase', P(-0.2, -0.4, 0.22), (0.06, 0.06, 0.06), 'metal', 0.008, 1))
# Rear bustle: a sensor box with fins.
tur.append(box('bustle', P(0, -0.56, 0.12), (0.4, 0.14, 0.14), 'hull_dark', 0.015, 1))
for k in range(4):
    tur.append(box(f'bfin{k}', P(-0.15 + k * 0.1, -0.64, 0.12), (0.02, 0.04, 0.11), 'metal', 0.003, 1))

vp.finish('lattice_strider', body, tur, PIV)

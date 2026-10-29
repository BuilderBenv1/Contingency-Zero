"""Crucible "Ironclad" heavy assault walker. Run: python tools/models/crucible_heavy_bot.py <out_dir>

A squat armoured walker built like a furnace on legs: thick stomper legs set wide, a broad
riveted hull with a sloped team glacis and a slit visor, big slab shoulder armour, a heavy
cannon on the right arm and a twin-coil laser on the left, a tall back plate with two hot
stacks. Much wider and heavier than the Brawler. The upper body is the Turret.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels
from bots_parts import crucible_leg, beam, obox, barrel, stack, finish, rivet_row

kit.reset('crucible')
body = []
for sx in (-1, 1):
    body += crucible_leg(f'leg{sx}', (sx * 0.34, -0.05, 0.5), (sx * 0.46, 0.05 if sx < 0 else -0.05, 0.0), 0.21)
body.append(box('pelvis', (0, -0.05, 0.53), (0.62, 0.38, 0.18), 'hull_dark', 0.02, 1))
body.append(box('belt', (0, -0.05, 0.6), (0.66, 0.4, 0.05), 'mech', 0.008, 1))
for sx in (-1, 1):
    body.append(box(f'skirt{sx}', (sx * 0.34, 0.06, 0.48), (0.2, 0.2, 0.16), 'hull', 0.015, 1))

PIV = (0, -0.05, 0.62)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = [cyl('waist', P(0, 0, -0.03), P(0, 0, 0.06), 0.2, 'mech', 10)]
hull = hull_from('torso', [P(-0.36, -0.28, 0.04), P(0.36, -0.28, 0.04), P(-0.34, 0.24, 0.04), P(0.34, 0.24, 0.04),
                           P(-0.4, -0.3, 0.34), P(0.4, -0.3, 0.34), P(-0.38, 0.32, 0.26), P(0.38, 0.32, 0.26),
                           P(-0.36, -0.28, 0.46), P(0.36, -0.28, 0.46), P(-0.32, 0.18, 0.46), P(0.32, 0.18, 0.46)], 'hull', 0.025, 2)
inset_panels(hull, 0.03, 0.01, lambda f: abs(f.normal.x) > 0.7)
tur.append(hull)
# Sloped team glacis with a visor slit and rivets
tur.append(hull_from('glacis', [P(-0.34, 0.19, 0.47), P(0.34, 0.19, 0.47), P(-0.38, 0.33, 0.27), P(0.38, 0.33, 0.27),
                                P(-0.34, 0.22, 0.49), P(0.34, 0.22, 0.49), P(-0.38, 0.36, 0.29), P(0.38, 0.36, 0.29)], 'team', 0.01, 1))
tur.append(box('visor', P(0, 0.3, 0.37), (0.36, 0.06, 0.035), 'mech', 0.004, 1))
tur.append(box('visorg', P(0, 0.335, 0.37), (0.3, 0.01, 0.018), 'glow', 0, 1))
tur += rivet_row('gr', P(-0.3, 0.355, 0.24), P(0.3, 0.355, 0.24), 6, 0.018)
tur.append(box('top', P(0, -0.06, 0.475), (0.62, 0.42, 0.03), 'team', 0.01, 1))
for sx in (-1, 1):
    # Slab shoulders
    tur.append(hull_from(f'sh{sx}', [P(sx * 0.38, -0.2, 0.2), P(sx * 0.38, 0.2, 0.2), P(sx * 0.38, -0.2, 0.5), P(sx * 0.38, 0.18, 0.5),
                                     P(sx * 0.6, -0.2, 0.22), P(sx * 0.6, 0.2, 0.22), P(sx * 0.56, -0.18, 0.48), P(sx * 0.56, 0.16, 0.48)], 'team', 0.015, 1))
    tur.append(box(f'shrim{sx}', P(sx * 0.5, 0, 0.19), (0.24, 0.42, 0.04), 'hull_dark', 0.006, 1))
    tur += rivet_row(f'shr{sx}', P(sx * 0.5, -0.16, 0.5), P(sx * 0.5, 0.14, 0.5), 4, 0.017)
    tur.append(beam(f'uarm{sx}', P(sx * 0.5, 0, 0.2), P(sx * 0.5, 0.02, 0.06), 0.13, 0.13, 'mech'))
# Right arm: heavy cannon
tur.append(box('cgun', P(0.5, 0.1, 0.06), (0.2, 0.46, 0.2), 'hull_dark', 0.018, 1))
tur.append(box('cguntop', P(0.5, 0.06, 0.165), (0.15, 0.3, 0.02), 'team_dark', 0.004, 1))
tur += barrel('cbar', P(0.5, 0.33, 0.06), P(0.5, 0.78, 0.06), 0.055)
tur.append(cyl('cjack', P(0.5, 0.33, 0.06), P(0.5, 0.45, 0.06), 0.075, 'mech', 10))
# Left arm: twin-coil laser
tur.append(box('lgun', P(-0.5, 0.08, 0.06), (0.18, 0.4, 0.18), 'hull_dark', 0.018, 1))
for dz in (-0.04, 0.05):
    tur.append(cyl(f'lbar{dz}', P(-0.5, 0.28, 0.06 + dz), P(-0.5, 0.62, 0.06 + dz), 0.026, 'metal', 8))
    tur.append(cyl(f'ltip{dz}', P(-0.5, 0.62, 0.06 + dz), P(-0.5, 0.645, 0.06 + dz), 0.03, 'glow', 8, bevel=0))
for k in range(3):
    tur.append(cyl(f'lcoil{k}', P(-0.5, 0.32 + k * 0.07, 0.065), P(-0.5, 0.34 + k * 0.07, 0.065), 0.09, 'glow_dim', 10, bevel=0))
# Back plate and stacks
tur.append(box('back', P(0, -0.33, 0.36), (0.6, 0.08, 0.4), 'hull_dark', 0.015, 1))
for sx in (-1, 1):
    tur += stack(f'stack{sx}', P(sx * 0.2, -0.36, 0.42), 0.06, 0.32)
tur.append(box('pack', P(0, -0.42, 0.22), (0.3, 0.12, 0.2), 'accent', 0.012, 1))
for k in range(3):
    tur.append(box(f'pv{k}', P(0, -0.485, 0.15 + k * 0.06), (0.22, 0.01, 0.02), 'glow_dim', 0, 1))

finish('crucible_heavy_bot', body, tur, PIV)

"""Crucible "Spark" scout bot. Run: python tools/models/crucible_scout_bot.py <out_dir>

A scuttling, lamp-eyed runner: a small riveted box body leaning forward on short stomper legs,
a big round searchlight eye, a little hot exhaust stack, a whip antenna, and a stubby gun.
Matches the procedural model: hips at 0.5, body z 0.48..0.76, gun at (0.2, y 0.2..0.5, z 0.56).
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels
from bots_parts import crucible_leg, beam, obox, stack, barrel, finish, add, lerp, rivet_row

kit.reset('crucible')
body = []
for sx in (-1, 1):
    body += crucible_leg(f'leg{sx}', (sx * 0.2, -0.06, 0.5), (sx * 0.26, 0.07 if sx < 0 else -0.07, 0.0), 0.095, bend=0.12)

# Body: a wedge, leaning forward, team top and riveted seams
b = hull_from('body', [(-0.22, 0.28, 0.5), (0.22, 0.28, 0.5), (-0.23, -0.34, 0.48), (0.23, -0.34, 0.48),
                       (-0.2, 0.36, 0.62), (0.2, 0.36, 0.62), (-0.23, -0.3, 0.82), (0.23, -0.3, 0.82)], 'hull', 0.02, 2)
inset_panels(b, 0.02, 0.008, lambda f: abs(f.normal.x) > 0.7)
body.append(b)
body.append(hull_from('top', [(-0.19, 0.32, 0.64), (0.19, 0.32, 0.64), (-0.21, -0.28, 0.82), (0.21, -0.28, 0.82),
                              (-0.17, 0.3, 0.67), (0.17, 0.3, 0.67), (-0.19, -0.26, 0.85), (0.19, -0.26, 0.85)], 'team', 0.01, 1))
body += rivet_row('tr0', (-0.17, 0.24, 0.69), (-0.17, -0.22, 0.85), 4, 0.014)
body += rivet_row('tr1', (0.17, 0.24, 0.69), (0.17, -0.22, 0.85), 4, 0.014)
for sx in (-1, 1):
    body.append(box(f'side{sx}', (sx * 0.24, -0.02, 0.62), (0.03, 0.4, 0.16), 'team_dark', 0.008, 1))
# Searchlight eye in a square hood
body.append(box('hood', (0, 0.34, 0.56), (0.2, 0.08, 0.16), 'mech', 0.012, 1))
body.append(cyl('lamp', (0, 0.36, 0.56), (0, 0.42, 0.56), 0.075, 'metal', 12))
body.append(cyl('lampg', (0, 0.42, 0.56), (0, 0.43, 0.56), 0.06, 'glow', 12, bevel=0))
body.append(obox('visor', (0, 0.4, 0.645), (0.2, 0.08, 0.02), 'mech', (-15, 0, 0), 0.004))
# Stack, antenna and gun
body.append(cyl('exh0', (-0.13, -0.2, 0.8), (-0.14, -0.4, 1.0), 0.04, 'metal', 8))
body.append(cyl('exh1', (-0.14, -0.4, 1.0), (-0.14, -0.43, 1.03), 0.046, 'glow', 8, bevel=0))
body.append(cyl('ant', (0.14, -0.24, 0.83), (0.18, -0.4, 1.2), 0.008, 'metal', 5, bevel=0))
body.append(sphere('anttip', (0.18, -0.4, 1.21), 0.016, 'glow', 8, 5))
body.append(box('gunmount', (0.25, 0.12, 0.56), (0.07, 0.16, 0.08), 'hull_dark', 0.008, 1))
body += barrel('gun', (0.25, 0.18, 0.56), (0.25, 0.5, 0.56), 0.032, brake=True)
body.append(box('pack', (0, -0.38, 0.62), (0.3, 0.1, 0.18), 'accent', 0.01, 1))

finish('crucible_scout_bot', body)

"""Crucible "Rivet Gun" long-range rifle bot. Run: python tools/models/crucible_sniper_bot.py <out_dir>

A tall, stilt-legged walker carrying one enormous rifle: a narrow riveted body with a big
round rangefinder eye, a long barrel with a slotted muzzle brake resting on a fold-down brace,
a drum magazine and a counterweight block at the back. Taller and leaner than the Brawler,
with a gun nearly twice its length. The upper body is the Turret.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere
from bots_parts import crucible_leg, beam, obox, barrel, stack, finish, rivet_row

kit.reset('crucible')
body = []
for sx in (-1, 1):
    body += crucible_leg(f'leg{sx}', (sx * 0.22, -0.05, 0.74), (sx * 0.3, 0.06 if sx < 0 else -0.06, 0.0), 0.13, bend=0.26)
body.append(box('pelvis', (0, -0.05, 0.76), (0.4, 0.26, 0.14), 'hull_dark', 0.02, 1))

PIV = (0, -0.05, 0.84)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = [cyl('waist', P(0, 0, -0.04), P(0, 0, 0.05), 0.14, 'mech', 8)]
tur.append(hull_from('torso', [P(-0.2, -0.22, 0.03), P(0.2, -0.22, 0.03), P(-0.2, 0.18, 0.03), P(0.2, 0.18, 0.03),
                               P(-0.22, -0.24, 0.3), P(0.22, -0.24, 0.3), P(-0.2, 0.2, 0.28), P(0.2, 0.2, 0.28)], 'hull', 0.02, 2))
tur.append(box('top', P(0, -0.02, 0.305), (0.4, 0.4, 0.025), 'team', 0.008, 1))
tur.append(hull_from('chest', [P(-0.19, 0.19, 0.27), P(0.19, 0.19, 0.27), P(-0.19, 0.23, 0.06), P(0.19, 0.23, 0.06),
                               P(-0.19, 0.22, 0.27), P(0.19, 0.22, 0.27), P(-0.19, 0.26, 0.06), P(0.19, 0.26, 0.06)], 'team', 0.006, 1))
tur += rivet_row('chr', P(-0.15, 0.25, 0.22), P(0.15, 0.25, 0.22), 4, 0.014)
# Rangefinder eye on the left shoulder
tur.append(cyl('eyeh', P(-0.24, 0.0, 0.38), P(-0.24, 0.2, 0.38), 0.085, 'hull_dark', 12))
tur.append(cyl('eyer', P(-0.24, 0.2, 0.38), P(-0.24, 0.23, 0.38), 0.095, 'mech', 12, bevel=0))
tur.append(cyl('eye', P(-0.24, 0.23, 0.38), P(-0.24, 0.235, 0.38), 0.06, 'glow', 12, bevel=0))
tur.append(beam('eyearm', P(-0.18, 0.0, 0.28), P(-0.24, 0.05, 0.36), 0.05, 0.05, 'mech'))
# The rifle along the right side: receiver, long barrel, brake, brace, drum
X = 0.22
tur.append(box('rcv', P(X, 0.05, 0.16), (0.14, 0.5, 0.15), 'hull_dark', 0.014, 1))
tur.append(box('rcvtop', P(X, 0.05, 0.24), (0.1, 0.4, 0.02), 'team_dark', 0.004, 1))
tur += rivet_row('rr', P(X + 0.075, -0.12, 0.16), P(X + 0.075, 0.24, 0.16), 5, 0.012)
tur.append(cyl('bar', P(X, 0.3, 0.17), P(X, 1.25, 0.17), 0.032, 'metal', 10, r2=0.028))
tur.append(cyl('jack', P(X, 0.3, 0.17), P(X, 0.55, 0.17), 0.05, 'mech', 10))
for k in range(4):
    tur.append(cyl(f'fin{k}', P(X, 0.36 + k * 0.045, 0.17), P(X, 0.375 + k * 0.045, 0.17), 0.058, 'glow_dim', 10, bevel=0))
tur.append(box('brake', P(X, 1.22, 0.17), (0.09, 0.12, 0.07), 'mech', 0.01, 1))
for k in range(3):
    tur.append(box(f'bslot{k}', P(X, 1.18 + k * 0.035, 0.208), (0.07, 0.012, 0.01), 'glow', 0, 1))
tur.append(beam('brace', P(X, 0.62, 0.15), P(X, 0.78, 0.04), 0.03, 0.03, 'mech'))
tur.append(cyl('drum', P(X - 0.1, -0.04, 0.1), P(X - 0.1, 0.12, 0.1), 0.07, 'accent', 12))
tur.append(cyl('scope', P(X, 0.0, 0.29), P(X, 0.3, 0.29), 0.035, 'mech', 10))
tur.append(cyl('scopeg', P(X, 0.3, 0.29), P(X, 0.305, 0.29), 0.025, 'glow', 10, bevel=0))
for sx in (-1, 1):
    tur.append(box(f'sh{sx}', P(sx * 0.24, -0.02, 0.24), (0.1, 0.24, 0.12), 'team', 0.012, 1))
tur.append(beam('arm', P(0.24, 0.0, 0.2), P(X, 0.12, 0.1), 0.07, 0.07, 'mech'))
tur.append(beam('arm2', P(-0.22, 0.02, 0.18), P(X - 0.04, 0.38, 0.13), 0.06, 0.06, 'hull'))
# Counterweight and a short stack behind
tur.append(box('cw', P(0.04, -0.3, 0.14), (0.3, 0.14, 0.2), 'hull_dark', 0.014, 1))
tur.append(box('cwg', P(0.04, -0.375, 0.14), (0.2, 0.01, 0.03), 'glow_dim', 0, 1))
tur += stack('stack', P(-0.1, -0.2, 0.31), 0.04, 0.18)

finish('crucible_sniper_bot', body, tur, PIV)

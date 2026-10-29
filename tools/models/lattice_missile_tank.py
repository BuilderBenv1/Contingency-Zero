"""Lattice "Volley" missile tank. Run: python tools/models/lattice_missile_tank.py <out_dir>

Tier 2 rocket vehicle on the Prism's slim twin-pod chassis, lengthened and armoured with team
side skirts. Its turret is a broad armoured launcher: two tall missile boxes pitched up either
side of a sensor head with a tracking eye, hitting ground and air alike.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import t2_base as T
import kit
from kit import box, hull_from, cyl, sphere
import vehicles_parts as vp
from bots_parts import missile_pod

c = T.capture('lattice_flak_vehicle')
import bpy
for o in c.turret:
    bpy.data.objects.remove(o, do_unlink=True)
body = c.body
T.transform(body, (1.05, 1.12, 1.0))
for sx in (-1, 1):
    body.append(hull_from(f'skirt{sx}', [(sx * 0.82, -0.95, 0.18), (sx * 0.82, 1.0, 0.18), (sx * 0.9, -0.9, 0.42), (sx * 0.9, 0.95, 0.42),
                                         (sx * 0.84, -0.85, 0.5), (sx * 0.84, 0.9, 0.5)], 'team', 0.012, 1))
    body.append(box(f'skirtg{sx}', (sx * 0.905, 0.4, 0.36), (0.01, 0.5, 0.03), 'glow_dim', 0, 1))
PIV = (0.0, -0.18, 0.46)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = []
tur.append(cyl('ring', P(0, 0, -0.02), P(0, 0, 0.06), 0.42, 'mech', 16, bevel=0.01))
tur.append(hull_from('base', [P(-0.36, -0.4, 0.04), P(0.36, -0.4, 0.04), P(-0.3, 0.42, 0.04), P(0.3, 0.42, 0.04),
                              P(-0.3, -0.34, 0.2), P(0.3, -0.34, 0.2), P(-0.2, 0.32, 0.2), P(0.2, 0.32, 0.2)], 'hull', 0.015, 1))
tur.append(box('basetop', P(0, -0.02, 0.205), (0.36, 0.5, 0.01), 'team', 0.004, 1))
for sx in (-1, 1):
    tur += missile_pod(f'pod{sx}', P(sx * 0.42, 0.0, 0.38), (0.26, 0.6, 0.3), 25, 2, 3, 'hull', 'accent', straps=2)
    tur.append(box(f'podarm{sx}', P(sx * 0.29, 0.0, 0.26), (0.06, 0.3, 0.12), 'mech', 0.008, 1))
tur.append(hull_from('head', [P(-0.13, -0.12, 0.2), P(0.13, -0.12, 0.2), P(-0.1, 0.28, 0.2), P(0.1, 0.28, 0.2),
                              P(-0.1, -0.1, 0.44), P(0.1, -0.1, 0.44), P(-0.07, 0.18, 0.42), P(0.07, 0.18, 0.42)], 'hull_dark', 0.012, 1))
tur += vp.lattice_eye('eye', P(0, 0.26, 0.3), 0.14)
tur.append(cyl('mast', P(0, -0.08, 0.44), P(0, -0.12, 0.74), 0.012, 'metal', 6, bevel=0))
tur.append(sphere('masttip', P(0, -0.12, 0.75), 0.03, 'glow', 8, 5))

vp.finish('lattice_missile_tank', body, tur, PIV)

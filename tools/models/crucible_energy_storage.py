"""Crucible Energy Storage: three massive battery cells round a glowing core. Run: python <this> <out_dir>

Riveted cylinder cells with glowing charge bands and hoops, team-painted caps carrying chunky
terminals, heavy bus bars to a central core pillar that glows, hazard-striped bases, and a
cable trunk to a switch box. Footprint 2 (16 units): kept inside 15 across.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('crucible')
body = []
E = 7.2
body.append(box('apron', (0, 0, 0.25), (2 * E, 2 * E, 0.5), 'hull_dark', 0.08))
for s in (-1, 1):
    body += ep.rivet_line(f'arx{s}', (-E + 0.4, s * (E - 0.25), 0.5), (E - 0.4, s * (E - 0.25), 0.5), 12, 'metal', 0.07)
    body += ep.rivet_line(f'ary{s}', (s * (E - 0.25), -E + 0.9, 0.5), (s * (E - 0.25), E - 0.9, 0.5), 10, 'metal', 0.07)
CR, CH = 2.55, 4.4
for i in range(3):
    a = i * 2 * math.pi / 3 + math.pi / 2
    c = (math.cos(a) * 3.7, math.sin(a) * 3.7)
    # Hazard-striped plinth (accent / dark segments).
    for k in range(12):
        aa = k * 2 * math.pi / 12
        body.append(ep.beam(f'haz{i}{k}', (c[0] + math.cos(aa) * (CR + 0.3), c[1] + math.sin(aa) * (CR + 0.3), 0.5),
                            (c[0] + math.cos(aa) * (CR + 0.3), c[1] + math.sin(aa) * (CR + 0.3), 0.85), 1.35, 0.3,
                            'accent' if k % 2 else 'tread', 0.0))
    body.append(cyl(f'plinth{i}', (c[0], c[1], 0.5), (c[0], c[1], 0.85), CR + 0.25, 'hull_dark', 12, bevel=0.02))
    body.append(cyl(f'cell{i}', (c[0], c[1], 0.85), (c[0], c[1], 0.85 + CH), CR, 'hull', 18, bevel=0.04))
    for z in (1.4, 4.3):
        body.append(cyl(f'hoop{i}{z}', (c[0], c[1], z), (c[0], c[1], z + 0.25), CR + 0.08, 'mech', 18, bevel=0.01))
        body += ep.rivet_ring(f'hr{i}{z}', c, CR + 0.09, z + 0.125, 12, 'metal', 0.06)
    body.append(cyl(f'charge{i}', (c[0], c[1], 2.3), (c[0], c[1], 3.4), CR + 0.04, 'glow', 18, bevel=0))
    for k in range(6):
        aa = k * math.pi / 3 + a
        body.append(box(f'grill{i}{k}', (0, 0, 0), (0.3, 0.45, 1.25), 'mech', 0.02, 1))
        body[-1].data.transform(Matrix.Translation((c[0] + math.cos(aa) * (CR + 0.1), c[1] + math.sin(aa) * (CR + 0.1), 2.85)) @ Matrix.Rotation(aa, 4, 'Z'))
    top = 0.85 + CH
    body.append(cyl(f'cap{i}', (c[0], c[1], top), (c[0], c[1], top + 0.5), CR + 0.1, 'team', 18, r2=CR - 0.25, bevel=0.03))
    body += ep.rivet_ring(f'cr{i}', c, CR - 0.45, top + 0.52, 10, 'metal', 0.07)
    # Terminals: positive glows, negative is dark.
    for s, paint in ((-1, 'glow'), (1, 'tread')):
        tx, ty = c[0] + math.cos(a + math.pi / 2) * s * 0.85, c[1] + math.sin(a + math.pi / 2) * s * 0.85
        body.append(cyl(f'term{i}{s}', (tx, ty, top + 0.5), (tx, ty, top + 1.1), 0.36, 'metal', 10, bevel=0.02))
        body.append(cyl(f'termcap{i}{s}', (tx, ty, top + 1.1), (tx, ty, top + 1.3), 0.3, paint, 10, bevel=0.01))
    # Bus bar from the cell top to the core.
    body.append(ep.beam(f'bus{i}', (c[0] - math.cos(a) * 0.5, c[1] - math.sin(a) * 0.5, top + 0.75), (math.cos(a) * 1.0, math.sin(a) * 1.0, 5.4),
                        0.45, 0.28, 'accent', 0.03))
# Core pillar: dark casing with glowing slots and a hot cap.
body.append(cyl('core', (0, 0, 0.5), (0, 0, 5.6), 1.15, 'hull_dark', 12, bevel=0.03))
for k in range(6):
    a = k * math.pi / 3 + math.pi / 6
    body.append(box(f'coreslot{k}', (0, 0, 0), (0.12, 0.35, 3.4), 'glow', 0, 1))
    body[-1].data.transform(Matrix.Translation((math.cos(a) * 1.13, math.sin(a) * 1.13, 2.9)) @ Matrix.Rotation(a, 4, 'Z'))
body.append(cyl('corecap', (0, 0, 5.6), (0, 0, 6.0), 1.35, 'mech', 12, bevel=0.03))
body.append(cyl('coreglow', (0, 0, 6.0), (0, 0, 6.2), 0.8, 'glow', 12, bevel=0))
body += ep.stack('vent', (0.0, 0.0, 6.0), 0.3, 0.9)
# Switch box and cable trunk at the front-right.
body.append(box('switch', (5.3, -5.2, 1.3), (2.2, 1.4, 1.6), 'hull', 0.05))
body.append(box('switchroof', (5.3, -5.2, 2.17), (2.4, 1.6, 0.14), 'team', 0.02))
body.append(box('switchlamp', (4.7, -5.92, 1.7), (0.3, 0.04, 0.2), 'glow', 0, 1))
body += ep.pipe('trunk', [(4.4, -4.6, 0.75), (1.0, -1.0, 0.75), (0.8, -0.8, 1.2)], 0.17, 'tread')

B = kit.join('Body', body)
ep.finish('crucible_energy_storage', [B], 1024, bake_scale=0.3)

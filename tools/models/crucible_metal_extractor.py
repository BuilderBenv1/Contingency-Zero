"""Crucible Metal Extractor: a riveted drilling rig straddling the ore. Run: python <this> <out_dir>

Open skid frame on four pads so the ore shows through, a tapering girder derrick over a glowing
wellhead, a diesel engine house with a smokestack, an ore conveyor tipping into a hopper, and a
spinning crown head (Rotor_Y) with a team-painted walking beam and counterweights.
Footprint 1 (8 units): kept inside 7 across.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels
import economy_parts as ep

UID = 'crucible_metal_extractor'
DEEP = False


def build(deep):
    kit.reset('crucible')
    body = []
    H = 6.2 if deep else 4.0           # derrick top
    BH = 1.75 if deep else 1.6          # derrick base half-width
    # ---- Skid frame on four pads, open in the middle so the ore shows ----
    for sx in (-1, 1):
        for sy in (-1, 1):
            body.append(box(f'pad{sx}{sy}', (sx * 2.75, sy * 2.75, 0.14), (1.2, 1.2, 0.28), 'hull_dark', 0.05))
            body.append(box(f'padtop{sx}{sy}', (sx * 2.75, sy * 2.75, 0.33), (0.9, 0.9, 0.12), 'team', 0.03))
            body += ep.rivet_ring(f'padr{sx}{sy}', (sx * 2.75, sy * 2.75), 0.5, 0.29, 6, 'metal', 0.05)
        body.append(box(f'skid{sx}', (sx * 2.75, 0, 0.42), (0.42, 5.0, 0.36), 'hull', 0.04))
        body.append(box(f'cross{sx}', (0, sx * 2.75, 0.42), (5.0, 0.36, 0.32), 'hull', 0.04))
        body += ep.rivet_line(f'skr{sx}', (sx * 2.75, -2.2, 0.61), (sx * 2.75, 2.2, 0.61), 6, 'metal', 0.045)
    # Diagonal ties from the skids to the derrick feet.
    for sx in (-1, 1):
        for sy in (-1, 1):
            body.append(ep.beam(f'tie{sx}{sy}', (sx * 2.75, sy * 2.2, 0.55), (sx * BH, sy * BH, 0.6), 0.2, 0.16, 'mech', 0))
    # ---- Wellhead over the bore: a glowing hole, collar, and a blow-out stack ----
    body.append(ep.ring('bore', (0, 0), 1.25, 0.8, 0.0, 0.32, 'hull_dark', 12, bevel=0.03))
    body.append(cyl('boreglow', (0, 0, 0.02), (0, 0, 0.06), 0.82, 'glow', 16, bevel=0))
    body.append(ep.ring('collar', (0, 0), 0.95, 0.42, 0.3, 0.55, 'mech', 12))
    body.append(box('bop', (0, 0, 0.95), (0.9, 0.9, 0.7), 'hull', 0.06))
    body.append(cyl('bopband', (0, 0, 0.75), (0, 0, 0.9), 0.62, 'accent', 12, bevel=0.01))
    for k in range(4):
        a = k * math.pi / 2
        body.append(cyl(f'valve{k}', (math.cos(a) * 0.45, math.sin(a) * 0.45, 1.0), (math.cos(a) * 0.72, math.sin(a) * 0.72, 1.0), 0.11, 'metal', 8, bevel=0))
        body.append(cyl(f'wheel{k}', (math.cos(a) * 0.72, math.sin(a) * 0.72, 1.0), (math.cos(a) * 0.78, math.sin(a) * 0.78, 1.0), 0.17, 'accent', 10, bevel=0))
    # Drill string (pipe) running up through the derrick, with tool joints.
    body.append(cyl('kelly', (0, 0, 1.3), (0, 0, H + 0.2), 0.26, 'metal', 10, bevel=0.01))
    for z in [1.6 + i * (H - 1.8) / 4 for i in range(4)]:
        body.append(cyl(f'joint{z:.1f}', (0, 0, z), (0, 0, z + 0.18), 0.33, 'mech', 10, bevel=0.01))
    # ---- Derrick: a tapering four-leg girder tower ----
    body += ep.tower_truss('der', BH, 0.95, 0.55, H, 'hull_dark', bays=5 if deep else 3, leg=0.26, brace=0.1, brace_paint='mech')
    # Drill floor half way up: grated platform with rails.
    fz = 1.75
    for sx in (-1, 1):
        body.append(box(f'floor{sx}', (sx * 0.95, 0, fz), (0.8, 2.6, 0.12), 'hull', 0.02))
    body += ep.rail('frail', [(-1.4, -1.35, fz), (1.4, -1.35, fz)], 0.55, 'accent', r=0.03)
    if deep:
        # Racking platform two-thirds up: a team-painted walkway ringing the derrick, with rails.
        mz = 3.5
        for k in range(4):
            a = k * math.pi / 2
            ca, sa = round(math.cos(a)), round(math.sin(a))
            body.append(box(f'mon{k}', (ca * 1.62, sa * 1.62, mz), (0.62 if ca else 3.86, 0.62 if sa else 3.86, 0.14), 'team', 0.03))
        body += ep.rail('mrail', [(-1.93, -1.93, mz), (1.93, -1.93, mz), (1.93, 1.93, mz), (-1.93, 1.93, mz)], 0.5, 'accent', r=0.03, closed=True)
        body += ep.ladder('mlad', (1.2, -1.97, fz), mz, 'mech', w=0.35, normal=(0, -1, 0))
    # Crown block on top: team-painted housing with rivets.
    body.append(box('crown', (0, 0, H + 0.3), (2.3, 2.3, 0.6), 'hull', 0.06))
    crown_top = box('crowntop', (0, 0, H + 0.64), (2.0, 2.0, 0.1), 'team', 0.03)
    body.append(crown_top)
    for sy in (-1, 1):
        body += ep.rivet_line(f'crr{sy}', (-0.95, sy * 1.17, H + 0.45), (0.95, sy * 1.17, H + 0.45), 5, 'metal', 0.045)
    # ---- Engine house on the left skid with a smokestack ----
    ex = -2.55
    body.append(box('engine', (ex, 0.15, 1.15), (1.3, 2.4, 1.15), 'hull', 0.06))
    roof = hull_from('eroof', [(ex - 0.68, -1.08, 1.7), (ex + 0.68, -1.08, 1.7), (ex - 0.68, 1.38, 1.7), (ex + 0.68, 1.38, 1.7),
                              (ex - 0.45, -1.0, 2.0), (ex + 0.45, -1.0, 2.0), (ex - 0.45, 1.3, 2.0), (ex + 0.45, 1.3, 2.0)], 'team', 0.04)
    inset_panels(roof, 0.06, 0.02, lambda f: f.normal.z > 0.5)
    body.append(roof)
    for i in range(5):
        body.append(box(f'louvre{i}', (ex - 0.66, -0.6 + i * 0.3, 1.15), (0.04, 0.2, 0.7), 'mech', 0.01, 1))
    body.append(box('rad', (ex, -1.12, 1.1), (1.0, 0.1, 0.8), 'mech', 0.02))
    for i in range(4):
        body.append(box(f'radfin{i}', (ex - 0.36 + i * 0.24, -1.18, 1.1), (0.06, 0.04, 0.7), 'metal', 0, 1))
    body += ep.stack('stack', (ex + 0.1, 1.0, 2.0), 0.24, (3.2 if deep else 2.4))
    if deep:
        body += ep.stack('stack2', (ex + 0.1, 0.35, 2.0), 0.2, 2.6)
    # Drive shaft / chain housing from the engine to the wellhead.
    body.append(ep.beam('chain', (ex + 0.6, 0.2, 1.0), (-0.45, 0.0, 1.0), 0.36, 0.3, 'accent', 0.03))
    # ---- Ore conveyor up to a hopper on the right ----
    hx, hy = 2.45, -1.55
    body.append(ep.beam('conv', (0.55, -0.35, 0.7), (hx - 0.3, hy + 0.2, 2.2), 0.5, 0.18, 'hull_dark', 0.02))
    for i in range(6):
        t = (i + 0.5) / 6
        p = (0.55 + (hx - 0.85) * t, -0.35 + (hy + 0.55) * t, 0.7 + 1.5 * t + 0.11)
        body.append(box(f'ore{i}', p, (0.3, 0.26, 0.1), 'tread', 0.03, 1))
    for t in (0.35, 0.8):
        x, y, z = 0.55 + (hx - 0.85) * t, -0.35 + (hy + 0.55) * t, 0.7 + 1.5 * t
        body.append(ep.rod(f'convleg{t}', (x, y, 0.5), (x, y, z), 0.06, 'mech'))
    body.append(hull_from('hopper', [(hx - 0.35, hy - 0.35, 0.55), (hx + 0.35, hy - 0.35, 0.55), (hx - 0.35, hy + 0.35, 0.55), (hx + 0.35, hy + 0.35, 0.55),
                                      (hx - 0.7, hy - 0.7, 2.0), (hx + 0.7, hy - 0.7, 2.0), (hx - 0.7, hy + 0.7, 2.0), (hx + 0.7, hy + 0.7, 2.0)], 'accent', 0.04))
    body.append(box('hopperlip', (hx, hy, 2.02), (1.5, 1.5, 0.1), 'hull_dark', 0.02))
    for k, (ox, oy) in enumerate(((-0.25, -0.2), (0.2, 0.15), (0.05, -0.3), (-0.15, 0.25), (0.3, -0.2))):
        body.append(sphere(f'orelump{k}', (hx + ox, hy + oy, 1.95), 0.26, 'tread', 6, 4))
    for sx in (-1, 1):
        for sy in (-1, 1):
            body.append(ep.rod(f'hleg{sx}{sy}', (hx + sx * 0.55, hy + sy * 0.55, 0.45), (hx + sx * 0.55, hy + sy * 0.55, 1.6), 0.07, 'mech'))
    # Warning lamp on the crown and a work light on the floor.
    body.append(cyl('lamp', (0.85, 0.85, H + 0.69), (0.85, 0.85, H + 0.85), 0.1, 'glow', 8, bevel=0))
    body.append(box('worklight', (1.15, -1.25, fz + 0.15), (0.25, 0.15, 0.2), 'mech', 0.02, 1))
    body.append(box('worklens', (1.15, -1.33, fz + 0.15), (0.18, 0.02, 0.12), 'glow', 0, 1))

    # ---- Rotor: the spinning crown head with a walking beam and counterweights ----
    PIV = (0, 0, H + 0.7)
    P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
    rot = []
    rot.append(cyl('hub', P(0, 0, 0), P(0, 0, 0.45), 0.55, 'mech', 12, bevel=0.02))
    rot.append(cyl('hubcap', P(0, 0, 0.45), P(0, 0, 0.6), 0.36, 'metal', 12, bevel=0.02))
    for k in range(12):
        a = k * math.pi / 6
        rot.append(box(f'gear{k}', P(math.cos(a) * 0.58, math.sin(a) * 0.58, 0.12), (0.14, 0.14, 0.2), 'metal', 0.01, 1))
    arms = [0, math.pi / 2] if deep else [0]
    span = 2.0 if deep else 1.75
    for ai, a in enumerate(arms):
        ca, sa = math.cos(a), math.sin(a)
        arm = ep.beam(f'arm{ai}', P(-ca * span, -sa * span, 0.32), P(ca * span, sa * span, 0.32), 0.62, 0.42, 'team', 0.04)
        rot.append(arm)
        for s in (-1, 1):
            c = P(s * ca * span, s * sa * span, 0.32)
            rot.append(box(f'cw{ai}{s}', c, (0.7 if ai == 0 else 0.6, 0.7 if ai == 1 else 0.6, 0.72), 'hull_dark', 0.05))
            rot.append(box(f'cwt{ai}{s}', (c[0], c[1], c[2] + 0.38), (0.5, 0.5, 0.06), 'accent', 0.01, 1))
            rot += ep.rivet_line(f'armr{ai}{s}', P(s * ca * 0.75 - sa * 0.18, s * sa * 0.75 + ca * 0.18, 0.55),
                                 P(s * ca * (span - 0.5) - sa * 0.18, s * sa * (span - 0.5) + ca * 0.18, 0.55), 3, 'metal', 0.04)
    B = kit.join('Body', body)
    R = kit.join('Rotor_Y', rot, parent_origin=PIV)
    return [B, R]


if __name__ == '__main__':
    parts = build(DEEP)
    ep.finish(UID, parts, 512, bake_scale=0.5)

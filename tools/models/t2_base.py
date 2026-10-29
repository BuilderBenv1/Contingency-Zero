"""Shared helpers for tier 2 refits and heavier variants built on a tier 1 model script.

capture('lattice_bot_lab') runs that script with its finish() call intercepted and returns the
parts it would have exported, still as separate Blender objects, so a tier 2 script can reshape
them (scale, move, drop pieces) and add its own before calling the real finish under a new id.
The tier 1 script resets the scene itself, so call capture() before building anything else.
"""
import os, sys, runpy
sys.path.insert(0, os.path.dirname(__file__))
import bpy
import kit
from mathutils import Matrix, Vector
import bots_parts, vehicles_parts, airsea_parts, factories_parts

_HERE = os.path.dirname(os.path.abspath(__file__))


class Captured:
    """What the tier 1 script handed to finish: body objects, optional turret/rotor and pivots."""
    def __init__(self):
        self.body = []
        self.turret = None
        self.pivot = None
        self.rotor = None
        self.rotor_name = None
        self.rotor_pivot = None


def capture(script):
    cap = Captured()
    saved = (bots_parts.finish, vehicles_parts.finish, airsea_parts.finish, factories_parts.finish)

    def mobile(uid, body, turret=None, pivot=None, size=512):
        cap.body, cap.turret, cap.pivot = list(body), (list(turret) if turret else None), pivot

    def air(uid, parts, size=512):
        for name, objs, pivot in parts:
            if name == 'Body':
                cap.body = list(objs)
            elif name == 'Turret':
                cap.turret, cap.pivot = list(objs), pivot
            else:
                cap.rotor, cap.rotor_name, cap.rotor_pivot = list(objs), name, pivot

    def factory(uid, out_dir, body, rotor=None, rotor_name=None, rotor_pivot=None, s=4.0):
        cap.body = list(body)
        if rotor:
            cap.rotor, cap.rotor_name, cap.rotor_pivot = list(rotor), rotor_name, rotor_pivot

    bots_parts.finish = vehicles_parts.finish = mobile
    airsea_parts.finish = air
    factories_parts.finish = factory
    try:
        runpy.run_path(os.path.join(_HERE, script + '.py'), run_name='__t2__')
    finally:
        bots_parts.finish, vehicles_parts.finish, airsea_parts.finish, factories_parts.finish = saved
    return cap


def transform(objs, scale=(1, 1, 1), move=(0, 0, 0), pivot=(0, 0, 0)):
    """Scales (per axis) about pivot then moves, baked into each object's mesh."""
    if not objs:
        return objs
    p = Vector(pivot)
    S = Matrix.Diagonal(Vector(tuple(scale) + (1.0,)))
    M = Matrix.Translation(p + Vector(move)) @ S @ Matrix.Translation(-p)
    for o in objs:
        o.data.transform(M)
    return objs


def drop(objs, *prefixes):
    """Removes objects whose names start with any prefix; returns the kept list."""
    import bpy
    kept = []
    for o in objs:
        if any(o.name.startswith(pf) for pf in prefixes):
            bpy.data.objects.remove(o, do_unlink=True)
        else:
            kept.append(o)
    return kept


def bounds(objs):
    lo = [min((o.matrix_world @ v.co)[i] for o in objs for v in o.data.vertices) for i in range(3)]
    hi = [max((o.matrix_world @ v.co)[i] for o in objs for v in o.data.vertices) for i in range(3)]
    return lo, hi

"""Crucible Deep Bore: the metal extractor's rig built taller and heavier. Run: python <this> <out_dir>

Same skid, wellhead and hopper as crucible_metal_extractor (shared build), but a five-bay derrick,
twin smokestacks and a four-armed crown head with counterweights (Rotor_Y).
"""
import os, sys
sys.path.insert(0, os.path.dirname(__file__))
import economy_parts as ep
import crucible_metal_extractor as cme

if __name__ == '__main__':
    ep.finish('crucible_deep_extractor', cme.build(True), 512, bake_scale=0.5)

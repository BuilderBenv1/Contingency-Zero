"""Lattice Deep Extractor: the tripod rig built taller, with a caged glowing core, a team-painted
core flange and a wider haloed three-blade head (Rotor_Y). Shares lattice_metal_extractor.build.
Run: python <this> <out_dir>
"""
import os, sys
sys.path.insert(0, os.path.dirname(__file__))
import economy_parts as ep
import lattice_metal_extractor as lme

if __name__ == '__main__':
    ep.finish('lattice_deep_extractor', lme.build(True), 512, bake_scale=0.5)

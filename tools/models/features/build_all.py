"""Rebuilds every nature / debris model: trees, rocks, wrecks + rubble, ground props (+ props.txt).
Run from the repo root: /tmp/claude-0/bpyenv/bin/python tools/models/features/build_all.py [out_dir]
(out_dir defaults to game/models/features). Each script runs in its own Blender process.
Set FAST=1 in the environment for tiny bakes while iterating."""
import os, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
out = sys.argv[1] if len(sys.argv) > 1 else os.path.join('game', 'models', 'features')
for script in ('trees.py', 'rocks.py', 'debris.py', 'props.py'):
    print('==', script, flush=True)
    subprocess.run([sys.executable, os.path.join(HERE, script), out], check=True)

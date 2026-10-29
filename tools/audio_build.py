"""Builds the game's recorded sound effects and ambience loops from CC0 source libraries.

Every sound is designed here by layering, pitching, filtering and mixing CC0 recordings, then
loudness-matched to the synthesised sound it replaces (so the mix balance tuned in
SfxSynth.Traits still holds) and written as mono Ogg Vorbis to game/audio/. A credits file listing
every source used is written to game/audio/CREDITS.md.

Sources (all CC0 1.0, cloned from GitHub):
  open-game-sfx-index  https://github.com/Legerdo/open-game-sfx-index  (Kenney + OpenGameArt packs)
  sfx-kit              https://github.com/wyzowl/sfx-kit
  ambientsounds        https://github.com/Muges/ambientsounds  (only its CC0 files: wind, fireplace)

Usage:
  python tools/audio_build.py <folder holding the three clones> [--synth <dir of synth .f32 dumps>]

Needs ffmpeg, numpy and scipy. Deterministic: the same sources give the same output.
"""
from __future__ import annotations

import glob
import json
import os
import subprocess
import sys

import numpy as np
from scipy import signal

SR = 44100
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_SFX = os.path.join(ROOT, "game", "audio", "sfx")
OUT_AMB = os.path.join(ROOT, "game", "audio", "ambience")

SRC = ""
USED: dict[str, tuple[str, str]] = {}  # relative path -> (licence, original source)
_INDEX: dict[str, str] = {}
_CACHE: dict[str, np.ndarray] = {}


# ---- Loading ---------------------------------------------------------------------------------------

def _index() -> None:
    meta = json.load(open(os.path.join(SRC, "open-game-sfx-index", "index.json")))["sounds"]
    for s in meta:
        for key in ("file_ogg", "file_wav", "file_flac", "file_mp3"):
            if s.get(key):
                _INDEX[f"{s['pack']}/{os.path.basename(s[key]).rsplit('.', 1)[0]}"] = s["license"] + "|" + s["source"]


def L(name: str) -> np.ndarray:
    """Loads a source by short name: 'k:<pack>/<file>' (open-game-sfx-index), 'x:<folder>/<file>'
    (sfx-kit) or 'a:<file>' (ambientsounds). Mono float32 at 44.1 kHz, peak-normalised."""
    if name in _CACHE:
        return _CACHE[name].copy()
    lib, rest = name.split(":", 1)
    if lib == "k":
        hits = glob.glob(os.path.join(SRC, "open-game-sfx-index", "audio", rest + ".*"))
        lic, src = _INDEX[rest].split("|", 1)
    elif lib == "x":
        hits = glob.glob(os.path.join(SRC, "sfx-kit", "sounds", rest + ".wav"))
        lic, src = "CC0-1.0", "https://github.com/wyzowl/sfx-kit"
    elif lib == "a":
        hits = glob.glob(os.path.join(SRC, "ambientsounds", rest + ".ogg"))
        lic, src = "CC0-1.0", {"wind": "https://freesound.org/people/felix.blume/sounds/139337/",
                               "fireplace": "https://freesound.org/people/inchadney/sounds/132534/"}[rest]
    else:
        raise ValueError(name)
    if not hits:
        raise FileNotFoundError(name)
    if lic != "CC0-1.0":
        raise ValueError(f"{name} is {lic}, not CC0")
    path = hits[0]
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-ac", "1", "-ar", str(SR), "-f", "f32le", "-"],
                         capture_output=True, check=True).stdout
    x = np.frombuffer(raw, np.float32).astype(np.float64)
    x /= max(1e-9, np.abs(x).max())
    USED[os.path.relpath(path, SRC)] = (lic, src)
    _CACHE[name] = x
    return x.copy()


# ---- Processing ------------------------------------------------------------------------------------

def seg(x, start, dur=None):
    a = int(start * SR)
    b = len(x) if dur is None else a + int(dur * SR)
    return x[a:b].copy()


def pitch(x, ratio):
    """Plays the sound at `ratio` speed (0.8 = lower and longer), like a tape."""
    if abs(ratio - 1) < 1e-4:
        return x
    up, down = 1000, int(round(1000 * ratio))
    return signal.resample_poly(x, up, down)


def _sos(kind, f, order=2):
    if isinstance(f, (tuple, list)):
        f = [min(max(v, 10), SR / 2 - 100) for v in f]
    else:
        f = min(max(f, 10), SR / 2 - 100)
    return signal.butter(order, f, btype=kind, fs=SR, output="sos")


def lp(x, f, order=2): return signal.sosfilt(_sos("lowpass", f, order), x)
def hp(x, f, order=2): return signal.sosfilt(_sos("highpass", f, order), x)
def bp(x, lo, hi, order=2): return signal.sosfilt(_sos("bandpass", (lo, hi), order), x)


def db(v): return 10 ** (v / 20)


def fade(x, fin=0.002, fout=0.05):
    x = x.copy()
    n = min(len(x), int(fin * SR))
    if n > 0:
        x[:n] *= np.linspace(0, 1, n)
    n = min(len(x), int(fout * SR))
    if n > 0:
        x[-n:] *= np.linspace(1, 0, n) ** 2
    return x


def decay(x, t60):
    """Imposes an exponential decay reaching -60 dB at t60 seconds."""
    t = np.arange(len(x)) / SR
    return x * 10 ** (-3 * t / t60)


def sat(x, drive=1.5):
    """Soft-clip saturation: grit and density without hard clipping."""
    return np.tanh(x * drive) / np.tanh(drive)


def mix(*layers):
    """Each layer is (signal, offset seconds, gain dB)."""
    n = max(int(o * SR) + len(s) for s, o, _ in layers)
    out = np.zeros(n)
    for s, o, g in layers:
        a = int(o * SR)
        out[a:a + len(s)] += s * db(g)
    return out


def rng(seed): return np.random.default_rng(seed)


def crack(seed, dur=0.03, lo=1800, hi=9000):
    """A synthetic muzzle-blast transient: a burst of band-limited noise with a very fast decay."""
    n = rng(seed).standard_normal(int(dur * SR) + int(0.2 * SR))
    return decay(bp(n, lo, hi), dur * 2.0)


def outdoor(x, t60=0.8, wet=-12, seed=1, echo=0.0, echo_db=-14, tone=3000):
    """Open-air tail: diffuse decaying noise convolved in, plus an optional distant slap-back."""
    ir_len = int(t60 * SR)
    r = rng(seed)
    ir = lp(r.standard_normal(ir_len), tone) * 10 ** (-3 * np.arange(ir_len) / SR / t60)
    ir[: int(0.012 * SR)] *= np.linspace(0, 1, int(0.012 * SR))
    ir /= np.sqrt((ir ** 2).sum())
    wet_sig = signal.fftconvolve(x, ir)
    out = np.zeros(len(wet_sig) + (int(echo * SR) if echo else 0))
    out[: len(x)] += x
    out[: len(wet_sig)] += wet_sig * db(wet)
    if echo:
        e = lp(x, 1500)
        a = int(echo * SR)
        out[a:a + len(e)] += e * db(echo_db)
    return out


def trim_tail(x, floor_db=-60):
    env = np.sqrt(np.convolve(x * x, np.ones(441) / 441, "same"))
    above = np.where(env > env.max() * db(floor_db))[0]
    end = min(len(x), (above[-1] if len(above) else len(x)) + int(0.02 * SR))
    return fade(x[:end], 0.0005, min(0.08, end / SR * 0.25))


# ---- Loudness --------------------------------------------------------------------------------------

def _kweight(x, sr):
    # ITU-R BS.1770 K-weighting at 48 kHz; resample first so the coefficients apply.
    if sr != 48000:
        x = signal.resample_poly(x, 480, sr // 100)
    x = signal.lfilter([1.53512485958697, -2.69169618940638, 1.19839281085285],
                       [1, -1.69065929318241, 0.73248077421585], x)
    return signal.lfilter([1.0, -2.0, 1.0], [1, -1.99004745483398, 0.99007225036621], x)


def loudness(x, sr=SR):
    """Peak short-term loudness: the loudest 100 ms of K-weighted power, in dB."""
    k = _kweight(np.asarray(x, np.float64), sr)
    win = int(0.1 * 48000)
    p = np.convolve(k * k, np.ones(win) / win, "valid") if len(k) > win else np.array([np.mean(k * k)])
    return 10 * np.log10(p.max() + 1e-12)


def limit(x, ceiling_db=-1.0):
    """Brings peaks under the ceiling with a smooth tanh knee (only when they exceed it)."""
    c = db(ceiling_db)
    pk = np.abs(x).max()
    if pk <= c:
        return x
    knee = 0.7 * c
    y = x.copy()
    over = np.abs(y) > knee
    y[over] = np.sign(y[over]) * (knee + (c - knee) * np.tanh((np.abs(y[over]) - knee) / (c - knee)))
    return y


def write_ogg(path, x, quality=4):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2").tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(SR), "-ac", "1", "-i", "-",
                    "-c:a", "libvorbis", "-q:a", str(quality), "-map_metadata", "-1", path],
                   input=pcm, check=True)


# ---- Sound designs ---------------------------------------------------------------------------------
# Each returns a list of variants. Short names: k: = open-game-sfx-index, x: = sfx-kit, a: = ambientsounds.

SCI = "k:sci-fi-sounds/"
IMP = "k:impact-sounds/"
RD1 = "k:oga-rubberduck-100-foley/"
RD2 = "k:oga-rubberduck-foley-100-v2/sfx100v2_"
RDS = "k:oga-rubberduck-scifi-50/"
RDB = "k:oga-rubberduck-breaking-75/bfh1_"
RDM = "k:oga-rubberduck-metal-wood-100/"
RDL = "k:oga-rubberduck-loops-30/"


def click():
    # A chunky panel switch.
    return [mix((L("x:interface/click-rocker-firm"), 0, 0), (lp(L("x:mechanical/mechanical-switch-heavy"), 3000), 0, -8))]

def hover():
    # Barely there: a soft relay tick, dulled.
    return [lp(L("x:interface/click-latch-soft"), 2500)]

def laser():
    out = []
    for i, (src, p) in enumerate([("laserLarge_000", 0.82), ("laserLarge_001", 0.9), ("laserLarge_003", 0.78)]):
        z = pitch(L(SCI + src), p)
        thump = lp(L("x:impacts/impact-heavy-firm"), 400)
        x = mix((z, 0, 0), (thump, 0, -9), (crack(10 + i, 0.012, 2500, 8000), 0, -14))
        out.append(outdoor(sat(x, 1.3), 0.35, -18, seed=20 + i, tone=4000))
    return out


def cannon():
    out = []
    clanks = ["impactMetal_heavy_000", "impactMetal_heavy_001", "impactMetal_heavy_003"]
    bodies = [0.92, 0.84, 0.88]
    for i in range(3):
        top = hp(seg(L(RD1 + ("shot_02" if i != 1 else "shot_01")), 0, 0.25), 900)
        body = pitch(L("x:impacts/impact-heavy-hard"), bodies[i])
        boom = lp(seg(L(SCI + ["explosionCrunch_000", "explosionCrunch_001", "explosionCrunch_000"][i]), 0, 0.6), 900)
        clank = hp(L(IMP + clanks[i]), 300)
        x = mix((crack(30 + i, 0.02), 0, -4), (top, 0, -5), (body, 0, 0), (fade(boom, 0.001, 0.25), 0.004, -3),
                (clank, 0.05, -15))
        x = outdoor(sat(x, 1.8), 0.7, -11, seed=40 + i, echo=0.21 + 0.03 * i, echo_db=-20)
        out.append(x)
    return out


def artillery():
    out = []
    for i in range(3):
        top = hp(seg(L(RD1 + "shot_01"), 0, 0.4), 700)
        deep = pitch(L("x:impacts/impact-deep-hard"), [1.0, 0.9, 0.95][i])
        boom = seg(L("x:booms-and-bass/boom-hit-strong"), 0, 1.3)
        lf = L(SCI + "lowFrequency_explosion_001")
        plate = hp(L(IMP + f"impactPlate_heavy_00{i}"), 400)
        x = mix((crack(50 + i, 0.035, 1200, 8000), 0, -2), (top, 0, -4), (deep, 0, 0), (fade(boom, 0.001, 0.4), 0, -2),
                (lf, 0.01, -5), (plate, 0.07, -17))
        out.append(outdoor(sat(x, 2.0), 1.3, -9, seed=60 + i, echo=0.32 + 0.05 * i, echo_db=-15))
    return out


def missile():
    out = []
    whooshes = ["whoosh-medium-hard", "whoosh-fast-hard", "whoosh-medium-hard"]
    for i in range(3):
        ign = fade(seg(L(RDS + "rocket_01"), 0.25 + 0.15 * i, 0.9), 0.01, 0.4)
        rush = pitch(L("x:whooshes/" + whooshes[i]), [0.8, 0.75, 0.7][i])
        thr = fade(lp(seg(L(SCI + f"thrusterFire_00{i}"), 0.5, 0.8), 3000), 0.02, 0.5)
        pop = lp(L("x:impacts/impact-heavy-firm"), 600)
        x = mix((pop, 0, -4), (crack(70 + i, 0.015, 1500, 6000), 0, -10), (ign, 0, -2), (rush, 0.02, -3), (thr, 0.03, -9))
        out.append(outdoor(sat(x, 1.4), 0.6, -14, seed=80 + i))
    return out


def flak():
    out = []
    for i in range(3):
        top = hp(L(RD1 + "shot_02"), 1200)
        body = pitch(L("x:impacts/impact-heavy-hard"), [1.15, 1.25, 1.1][i])
        burst = hp(fade(seg(L(SCI + "explosionCrunch_002"), 0.2 + 0.1 * i, 0.3), 0.003, 0.15), 250)
        x = mix((crack(90 + i, 0.015), 0, -3), (top, 0, -4), (body, 0, -2), (burst, 0.03, -8))
        out.append(outdoor(sat(x, 1.8), 0.5, -14, seed=100 + i, echo=0.18, echo_db=-22))
    return out


def bomb():
    # The release: a latch clunk and the heavy rush of a falling bomb.
    out = []
    for i in range(3):
        latch = L("x:mechanical/mechanical-latch-heavy")
        rush = lp(pitch(L("x:whooshes/" + ["whoosh-slow-hard", "whoosh-slow-medium", "whoosh-slow-hard"][i]),
                        [0.62, 0.68, 0.56][i]), 2500)
        x = mix((latch, 0, -4), (L(IMP + "impactMetal_medium_000"), 0.01, -12), (rush, 0.05, 0))
        out.append(outdoor(x, 0.5, -16, seed=110 + i))
    return out


def dgun():
    out = []
    for i in range(3):
        field = pitch(L(SCI + f"forceField_00{i}"), 0.55)
        zap = pitch(L(SCI + "laserLarge_002"), [0.5, 0.45, 0.55][i])
        lf = L(SCI + "lowFrequency_explosion_001")
        deep = L("x:impacts/impact-deep-hard")
        x = mix((crack(120 + i, 0.03, 1000, 7000), 0, -4), (zap, 0, -1), (fade(field, 0.005, 0.4), 0, -6),
                (deep, 0, -2), (lf, 0.01, -4))
        out.append(outdoor(sat(x, 2.2), 1.0, -10, seed=130 + i, echo=0.3, echo_db=-16))
    return out


def impact_small():
    out = []
    metals = ["impactMetal_heavy_002", "impactPlate_heavy_001", "impactMetal_heavy_004"]
    for i in range(3):
        hit = L(IMP + metals[i])
        dirt = hp(fade(seg(L(SCI + "explosionCrunch_002"), 0.25 + 0.15 * i, 0.25), 0.002, 0.15), 300)
        rock = L(RDB + ["rock_hit_01", "metal_hit_05", "metal_hit_01"][i])
        x = mix((crack(140 + i, 0.012, 2000, 9000), 0, -8), (hit, 0, -2), (rock, 0.005, -7), (dirt, 0.01, -6),
                (pitch(L("x:impacts/impact-heavy-firm"), 1.2), 0, -6))
        out.append(outdoor(sat(x, 1.5), 0.45, -16, seed=150 + i))
    return out


def explosion_small():
    out = []
    srcs = ["explosionCrunch_000", "explosionCrunch_001", "explosionCrunch_003"]
    debris = ["metal_falling_03", "metal_falling_04", "rock_falling_05"]
    for i in range(3):
        body = L(SCI + srcs[i])
        top = hp(pitch(seg(L(RD1 + "shot_01"), 0, 0.4), 0.7), 800)
        x = mix((crack(160 + i, 0.03, 900, 7000), 0, -4), (top, 0, -6), (body, 0, 0),
                (pitch(L("x:impacts/impact-heavy-hard"), 0.8), 0, -3), (hp(L(RDB + debris[i]), 300), 0.18, -13))
        out.append(outdoor(sat(x, 1.8), 0.9, -10, seed=170 + i, echo=0.25, echo_db=-20))
    return out


def explosion_large():
    out = []
    for i in range(3):
        lf = pitch(L(SCI + "lowFrequency_explosion_000"), [1.0, 0.9, 0.95][i])
        crunch = L(SCI + ["explosionCrunch_004", "explosionCrunch_003", "explosionCrunch_001"][i])
        boom = seg(L("x:booms-and-bass/boom-hit-strong"), 0, 1.6)
        top = hp(pitch(seg(L(RD1 + "shot_01"), 0, 0.5), 0.55), 600)
        deb1 = hp(L(RDB + ["metal_falling_01", "metal_falling_02", "rock_falling_01"][i]), 250)
        deb2 = hp(L(RDB + ["rock_falling_03", "metal_falling_05", "rock_falling_09"][i]), 250)
        x = mix((crack(180 + i, 0.05, 700, 6000), 0, -3), (top, 0, -5), (crunch, 0, -1), (lf, 0, -2), (boom, 0, -4),
                (deb1, 0.32, -12), (deb2, 0.55, -14))
        out.append(outdoor(sat(x, 2.0), 1.6, -8, seed=190 + i, echo=0.4, echo_db=-14))
    return out


def explosion_huge():
    out = []
    for i in range(2):
        thunder = L("x:cinematic-hits/" + ["cinematic-thunder-massive", "cinematic-thunder-big"][i])
        drop = L("x:booms-and-bass/boom-drop-strong")
        lf = pitch(L(SCI + "lowFrequency_explosion_000"), 0.7)
        rumble = seg(L("x:booms-and-bass/bass-rumble-long"), 1.0, 3.5)
        crunch = pitch(L(SCI + "explosionCrunch_004"), 0.8)
        top = hp(pitch(seg(L(RD1 + "shot_01"), 0, 0.6), 0.45), 500)
        deb = [hp(L(RDB + n), 200) for n in ("metal_falling_01", "rock_falling_01", "metal_falling_02", "rock_falling_03")]
        x = mix((crack(200 + i, 0.08, 500, 5000), 0, -2), (top, 0, -4), (thunder, 0, -2), (lf, 0, 0), (crunch, 0, -3),
                (drop, 0.02, -3), (fade(rumble, 0.5, 1.0), 0.3, -6),
                (deb[0], 0.6, -12), (deb[1], 0.9, -13), (deb[2], 1.4, -15), (deb[3], 1.9, -16))
        out.append(outdoor(sat(x, 1.8), 2.2, -9, seed=210 + i, echo=0.55, echo_db=-12))
    return out


def build_start():
    lock = L("x:mechanical/mechanical-lock-heavy")
    air = hp(L(RD2 + "air_03"), 600)
    return [mix((lock, 0, 0), (L(IMP + "impactMetal_medium_000"), 0.02, -8), (air, 0.12, -10))]


def build_complete():
    # Heavy doors seat home: a slam, a lock and a hydraulic sigh.
    slam = L(RDM + "metal_slam_01")
    lock = L("x:mechanical/mechanical-lock-heavy")
    air = lp(hp(L(RD2 + "air_02"), 400), 5000)
    return [outdoor(mix((slam, 0, 0), (lock, 0.08, -5), (fade(air, 0.02, 0.4), 0.15, -9)), 0.5, -16, seed=300)]

def unit_ready():
    # A factory bay opening: shutter clunk and a short hiss.
    shutter = L("x:mechanical/mechanical-shutter-heavy")
    air = hp(L(RD2 + "air_03"), 500)
    return [mix((shutter, 0, 0), (L(IMP + "impactMetal_heavy_000"), 0, -9), (air, 0.06, -8))]

def nano():
    # A short buzz of the construction beam: force-field hum over a machine whine, faded at both ends.
    out = []
    for i in range(3):
        field = seg(L(SCI + f"forceField_00{i + 2}"), 0.1, 0.5)
        mach = hp(seg(L(RD2 + "loop_machine_02"), 1.0 + 2.3 * i, 0.5), 500)
        out.append(fade(mix((field, 0, -2), (mach, 0, -6)), 0.06, 0.18))
    return out


def reclaim():
    out = []
    for i in range(2):
        mine = L(IMP + f"impactMining_00{i}")
        crunch = hp(L(RDB + ["metal_hit_02", "rock_breaking_03"][i]), 300)
        x = mix((mine, 0, 0), (crunch, 0.03, -6), (fade(seg(L(SCI + "forceField_001"), 0.2, 0.35), 0.03, 0.2), 0, -12))
        out.append(x)
    return out


def radio(seed, dur=0.09):
    """A short field-radio squelch burst: band-limited static with a hard key-up click."""
    st = bp(seg(L("x:digital/digital-radio"), 0.2 + 0.1 * seed, dur), 500, 3200)
    return fade(st, 0.001, dur * 0.5)


def select():
    # Radio key-up: a switch and a burst of squelch.
    return [mix((L("x:mechanical/mechanical-switch-light"), 0, -2), (radio(0, 0.07), 0.005, -4))]

def acknowledge():
    # Order received: a heavier switch, a squelch, and the radio key dropping.
    return [mix((L("x:mechanical/mechanical-switch-heavy"), 0, -1), (radio(1, 0.11), 0.01, -5),
                (L("x:mechanical/mechanical-switch-light"), 0.13, -8))]

def error():
    return [L("x:digital/digital-denied")]


def alert():
    # A klaxon: the two-tone alarm, lowered and roughened.
    return [sat(pitch(L("x:digital/digital-breach"), 0.8), 2.0)]

def mission_message():
    # Incoming transmission: squelch, static, squelch.
    static = hp(fade(seg(L("x:digital/digital-radio"), 0.1, 0.32), 0.01, 0.08), 600)
    return [mix((L("x:mechanical/mechanical-switch-light"), 0, -3), (radio(2, 0.08), 0, -3), (static, 0.07, -8),
                (radio(3, 0.06), 0.36, -4))]

def victory():
    # One great orchestral-sized hit to open the victory music.
    return [outdoor(mix((L("x:cinematic-hits/cinematic-drum-big"), 0, 0), (L("x:cinematic-hits/cinematic-metal-big"), 0, -4),
                        (seg(L("x:booms-and-bass/boom-hit-strong"), 0, 1.4), 0, -6)), 1.5, -12, seed=310)]


def defeat():
    # The base dying: a sinking boom and power draining away.
    return [mix((L("x:booms-and-bass/boom-drop-strong"), 0, 0), (lp(L("x:digital/digital-power-down"), 1800), 0.1, -6),
                (L("x:cinematic-hits/cinematic-metal-big"), 0, -8))]


DESIGNS = {
    "Click": click, "Hover": hover,
    "Laser": laser, "Cannon": cannon, "Artillery": artillery, "Missile": missile, "Flak": flak, "Bomb": bomb,
    "DGun": dgun,
    "ImpactSmall": impact_small, "ExplosionSmall": explosion_small, "ExplosionLarge": explosion_large,
    "ExplosionHuge": explosion_huge,
    "BuildStart": build_start, "BuildComplete": build_complete, "UnitReady": unit_ready, "Nano": nano,
    "Reclaim": reclaim,
    "Select": select, "Acknowledge": acknowledge, "Error": error, "Alert": alert, "MissionMessage": mission_message,
    "Victory": victory, "Defeat": defeat,
}

# Extra loudness offset (dB) on top of matching the synth: heavier weapons and blasts a touch louder.
TRIM = {"Cannon": 1.5, "Artillery": 1.5, "ExplosionSmall": 1.0, "ExplosionLarge": 1.5, "ExplosionHuge": 1.0,
        "Laser": -1.0, "Nano": -2.0, "Hover": -8.0, "Acknowledge": -3.0, "Select": -2.0}


# ---- Ambience --------------------------------------------------------------------------------------

def loopify(x, seconds, xfade=3.0):
    """Cuts `seconds` (+ crossfade) and folds the tail into the head so the loop has no seam."""
    n, f = int(seconds * SR), int(xfade * SR)
    x = x[: n + f].copy()
    w = np.sin(np.linspace(0, np.pi / 2, f)) ** 2
    head = x[:f] * w + x[n:n + f] * (1 - w)
    return np.concatenate([head, x[f:n]])


def tile(x, seconds):
    reps = int(np.ceil(seconds * SR / len(x))) + 1
    return np.tile(x, reps)[: int(seconds * SR)]


def ambience():
    wind = L("a:wind")
    fire = L("a:fireplace")
    secs = 48.0
    d = {}
    d["desert"] = lp(hp(seg(wind, 2, secs + 3), 60), 2200)
    arctic_wind = hp(pitch(seg(wind, 60, (secs + 3) * 1.2), 1.18), 180)
    hiss = tile(hp(L(RDL + "noise_02"), 2500), secs + 3)
    d["arctic"] = mix((arctic_wind[: int((secs + 3) * SR)], 0, 0), (lp(hiss, 7000), 0, -20))
    d["temperate"] = lp(hp(seg(wind, 110, secs + 3), 80), 1400)
    boil = tile(pitch(L(RDL + "water_boiling"), 0.55), secs + 3)
    rumble = tile(seg(L("x:booms-and-bass/bass-rumble-long"), 0.5, 3.8), secs + 3)
    d["lava"] = mix((lp(seg(fire, 5, secs + 3), 3500), 0, 0), (lp(boil, 1200), 0, -7), (rumble, 0, -9))
    out = {}
    for name, x in d.items():
        x = loopify(x[: int((secs + 3) * SR)], secs)
        x *= db(-30 - loudness(x))  # every theme at the same level; the game sets the playback volume
        out[name] = limit(x, -3)
    return out


# ---- Main ------------------------------------------------------------------------------------------

def synth_loudness(dump_dir, kind):
    vals = []
    for p in sorted(glob.glob(os.path.join(dump_dir, f"{kind}_*.f32"))):
        vals.append(loudness(np.fromfile(p, "<f4").astype(np.float64), 22050))
    return float(np.mean(vals)) if vals else None


def main():
    global SRC
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        sys.exit(1)
    SRC = os.path.abspath(args[0])
    dump = args[args.index("--synth") + 1] if "--synth" in args else None
    _index()
    for f in glob.glob(os.path.join(OUT_SFX, "*.ogg")) + glob.glob(os.path.join(OUT_AMB, "*.ogg")):
        os.remove(f)
    total = 0
    targets = {}
    if dump:
        for kind in DESIGNS:
            targets[kind] = synth_loudness(dump, kind)
        json.dump(targets, open(os.path.join(ROOT, "tools", "audio_targets.json"), "w"), indent=1, sort_keys=True)
    else:
        targets = json.load(open(os.path.join(ROOT, "tools", "audio_targets.json")))
    levels = {}
    for kind, fn in DESIGNS.items():
        variants = fn()
        # One gain for all variants keeps their relative levels; matched to the synth's loudness.
        mean_l = float(np.mean([loudness(v) for v in variants]))
        g = (targets[kind] - mean_l + TRIM.get(kind, 0.0))
        variants = [trim_tail(v * db(g)) for v in variants]
        # Files can't exceed full scale, so a sound that would is turned down to a -1.5 dBFS peak and the
        # game plays it back louder by the same amount (RecordedSfx.g.cs; Godot mixes in floating point).
        peak = max(np.abs(v).max() for v in variants)
        lift = max(0.0, 20 * np.log10(peak) + 1.5)
        levels[kind] = (len(variants), round(lift, 2))
        for i, v in enumerate(variants):
            path = os.path.join(OUT_SFX, f"{kind}_{i + 1}.ogg")
            write_ogg(path, v * db(-lift))
            total += os.path.getsize(path)
        print(f"{kind:15s} {len(variants)} variants, target {targets[kind]:6.1f} dB, gain {g:+5.1f} dB, lift {lift:4.1f} dB")
    write_table(levels)
    for name, x in ambience().items():
        path = os.path.join(OUT_AMB, f"{name}.ogg")
        write_ogg(path, x, quality=3)
        total += os.path.getsize(path)
        print(f"ambience {name}: {len(x) / SR:.0f} s")
    print(f"total {total / 1e6:.1f} MB")
    write_credits()


def write_table(levels):
    rows = "\n".join(f'        ["{k}"] = ({n}, {lift:.2f}f),' for k, (n, lift) in levels.items())
    code = f"""// Generated by tools/audio_build.py. Do not edit.
using System.Collections.Generic;

namespace ContingencyZero.Game.Audio;

/// <summary>The recorded sounds in res://audio/sfx: variants per kind, and the playback lift (dB) for files
/// turned down to fit under full scale.</summary>
internal static class RecordedSfx
{{
    public static readonly Dictionary<string, (int Variants, float LiftDb)> Kinds = new()
    {{
{rows}
    }};
}}
"""
    open(os.path.join(ROOT, "game", "src", "Audio", "RecordedSfx.g.cs"), "w").write(code)


def write_credits():
    lines = ["# Audio credits", "",
             "Every recorded sound in Contingency Zero is built by `tools/audio_build.py` from the CC0 (public domain)",
             "recordings below, layered and processed. CC0 needs no attribution; this list is a courtesy and a licence",
             "record. The music is listed at the end.", "",
             "| Source file | Licence | Origin |", "|---|---|---|"]
    for rel, (lic, src) in sorted(USED.items()):
        name = rel.replace("open-game-sfx-index/audio/", "").replace("sfx-kit/sounds/", "sfx-kit/")
        lines.append(f"| {name} | {lic} | {src} |")
    lines += ["", "Collections: Kenney (kenney.nl), rubberduck on OpenGameArt (opengameart.org), sfx-kit by wyzowl,",
              "felix.blume and inchadney on Freesound, indexed by github.com/Legerdo/open-game-sfx-index and",
              "github.com/Muges/ambientsounds.", ""]
    open(os.path.join(ROOT, "game", "audio", "CREDITS.md"), "w").write("\n".join(lines))


if __name__ == "__main__":
    main()

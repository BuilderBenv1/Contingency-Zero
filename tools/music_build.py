"""Builds the game's music from CC0 orchestral tracks on OpenGameArt.

Download the files listed in TRACKS (their pages say CC0) into one folder, then run:
  python tools/music_build.py <folder>

Each track is loudness-normalised (EBU R128, -18 LUFS; outros -16) and written as stereo Ogg Vorbis to
game/audio/music/. Menu and battle music are played as shuffled, crossfaded playlists by MusicLibrary.
Needs ffmpeg. The licence record is appended to game/audio/CREDITS.md.
"""
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "game", "audio", "music")

# out name: (downloaded file, title, author, page, extra ffmpeg filter or None, loudness target)
TRACKS = {
    "menu_1": ("cz_war_theme.ogg", "War Theme", "Spring Spring", "https://opengameart.org/content/war-theme", None, -18),
    "menu_2": ("cz_ChoirChordsBassMaster.wav", "Epic Endgame Cinematic", "cynicmusic",
               "https://opengameart.org/content/epic-endgame-cinematic", None, -18),
    "battle_1": ("cz_determined_pursuit_loop.wav", "Determined Pursuit (Epic Orchestra Loop)", "Emma_MA",
                 "https://opengameart.org/content/determined-pursuit-epic-orchestra-loop", None, -18),
    "battle_2": ("cz_battleThemeA.mp3", "Battle Theme A", "cynicmusic", "https://opengameart.org/content/battle-theme-a",
                 None, -18),
    "battle_3": ("cz_call_to_war.wav", "Call to War", "Umplix", "https://opengameart.org/content/call-to-war", None, -18),
    "battle_4": ("cz_wolfgang_Battle.mp3", "Battle Theme", "Wolfgang_", "https://opengameart.org/content/battle-theme-0",
                 None, -18),
    "battle_5": ("cz_war_theme_ver2.ogg", "War Theme (ver 2)", "Spring Spring", "https://opengameart.org/content/war-theme",
                 None, -18),
    "battle_6": ("cz_march2.ogg", "Some Militaristic Tune", "Spring Spring",
                 "https://opengameart.org/content/some-militaristic-tune", None, -18),
    "battle_7": ("cz_wowchapter3.ogg", "War on Water: Chapter 3", "yd", "https://opengameart.org/content/war-on-water-tracks",
                 None, -18),
    "victory": ("cz_Production_Intro_Theme.wav", "Production Theme (Intro, Epic Orchestra)", "nene",
                "https://opengameart.org/content/production-theme-intro-epic-orchestra", None, -16),
    # The closing bars of the endgame theme, slowed and darkened, fading out.
    "defeat": ("cz_ChoirChordsBassMaster.wav", "Epic Endgame Cinematic (closing bars, slowed)", "cynicmusic",
               "https://opengameart.org/content/epic-endgame-cinematic",
               "atrim=start=66,asetpts=PTS-STARTPTS,asetrate=44100*0.84,aresample=44100,asetpts=N/SR/TB,"
               "lowpass=f=2400,afade=t=out:st=24:d=6,atrim=end=30", -17),
}


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    src = sys.argv[1]
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        if f.endswith(".ogg"):
            os.remove(os.path.join(OUT, f))
    total = 0
    for name, (file, _title, _author, _page, extra, lufs) in TRACKS.items():
        filters = ",".join(x for x in [extra, f"loudnorm=I={lufs}:TP=-1.5:LRA=14", "aresample=44100"] if x)
        dest = os.path.join(OUT, f"{name}.ogg")
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", os.path.join(src, file), "-af", filters, "-ac", "2",
                        "-c:a", "libvorbis", "-q:a", "3", "-map_metadata", "-1", dest], check=True)
        total += os.path.getsize(dest)
        print(f"{name:9s} {os.path.getsize(dest) / 1e6:5.1f} MB  {file}")
    print(f"total {total / 1e6:.1f} MB")
    credits = os.path.join(ROOT, "game", "audio", "CREDITS.md")
    text = open(credits).read().split("\n## Music")[0].rstrip() + "\n\n## Music\n\n" \
        "Orchestral tracks from OpenGameArt, all CC0 (public domain), loudness-matched by `tools/music_build.py`.\n\n" \
        "| In game | Track | Composer | Licence | Page |\n|---|---|---|---|---|\n"
    for name, (_file, title, author, page, _extra, _l) in TRACKS.items():
        text += f"| {name} | {title} | {author} | CC0-1.0 | {page} |\n"
    open(credits, "w").write(text)


if __name__ == "__main__":
    main()

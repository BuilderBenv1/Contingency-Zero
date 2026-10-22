using ContingencyZero.Game.Audio;

namespace ContingencyZero.Game.App;

/// <summary>Global access to the one music player, so any screen can change the track.</summary>
public static class MusicHost
{
    private static MusicPlayer? _player;
    private static MusicPlayer.Track _current = MusicPlayer.Track.None;

    public static void Register(MusicPlayer player) => _player = player;

    public static void Play(MusicPlayer.Track track)
    {
        if (_player is null || track == _current) return;
        _current = track;
        _player.Play(track);
    }
}

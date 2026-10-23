using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContingencyZero.Sim.Replay;
using Xunit;

namespace ContingencyZero.Sim.Tests;

/// <summary>Replay file format version 3: bookmarks after the payload, version 2 files still load.</summary>
public class ReplayBookmarkTests
{
    private const int UnitsPerSide = 6;
    private const ulong Seed = 99;
    private const int Ticks = 20 * World.TicksPerSecond;

    private static MatchReplay WithBookmarks()
    {
        MatchReplay replay = TestData.Battle(UnitsPerSide, Seed, Ticks);
        replay.Title = "Bookmarked";
        replay.Bookmarks.Add(new ReplayBookmark(150, ReplayBookmarkKind.CommanderAttacked, "Commander under attack", 1));
        replay.Bookmarks.Add(new ReplayBookmark(45, ReplayBookmarkKind.Wave, "Wave hits A", 0));
        replay.Bookmarks.Add(new ReplayBookmark(299, ReplayBookmarkKind.GameOver, "Game over: ünïcödé · dash", -1));
        return replay;
    }

    [Fact]
    public void Bookmarks_survive_a_save_and_load_in_tick_order()
    {
        MatchReplay original = WithBookmarks();
        using var stream = new MemoryStream();
        original.Write(stream);
        stream.Position = 0;
        MatchReplay loaded = MatchReplay.Read(stream);

        Assert.Equal(3, loaded.Bookmarks.Count);
        Assert.Equal(new[] { 45, 150, 299 }, loaded.Bookmarks.Select(b => b.Tick).ToArray());
        Assert.Equal(ReplayBookmarkKind.Wave, loaded.Bookmarks[0].Kind);
        Assert.Equal("Wave hits A", loaded.Bookmarks[0].Text);
        Assert.Equal(0, loaded.Bookmarks[0].Player);
        Assert.Equal(ReplayBookmarkKind.CommanderAttacked, loaded.Bookmarks[1].Kind);
        Assert.Equal(1, loaded.Bookmarks[1].Player);
        Assert.Equal("Game over: ünïcödé · dash", loaded.Bookmarks[2].Text);
        Assert.Equal(-1, loaded.Bookmarks[2].Player);
        Assert.Equal("Bookmarked", loaded.Title);
        Assert.Equal(original.Commands.Count, loaded.Commands.Count);
        Assert.Equal(original.TickCount, loaded.TickCount);
    }

    [Fact]
    public void Bookmarks_do_not_change_playback()
    {
        MatchReplay plain = TestData.Battle(UnitsPerSide, Seed, Ticks);
        MatchReplay marked = WithBookmarks();
        using var stream = new MemoryStream();
        marked.Write(stream);
        stream.Position = 0;
        MatchReplay loaded = MatchReplay.Read(stream);
        Assert.Equal(plain.Play(TestData.Catalog).ComputeHash(), loaded.Play(TestData.Catalog).ComputeHash());
    }

    [Fact]
    public void Version_2_stream_still_reads_with_no_bookmarks()
    {
        MatchReplay original = WithBookmarks();
        using var stream = new MemoryStream();
        original.Write(stream, 2);
        stream.Position = 0;

        // Header: magic then the version we asked for.
        using (var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            Assert.Equal(0x5A4E_4F43u, r.ReadUInt32());
            Assert.Equal(2, r.ReadInt32());
        }
        stream.Position = 0;

        MatchReplay loaded = MatchReplay.Read(stream);
        Assert.Empty(loaded.Bookmarks);
        Assert.Equal(original.Title, loaded.Title);
        Assert.Equal(original.Commands.Count, loaded.Commands.Count);
        Assert.Equal(original.Play(TestData.Catalog).ComputeHash(), loaded.Play(TestData.Catalog).ComputeHash());
    }

    [Fact]
    public void Version_3_file_is_marked_as_such_and_empty_bookmark_list_round_trips()
    {
        MatchReplay original = TestData.Battle(UnitsPerSide, Seed, Ticks);
        using var stream = new MemoryStream();
        original.Write(stream);
        stream.Position = 4;
        using (var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            Assert.Equal(3, r.ReadInt32());
        stream.Position = 0;
        MatchReplay loaded = MatchReplay.Read(stream);
        Assert.Empty(loaded.Bookmarks);
    }

    [Fact]
    public void Unknown_future_version_is_rejected_clearly()
    {
        MatchReplay original = TestData.Battle(UnitsPerSide, Seed, Ticks);
        using var stream = new MemoryStream();
        original.Write(stream);
        byte[] bytes = stream.ToArray();
        bytes[4] = 99; // version byte (little-endian int at offset 4)
        var ex = Assert.Throws<InvalidDataException>(() => MatchReplay.Read(new MemoryStream(bytes)));
        Assert.True(ex.Message.Contains("99"), ex.Message);
    }

    [Fact]
    public void Every_bookmark_kind_round_trips_and_the_final_hash_is_unchanged()
    {
        MatchReplay replay = TestData.Battle(UnitsPerSide, Seed, Ticks);
        ulong before = replay.FinalHash;
        var kinds = System.Enum.GetValues<ReplayBookmarkKind>();
        for (int i = 0; i < kinds.Length; i++)
            replay.Bookmarks.Add(new ReplayBookmark(10 * i, kinds[i], kinds[i].ToString(), i % 3 - 1));
        using var stream = new MemoryStream();
        replay.Write(stream);
        stream.Position = 0;
        MatchReplay loaded = MatchReplay.Read(stream);
        Assert.Equal(kinds, loaded.Bookmarks.Select(b => b.Kind).ToArray());
        Assert.Equal(ReplayBookmarkKind.BigBattle, loaded.Bookmarks.Single(b => b.Text == "BigBattle").Kind);
        Assert.Equal(before, loaded.FinalHash);
        Assert.Equal(replay.Play(TestData.Catalog).ComputeHash(), loaded.Play(TestData.Catalog).ComputeHash());
    }
}

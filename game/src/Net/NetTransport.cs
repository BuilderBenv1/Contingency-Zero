using System;
using System.Collections.Generic;

namespace ContingencyZero.Game.Net;

/// <summary>What happened on the wire since the last poll.</summary>
public enum NetEventKind : byte
{
    /// <summary>A peer connected (on a client: the connection to the host is up).</summary>
    Connected,
    /// <summary>A peer went away (on a client: the host is gone, or the connection attempt failed).</summary>
    Disconnected,
    /// <summary>A message arrived.</summary>
    Data,
}

/// <summary>One transport event. <see cref="Peer"/> is the remote peer's id; the host is always 1.</summary>
public readonly record struct NetEvent(NetEventKind Kind, int Peer, byte[]? Data = null);

/// <summary>
/// The smallest thing the lockstep and lobby code needs from a network: a star topology with the
/// host in the middle (peer id 1), reliable delivery, and a poll that hands back what arrived.
/// The real game uses ENet (<see cref="EnetTransport"/>); tests use an in-memory one with fake latency.
/// Delivery order is not relied on for in-game traffic, only reliability.
/// </summary>
public interface INetTransport : IDisposable
{
    /// <summary>The host's peer id, as seen by every client.</summary>
    public const int HostPeer = 1;

    /// <summary>True on the machine hosting the game.</summary>
    bool IsHost { get; }

    /// <summary>Sends <paramref name="data"/> reliably to one peer (clients can only send to <see cref="HostPeer"/>).</summary>
    void Send(int peer, byte[] data);

    /// <summary>Appends everything that happened since the last call to <paramref name="into"/>.</summary>
    void Poll(List<NetEvent> into);

    /// <summary>Pushes queued packets onto the wire now rather than at the next poll.</summary>
    void Flush() { }

    /// <summary>Host only: drops a peer (it sees a disconnect).</summary>
    void Kick(int peer) { }
}

using System;
using System.Collections.Generic;
using Godot;

namespace ContingencyZero.Game.Net;

/// <summary>
/// <see cref="INetTransport"/> over Godot's ENet, using raw packets on one reliable, ordered
/// channel (no scene RPCs). The host listens on a UDP port; clients connect by address.
/// </summary>
public sealed class EnetTransport : INetTransport
{
    private readonly ENetMultiplayerPeer _peer;
    private readonly List<NetEvent> _pending = new();
    private readonly HashSet<int> _live = new();
    private bool _connected;
    private bool _closed;

    public bool IsHost { get; }

    private EnetTransport(ENetMultiplayerPeer peer, bool host)
    {
        _peer = peer;
        IsHost = host;
        _peer.TransferMode = MultiplayerPeer.TransferModeEnum.Reliable;
        _peer.TransferChannel = 0;
        _peer.PeerConnected += id =>
        {
            _live.Add((int)id);
            Configure((int)id);
            if (!IsHost) _connected = true;
            _pending.Add(new NetEvent(NetEventKind.Connected, (int)id));
        };
        _peer.PeerDisconnected += id =>
        {
            _live.Remove((int)id);
            _pending.Add(new NetEvent(NetEventKind.Disconnected, (int)id));
        };
    }

    /// <summary>Starts listening. Returns null and a reason if the port can't be opened.</summary>
    public static EnetTransport? Host(int port, int maxClients, out string error)
    {
        var peer = new ENetMultiplayerPeer();
        Error err = peer.CreateServer(port, maxClients);
        if (err != Error.Ok)
        {
            error = err == Error.AlreadyInUse || err == Error.CantCreate
                ? $"Couldn't open port {port}: it may already be in use."
                : $"Couldn't host on port {port} ({err}).";
            return null;
        }
        error = "";
        return new EnetTransport(peer, true);
    }

    /// <summary>Starts connecting. The connection is up when <see cref="NetEventKind.Connected"/> arrives.</summary>
    public static EnetTransport? Join(string address, int port, out string error)
    {
        var peer = new ENetMultiplayerPeer();
        Error err = peer.CreateClient(address, port);
        if (err != Error.Ok)
        {
            error = $"Couldn't connect to {address}:{port} ({err}).";
            return null;
        }
        error = "";
        return new EnetTransport(peer, false);
    }

    /// <summary>
    /// Drop a peer after ~8 seconds of silence rather than ENet's default 30: long enough to ride
    /// out a hiccup, short enough that a vanished player doesn't hold everyone up for long.
    /// </summary>
    private void Configure(int id)
    {
        ENetPacketPeer? p = _peer.GetPeer(id);
        p?.SetTimeout(32, 3000, 8000);
    }

    public void Send(int peer, byte[] data)
    {
        if (_closed || !_live.Contains(peer)) return;
        _peer.SetTargetPeer(peer);
        _peer.PutPacket(data);
    }

    public void Poll(List<NetEvent> into)
    {
        if (_closed) return;
        _peer.Poll();
        // A client whose connection attempt failed never gets a peer event, only a status change.
        if (!IsHost && !_connected && _peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
        {
            _connected = true; // report once
            _pending.Add(new NetEvent(NetEventKind.Disconnected, INetTransport.HostPeer));
        }
        // Keep connect/disconnect events in order with the packets around them.
        into.AddRange(_pending);
        _pending.Clear();
        while (_peer.GetAvailablePacketCount() > 0)
        {
            int from = _peer.GetPacketPeer();
            byte[] data = _peer.GetPacket();
            into.Add(new NetEvent(NetEventKind.Data, from, data));
        }
    }

    public void Flush()
    {
        if (_closed) return;
        _peer.Host?.Flush();
    }

    public void Kick(int peer)
    {
        if (_closed || !IsHost || !_live.Remove(peer)) return;
        // "Later" lets anything already queued for them (say, the reason they were refused) go first.
        if (_peer.GetPeer(peer) is { } p) p.PeerDisconnectLater();
        else _peer.DisconnectPeer(peer);
    }

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        _peer.Host?.Flush();
        _peer.Close();
    }
}

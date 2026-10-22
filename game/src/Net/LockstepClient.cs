using System;
using System.Collections.Generic;
using ContingencyZero.Sim.Core;

namespace ContingencyZero.Game.Net;

/// <summary>
/// A client's end of the lockstep: sends its input to the host every tick, steps on the host's
/// bundles, reports its state hash once a second, and runs slightly fast when it has fallen
/// behind the host's clock (after a hitch, say) so the host doesn't have to keep waiting for it.
/// </summary>
public sealed class LockstepClient : LockstepChannel
{
    private readonly int _hostPlayer;
    private int _delayTick = -1;
    private int _hostTick;
    private bool _loadedSent;
    private IReadOnlyList<int> _hostWaiting = Array.Empty<int>();

    public LockstepClient(INetTransport transport, Func<double> clock, int localPlayer, int hostPlayer,
        IReadOnlyList<string> names, int delay)
        : base(transport, clock, localPlayer, names, delay)
    {
        _hostPlayer = hostPlayer;
    }

    /// <summary>Whoever the host says it's waiting on, or the host itself when its bundles are late.</summary>
    public override IReadOnlyList<int> WaitingOn
    {
        get
        {
            if (_hostWaiting.Count > 0) return _hostWaiting;
            return StallSeconds > 0 ? new[] { _hostPlayer } : Array.Empty<int>();
        }
    }

    /// <summary>Ticks we are behind the host's clock (as of its last bundle).</summary>
    public int Behind => _hostTick - NextTick;

    public override double TimeScale
    {
        get
        {
            int behind = Behind;
            if (behind >= 8) return 1.5;
            if (behind >= 3) return 1.2;
            return 1.0;
        }
    }

    protected override void Handle(NetEvent e)
    {
        if (e.Peer != INetTransport.HostPeer) return;
        if (e.Kind == NetEventKind.Disconnected)
        {
            RaiseLost("Lost connection to host.");
            return;
        }
        if (e.Kind != NetEventKind.Data || e.Data is null) return;

        using var r = NetProtocol.Open(e.Data, out Msg type);
        switch (type)
        {
            case Msg.Go:
                Started = true;
                break;
            case Msg.Bundle:
            {
                int tick = r.ReadInt32();
                int hostTick = r.ReadInt32();
                int delay = r.ReadByte();
                List<Command> commands = NetProtocol.ReadCommands(r);
                if (tick >= NextTick) Bundles.TryAdd(tick, commands);
                _hostTick = Math.Max(_hostTick, hostTick);
                if (tick > _delayTick)
                {
                    _delayTick = tick;
                    if (delay != Delay) SetDelay(delay);
                }
                break;
            }
            case Msg.Desync:
                RaiseDesync(new DesyncReport(r.ReadInt32(), r.ReadUInt64(), r.ReadUInt64(), r.ReadInt32()));
                break;
            case Msg.PlayerLeft:
                RaisePlayerLeft(r.ReadInt32());
                break;
            case Msg.Waiting:
            {
                int n = r.ReadByte();
                var list = new List<int>(n);
                for (int i = 0; i < n; i++) list.Add(r.ReadInt32());
                _hostWaiting = list;
                break;
            }
            case Msg.Ping:
            {
                double sent = r.ReadDouble();
                Transport.Send(INetTransport.HostPeer, NetProtocol.Build(Msg.Pong, w => w.Write(sent)));
                break;
            }
            case Msg.Chat:
                RaiseChat(r.ReadInt32(), r.ReadString(), r.ReadString());
                break;
            case Msg.Leave:
                RaiseLost(r.ReadString());
                break;
        }
    }

    protected override void AfterPoll()
    {
        if (_loadedSent || LostReason is not null) return;
        _loadedSent = true;
        Transport.Send(INetTransport.HostPeer, NetProtocol.Build(Msg.Loaded));
    }

    protected override void SendInput(int from, int through, List<Command> commands)
    {
        if (LostReason is not null) return;
        Transport.Send(INetTransport.HostPeer, NetProtocol.Build(Msg.Input, w =>
        {
            w.Write(from);
            w.Write(through);
            NetProtocol.WriteCommands(w, commands);
        }));
    }

    protected override void OnLocalHash(int tick, ulong hash)
    {
        if (LostReason is not null) return;
        Transport.Send(INetTransport.HostPeer, NetProtocol.Build(Msg.Hash, w =>
        {
            w.Write(tick);
            w.Write(hash);
        }));
    }

    public override void SendChat(string text)
    {
        text = NetProtocol.Clean(text, 200);
        if (text.Length == 0 || LostReason is not null) return;
        Transport.Send(INetTransport.HostPeer, NetProtocol.Build(Msg.Chat, w => w.Write(text)));
    }

    public override void Dispose()
    {
        if (LostReason is null)
        {
            try
            {
                Transport.Send(INetTransport.HostPeer, NetProtocol.Build(Msg.Leave, w => w.Write("left")));
                Transport.Flush();
            }
            catch (Exception e) when (e is InvalidOperationException or ObjectDisposedException)
            {
                // Already closed.
            }
        }
        base.Dispose();
    }
}

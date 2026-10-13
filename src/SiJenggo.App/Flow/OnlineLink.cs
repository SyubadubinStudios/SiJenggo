using SiJenggo.Game;
using SiJenggo.Game.Net;
using ThreeNet.Networking;

namespace SiJenggo.App.Flow;

/// <summary>
/// Thin wrapper over ThreeNet's UDP <see cref="NetworkSession"/> for two-player online co-op.
/// The host is player one, the client player two. A dedicated relay (SiJenggo.Server) speaks the same protocol.
/// </summary>
public sealed class OnlineLink : IDisposable
{
    public const int DefaultPort = 27960;

    private readonly NetworkSession _session;
    private LockstepSession? _lockstep;
    private RollbackSession? _rollback;

    private int _slot;
    private int _relayPeers;
    private bool _viaRelay;

    private OnlineLink(NetworkSession session, bool isHost)
    {
        _session = session;
        _slot = isHost ? 0 : 1;
        _session.PeerConnected += _ => PeerJoined?.Invoke();
        _session.Connected += () => PeerJoined?.Invoke();
        _session.PeerDisconnected += _ => Lost?.Invoke("peer left");
        _session.Closed += reason => Lost?.Invoke(reason);
        _session.MessageReceived += OnMessage;
    }

    /// <summary>Player one picks the mode: the hosting peer, or whoever the relay made player one.</summary>
    public bool IsHost => _slot == 0;
    public int LocalSlot => _slot;
    public bool ViaRelay => _viaRelay;
    public bool HasPeer => _viaRelay ? _relayPeers >= 2 : _slot == 0 ? _session.Peers.Count > 0 : _session.IsConnected;
    public int Port => _session.Port;
    /// <summary>
    /// Netcode for the next match. Player one (host) sets it before <see cref="SendStart"/>, which stamps it into
    /// the <see cref="MatchStart"/>; the other peer adopts the host's choice when the start message arrives.
    /// Defaults to the delay-based <see cref="NetMode.Lockstep"/>. (UI setting: "Netcode: Lockstep / Rollback".)
    /// </summary>
    public NetMode NetMode { get; set; } = NetMode.Lockstep;

    /// <summary>Rollback window in frames (used when <see cref="NetMode"/> is rollback). Local only, peers may differ.</summary>
    public int RollbackFrames { get; set; } = 8;

    /// <summary>Input delay in ticks for rollback mode (lockstep always uses 3). Local only, peers may differ.</summary>
    public int RollbackInputDelay { get; set; } = 1;

    public TimeSpan RoundTrip => _session.Peers.Count > 0 ? _session.Peers.First().RoundTripTime : TimeSpan.Zero;

    public event Action? PeerJoined;
    public event Action<string>? Lost;
    public event Action<MatchStart>? MatchStarted;

    /// <summary>Raised when a dedicated relay assigns this client its player slot.</summary>
    public event Action<int>? RoleAssigned;

    public static OnlineLink Host(int port) => new(NetworkSession.Host(port, "si-jenggo-host"), true);

    public static OnlineLink Join(string address, int port) => new(NetworkSession.Connect(address, port, "si-jenggo-client"), false);

    /// <summary>Lockstep that the game controller drives; created when a match starts.</summary>
    public LockstepSession CreateLockstep(int players)
    {
        _rollback = null;
        _lockstep = new LockstepSession(LocalSlot, players, (data, reliable) => _session.Send(data, 0, reliable));
        return _lockstep;
    }

    /// <summary>Rollback session for one stage; <paramref name="epoch"/> must change every stage on both peers.</summary>
    public RollbackSession CreateRollback(GameSession session, int players, byte epoch)
    {
        _lockstep = null;
        _rollback = new RollbackSession(session, LocalSlot, players, (data, reliable) => _session.Send(data, 0, reliable), RollbackFrames, RollbackInputDelay, epoch);
        return _rollback;
    }

    /// <summary>Sends the match setup, stamped with this link's <see cref="NetMode"/>.</summary>
    public void SendStart(MatchStart start) => _session.Send((start with { NetMode = NetMode }).Encode(), 0, true);

    /// <summary>Pumps the socket; call every frame.</summary>
    public void Update() => _session.Update();

    private void OnMessage(NetworkMessage message)
    {
        var data = message.Data;
        if (data.Length == 0)
        {
            return;
        }

        if (data[0] == RelayProtocol.MsgRole && data.Length >= 2)
        {
            _viaRelay = true;
            _slot = data[1];
            RoleAssigned?.Invoke(_slot);
            return;
        }

        if (data[0] == RelayProtocol.MsgPeers && data.Length >= 2)
        {
            _relayPeers = data[1];
            if (_relayPeers >= 2)
            {
                PeerJoined?.Invoke();
            }

            return;
        }

        if (data[0] == LockstepSession.MsgStart)
        {
            if (MatchStart.Decode(data) is { } start)
            {
                NetMode = start.NetMode;
                MatchStarted?.Invoke(start);
            }

            return;
        }

        if (_rollback is not null)
        {
            _rollback.Receive(data);
        }
        else
        {
            _lockstep?.Receive(data);
        }
    }

    public void Dispose() => _session.Dispose();
}

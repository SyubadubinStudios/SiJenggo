namespace SiJenggo.Game.Net;

/// <summary>Messages only the dedicated relay sends (first byte), alongside <see cref="LockstepSession"/>'s.</summary>
public static class RelayProtocol
{
    /// <summary>[MsgRole, slot]: the relay tells a client which player it is (0 = picks the mode).</summary>
    public const byte MsgRole = 10;

    /// <summary>[MsgPeers, count]: how many players are connected to the relay.</summary>
    public const byte MsgPeers = 11;
}

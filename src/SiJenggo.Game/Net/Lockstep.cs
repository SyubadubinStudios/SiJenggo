using System.Buffers.Binary;
using SiJenggo.Game.Data;

namespace SiJenggo.Game.Net;

/// <summary>Sends one packet to every other peer. Spans keep the hot path allocation-free.</summary>
public delegate void PacketSender(ReadOnlySpan<byte> data, bool reliable);

/// <summary>
/// Deterministic lockstep for online co-op. Every peer simulates the whole game; only inputs travel.
/// Local input is scheduled <see cref="InputDelay"/> ticks ahead and sent redundantly (the last
/// <see cref="Redundancy"/> frames in every packet), so unreliable datagrams are enough and a lost
/// packet costs nothing. The simulation only advances a tick when every player's input for it is known.
/// The transport is abstract: ThreeNet's NetworkSession in the game, an in-memory pipe in tests,
/// and the relay in the dedicated server.
/// </summary>
public sealed class LockstepSession
{
    public const byte MsgInputs = 1;
    public const byte MsgChecksum = 2;
    public const byte MsgStart = 3;
    public const int Redundancy = 10;
    private const int Ring = 512;

    private readonly ushort[,] _inputs;
    private readonly int[,] _frames;
    private readonly InputFrame[] _frameInputs;
    private readonly PacketSender _send;
    private readonly byte[] _checksumPacket = new byte[10];
    private readonly Dictionary<int, uint> _localChecksums = [];
    private readonly byte[] _packet = new byte[8 + Redundancy * 2];
    private int _nextLocalFrame;

    public LockstepSession(int localSlot, int playerCount, PacketSender send, int inputDelay = 3)
    {
        LocalSlot = localSlot;
        PlayerCount = playerCount;
        InputDelay = inputDelay;
        _send = send;
        _inputs = new ushort[playerCount, Ring];
        _frames = new int[playerCount, Ring];
        _frameInputs = new InputFrame[playerCount];
        _nextLocalFrame = inputDelay;
        for (int s = 0; s < playerCount; s++)
        {
            for (int i = 0; i < Ring; i++)
            {
                _frames[s, i] = -1;
            }

            // The first InputDelay frames are empty for everyone.
            for (int f = 0; f < inputDelay; f++)
            {
                Store(s, f, 0);
            }
        }
    }

    public int LocalSlot { get; }
    public int PlayerCount { get; }
    public int InputDelay { get; }
    /// <summary>Next frame to simulate.</summary>
    public int Frame { get; private set; }
    public bool Desynced { get; private set; }
    public int DesyncFrame { get; private set; } = -1;
    /// <summary>Ticks spent waiting for a remote peer (shown as "waiting for partner").</summary>
    public int StallTicks { get; private set; }

    private void Store(int slot, int frame, ushort buttons)
    {
        int i = frame & (Ring - 1);
        _inputs[slot, i] = buttons;
        _frames[slot, i] = frame;
    }

    private bool Has(int slot, int frame) => _frames[slot, frame & (Ring - 1)] == frame;

    /// <summary>Records this tick's local input and sends the recent history to the peers.</summary>
    public void SubmitLocal(InputButtons buttons)
    {
        // Schedule inputs back to back (no gaps), never more than InputDelay ahead of the simulation.
        if (_nextLocalFrame <= Frame + InputDelay)
        {
            Store(LocalSlot, _nextLocalFrame, (ushort)buttons);
            _nextLocalFrame++;
        }

        int target = _nextLocalFrame - 1;

        int count = Math.Min(Redundancy, target + 1);
        int start = target - count + 1;
        _packet[0] = MsgInputs;
        _packet[1] = (byte)LocalSlot;
        BinaryPrimitives.WriteInt32LittleEndian(_packet.AsSpan(2), start);
        _packet[6] = (byte)count;
        for (int i = 0; i < count; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(_packet.AsSpan(7 + i * 2), _inputs[LocalSlot, (start + i) & (Ring - 1)]);
        }

        _send(_packet.AsSpan(0, 7 + count * 2), false);
    }

    /// <summary>Feeds a packet that arrived from the network.</summary>
    public void Receive(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2)
        {
            return;
        }

        switch (data[0])
        {
            case MsgInputs when data.Length >= 7:
            {
                int slot = data[1];
                if (slot >= PlayerCount || slot == LocalSlot)
                {
                    return;
                }

                int start = BinaryPrimitives.ReadInt32LittleEndian(data[2..]);
                int count = data[6];
                for (int i = 0; i < count && 7 + i * 2 + 1 < data.Length; i++)
                {
                    int frame = start + i;
                    if (frame >= Frame && !Has(slot, frame))
                    {
                        Store(slot, frame, BinaryPrimitives.ReadUInt16LittleEndian(data[(7 + i * 2)..]));
                    }
                }

                break;
            }

            case MsgChecksum when data.Length >= 10:
            {
                int frame = BinaryPrimitives.ReadInt32LittleEndian(data[2..]);
                uint remote = BinaryPrimitives.ReadUInt32LittleEndian(data[6..]);
                if (_localChecksums.TryGetValue(frame, out uint local) && local != remote && !Desynced)
                {
                    Desynced = true;
                    DesyncFrame = frame;
                }

                break;
            }
        }
    }

    /// <summary>Inputs for the next frame if every player's is known; advances <see cref="Frame"/>.</summary>
    public bool TryNextFrame(out ReadOnlySpan<InputFrame> inputs)
    {
        for (int s = 0; s < PlayerCount; s++)
        {
            if (!Has(s, Frame))
            {
                StallTicks++;
                inputs = default;
                return false;
            }
        }

        for (int s = 0; s < PlayerCount; s++)
        {
            _frameInputs[s] = new InputFrame((InputButtons)_inputs[s, Frame & (Ring - 1)]);
        }

        Frame++;
        inputs = _frameInputs;
        return true;
    }

    /// <summary>Call after simulating a frame; every second the checksum is exchanged to catch desyncs early.</summary>
    public void AfterFrame(int frame, uint checksum)
    {
        if (frame % 60 != 0)
        {
            return;
        }

        _localChecksums[frame] = checksum;
        if (_localChecksums.Count > 16)
        {
            _localChecksums.Remove(frame - 16 * 60);
        }

        var msg = _checksumPacket;
        msg[0] = MsgChecksum;
        msg[1] = (byte)LocalSlot;
        BinaryPrimitives.WriteInt32LittleEndian(msg.AsSpan(2), frame);
        BinaryPrimitives.WriteUInt32LittleEndian(msg.AsSpan(6), checksum);
        _send(msg, true);
    }
}

/// <summary>
/// The host tells clients what to play; everyone builds the same <see cref="Campaign"/> from it.
/// <see cref="NetMode"/> travels as an optional trailing byte, so older builds that do not send it mean lockstep.
/// </summary>
public sealed record MatchStart(ulong Seed, GameModeKind Mode, int Chapter, Difficulty Difficulty, CharacterId[] Characters, NetMode NetMode = NetMode.Lockstep)
{
    public byte[] Encode()
    {
        var data = new byte[14 + Characters.Length];
        data[0] = LockstepSession.MsgStart;
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(1), Seed);
        data[9] = (byte)Mode;
        data[10] = (byte)Chapter;
        data[11] = (byte)Difficulty;
        data[12] = (byte)Characters.Length;
        for (int i = 0; i < Characters.Length; i++)
        {
            data[13 + i] = (byte)Characters[i];
        }

        data[13 + Characters.Length] = (byte)NetMode;
        return data;
    }

    public static MatchStart? Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < 13 || data[0] != LockstepSession.MsgStart || data.Length < 13 + data[12])
        {
            return null;
        }

        var chars = new CharacterId[data[12]];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = (CharacterId)data[13 + i];
        }

        var netMode = data.Length > 13 + chars.Length && data[13 + chars.Length] == (byte)NetMode.Rollback ? NetMode.Rollback : NetMode.Lockstep;
        return new MatchStart(BinaryPrimitives.ReadUInt64LittleEndian(data[1..]), (GameModeKind)data[9], data[10], (Difficulty)data[11], chars, netMode);
    }
}

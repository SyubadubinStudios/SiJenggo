using System.Buffers.Binary;

namespace SiJenggo.Game.Net;

/// <summary>How online peers keep their simulations in step.</summary>
public enum NetMode : byte
{
    /// <summary><see cref="LockstepSession"/>: wait for every input, fixed 3-tick input delay. The default.</summary>
    Lockstep,

    /// <summary><see cref="RollbackSession"/>: predict missing remote input, rewind and re-simulate on a misprediction.</summary>
    Rollback,
}

/// <summary>
/// Rollback ("GGPO-style") netcode for online co-op, an alternative to <see cref="LockstepSession"/>.
/// Every peer still simulates the whole game and only inputs travel, but the simulation never waits for a
/// remote input that is late: it predicts it (the remote player keeps holding what they last held), keeps
/// a <see cref="SessionSnapshot"/> of every unconfirmed frame, and when the real input arrives and differs from
/// the prediction it restores the snapshot of that frame and re-simulates up to the present in the same tick.
/// <para>Local input is scheduled <see cref="InputDelay"/> ticks ahead (default 1), and the simulation runs at
/// most <see cref="MaxRollback"/> frames past the last frame whose inputs are all confirmed, after that it stalls
/// like lockstep would. Packets are unreliable; each one carries this peer's inputs from the oldest frame a peer
/// has not acknowledged yet (acks travel in the same packets), so loss only delays confirmation.</para>
/// <para>Presentation events: frames that are re-simulated were already shown, so the session's events are
/// suppressed (<see cref="GameSession.SuppressEvents"/>) while re-simulating. A corrected outcome (a hit that
/// did land after all) therefore appears without its spark or sound, but nothing is ever played twice.</para>
/// <para>Desync detection compares <see cref="GameSession.Checksum"/> on confirmed frames only (every 60 ticks).</para>
/// </summary>
public sealed class RollbackSession
{
    public const byte MsgInputs = 4;
    public const byte MsgChecksum = 5;
    public const int MaxRollbackLimit = 16;
    public const int MaxInputDelay = 8;
    /// <summary>Inputs per packet; covers the worst case gap of 2 × (rollback + delay) + 1 frames between peers.</summary>
    public const int MaxInputsPerPacket = 64;
    private const int Ring = 256;
    private const int ChecksumInterval = 60;
    private const int ChecksumRing = 16;

    private readonly GameSession _session;
    private readonly PacketSender _send;
    private readonly byte _epoch;

    private readonly ushort[] _inputs;       // [slot * Ring + frame % Ring] confirmed input
    private readonly int[] _inputFrames;     // frame stored in that slot, -1 = none
    private readonly ushort[] _used;         // input the simulation actually used for that frame
    private readonly int[] _usedFrames;
    private readonly int[] _contiguous;      // per slot: every frame up to here is confirmed
    private readonly int[] _peerAck;         // per slot: highest frame of OUR input that peer has confirmed
    private readonly int[] _peerFrame;       // per slot: the peer's simulation frame, latest report
    private readonly int[] _peerAdvantage;   // per slot: the peer's own frame advantage over us
    private readonly InputFrame[] _frameInputs;
    private readonly SessionSnapshot[] _snapshots;
    private readonly byte[] _packet;
    private readonly byte[] _checksumPacket = new byte[11];

    private readonly int[] _localSumTick = new int[ChecksumRing];
    private readonly uint[] _localSum = new uint[ChecksumRing];
    private readonly int[] _confirmedSumTick = new int[ChecksumRing];
    private readonly uint[] _confirmedSum = new uint[ChecksumRing];
    private readonly int[] _remoteSumTick = new int[ChecksumRing];
    private readonly uint[] _remoteSum = new uint[ChecksumRing];

    private int _nextLocalFrame;
    private int _mispredicted = int.MaxValue;
    private int _nextChecksumTick = ChecksumInterval;
    private int _calls;
    private int _lastSyncWait;

    /// <param name="session">The session this peer simulates; it must be fresh (tick 0).</param>
    /// <param name="epoch">Changes every stage, so packets left over from the previous stage are ignored.</param>
    public RollbackSession(GameSession session, int localSlot, int playerCount, PacketSender send, int maxRollback = 8, int inputDelay = 1, byte epoch = 0)
    {
        if (session.Tick != 0)
        {
            throw new ArgumentException("Rollback must start from a fresh session.", nameof(session));
        }

        _session = session;
        _send = send;
        _epoch = epoch;
        LocalSlot = localSlot;
        PlayerCount = playerCount;
        MaxRollback = Math.Clamp(maxRollback, 1, MaxRollbackLimit);
        InputDelay = Math.Clamp(inputDelay, 0, MaxInputDelay);

        _inputs = new ushort[playerCount * Ring];
        _inputFrames = new int[playerCount * Ring];
        _used = new ushort[playerCount * Ring];
        _usedFrames = new int[playerCount * Ring];
        Array.Fill(_inputFrames, -1);
        Array.Fill(_usedFrames, -1);
        _contiguous = new int[playerCount];
        _peerAck = new int[playerCount];
        _peerFrame = new int[playerCount];
        _peerAdvantage = new int[playerCount];
        Array.Fill(_contiguous, -1);
        Array.Fill(_peerAck, -1);
        _frameInputs = new InputFrame[playerCount];
        _snapshots = new SessionSnapshot[MaxRollback + 2];
        for (int i = 0; i < _snapshots.Length; i++)
        {
            _snapshots[i] = new SessionSnapshot();
        }

        _packet = new byte[16 + 4 * playerCount + 2 * MaxInputsPerPacket];
        Array.Fill(_localSumTick, -1);
        Array.Fill(_confirmedSumTick, -1);
        Array.Fill(_remoteSumTick, -1);

        // The first InputDelay frames are empty for everyone.
        for (int s = 0; s < playerCount; s++)
        {
            for (int f = 0; f < InputDelay; f++)
            {
                StoreConfirmed(s, f, 0);
            }

            AdvanceContiguous(s);
        }

        _nextLocalFrame = InputDelay;

        // Warm the snapshot buffers up now, so the first ticks of the match do not allocate.
        foreach (var snapshot in _snapshots)
        {
            _session.SaveState(snapshot);
            snapshot.Tick = -1;
        }
    }

    public GameSession Session => _session;
    public int LocalSlot { get; }
    public int PlayerCount { get; }
    public int MaxRollback { get; }
    public int InputDelay { get; }

    /// <summary>Next frame to simulate (equals <see cref="GameSession.Tick"/>).</summary>
    public int Frame { get; private set; }

    /// <summary>Highest frame for which every player's input is known (-1 before the first).</summary>
    public int ConfirmedFrame
    {
        get
        {
            int min = int.MaxValue;
            for (int s = 0; s < PlayerCount; s++)
            {
                min = Math.Min(min, _contiguous[s]);
            }

            return min;
        }
    }

    public bool Desynced { get; private set; }
    public int DesyncFrame { get; private set; } = -1;
    /// <summary>Ticks spent waiting because the simulation was <see cref="MaxRollback"/> frames ahead.</summary>
    public int StallTicks { get; private set; }
    /// <summary>Ticks skipped on purpose because this peer ran ahead of the others (time sync).</summary>
    public int SyncWaitTicks { get; private set; }
    /// <summary>How many times a misprediction forced a rewind.</summary>
    public int Rollbacks { get; private set; }
    /// <summary>Total frames re-simulated by rollbacks.</summary>
    public int RolledBackFrames { get; private set; }
    /// <summary>Deepest rewind so far, in frames.</summary>
    public int DeepestRollback { get; private set; }
    /// <summary>True on the call that just stalled; the UI can show "waiting for partner" after a few of these.</summary>
    public bool Stalled { get; private set; }

    /// <summary>A remote input proved a prediction wrong; the next <see cref="AdvanceFrame"/> rewinds first.</summary>
    public bool HasPendingRollback => _mispredicted != int.MaxValue;

    /// <summary>
    /// The confirmed input of <paramref name="slot"/> for <paramref name="frame"/>, while it is still in the
    /// history ring (the last ~256 frames). Lets a replay recorder or a test rebuild the authoritative stream.
    /// </summary>
    public bool TryGetConfirmedInput(int slot, int frame, out InputButtons buttons)
    {
        if ((uint)slot < (uint)PlayerCount && HasConfirmed(slot, frame))
        {
            buttons = (InputButtons)_inputs[Index(slot, frame)];
            return true;
        }

        buttons = InputButtons.None;
        return false;
    }

    private int Index(int slot, int frame) => slot * Ring + (frame & (Ring - 1));

    private bool HasConfirmed(int slot, int frame) => frame >= 0 && _inputFrames[Index(slot, frame)] == frame;

    private void StoreConfirmed(int slot, int frame, ushort buttons)
    {
        int i = Index(slot, frame);
        _inputs[i] = buttons;
        _inputFrames[i] = frame;
    }

    private void AdvanceContiguous(int slot)
    {
        while (HasConfirmed(slot, _contiguous[slot] + 1))
        {
            _contiguous[slot]++;
        }
    }

    /// <summary>
    /// One game tick: records the local input, sends it, rewinds and re-simulates if a remote input proved a
    /// prediction wrong, then simulates the next frame. Returns false when it had to wait instead.
    /// </summary>
    public bool AdvanceFrame(InputButtons local)
    {
        _calls++;
        ScheduleLocal(local);
        SendInputs();
        RollbackIfNeeded();
        ReportConfirmedChecksums();

        Stalled = Frame - ConfirmedFrame > MaxRollback;
        if (Stalled)
        {
            StallTicks++;
            return false;
        }

        // Time sync: if we are clearly ahead of a peer, wait a tick now and then so the peer catches up and
        // neither side keeps rolling back deep. Only affects when frames run, never what they compute.
        if (TimeSyncLead() >= 2 && _calls - _lastSyncWait >= 10)
        {
            _lastSyncWait = _calls;
            SyncWaitTicks++;
            return false;
        }

        SimulateFrame();
        ReportConfirmedChecksums();
        return true;
    }

    private void ScheduleLocal(InputButtons buttons)
    {
        // Back to back, never more than InputDelay ahead of the simulation (same rule as lockstep).
        if (_nextLocalFrame <= Frame + InputDelay)
        {
            StoreConfirmed(LocalSlot, _nextLocalFrame, (ushort)buttons);
            _nextLocalFrame++;
            AdvanceContiguous(LocalSlot);
        }
    }

    /// <summary>Frames we are ahead of the slowest peer, estimated from both sides' view (cancels out latency).</summary>
    private int TimeSyncLead()
    {
        int lead = 0;
        for (int s = 0; s < PlayerCount; s++)
        {
            if (s == LocalSlot || _peerFrame[s] <= 0)
            {
                continue;
            }

            int ours = Frame - _peerFrame[s];
            lead = Math.Max(lead, (ours - _peerAdvantage[s]) / 2);
        }

        return lead;
    }

    private int LocalAdvantage()
    {
        int adv = 0;
        for (int s = 0; s < PlayerCount; s++)
        {
            if (s != LocalSlot)
            {
                adv = Math.Max(adv, Frame - _peerFrame[s]);
            }
        }

        return Math.Clamp(adv, sbyte.MinValue, sbyte.MaxValue);
    }

    // Packet: [4][slot][epoch][n][n × ack:int32][frame:int32][advantage:sbyte][start:int32][count][count × uint16]
    private void SendInputs()
    {
        int target = _nextLocalFrame - 1;
        if (target < 0)
        {
            return;
        }

        int start = int.MaxValue;
        for (int s = 0; s < PlayerCount; s++)
        {
            if (s != LocalSlot)
            {
                start = Math.Min(start, _peerAck[s] + 1);
            }
        }

        start = Math.Clamp(start, Math.Max(0, target - MaxInputsPerPacket + 1), target);
        int count = target - start + 1;

        var p = _packet.AsSpan();
        p[0] = MsgInputs;
        p[1] = (byte)LocalSlot;
        p[2] = _epoch;
        p[3] = (byte)PlayerCount;
        int o = 4;
        for (int s = 0; s < PlayerCount; s++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(p[o..], _contiguous[s]);
            o += 4;
        }

        BinaryPrimitives.WriteInt32LittleEndian(p[o..], Frame);
        p[o + 4] = (byte)(sbyte)LocalAdvantage();
        BinaryPrimitives.WriteInt32LittleEndian(p[(o + 5)..], start);
        p[o + 9] = (byte)count;
        o += 10;
        for (int i = 0; i < count; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(p[o..], _inputs[Index(LocalSlot, start + i)]);
            o += 2;
        }

        _send(p[..o], false);
    }

    /// <summary>Feeds a packet that arrived from the network (any thread-free point before the next tick).</summary>
    public void Receive(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4 || data[2] != _epoch)
        {
            return;
        }

        int slot = data[1];
        if (slot >= PlayerCount || slot == LocalSlot)
        {
            return;
        }

        switch (data[0])
        {
            case MsgInputs:
                ReceiveInputs(slot, data);
                break;

            case MsgChecksum when data.Length >= 11:
            {
                int tick = BinaryPrimitives.ReadInt32LittleEndian(data[3..]);
                uint sum = BinaryPrimitives.ReadUInt32LittleEndian(data[7..]);
                int i = (tick / ChecksumInterval) & (ChecksumRing - 1);
                _remoteSumTick[i] = tick;
                _remoteSum[i] = sum;
                Compare(i);
                break;
            }
        }
    }

    private void ReceiveInputs(int slot, ReadOnlySpan<byte> data)
    {
        int n = data[3];
        int header = 4 + 4 * n + 10;
        if (n != PlayerCount || data.Length < header)
        {
            return;
        }

        int ack = BinaryPrimitives.ReadInt32LittleEndian(data[(4 + 4 * LocalSlot)..]);
        _peerAck[slot] = Math.Max(_peerAck[slot], Math.Min(ack, _nextLocalFrame - 1));
        int o = 4 + 4 * n;
        int peerFrame = BinaryPrimitives.ReadInt32LittleEndian(data[o..]);
        if (peerFrame >= _peerFrame[slot])
        {
            _peerFrame[slot] = peerFrame;
            _peerAdvantage[slot] = (sbyte)data[o + 4];
        }

        int start = BinaryPrimitives.ReadInt32LittleEndian(data[(o + 5)..]);
        int count = Math.Min(data[o + 9], (data.Length - header) / 2);
        for (int i = 0; i < count; i++)
        {
            int frame = start + i;
            // Already known, or so far ahead it would overwrite history we still need.
            if (frame <= _contiguous[slot] || frame >= _contiguous[slot] + Ring / 2 || HasConfirmed(slot, frame))
            {
                continue;
            }

            ushort buttons = BinaryPrimitives.ReadUInt16LittleEndian(data[(header + i * 2)..]);
            StoreConfirmed(slot, frame, buttons);
            int u = Index(slot, frame);
            if (frame < Frame && (_usedFrames[u] != frame || _used[u] != buttons))
            {
                _mispredicted = Math.Min(_mispredicted, frame);
            }
        }

        AdvanceContiguous(slot);
    }

    private InputButtons InputFor(int slot, int frame)
    {
        if (HasConfirmed(slot, frame))
        {
            return (InputButtons)_inputs[Index(slot, frame)];
        }

        // Prediction: the player keeps holding whatever they held in their last confirmed frame.
        int last = _contiguous[slot];
        return last >= 0 ? (InputButtons)_inputs[Index(slot, last)] : InputButtons.None;
    }

    private void SimulateFrame()
    {
        int frame = Frame;
        _session.SaveState(_snapshots[frame % _snapshots.Length]);
        for (int s = 0; s < PlayerCount; s++)
        {
            var b = InputFor(s, frame);
            int u = Index(s, frame);
            _used[u] = (ushort)b;
            _usedFrames[u] = frame;
            _frameInputs[s] = new InputFrame(b);
        }

        _session.Step(_frameInputs);
        Frame++;

        int tick = _session.Tick;
        if (tick % ChecksumInterval == 0)
        {
            int i = (tick / ChecksumInterval) & (ChecksumRing - 1);
            _localSumTick[i] = tick;
            _localSum[i] = _session.Checksum();
        }
    }

    private void RollbackIfNeeded()
    {
        if (_mispredicted == int.MaxValue)
        {
            return;
        }

        int from = _mispredicted;
        _mispredicted = int.MaxValue;
        var snapshot = _snapshots[from % _snapshots.Length];
        if (snapshot.Tick != from)
        {
            // Cannot happen while Frame - ConfirmedFrame <= MaxRollback holds; fail loudly rather than desync.
            throw new InvalidOperationException($"No snapshot for frame {from} (at frame {Frame}).");
        }

        int present = Frame;
        _session.LoadState(snapshot);
        Frame = from;
        _session.SuppressEvents = true;
        try
        {
            while (Frame < present)
            {
                SimulateFrame();
            }
        }
        finally
        {
            _session.SuppressEvents = false;
        }

        Rollbacks++;
        RolledBackFrames += present - from;
        DeepestRollback = Math.Max(DeepestRollback, present - from);
    }

    // Checksum packet: [5][slot][epoch][tick:int32][checksum:uint32], reliable. Tick T = state after frames 0..T-1.
    private void ReportConfirmedChecksums()
    {
        int confirmed = ConfirmedFrame;
        while (_nextChecksumTick <= confirmed + 1 && _nextChecksumTick <= Frame)
        {
            int tick = _nextChecksumTick;
            _nextChecksumTick += ChecksumInterval;
            int i = (tick / ChecksumInterval) & (ChecksumRing - 1);
            if (_localSumTick[i] != tick)
            {
                continue;
            }

            _confirmedSumTick[i] = tick;
            _confirmedSum[i] = _localSum[i];
            var msg = _checksumPacket;
            msg[0] = MsgChecksum;
            msg[1] = (byte)LocalSlot;
            msg[2] = _epoch;
            BinaryPrimitives.WriteInt32LittleEndian(msg.AsSpan(3), tick);
            BinaryPrimitives.WriteUInt32LittleEndian(msg.AsSpan(7), _localSum[i]);
            _send(msg, true);
            Compare(i);
        }
    }

    private void Compare(int i)
    {
        if (!Desynced && _confirmedSumTick[i] >= 0 && _confirmedSumTick[i] == _remoteSumTick[i] && _confirmedSum[i] != _remoteSum[i])
        {
            Desynced = true;
            DesyncFrame = _confirmedSumTick[i];
        }
    }
}

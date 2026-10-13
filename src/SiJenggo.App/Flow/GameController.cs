using SiJenggo.App.Input;
using SiJenggo.Core;
using SiJenggo.Game;
using SiJenggo.Game.Net;
using SiJenggo.Presentation;

namespace SiJenggo.App.Flow;

/// <summary>
/// Runs a <see cref="Campaign"/>: fixed-step simulation at 60 Hz fed by local input (or by lockstep
/// when online), render interpolation, pause, the boss-defeat slow motion, and stage transitions.
/// </summary>
public sealed class GameController
{
    private readonly GameView _view;
    private readonly InputMapper _input;
    private readonly FixedStepClock _clock = new();
    private readonly InputFrame[] _frames = new InputFrame[GameSession.MaxPlayers];
    private float _timeScale = 1f;
    private float _slowMo;
    private float _endTimer;
    private bool _pauseHeld;

    public GameController(GameView view, InputMapper input)
    {
        _view = view;
        _input = input;
        _view.GameEventRaised += OnGameEvent;
    }

    public Campaign? Campaign { get; private set; }
    public GameSession? Session { get; private set; }
    public OnlineLink? Online { get; private set; }
    public LockstepSession? Lockstep { get; private set; }

    /// <summary>Set instead of <see cref="Lockstep"/> when the online match uses <see cref="NetMode.Rollback"/>.</summary>
    public RollbackSession? Rollback { get; private set; }

    /// <summary>Netcode of the running online match (the host picks it via <see cref="OnlineLink.NetMode"/>).</summary>
    public NetMode NetMode => Rollback is not null ? NetMode.Rollback : NetMode.Lockstep;

    /// <summary>True when the online peers' checksums disagreed on a confirmed frame.</summary>
    public bool NetDesynced => Lockstep?.Desynced == true || Rollback?.Desynced == true;

    public bool Paused { get; set; }

    /// <summary>
    /// Holds the simulation at its current tick while the view keeps animating with real time
    /// (in-engine cutscenes). Unlike <see cref="Paused"/>, the camera, weather and idle clips move.
    /// </summary>
    public bool HoldSimulation { get; set; }
    public bool Waiting => (Lockstep is not null || Rollback is not null) && _stalledFrames > 20;
    private int _stalledFrames;
    private int _stageCounter;

    /// <summary>Local seat for each player slot: -1 = AI or remote, 0/1 = keyboard/gamepad set.</summary>
    private readonly int[] _seat = new int[GameSession.MaxPlayers];

    public event Action<StageResult>? StageEnded;
    public event Action<GameEvent>? Event;
    public event Action? PauseRequested;

    public void Start(Campaign campaign, OnlineLink? online = null)
    {
        Campaign = campaign;
        Online = online;
        Paused = false;
        HoldSimulation = false;
        BeginStage();
    }

    private void BeginStage()
    {
        var campaign = Campaign!;
        Session = campaign.CreateSession();
        _view.LoadStage(campaign.CurrentStage.Chapter);
        _clock.Reset();
        _endTimer = 0;
        _slowMo = 0;
        _timeScale = 1f;

        int localSeat = 0;
        for (int s = 0; s < GameSession.MaxPlayers; s++)
        {
            _seat[s] = -1;
        }

        for (int s = 0; s < campaign.Players.Count; s++)
        {
            if (campaign.Players[s].IsAi)
            {
                continue;
            }

            if (Online is not null)
            {
                _seat[s] = s == Online.LocalSlot ? 0 : -1;
            }
            else
            {
                _seat[s] = localSeat++;
            }
        }

        Lockstep = null;
        Rollback = null;
        _stalledFrames = 0;
        _stageCounter++;
        if (Online is not null)
        {
            if (Online.NetMode == NetMode.Rollback)
            {
                // Both peers go through the same stages in the same order, so the counter doubles as the epoch.
                Rollback = Online.CreateRollback(Session, campaign.Players.Count, (byte)_stageCounter);
            }
            else
            {
                Lockstep = Online.CreateLockstep(campaign.Players.Count);
            }
        }
    }

    public void Restart()
    {
        BeginStage();
        Paused = false;
        HoldSimulation = false;
    }

    public void NextStage()
    {
        Campaign!.Advance();
        BeginStage();
    }

    public void Stop()
    {
        Session = null;
        Campaign = null;
        Lockstep = null;
        Rollback = null;
        Online?.Dispose();
        Online = null;
        _view.UnloadStage();
        _view.Audio?.StopMusic();
    }

    private bool SingleKeyboardPlayer
    {
        get
        {
            int humans = 0;
            for (int s = 0; s < GameSession.MaxPlayers; s++)
            {
                if (_seat[s] >= 0)
                {
                    humans++;
                }
            }

            return humans <= 1;
        }
    }

    /// <summary>Called every rendered frame by the 3D view.</summary>
    public void Frame(float dt)
    {
        var session = Session;
        if (session is null)
        {
            return;
        }

        _input.Poll();
        Online?.Update();

        // Pause on the press edge (offline only).
        bool pause = (_input.Read(0, true) & InputButtons.Pause) != 0;
        if (pause && !_pauseHeld && Online is null)
        {
            PauseRequested?.Invoke();
        }

        _pauseHeld = pause;

        if (_slowMo > 0)
        {
            _slowMo -= dt;
            _timeScale = _slowMo > 0 ? 0.3f : 1f;
        }

        if (!Paused && !HoldSimulation)
        {
            int ticks = _clock.Advance(dt * _timeScale);
            for (int i = 0; i < ticks; i++)
            {
                if (!StepOnce(session))
                {
                    break;
                }
            }
        }

        _view.Update(session, Paused || HoldSimulation ? 1f : Math.Clamp(_clock.Alpha, 0f, 1f), Paused ? 0f : dt);

        if (session.State is SessionState.StageClear or SessionState.GameOver)
        {
            _endTimer += dt;
            if (_endTimer > 2.2f && _endTimer - dt <= 2.2f)
            {
                StageEnded?.Invoke(Campaign!.Complete(session));
            }
        }
    }

    private bool StepOnce(GameSession session)
    {
        bool single = SingleKeyboardPlayer;
        if (Rollback is { } rb)
        {
            // Rollback steps (and, after a misprediction, rewinds and re-simulates) the session itself.
            if (!rb.AdvanceFrame(_input.Read(0, true) & ~InputButtons.Pause))
            {
                _stalledFrames++;
                return false;
            }

            _stalledFrames = 0;
            return true;
        }

        if (Lockstep is { } ls)
        {
            ls.SubmitLocal(_input.Read(0, true) & ~InputButtons.Pause);
            if (!ls.TryNextFrame(out var inputs))
            {
                _stalledFrames++;
                return false;
            }

            _stalledFrames = 0;
            session.Step(inputs);
            ls.AfterFrame(ls.Frame, session.Checksum());
            return true;
        }

        for (int s = 0; s < session.PlayerCount; s++)
        {
            _frames[s] = _seat[s] >= 0 ? new InputFrame(_input.Read(_seat[s], single) & ~InputButtons.Pause) : default;
        }

        session.Step(_frames.AsSpan(0, session.PlayerCount));
        return true;
    }

    private void OnGameEvent(GameEvent ev)
    {
        if (ev.Kind == GameEventKind.BossDefeated)
        {
            _slowMo = 1.6f;
        }

        Event?.Invoke(ev);
    }
}

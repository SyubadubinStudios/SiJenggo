using SiJenggo.Core;
using SiJenggo.Core.Ecs;
using SiJenggo.Game.Components;
using SiJenggo.Game;
using SiJenggo.Game.Data;
using ThreeNet;

namespace SiJenggo.Presentation.Audio;

/// <summary>
/// Plays synthesised effects for gameplay events and mixes the three music stems live
/// from the session's <see cref="MusicState"/> and intensity. If no audio device is present the
/// game runs silently instead of failing.
/// </summary>
public sealed class AudioDirector : IDisposable
{
    private readonly AudioEngine? _engine;
    private readonly Dictionary<Sfx, AudioClip> _sfx = [];
    private readonly Dictionary<MusicStyle, AudioClip[]> _music = [];
    private readonly SoundInstance?[] _stems = new SoundInstance?[3];
    private readonly float[] _stemGain = new float[3];
    private MusicStyle? _style;
    private DeterministicRandom _rng = new(5);
    private float _duck;
    private readonly VoiceBank _voices = new();
    private readonly Dictionary<string, AudioClip?> _voiceClips = [];
    private SoundInstance? _voice;
    private int _voicePriority;
    private float _barkCooldown;

    public AudioDirector()
    {
        try
        {
            _engine = AudioEngine.Open();
            foreach (var s in Enum.GetValues<Sfx>())
            {
                _sfx[s] = _engine.CreateClip(Synth.Effect(s), 1, Synth.Rate);
            }
        }
        catch (Exception ex) when (ex is ThreeNetException or DllNotFoundException or InvalidOperationException)
        {
            _engine = null;
            Error = ex.Message;
        }
    }

    public bool Available => _engine is not null;
    public string? Error { get; }
    public float MasterVolume { get; set; } = 0.8f;
    public float MusicVolume { get; set; } = 0.7f;
    public float SfxVolume { get; set; } = 0.9f;
    public float VoiceVolume { get; set; } = 1f;

    /// <summary>
    /// Plays a voice line. Only one voice speaks at a time: a line of higher or equal
    /// <paramref name="priority"/> interrupts the current one, lower ones are dropped.
    /// Returns false when the line has no recording (the subtitle carries it alone).
    /// </summary>
    public bool PlayVoice(string key, int priority)
    {
        if (_engine is null || (VoiceActive && priority < _voicePriority))
        {
            return false;
        }

        if (!_voiceClips.TryGetValue(key, out var clip))
        {
            clip = _voices.TryGet(key, out var samples, out int rate) ? _engine.CreateClip(samples, 1, rate) : null;
            _voiceClips[key] = clip;
        }

        if (clip is null)
        {
            return false;
        }

        _voice?.Stop();
        var o = SoundOptions.Flat(VoiceVolume);
        _voice = _engine.Play(clip, in o);
        _voicePriority = priority;
        _duck = MathF.Max(_duck, 0.8f);
        return true;
    }

    private bool VoiceActive => _voice is { IsPlaying: true };

    /// <summary>A short player shout; rate-limited so combat does not turn into chatter.</summary>
    private void Bark(World world, Entity e, VoiceCue cue, float chance = 1f)
    {
        if (_barkCooldown > 0 || !world.Has<Player>(e) || (chance < 1f && _rng.NextFloat() > chance))
        {
            return;
        }

        if (PlayVoice(VoiceBank.PlayerKey(world.Get<Player>(e).Character, cue), 1))
        {
            _barkCooldown = 3.5f;
        }
    }

    public void Play(Sfx sfx, float gain = 1f, float pitchVariance = 0.06f)
    {
        if (_engine is null || !_sfx.TryGetValue(sfx, out var clip))
        {
            return;
        }

        var o = SoundOptions.Flat(gain * SfxVolume);
        o.Pitch = 1f + _rng.Range(-pitchVariance, pitchVariance);
        _engine.Play(clip, in o);
    }

    /// <summary>Starts a chapter's soundtrack (stems are rendered once and cached).</summary>
    public void PlayMusic(MusicStyle style)
    {
        if (_engine is null || _style == style)
        {
            return;
        }

        StopMusic();
        if (!_music.TryGetValue(style, out var stems))
        {
            stems = new AudioClip[3];
            for (int i = 0; i < 3; i++)
            {
                stems[i] = _engine.CreateClip(Synth.MusicStem(style, i), 2, Synth.Rate);
            }

            _music[style] = stems;
        }

        for (int i = 0; i < 3; i++)
        {
            var o = SoundOptions.Flat(0f, loop: true);
            _stems[i] = _engine.Play(stems[i], in o);
            _stemGain[i] = i == 0 ? 0.6f : 0f;
        }

        _style = style;
    }

    public void StopMusic()
    {
        for (int i = 0; i < _stems.Length; i++)
        {
            _stems[i]?.Stop();
            _stems[i] = null;
        }

        _style = null;
    }

    /// <summary>Mixes the stems toward the session's mood. Call every frame.</summary>
    public void UpdateMix(MusicState state, float intensity, float dt, bool paused)
    {
        if (_engine is null)
        {
            return;
        }

        _engine.MasterGain = MasterVolume;
        var (t0, t1, t2) = state switch
        {
            MusicState.Explore => (0.55f, 0.25f, 0f),
            MusicState.Combat => (0.7f, 0.45f + 0.35f * intensity, 0.4f * intensity),
            MusicState.Boss => (0.8f, 0.8f, 0.35f + 0.4f * intensity),
            MusicState.Rage => (0.85f, 0.9f, 0.85f),
            MusicState.Victory => (0.4f, 0.5f, 0.6f),
            _ => (0.25f, 0f, 0f),
        };
        _barkCooldown = MathF.Max(0, _barkCooldown - dt);
        _duck = MathF.Max(VoiceActive ? 0.6f : 0, _duck - dt * 2f);
        for (int i = 0; i < 3; i++)
        {
            _stemGain[i] = MathX.MoveTowards(_stemGain[i], i == 0 ? t0 : i == 1 ? t1 : t2, dt * 0.8f);
            if (_stems[i] is { } inst)
            {
                float g = _stemGain[i] * MusicVolume * (paused ? 0.35f : 1f) * (1f - _duck * 0.5f);
                inst.Update(o =>
                {
                    o.Gain = g;
                    return o;
                });
            }
        }
    }

    /// <summary>Briefly dips the music under a big moment (boss roar, ultimate).</summary>
    public void Duck() => _duck = 1f;

    /// <summary>Voice lines for bosses and player barks; needs the world to know who is speaking.</summary>
    public void HandleVoice(in GameEvent ev, World world)
    {
        switch (ev.Kind)
        {
            case GameEventKind.BossIntro: PlayVoice(VoiceBank.BossKey((BossId)ev.Param, VoiceCue.Intro), 3); break;
            case GameEventKind.BossDefeated: PlayVoice(VoiceBank.BossKey((BossId)ev.Param, VoiceCue.Defeat), 3); break;
            case GameEventKind.BossTaunt when world.Has<Boss>(ev.Source):
                PlayVoice(VoiceBank.BossKey(world.Get<Boss>(ev.Source).Id, ev.Param >= 2 ? VoiceCue.Taunt2 : VoiceCue.Taunt1), 2);
                break;
            case GameEventKind.Ultimate: _barkCooldown = 0; Bark(world, ev.Source, VoiceCue.Ultimate); break;
            case GameEventKind.Special: Bark(world, ev.Source, VoiceCue.Special, 0.35f); break;
            case GameEventKind.PlayerDown: Bark(world, ev.Source, VoiceCue.Down); break;
            case GameEventKind.PlayerRevived: _barkCooldown = 0; Bark(world, ev.Source, VoiceCue.Revive); break;
            case GameEventKind.StageClear:
                var players = world.Pool<Player>();
                foreach (ref readonly var p in players.Components)
                {
                    if (p.Slot == 0)
                    {
                        PlayVictory(p.Character);
                    }
                }

                break;
        }
    }

    /// <summary>The first player's victory line on the results screen.</summary>
    public void PlayVictory(CharacterId character) => PlayVoice(VoiceBank.PlayerKey(character, VoiceCue.Victory), 2);

    public void Handle(in GameEvent ev)
    {
        switch (ev.Kind)
        {
            case GameEventKind.Hit:
                Play(ev.Fx switch
                {
                    HitFx.Kick => Sfx.Kick,
                    HitFx.Heavy => Sfx.Heavy,
                    HitFx.Slash => Sfx.Slash,
                    HitFx.Blunt => Sfx.Blunt,
                    HitFx.Fire => Sfx.Fire,
                    HitFx.Lightning => Sfx.Lightning,
                    HitFx.Wind => Sfx.Wind,
                    HitFx.Shockwave => Sfx.Shockwave,
                    HitFx.Gadget => Sfx.Gadget,
                    _ => Sfx.Punch,
                }, MathF.Min(1f, 0.55f + ev.Value / 30f));
                if (ev.Param == 1)
                {
                    Play(Sfx.KnockOut, 0.6f, 0f);
                }

                break;
            case GameEventKind.Blocked: Play(Sfx.Counter, 0.5f); break;
            case GameEventKind.Whiff: Play(Sfx.Whiff, 0.35f, 0.15f); break;
            case GameEventKind.Jump: Play(Sfx.Jump, 0.5f); break;
            case GameEventKind.Land: Play(Sfx.Land, MathF.Min(1f, ev.Value / 12f)); break;
            case GameEventKind.RageOn: Play(Sfx.RageOn, 1f, 0f); break;
            case GameEventKind.Ultimate: Play(Sfx.Ultimate, 1f, 0f); Duck(); break;
            case GameEventKind.Special: Play(Sfx.Wind, 0.6f); break;
            case GameEventKind.Counter: Play(Sfx.Counter); break;
            case GameEventKind.Throw: Play(Sfx.Whiff, 0.7f); break;
            case GameEventKind.PropBroken: Play(Sfx.Break); break;
            case GameEventKind.PickupTaken: Play(ev.Param == (int)PickupKind.Uang ? Sfx.Coin : Sfx.Pickup, 0.8f, 0f); break;
            case GameEventKind.WeaponBroken: Play(Sfx.Break, 0.8f); break;
            case GameEventKind.BossIntro: Play(Sfx.BossRoar, 1f, 0f); Duck(); break;
            case GameEventKind.BossPhase: Play(Sfx.BossRoar, 0.9f, 0.05f); Play(Sfx.Shockwave); break;
            case GameEventKind.BossDefeated: Play(Sfx.KnockOut, 1f, 0f); Play(Sfx.Shockwave, 1f, 0f); break;
            case GameEventKind.Go: Play(Sfx.Go, 0.8f, 0f); break;
            case GameEventKind.HazardStrike:
                Play((Game.Components.HazardKind)ev.Param switch
                {
                    Game.Components.HazardKind.Lightning => Sfx.Thunder,
                    Game.Components.HazardKind.LaserSweep => Sfx.Gadget,
                    Game.Components.HazardKind.LavaPool => Sfx.Fire,
                    _ => Sfx.Shockwave,
                }, 0.8f);
                break;
            case GameEventKind.CoopMove: Play(Sfx.Pickup, 0.8f, 0f); break;
            case GameEventKind.PlayerDown or GameEventKind.PlayerOut: Play(Sfx.Heavy, 0.8f, 0f); break;
            case GameEventKind.PlayerRevived or GameEventKind.PlayerRespawn: Play(Sfx.Pickup, 0.9f, 0f); break;
        }
    }

    public void Dispose()
    {
        StopMusic();
        _engine?.Dispose();
    }
}

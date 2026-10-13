using SiJenggo.Core;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;
using ThreeNet;

namespace SiJenggo.Presentation.Actors;

/// <summary>
/// Drives the Blender-authored clips of one rigged model. Instead of letting clips run freely, the
/// animator scrubs each clip to the exact point the move's frame data is at, so the fist lands on
/// the frame the hitbox is active, whatever the attack speed, hit-stop or rage bonus.
/// </summary>
public sealed class ActorAnimator
{
    private const float FadeTime = 0.09f;

    /// <summary>Where the impact sits inside each authored clip (0..1 of its duration).</summary>
    private static readonly Dictionary<string, float> s_impact = new()
    {
        ["jab"] = 0.28f, ["cross"] = 0.3f, ["hook"] = 0.43f, ["uppercut"] = 0.45f, ["kick"] = 0.42f, ["roundkick"] = 0.43f,
        ["sweep"] = 0.48f, ["spin"] = 0.45f, ["slam"] = 0.45f, ["slash"] = 0.48f, ["thrust"] = 0.27f, ["throw"] = 0.58f,
        ["counter"] = 0.66f, ["cast"] = 0.3f, ["roar"] = 0.3f, ["grab"] = 0.15f, ["gadget"] = 0.36f, ["taunt"] = 0.3f,
    };

    /// <summary>
    /// Signature clips that only some rigs carry (tools/blender/character_styles.py) and the shared
    /// clip to use where they are missing.
    /// </summary>
    private static readonly Dictionary<string, string> s_fallback = new()
    {
        ["gadget"] = "jab", ["taunt"] = "roar", ["intro"] = "roar",
    };

    private readonly Dictionary<string, AnimationClip> _clips;
    private readonly Dictionary<string, AnimationPlayer> _players = [];
    private string? _current;
    private string? _previous;
    private float _fade = 1f;
    private float _loopTime;

    public ActorAnimator(Dictionary<string, AnimationClip> clips) => _clips = clips;

    /// <summary>Bosses play their "intro" clip while the session holds them for the intro cinematic.</summary>
    public bool IsBoss { get; set; }

    /// <summary>Length of the boss intro hold in ticks (StageSystem starts a 170-tick cinematic).</summary>
    private const float IntroTicks = 170f;

    public static string ClipFor(MovePose pose) => pose switch
    {
        MovePose.Jab => "jab",
        MovePose.Gadget => "gadget",
        MovePose.Cross or MovePose.DashPunch or MovePose.Headbutt => "cross",
        MovePose.Hook or MovePose.Elbow => "hook",
        MovePose.Uppercut => "uppercut",
        MovePose.FrontKick or MovePose.Knee or MovePose.AirKick or MovePose.DiveKick => "kick",
        MovePose.RoundKick => "roundkick",
        MovePose.LowSweep => "sweep",
        MovePose.SpinKick or MovePose.Spin => "spin",
        MovePose.GroundPound or MovePose.Slam or MovePose.Overhead => "slam",
        MovePose.Slash or MovePose.WideSlash => "slash",
        MovePose.Thrust or MovePose.Charge => "thrust",
        MovePose.Throw or MovePose.BackThrow => "throw",
        MovePose.Counter => "counter",
        MovePose.Cast => "cast",
        MovePose.Roar => "roar",
        MovePose.Taunt => "taunt",
        _ => "jab",
    };

    /// <summary>Picks clip + time for the fighter's state and applies the pose (with a short crossfade).</summary>
    public void Update(in Fighter f, float horizontalSpeed, bool rage, float dt)
    {
        string clip;
        float t; // normalised 0..1
        bool loop = false;
        switch (f.State)
        {
            case FighterState.Walk:
                clip = "walk";
                loop = true;
                _loopTime += dt * Math.Clamp(horizontalSpeed / 3.5f, 0.6f, 1.8f);
                t = _loopTime;
                break;
            case FighterState.Attack when !f.Move.IsNone:
            {
                var def = f.Move.Def;
                clip = ClipFor(def.Pose);
                float impact = s_impact.GetValueOrDefault(clip, 0.4f);
                float tick = f.MoveClock;
                if (tick < def.Startup)
                {
                    t = impact * (tick / Math.Max(1, def.Startup));
                }
                else if (tick < def.Startup + def.Active)
                {
                    t = impact + 0.08f * ((tick - def.Startup) / Math.Max(1, def.Active));
                }
                else
                {
                    t = MathX.Lerp(impact + 0.08f, 1f, (tick - def.Startup - def.Active) / Math.Max(1, def.Recovery));
                }

                break;
            }

            case FighterState.Airborne:
                clip = "jump";
                t = Math.Clamp(f.StateTicks / 40f, 0.2f, 0.95f);
                break;
            case FighterState.HitStun:
            case FighterState.Grabbed:
                clip = "hit";
                t = Math.Clamp(f.StateTicks / 18f, 0f, 0.35f);
                break;
            case FighterState.Juggle:
            case FighterState.Thrown:
                clip = "knockdown";
                t = Math.Clamp(f.StateTicks / 50f, 0.1f, 0.42f);
                break;
            case FighterState.Knockdown:
            case FighterState.Dead:
                clip = "knockdown";
                t = 0.45f + MathF.Min(0.5f, f.StateTicks / 60f);
                break;
            case FighterState.GetUp:
                clip = "getup";
                t = MathF.Min(1f, f.StateTicks / 22f);
                break;
            case FighterState.Grabbing:
                clip = "grab";
                t = MathF.Min(0.3f, f.StateTicks / 20f);
                break;
            case FighterState.Dodge:
                clip = "sweep";
                t = MathF.Min(0.45f, f.StateTicks / 30f);
                break;
            case FighterState.Carrying:
                clip = "carry";
                t = 0.5f;
                break;
            case FighterState.Downed:
                clip = "downed";
                loop = true;
                _loopTime += dt;
                t = _loopTime;
                break;
            case FighterState.Cinematic when IsBoss:
                clip = "intro";
                t = MathF.Min(1f, f.StateTicks / IntroTicks);
                break;
            case FighterState.Cinematic when _clips.ContainsKey("taunt"):
                // Heroes with a signature taunt answer the boss while the intro plays.
                clip = "taunt";
                t = Math.Clamp((f.StateTicks - 60) / 80f, 0f, 1f);
                break;
            case FighterState.Victory:
                clip = "victory";
                loop = true;
                _loopTime += dt;
                t = _loopTime;
                break;
            default:
                clip = "idle";
                loop = true;
                _loopTime += dt * (rage ? 1.6f : 1f);
                t = _loopTime;
                break;
        }

        Apply(clip, t, loop, dt);
    }

    /// <summary>Plays a named clip directly (menus, boss intros).</summary>
    public void Apply(string clip, float t, bool loop, float dt)
    {
        if (!_clips.ContainsKey(clip) && s_fallback.TryGetValue(clip, out var shared))
        {
            clip = shared;
        }

        if (!_clips.ContainsKey(clip))
        {
            clip = "idle";
            if (!_clips.ContainsKey(clip))
            {
                return;
            }
        }

        if (clip != _current)
        {
            if (_previous is not null && _previous != clip)
            {
                Silence(_previous);
            }

            _previous = _current;
            _current = clip;
            _fade = _previous is null ? 1f : 0f;
        }

        _fade = MathF.Min(1f, _fade + dt / FadeTime);
        var player = Player(_current);
        float duration = _clips[_current].Duration;
        player.Time = loop ? (t * 1f) % MathF.Max(0.01f, duration) : Math.Clamp(t, 0f, 1f) * duration;
        player.Weight = _fade;
        if (_previous is not null)
        {
            if (_fade >= 1f)
            {
                Silence(_previous);
                _previous = null;
            }
            else
            {
                Player(_previous).Weight = 1f - _fade;
            }
        }
    }

    private AnimationPlayer Player(string clip)
    {
        if (!_players.TryGetValue(clip, out var p))
        {
            p = _clips[clip].Play(loop: false, speed: 0f, weight: 0f);
            _players[clip] = p;
        }

        p.IsPlaying = true;
        return p;
    }

    private void Silence(string clip)
    {
        if (_players.TryGetValue(clip, out var p))
        {
            p.Weight = 0;
            p.IsPlaying = false;
        }
    }

    /// <summary>Silences every clip so a pooled model starts clean.</summary>
    public void Reset()
    {
        foreach (var (clip, p) in _players)
        {
            p.Weight = 0;
            p.IsPlaying = false;
        }

        _current = _previous = null;
        _fade = 1f;
        _loopTime = 0;
    }

    public void Dispose()
    {
        // Clips belong to the model's nodes and go away with them; only stop our players.
        foreach (var p in _players.Values)
        {
            p.Stop();
        }

        _players.Clear();
    }
}

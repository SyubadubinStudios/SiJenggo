using System.Numerics;
using SiJenggo.Core;
using SiJenggo.Game;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;
using SiJenggo.Presentation.Actors;
using SiJenggo.Presentation.Audio;
using SiJenggo.Presentation.Effects;
using SiJenggo.Presentation.Scenery;
using ThreeNet;

namespace SiJenggo.Presentation;

/// <summary>Graphics presets (the integrated-GPU floor is "Rendah").</summary>
public static class Quality
{
    public static RendererOptions Options(int level)
    {
        var o = RendererOptions.Default;
        o.ToneMapping = ToneMapping.Aces;
        o.Exposure = 1.05f;
        o.VSync = true;
        o.Bloom = level >= 1;
        o.BloomIntensity = 0.55f;
        o.BloomThreshold = 1.1f;
        o.Shadows = level >= 1;
        o.ShadowMapSize = level >= 3 ? 4096 : level >= 2 ? 2048 : 1024;
        o.ShadowDistance = 40f;
        o.ShadowCascades = level >= 2 ? 3 : 2;
        o.ShadowSoftness = level >= 2 ? 2 : 1;
        o.Ssao = level >= 2;
        o.SsaoIntensity = 0.9f;
        o.MsaaSamples = level >= 2 ? 4 : 1;
        o.MotionBlur = level >= 3;
        o.MotionBlurStrength = 0.4f;
        o.FrustumCulling = true;
        o.PowerPreference = PowerPreference.HighPerformance;
        return o;
    }

    public static double RenderScale(int level) => level switch { 0 => 0.6, 1 => 0.8, _ => 1.0 };
}

/// <summary>
/// The 3D view of a running <see cref="GameSession"/>: owns the ThreeNet scene contents for the stage,
/// syncs actors every frame, reacts to gameplay events with effects, sound and camera work.
/// </summary>
public sealed class GameView : IDisposable
{
    private const int LampCount = 6;

    private readonly Materials _materials;
    private readonly ProceduralModels _procedural;
    private readonly ActorManager _actors;
    private readonly VfxSystem _vfx;
    private readonly EnvironmentBuilder _builder;
    private readonly Node[] _lamps = new Node[LampCount];
    private readonly int[] _lampPick = new int[LampCount];
    private Node? _stageRoot;
    private StageDressing? _dressing;
    private Vector3 _camPos;
    private Vector3 _camLook;
    private float _shake;
    private float _shakeTime;
    private float _zoom;
    private Vector3 _focusPoint;
    private float _focusTime;
    private float _lightning;
    private float _time;
    private DeterministicRandom _rng = new(1234);
    private CutsceneShot[]? _shots;
    private int _shot;
    private float _shotTime;

    public GameView(Scene scene, AudioDirector? audio = null)
    {
        Scene = scene;
        Audio = audio;
        _materials = new Materials(scene);
        _procedural = new ProceduralModels(scene, _materials);
        _actors = new ActorManager(scene, _procedural);
        _vfx = new VfxSystem(scene, _materials);
        _builder = new EnvironmentBuilder(scene, _materials);
        Camera = scene.AddCamera(ThreeNet.Camera.Perspective(40f * MathF.PI / 180f, 0.2f, 400f), new Vector3(0, 3.5f, 10.6f));
        for (int i = 0; i < LampCount; i++)
        {
            _lamps[i] = scene.AddLight(Light.Point(Vector3.One, 0f, 12f), null, "lamp");
        }
    }

    public Scene Scene { get; }
    public Node Camera { get; }
    public AudioDirector? Audio { get; }
    public bool ScreenShakeEnabled { get; set; } = true;
    public int Quality { get; set; } = 1;
    public ChapterDef? Chapter => _dressing?.Chapter;

    /// <summary>True while a cutscene drives the camera.</summary>
    public bool CutsceneActive => _shots is not null;

    /// <summary>Story line of the current shot, or -1.</summary>
    public int CutsceneLine => _shots is { } s ? s[_shot].Line : -1;

    /// <summary>Raised once when the last shot of a cutscene ends (or it is skipped).</summary>
    public event Action? CutsceneFinished;

    /// <summary>Starts a scripted camera sequence; the gameplay camera takes over smoothly afterwards.</summary>
    public void PlayCutscene(CutsceneShot[] shots)
    {
        if (shots.Length == 0)
        {
            return;
        }

        _shots = shots;
        _shot = 0;
        _shotTime = 0;
        _camPos = shots[0].FromPos;
        _camLook = shots[0].FromLook;
    }

    /// <summary>Cuts to the next shot (the player pressed "continue").</summary>
    public void AdvanceCutscene()
    {
        if (_shots is null)
        {
            return;
        }

        if (++_shot >= _shots.Length)
        {
            StopCutscene();
            return;
        }

        _shotTime = 0;
    }

    public void StopCutscene()
    {
        if (_shots is null)
        {
            return;
        }

        _shots = null;
        CutsceneFinished?.Invoke();
    }

    private void UpdateCutsceneCamera(float dt)
    {
        var shot = _shots![_shot];
        _shotTime += dt;
        float k = Math.Clamp(_shotTime / shot.Duration, 0f, 1f);
        k = k * k * (3f - 2f * k);
        _camPos = Vector3.Lerp(shot.FromPos, shot.ToPos, k);
        _camLook = Vector3.Lerp(shot.FromLook, shot.ToLook, k);
        Camera.Position = _camPos;
        Camera.LookAt(_camLook);
        if (_shotTime >= shot.Duration)
        {
            AdvanceCutscene();
        }
    }

    /// <summary>Raised for each gameplay event after effects and sound have been triggered (the HUD listens).</summary>
    public event Action<GameEvent>? GameEventRaised;

    /// <summary>Builds the stage for a chapter (tears down the previous one).</summary>
    public void LoadStage(ChapterDef chapter)
    {
        UnloadStage();
        _stageRoot = Scene.CreateNode(null, "stage-root");
        _dressing = _builder.Build(chapter, _stageRoot, Quality);
        _vfx.SetWeather(chapter.Weather, Quality);
        Audio?.PlayMusic(chapter.Music);
        _camPos = new Vector3(GameSession.ViewHalfWidth, 4f, 13f);
        _camLook = new Vector3(GameSession.ViewHalfWidth, 1.2f, 0);
    }

    public void UnloadStage()
    {
        _shots = null;
        _actors.Clear();
        _vfx.Clear();
        if (_dressing is not null)
        {
            _dressing.Water?.Node.Remove();
            foreach (var f in _dressing.Fires)
            {
                f.Node.Remove();
            }
        }

        _stageRoot?.Remove();
        _stageRoot = null;
        _dressing = null;
        foreach (var l in _lamps)
        {
            l.Light = Light.Point(Vector3.One, 0f, 12f);
        }
    }

    /// <summary>Per render frame: interpolate actors, play effects, move the camera.</summary>
    public void Update(GameSession session, float alpha, float dt)
    {
        _time += dt;
        foreach (ref readonly var ev in session.Events.Items)
        {
            Handle(session, ev);
        }

        session.Events.Clear();

        _actors.Sync(session, alpha, dt);
        if (_shots is not null)
        {
            UpdateCutsceneCamera(dt);
        }
        else
        {
            UpdateCamera(session, dt);
        }

        _vfx.Update(session, Camera, _camLook, dt);
        UpdateStageEffects(dt);
        Audio?.UpdateMix(session.Music, session.MusicIntensity, dt, false);
        Scene.UpdateAnimations(dt);
    }

    private void Handle(GameSession session, in GameEvent ev)
    {
        _vfx.Handle(ev, session.World);
        Audio?.Handle(ev);
        Audio?.HandleVoice(ev, session.World);
        switch (ev.Kind)
        {
            case GameEventKind.Hit:
            {
                float shake = ev.Move.IsNone ? 0.12f : ev.Move.Def.ScreenShake;
                Shake(MathF.Max(shake, MathF.Min(0.3f, ev.Value / 80f)));
                break;
            }

            case GameEventKind.Land when ev.Param == 1 && ev.Value > 8f:
                Shake(0.12f);
                break;
            case GameEventKind.BossIntro:
                Focus(Space.ToRender(ev.Position) + new Vector3(0, 1.6f, 0), 2.6f, 0.55f);
                break;
            case GameEventKind.BossPhase:
                Shake(0.6f);
                Focus(Space.ToRender(ev.Position) + new Vector3(0, 1.5f, 0), 1.2f, 0.35f);
                break;
            case GameEventKind.BossDefeated:
                Shake(0.8f);
                Focus(Space.ToRender(ev.Position) + new Vector3(0, 1f, 0), 2.5f, 0.45f);
                break;
            case GameEventKind.Ultimate:
                Focus(Space.ToRender(ev.Position) + new Vector3(0, 1.4f, 0), 0.9f, 0.4f);
                Shake(0.4f);
                break;
            case GameEventKind.HazardStrike when ev.Param == (int)HazardKind.Lightning:
                _lightning = 1f;
                Shake(0.25f);
                break;
            case GameEventKind.HazardStrike:
                Shake(0.2f);
                break;
            case GameEventKind.PropBroken:
                Shake(0.1f);
                break;
        }

        GameEventRaised?.Invoke(ev);
    }

    private void Shake(float amount)
    {
        if (ScreenShakeEnabled)
        {
            _shake = MathF.Max(_shake, amount);
        }
    }

    private void Focus(Vector3 point, float seconds, float zoom)
    {
        _focusPoint = point;
        _focusTime = seconds;
        _zoom = zoom;
    }

    private void UpdateCamera(GameSession session, float dt)
    {
        float x = session.CameraX;
        // Track the players' average depth a little, so the lane stays framed.
        float z = 0;
        int n = 0;
        for (int s = 0; s < session.PlayerCount; s++)
        {
            var e = session.PlayerEntity(s);
            if (session.World.IsAlive(e))
            {
                z += session.World.Get<Transform>(e).Position.Z;
                n++;
            }
        }

        z = n > 0 ? -z / n * 0.25f : 0;
        var look = new Vector3(x, 1.15f, z - 0.3f);
        var pos = new Vector3(x, 3.5f, 10.6f + z);

        if (_focusTime > 0)
        {
            _focusTime -= dt;
            float k = MathF.Min(1f, _focusTime * 2f) * _zoom;
            look = Vector3.Lerp(look, _focusPoint, k * 1.4f);
            pos = Vector3.Lerp(pos, _focusPoint + new Vector3(0, 0.8f, 5.5f), k);
        }

        float follow = MathX.Damp(6f, dt);
        _camPos = Vector3.Lerp(_camPos, pos, follow);
        _camLook = Vector3.Lerp(_camLook, look, follow);

        var shakeOffset = Vector3.Zero;
        if (_shake > 0.001f)
        {
            _shakeTime += dt * 38f;
            shakeOffset = new Vector3(MathF.Sin(_shakeTime * 1.3f), MathF.Sin(_shakeTime * 1.7f + 1f), 0) * _shake * 0.35f;
            _shake = MathF.Max(0, _shake - dt * 2.2f);
        }

        Camera.Position = _camPos + shakeOffset;
        Camera.LookAt(_camLook + shakeOffset * 0.5f);
    }

    private void UpdateStageEffects(float dt)
    {
        if (_dressing is null)
        {
            return;
        }

        // Pick the lamp anchors nearest the camera and park the point light pool on them.
        var anchors = _dressing.LampAnchors;
        for (int i = 0; i < LampCount; i++)
        {
            _lampPick[i] = -1;
        }

        for (int a = 0; a < anchors.Count; a++)
        {
            float d = MathF.Abs(anchors[a].X - _camLook.X);
            if (d > 18f)
            {
                continue;
            }

            for (int i = 0; i < LampCount; i++)
            {
                if (_lampPick[i] < 0 || d < MathF.Abs(anchors[_lampPick[i]].X - _camLook.X))
                {
                    for (int j = LampCount - 1; j > i; j--)
                    {
                        _lampPick[j] = _lampPick[j - 1];
                    }

                    _lampPick[i] = a;
                    break;
                }
            }
        }

        for (int i = 0; i < LampCount; i++)
        {
            int a = _lampPick[i];
            if (a < 0)
            {
                _lamps[i].Light = Light.Point(Vector3.One, 0f, 1f);
                continue;
            }

            float flicker = 0.92f + 0.08f * MathF.Sin(_time * 13f + a * 1.7f);
            _lamps[i].Position = anchors[a];
            _lamps[i].Light = Light.Point(_dressing.LampColors[a], 14f * flicker, 11f);
        }

        foreach (var fire in _dressing.Fires)
        {
            if (MathF.Abs(fire.Node.WorldPosition.X - _camLook.X) < 22f)
            {
                fire.Update(dt);
            }
        }

        _dressing.Water?.Update(dt);

        // Thunderstorm lightning flashes the whole sky.
        if (_dressing.Chapter.Weather == WeatherKind.Thunderstorm && _rng.Chance(dt * 0.12f))
        {
            _lightning = 0.8f;
            Audio?.Play(Sfx.Thunder, 0.6f, 0.1f);
        }

        if (_lightning > 0)
        {
            _lightning = MathF.Max(0, _lightning - dt * 2.5f);
            var env = _dressing.Environment;
            env.AmbientIntensity = _dressing.Environment.AmbientIntensity + _lightning * 1.2f;
            Scene.Environment = env;
        }
    }

    public void Dispose()
    {
        UnloadStage();
        _actors.Dispose();
        _vfx.Dispose();
    }
}

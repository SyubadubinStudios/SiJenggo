using System.Runtime.InteropServices;
using Android.Content.PM;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using SiJenggo.Core;
using SiJenggo.Game;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;
using SiJenggo.Game.Persistence;
using SiJenggo.Presentation;
using SiJenggo.Presentation.Audio;
using ThreeNet;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace SiJenggo.Android;

/// <summary>
/// Story mode on Android: the GPU draws straight into a <see cref="SurfaceView"/> (ThreeNet's
/// <see cref="Renderer.CreateForAndroid"/>), the same deterministic <see cref="GameSession"/> runs at 60 Hz on
/// the render thread, and <see cref="TouchControls"/> supplies the buttons. Each chapter opens with its
/// in-engine cutscene; clearing a chapter moves on to the next one, losing retries it.
/// Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.
/// </summary>
[Activity(Label = "Si Jenggo", MainLauncher = true, ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
public class MainActivity : Activity, ISurfaceHolderCallback
{
    private const string Tag = "SiJenggo";
    private const string AssetVersion = "0.1.1";

    [DllImport("android")]
    private static extern nint ANativeWindow_fromSurface(nint env, nint surface);

    [DllImport("android")]
    private static extern void ANativeWindow_release(nint window);

    private readonly ManualResetEventSlim _surfaceReady = new(false);
    private readonly object _gate = new();
    private SurfaceView _surface = null!;
    private TouchControls _pad = null!;
    private TextView _hud = null!;
    private TextView _subtitle = null!;
    private Thread? _renderThread;
    private volatile bool _running;
    private volatile bool _paused;
    private nint _window;
    private int _width, _height;

    protected override void OnCreate(global::Android.OS.Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.AddFlags(WindowManagerFlags.KeepScreenOn | WindowManagerFlags.Fullscreen);
        UnpackAssets();

        var root = new FrameLayout(this);
        _surface = new SurfaceView(this);
        _surface.Holder!.AddCallback(this);
        _pad = new TouchControls(this);
        _pad.PauseTapped += () => _paused = !_paused;
        _hud = new TextView(this) { TextSize = 15 };
        _hud.SetTextColor(global::Android.Graphics.Color.Argb(255, 242, 233, 220));
        _hud.SetShadowLayer(4, 0, 2, global::Android.Graphics.Color.Black);
        _hud.SetPadding(32, 20, 32, 20);
        _subtitle = new TextView(this) { TextSize = 18, Gravity = GravityFlags.Center, Visibility = ViewStates.Gone };
        _subtitle.SetTextColor(global::Android.Graphics.Color.Argb(255, 242, 233, 220));
        _subtitle.SetBackgroundColor(global::Android.Graphics.Color.Argb(190, 22, 19, 15));
        _subtitle.SetPadding(40, 18, 40, 18);

        var match = ViewGroup.LayoutParams.MatchParent;
        root.AddView(_surface, new FrameLayout.LayoutParams(match, match));
        root.AddView(_pad, new FrameLayout.LayoutParams(match, match));
        root.AddView(_hud, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Top | GravityFlags.Left));
        root.AddView(_subtitle, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Bottom | GravityFlags.CenterHorizontal) { BottomMargin = 60 });
        SetContentView(root);
    }

    /// <summary>
    /// The engine loads models and voices from files, so the APK's assets/ are copied to the app's files
    /// once per asset version and <c>SIJENGGO_ASSETS</c> points <see cref="AssetPaths"/> at the copy.
    /// </summary>
    private void UnpackAssets()
    {
        string target = Path.Combine(FilesDir!.AbsolutePath, "assets");
        string marker = Path.Combine(target, ".version");
        if (!File.Exists(marker) || File.ReadAllText(marker) != AssetVersion)
        {
            foreach (string folder in new[] { "models", "voice", "lang" })
            {
                Directory.CreateDirectory(Path.Combine(target, folder));
                foreach (string name in Assets!.List(folder) ?? [])
                {
                    using var input = Assets.Open($"{folder}/{name}");
                    using var output = File.Create(Path.Combine(target, folder, name));
                    input.CopyTo(output);
                }
            }

            File.WriteAllText(marker, AssetVersion);
        }

        Environment.SetEnvironmentVariable("SIJENGGO_ASSETS", target);
    }

    // ───────────────────────────── surface ─────────────────────────────

    public void SurfaceCreated(ISurfaceHolder holder)
    {
        nint surface = holder.Surface?.Handle ?? nint.Zero;
        lock (_gate)
        {
            _window = surface == nint.Zero ? nint.Zero : ANativeWindow_fromSurface(JNIEnv.Handle, surface);
        }

        if (_window == nint.Zero)
        {
            Log.Error(Tag, "no native window for the surface");
            return;
        }

        _running = true;
        _renderThread = new Thread(RenderLoop) { IsBackground = true, Name = "sijenggo.render" };
        _renderThread.Start();
    }

    public void SurfaceChanged(ISurfaceHolder holder, [GeneratedEnum] global::Android.Graphics.Format format, int width, int height)
    {
        _width = width;
        _height = height;
        _surfaceReady.Set();
    }

    public void SurfaceDestroyed(ISurfaceHolder holder)
    {
        _running = false;
        _renderThread?.Join(2000);
        _renderThread = null;
        _surfaceReady.Reset();
        lock (_gate)
        {
            if (_window != nint.Zero)
            {
                ANativeWindow_release(_window);
                _window = nint.Zero;
            }
        }
    }

    protected override void OnPause()
    {
        _paused = true;
        base.OnPause();
    }

    // ───────────────────────────── game loop ─────────────────────────────

    private void RenderLoop()
    {
        nint window;
        lock (_gate)
        {
            window = _window;
        }

        if (!_surfaceReady.Wait(5000) || window == nint.Zero)
        {
            Log.Error(Tag, "the surface never reported a size");
            return;
        }

        const int quality = 0;
        Renderer? renderer = null;
        AudioDirector? audio = null;
        GameView? view = null;
        try
        {
            var options = Quality.Options(quality);
            options.Width = _width;
            options.Height = _height;
            renderer = Renderer.CreateForAndroid(window, options);
            Log.Info(Tag, $"renderer: {renderer.AdapterName} {_width}x{_height}");
            var scene = new Scene();
            audio = new AudioDirector();
            view = new GameView(scene, audio) { Quality = quality };
            var save = new SaveService(Path.Combine(FilesDir!.AbsolutePath, "save"));
            var profile = save.Load();
            var language = profile.Settings.Language;
            int chapter = Math.Clamp(profile.StoryChapterUnlocked, 0, Chapters.All.Length - 1);

            var clock = new FixedStepClock();
            var inputs = new InputFrame[1];
            var watch = Stopwatch.StartNew();
            double previous = 0, hudTimer = 0;
            int width = _width, height = _height;

            while (_running)
            {
                // One chapter per campaign, as in the desktop Story mode; the AI partner is Bima.
                var campaign = new Campaign(GameModeKind.Story, profile, [new PlayerSlot(CharacterId.Jenggo, false), new PlayerSlot(CharacterId.Bima, true)], profile.Settings.Difficulty, chapter);
                var session = campaign.CreateSession();
                var def = campaign.CurrentStage.Chapter;
                view.LoadStage(def);
                var lines = new string[def.Story.Length];
                for (int i = 0; i < lines.Length; i++)
                {
                    lines[i] = def.Story[i][language];
                }

                view.PlayCutscene(Cutscenes.ChapterIntro(def, lines));
                bool advanceHeld = true;
                int shownLine = -2;
                float endTimer = 0;
                clock.Reset();

                while (_running)
                {
                    double now = watch.Elapsed.TotalSeconds;
                    float dt = (float)Math.Clamp(now - previous, 0.001, 0.1);
                    previous = now;
                    if (_width != width || _height != height)
                    {
                        width = _width;
                        height = _height;
                        renderer.Resize(width, height);
                    }

                    var buttons = _pad.State;
                    if (view.CutsceneActive)
                    {
                        bool advance = (buttons & (InputButtons.Light | InputButtons.Jump)) != 0;
                        if (advance && !advanceHeld)
                        {
                            view.AdvanceCutscene();
                        }

                        advanceHeld = advance;
                        int line = view.CutsceneLine;
                        if (line != shownLine)
                        {
                            shownLine = line;
                            string text = line >= 0 ? lines[line] : $"{def.Subtitle[language]}\n{def.Name[language]}";
                            RunOnUiThread(() =>
                            {
                                _subtitle.Text = text;
                                _subtitle.Visibility = ViewStates.Visible;
                            });
                        }
                    }
                    else if (shownLine != -3)
                    {
                        shownLine = -3;
                        RunOnUiThread(() => _subtitle.Visibility = ViewStates.Gone);
                    }

                    if (!_paused && !view.CutsceneActive)
                    {
                        int ticks = clock.Advance(dt);
                        for (int i = 0; i < ticks; i++)
                        {
                            inputs[0] = new InputFrame(buttons);
                            session.Step(inputs);
                        }
                    }

                    view.Update(session, _paused || view.CutsceneActive ? 1f : Math.Clamp(clock.Alpha, 0f, 1f), _paused ? 0f : dt);
                    renderer.Render(scene, view.Camera);

                    hudTimer += dt;
                    if (hudTimer > 0.15)
                    {
                        hudTimer = 0;
                        string hud = Hud(session, language);
                        RunOnUiThread(() => _hud.Text = hud);
                    }

                    if (session.State is SessionState.StageClear or SessionState.GameOver)
                    {
                        endTimer += dt;
                        if (endTimer > 3f)
                        {
                            var result = campaign.Complete(session);
                            save.Save(profile);
                            if (result.Cleared && chapter + 1 < Chapters.All.Length)
                            {
                                chapter++;
                            }

                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(Tag, "game loop failed: " + ex);
            RunOnUiThread(() => _hud.Text = ex.Message);
        }
        finally
        {
            view?.Dispose();
            audio?.Dispose();
            renderer?.Dispose();
        }
    }

    private static string Hud(GameSession session, Language language)
    {
        var world = session.World;
        var p = session.PlayerEntity(0);
        string text = string.Empty;
        if (world.IsAlive(p))
        {
            ref var player = ref world.Get<Player>(p);
            text = $"{Characters.Get(player.Character).Name[language]}  {(int)(world.Get<Health>(p).Fraction * 100)}%   Rage {(int)world.Get<Rage>(p).Meter}%   ×{player.Lives}";
        }

        var boss = session.BossEntity;
        if (world.IsAlive(boss) && world.Get<Boss>(boss).IntroDone)
        {
            text += $"\n{Bosses.Get(world.Get<Boss>(boss).Id).Name[language]}  {(int)(world.Get<Health>(boss).Fraction * 100)}%";
        }

        return session.State switch
        {
            SessionState.StageClear => text + (language == Language.Indonesian ? "\nTAHAP SELESAI" : "\nSTAGE CLEAR"),
            SessionState.GameOver => text + (language == Language.Indonesian ? "\nKALAH — coba lagi" : "\nGAME OVER — retrying"),
            _ => text,
        };
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SiJenggo.App.Flow;
using SiJenggo.App.Input;
using SiJenggo.App.Screens;
using Screen = SiJenggo.App.Screens.Screen;
using Key = Avalonia.Input.Key;
using SiJenggo.App.Ui;
using SiJenggo.Game.Persistence;
using SiJenggo.Presentation;
using SiJenggo.Presentation.Audio;
using ThreeNet;
using ThreeNet.Avalonia;

namespace SiJenggo.App;

/// <summary>
/// The single window: the ThreeNet viewport at the bottom, the key art backdrop for menus, and the
/// current screen on top. Owns the long-lived services (profile, audio, input, 3D view, game loop).
/// </summary>
public sealed class MainWindow : Window
{
    private readonly Grid _layers = new();
    private readonly ContentControl _host = new();
    private readonly Panel _backdrop;
    private readonly TextBlock _fps;
    private readonly DispatcherTimer _menuPadTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private Screen? _screen;
    private int _frames;
    private DateTime _fpsStamp = DateTime.UtcNow;

    public MainWindow(LaunchOptions options)
    {
        Options = options;
        Title = "Si Jenggo — Preman Berhati Malaikat";
        Width = 1600;
        Height = 900;
        MinWidth = 1280;
        MinHeight = 720;
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://SiJenggo/Assets/sijenggo.ico")));
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        Save = new SaveService(options.CaptureDirectory is null ? null : Path.Combine(Path.GetTempPath(), "SiJenggoCapture"));
        Profile = options.CaptureDirectory is null ? Save.Load() : CaptureProfile();
        Strings.Language = Profile.Settings.Language;
        Audio = new AudioDirector();
        Input = new InputMapper();
        Scene = new Scene();
        View = new GameView(Scene, Audio);
        Controller = new GameController(View, Input);

        Viewport = new ThreeNetView
        {
            Scene = Scene,
            Camera = View.Camera,
            MaxFramesPerSecond = 60,
            IsRendering = false,
            IsVisible = false,
            EnableInteraction = false,
            Focusable = false,
        };
        Viewport.Frame += (_, e) => OnFrame((float)Math.Min(e.DeltaSeconds, 0.1));
        Viewport.RenderFailed += (_, message) => Toast($"Renderer: {message}");

        _backdrop = BuildBackdrop();
        _fps = new TextBlock { FontSize = 14, Foreground = Palette.KapurDim, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(12), IsHitTestVisible = false };
        _layers.Children.Add(Viewport);
        _layers.Children.Add(_backdrop);
        _layers.Children.Add(_host);
        _layers.Children.Add(_fps);
        Content = _layers;

        ApplySettings();
        _menuPadTimer.Tick += (_, _) => PumpMenuPad();
        _menuPadTimer.Start();

        Deactivated += (_, _) => Input.ReleaseAll();
        Opened += (_, _) =>
        {
            if (options.CaptureDirectory is not null)
            {
                new CaptureTour(this, options.CaptureDirectory).Start();
            }
            else
            {
                Navigate(new TitleScreen(this));
            }
        };
    }

    public LaunchOptions Options { get; }
    public SaveService Save { get; }
    public Profile Profile { get; }
    public AudioDirector Audio { get; }
    public InputMapper Input { get; }
    public Scene Scene { get; }
    public GameView View { get; }
    public GameController Controller { get; }
    public ThreeNetView Viewport { get; }

    private static Profile CaptureProfile()
    {
        // The screenshot tour shows a well-travelled profile so every screen has content.
        var p = new Profile { StoryChapterUnlocked = 6 };
        for (int i = 0; i < 5; i++)
        {
            p.StoryChaptersCleared.Add(i);
            p.LearnedJurus.Add(Game.Data.Moves.JurusForChapter(i));
        }

        p.UnlockedCharacters.Add(Game.Data.CharacterId.Rani);
        p.UnlockedCharacters.Add(Game.Data.CharacterId.Ucup);
        var j = p.For(Game.Data.CharacterId.Jenggo);
        j.Xp = Game.Data.Leveling.XpForLevel(9) + 300;
        j.Skills.AddRange(["power.1", "power.2", "rage.1", "defense.1", "technique.1"]);
        j.Equip(Game.Data.EquipmentId.JaketKulitKodir);
        j.Equip(Game.Data.EquipmentId.SabukJangkar);
        j.EquippedJurus = Game.Data.Moves.JurusBetawi;
        p.Inventory.Add(Game.Data.EquipmentId.JaketKulitKodir);
        p.Inventory.Add(Game.Data.EquipmentId.SabukJangkar);
        p.Inventory.Add(Game.Data.EquipmentId.KalungMutiara);
        p.Achievements.Add("turun-gunung");
        p.Achievements.Add("raja-jalanan");
        p.ArcadeHighScore = 482300;
        p.SurvivalBestWave = 12;
        return p;
    }

    private Panel BuildBackdrop()
    {
        var panel = new Panel { IsHitTestVisible = false };
        using var stream = AssetLoader.Open(new Uri("avares://SiJenggo/Assets/Art/keyart.png"));
        panel.Children.Add(new Image { Source = new Bitmap(stream), Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Right });
        // Darken the left third where the menu sits, like a shadowed alley wall.
        panel.Children.Add(new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#F216130F"), 0),
                    new GradientStop(Color.Parse("#D916130F"), 0.32),
                    new GradientStop(Color.Parse("#3316130F"), 0.62),
                    new GradientStop(Color.Parse("#0016130F"), 1),
                },
            },
        });
        return panel;
    }

    /// <summary>Shows a screen. Game screens reveal the 3D view; menu screens show the key art.</summary>
    public void Navigate(Screen screen)
    {
        _screen?.OnHidden();
        _screen = screen;
        _host.Content = screen;
        bool game = screen.Uses3D;
        _backdrop.IsVisible = !game;
        Viewport.IsVisible = game;
        Viewport.IsRendering = game;
        Input.ReleaseAll();
        screen.OnShown();
        Dispatcher.UIThread.Post(() => screen.FocusFirst(), DispatcherPriority.Input);
    }

    public void ApplySettings()
    {
        var s = Profile.Settings;
        int quality = Options.Quality ?? s.GraphicsQuality;
        Strings.Language = s.Language;
        Audio.MasterVolume = s.MasterVolume;
        Audio.MusicVolume = s.MusicVolume;
        Audio.SfxVolume = s.SfxVolume;
        View.Quality = quality;
        View.ScreenShakeEnabled = s.ScreenShake;
        Viewport.RendererOptions = Quality.Options(quality);
        Viewport.RenderScale = Presentation.Quality.RenderScale(quality);
        WindowState = s.Fullscreen && Options.CaptureDirectory is null ? WindowState.FullScreen : WindowState.Normal;
        _fps.IsVisible = s.ShowFps;
    }

    public void SaveProfile()
    {
        if (Options.CaptureDirectory is not null)
        {
            return;
        }

        try
        {
            Save.Save(Profile);
        }
        catch (IOException ex)
        {
            Toast(ex.Message);
        }
    }

    public void Toast(string message) => _screen?.ShowToast(message);

    private void OnFrame(float dt)
    {
        Controller.Frame(dt);
        _screen?.OnFrame(dt);
        if (_fps.IsVisible)
        {
            _frames++;
            var now = DateTime.UtcNow;
            if ((now - _fpsStamp).TotalSeconds >= 0.5)
            {
                _fps.Text = $"{_frames / (now - _fpsStamp).TotalSeconds:F0} {Strings.Get("fps")} · {Viewport.Stats.DrawCalls} draw · {Viewport.Stats.Triangles:N0} tri";
                _frames = 0;
                _fpsStamp = now;
            }
        }
    }

    private void PumpMenuPad()
    {
        if (_screen is null || _screen.Uses3D && Controller.Session is not null && !Controller.Paused && !_screen.ModalOpen)
        {
            Input.Poll();
            return;
        }

        Input.Poll();
        if (Input.MenuKeyFromPads() is { } key)
        {
            _screen.OnMenuKey(key);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        Input.KeyDown(e.Key);
        if (_screen?.OnKey(e.Key) == true)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F11)
        {
            WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
        }

        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        Input.KeyUp(e.Key);
        base.OnKeyUp(e);
    }


    protected override void OnClosed(EventArgs e)
    {
        SaveProfile();
        Controller.Stop();
        View.Dispose();
        Audio.Dispose();
        Input.Dispose();
        base.OnClosed(e);
    }
}

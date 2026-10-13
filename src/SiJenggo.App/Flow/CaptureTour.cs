using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SiJenggo.App.Screens;
using SiJenggo.Game;
using SiJenggo.Game.Data;

namespace SiJenggo.App.Flow;

/// <summary>
/// <c>SiJenggo --capture docs/screenshots</c>: walks through the menus and several stages with two
/// AI fighters and saves real screenshots of the running game for the README and docs.
/// </summary>
public sealed class CaptureTour
{
    private readonly MainWindow _shell;
    private readonly string _dir;

    public CaptureTour(MainWindow shell, string directory)
    {
        _shell = shell;
        _dir = Path.GetFullPath(directory);
        Directory.CreateDirectory(_dir);
    }

    public void Start() => Dispatcher.UIThread.Post(async () =>
    {
        try
        {
            await Run();
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(_dir, "capture-error.txt"), ex.ToString());
        }
        finally
        {
            _shell.Close();
        }
    });

    private async Task Shot(string name, int settleMs = 900)
    {
        await Task.Delay(settleMs);
        var size = new PixelSize((int)_shell.Bounds.Width, (int)_shell.Bounds.Height);
        using var rtb = new RenderTargetBitmap(size, new Vector(96, 96));
        rtb.Render(_shell);
        rtb.Save(Path.Combine(_dir, name + ".png"), PngBitmapEncoderOptions.Default);
    }

    private void FastForward(int ticks, Func<GameSession, bool>? until = null)
    {
        var session = _shell.Controller.Session!;
        Span<InputFrame> none = stackalloc InputFrame[GameSession.MaxPlayers];
        for (int i = 0; i < ticks && session.State is SessionState.Playing or SessionState.Cinematic; i++)
        {
            session.Step(none[..session.PlayerCount]);
            if (until?.Invoke(session) == true)
            {
                break;
            }

            if (session.Events.Count > 512)
            {
                session.Events.Clear();
            }
        }

        session.Events.Clear();
    }

    private GameScreen Play(GameModeKind mode, int chapter, CharacterId p1 = CharacterId.Jenggo, CharacterId p2 = CharacterId.Bima)
    {
        var campaign = new Campaign(mode, _shell.Profile, [new PlayerSlot(p1, true), new PlayerSlot(p2, true)], Difficulty.Normal, chapter, seed: 2026);
        var screen = new GameScreen(_shell, campaign);
        _shell.Navigate(screen);
        return screen;
    }

    private async Task Run()
    {
        _shell.Width = 1600;
        _shell.Height = 900;
        await Task.Delay(1500);

        _shell.Navigate(new TitleScreen(_shell));
        await Shot("01-title", 1200);

        var setup = new SetupScreen(_shell, GameModeKind.Story);
        _shell.Navigate(setup);
        setup.Configure(CharacterId.Jenggo, PartnerKind.Ai, CharacterId.Bima, 0);
        await Shot("02-character-select");

        _shell.Navigate(new ProgressionScreen(_shell));
        await Shot("03-skills-and-gear");

        _shell.Navigate(new CreditsScreen(_shell));
        await Shot("04-credits");

        // Chapter 1: story intro, street fight, pause menu, boss.
        var game = Play(GameModeKind.Story, 0);
        await Shot("05-story-intro", 1500);
        game.CloseOverlayForCapture();
        FastForward(900);
        await Shot("06-jakarta-street-fight", 2500);
        game.OpenPauseForCapture();
        await Shot("07-pause-controls");
        game.CloseOverlayForCapture();
        FastForward(12000, s => s.World.IsAlive(s.BossEntity) && s.Tick > 0 && s.World.Get<Game.Components.Boss>(s.BossEntity).IntroDone && s.World.Get<Game.Components.Health>(s.BossEntity).Fraction < 0.8f);
        await Shot("08-boss-bang-kodir", 2200);

        // Other chapters, a little way in.
        string[] names = ["09-pelabuhan-rain", "10-pantai-selatan-storm", "11-hutan-kalimantan", "12-gunung-berapi", "13-candi-kuno", "14-kota-garuda-hitam"];
        CharacterId[] leads = [CharacterId.Bima, CharacterId.Rani, CharacterId.Ucup, CharacterId.Jenggo, CharacterId.Rani, CharacterId.Jenggo];
        for (int c = 1; c <= 6; c++)
        {
            var stage = Play(GameModeKind.Arcade, 0, leads[c - 1], c % 2 == 0 ? CharacterId.Ucup : CharacterId.Jenggo);
            // Arcade starts at chapter 1; jump straight to the wanted chapter's stage.
            for (int i = 0; i < c; i++)
            {
                _shell.Controller.NextStage();
            }

            stage.ShowStageTitle();

            FastForward(700 + c * 60);
            await Shot(names[c - 1], 2600);
        }

        // A full clear for the results screen.
        Play(GameModeKind.Survival, 5);
        FastForward(60 * 60 * 2);
        await Shot("15-survival-temple", 2000);

        game = Play(GameModeKind.Story, 0);
        game.CloseOverlayForCapture();
        FastForward(60 * 60 * 15);
        await Shot("16-results", 4500);
    }
}

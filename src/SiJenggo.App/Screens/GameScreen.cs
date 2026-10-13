using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SiJenggo.App.Flow;
using SiJenggo.App.Ui;
using SiJenggo.Game;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;
using SiJenggo.Presentation.Audio;

namespace SiJenggo.App.Screens;

/// <summary>One player's corner of the HUD.</summary>
internal sealed class PlayerPanel
{
    public required Border Root;
    public required Image Portrait;
    public CharacterId? Shown;
    public required SablonText Name;
    public required SkewBar Health;
    public required SkewBar RageBar;
    public required SkewBar Spirit;
    public required TextBlock RageLabel;
    public required TextBlock Info;
    public required SablonText Combo;
    public required TextBlock ComboLabel;
    public required TextBlock Status;
    public float ComboFade;
    public int LastCombo;
}

/// <summary>Gameplay: the 3D view underneath, the HUD and every in-game overlay on top.</summary>
public sealed class GameScreen : Screen
{
    private readonly Campaign _campaign;
    private readonly OnlineLink? _online;
    private readonly GameController _controller;
    private readonly PlayerPanel[] _panels = new PlayerPanel[2];
    private readonly Border _bossPanel;
    private readonly SablonText _bossName;
    private readonly TextBlock _bossTitle;
    private readonly SkewBar _bossBar;
    private readonly StackPanel _bossPhases;
    private readonly Border _titleCard;
    private readonly SablonText _titleMain;
    private readonly TextBlock _titleSub;
    private readonly SablonText _go;
    private readonly Border _callout;
    private readonly SablonText _calloutText;
    private readonly Border _dialog;
    private readonly TextBlock _dialogName;
    private readonly TextBlock _dialogText;
    private readonly TextBlock _waveText;
    private readonly Border _waiting;
    private readonly Grid _overlay = new() { IsVisible = false };
    private float _titleTime, _calloutTime, _dialogTime, _goTime;
    private bool _storyShown;
    private readonly Grid _hud;
    private readonly Grid _cutLayer = new() { IsVisible = false, IsHitTestVisible = false };
    private readonly TextBlock _cutText;
    private readonly Border _cutCaption;
    private readonly StackPanel _cutTitle;
    private LocText[] _cutLines = [];
    private int _cutShownLine = -2;
    private bool _cutAdvanceHeld = true;
    private Action? _afterCutscene;
    private bool _cutShowsTitle;

    public GameScreen(MainWindow shell, Campaign campaign, OnlineLink? online = null) : base(shell)
    {
        _campaign = campaign;
        _online = online;
        _controller = shell.Controller;

        var hud = new Grid { IsHitTestVisible = false };
        _hud = hud;
        _panels[0] = Panel(false);
        _panels[1] = Panel(true);
        hud.Children.Add(_panels[0].Root);
        hud.Children.Add(_panels[1].Root);
        foreach (var p in _panels)
        {
            hud.Children.Add(p.Combo.Parent is null ? ComboHolder(p) : new Panel());
        }

        // Boss bar, bottom centre.
        _bossName = new SablonText(string.Empty, 30) { Misregister = 2.5 };
        _bossTitle = UiKit.Text(string.Empty, 17, Palette.KapurDim, FontWeight.SemiBold);
        _bossBar = new SkewBar { Height = 22, Fill = Palette.CabaiBrush, Slant = 14 };
        _bossPhases = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
        var bossHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        bossHeader.Children.Add(UiKit.Row(14, _bossName, _bossTitle));
        Grid.SetColumn(_bossPhases, 1);
        bossHeader.Children.Add(_bossPhases);
        _bossPanel = new Border
        {
            Width = 820,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 34),
            Child = UiKit.Stack(6, bossHeader, _bossBar),
            IsVisible = false,
        };
        hud.Children.Add(_bossPanel);

        // Chapter / area title card.
        _titleMain = new SablonText(string.Empty, 64) { Misregister = 4 };
        _titleSub = UiKit.Text(string.Empty, 24, Palette.KunyitBrush, FontWeight.Bold);
        _titleSub.LetterSpacing = 3;
        _titleCard = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 150, 0, 0),
            Background = Palette.Shade,
            Padding = new Thickness(36, 14),
            RenderTransform = new RotateTransform(-1.5),
            Child = UiKit.Stack(0, _titleSub, _titleMain),
            Opacity = 0,
        };
        hud.Children.Add(_titleCard);

        // GO arrow.
        _go = new SablonText(Strings.Get("hud.go") + " →", 54) { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 160, 60, 0), Ink = Palette.KunyitBrush, IsVisible = false };
        hud.Children.Add(_go);

        // Special-move / co-op callout.
        _calloutText = new SablonText(string.Empty, 40) { Misregister = 3 };
        _callout = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 260, 0, 0),
            Child = _calloutText,
            Opacity = 0,
            RenderTransform = new RotateTransform(-2),
        };
        hud.Children.Add(_callout);

        // Boss dialogue.
        _dialogName = UiKit.Text(string.Empty, 20, Palette.KunyitBrush, FontWeight.Bold);
        _dialogText = UiKit.Text(string.Empty, 24, Palette.KapurBrush, FontWeight.Medium);
        _dialogText.MaxWidth = 760;
        _dialog = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 120),
            Background = Palette.Shade,
            BorderBrush = Palette.CabaiBrush,
            BorderThickness = new Thickness(0, 0, 0, 3),
            Padding = new Thickness(24, 12),
            Child = UiKit.Stack(2, _dialogName, _dialogText),
            Opacity = 0,
        };
        hud.Children.Add(_dialog);

        _waveText = UiKit.Text(string.Empty, 22, Palette.KunyitBrush, FontWeight.Bold);
        _waveText.HorizontalAlignment = HorizontalAlignment.Center;
        _waveText.VerticalAlignment = VerticalAlignment.Top;
        _waveText.Margin = new Thickness(0, 24, 0, 0);
        hud.Children.Add(_waveText);

        _waiting = new Border
        {
            Background = Palette.Shade,
            Padding = new Thickness(24, 12),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = UiKit.Text(Strings.Get("hud.waiting"), 26, Palette.KunyitBrush, FontWeight.Bold),
            IsVisible = false,
        };
        hud.Children.Add(_waiting);

        // Cutscene: letterbox bars, the story line as a subtitle, and the chapter name on the last shot.
        _cutText = UiKit.Text(string.Empty, 28, Palette.KapurBrush, FontWeight.Medium);
        _cutText.MaxWidth = 1100;
        _cutText.TextAlignment = TextAlignment.Center;
        _cutText.LineHeight = 38;
        _cutCaption = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 128),
            Background = Palette.Shade,
            Padding = new Thickness(28, 12),
            Child = _cutText,
        };
        var stageChapter = campaign.CurrentStage.Chapter;
        _cutTitle = UiKit.Stack(0, UiKit.Eyebrow(Strings.T(stageChapter.Subtitle)), new SablonText(Strings.T(stageChapter.Name), 72) { Misregister = 4 });
        _cutTitle.HorizontalAlignment = HorizontalAlignment.Left;
        _cutTitle.VerticalAlignment = VerticalAlignment.Center;
        _cutTitle.Margin = new Thickness(110, 0, 0, 0);
        _cutTitle.IsVisible = false;
        var hint = UiKit.Text(Strings.Get("cutscene.hint"), 17, Palette.KapurDim, FontWeight.SemiBold);
        hint.HorizontalAlignment = HorizontalAlignment.Right;
        hint.VerticalAlignment = VerticalAlignment.Bottom;
        hint.Margin = new Thickness(0, 0, 40, 36);
        _cutLayer.Children.Add(new Border { Height = 104, VerticalAlignment = VerticalAlignment.Top, Background = Brushes.Black });
        _cutLayer.Children.Add(new Border { Height = 104, VerticalAlignment = VerticalAlignment.Bottom, Background = Brushes.Black });
        _cutLayer.Children.Add(_cutCaption);
        _cutLayer.Children.Add(_cutTitle);
        _cutLayer.Children.Add(hint);

        Root.Children.Add(hud);
        Root.Children.Add(_cutLayer);
        Root.Children.Add(_overlay);

        _controller.Event += OnEvent;
        _controller.StageEnded += OnStageEnded;
        _controller.PauseRequested += TogglePause;
        Shell.View.CutsceneFinished += OnCutsceneFinished;
    }

    public override bool Uses3D => true;

    public override bool ModalOpen => _overlay.IsVisible;

    public override void OnShown()
    {
        _controller.Start(_campaign, _online);
        if (_campaign.Mode == GameModeKind.Story && !_storyShown)
        {
            _storyShown = true;
            PlayChapterIntro();
        }
        else
        {
            ShowStageTitle();
        }
    }

    public override void OnHidden()
    {
        _controller.Event -= OnEvent;
        _controller.StageEnded -= OnStageEnded;
        _controller.PauseRequested -= TogglePause;
        Shell.View.CutsceneFinished -= OnCutsceneFinished;
        Shell.View.StopCutscene();
        _controller.Stop();
    }

    public override void FocusFirst()
    {
        if (_overlay.IsVisible)
        {
            base.FocusFirst();
        }
        else
        {
            Focus();
        }
    }

    // ───────────────────────────── HUD construction ─────────────────────────────

    private PlayerPanel Panel(bool right)
    {
        var name = new SablonText(string.Empty, 28) { Misregister = 2 };
        var health = new SkewBar { Height = 22, Width = 380, Fill = Palette.KunyitBrush, Slant = right ? -12 : 12 };
        var rage = new SkewBar { Height = 12, Width = 330, Fill = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative), GradientStops = { new GradientStop(Palette.Cabai, 0), new GradientStop(Palette.Kunyit, 1) } }, Slant = 8 };
        var spirit = new SkewBar { Height = 6, Width = 260, Fill = Palette.ToskaBrush, Slant = 5 };
        var rageLabel = UiKit.Text(Strings.Get("hud.rage"), 14, Palette.KapurDim, FontWeight.Bold);
        rageLabel.LetterSpacing = 2;
        var info = UiKit.Text(string.Empty, 17, Palette.KapurBrush, FontWeight.SemiBold);
        var status = UiKit.Text(string.Empty, 18, Palette.CabaiBrush, FontWeight.Bold);
        var stack = UiKit.Stack(5, name, health, rage, rageLabel, spirit, info, status);
        if (right)
        {
            foreach (var c in stack.Children)
            {
                c.HorizontalAlignment = HorizontalAlignment.Right;
            }

            health.RenderTransform = new ScaleTransform(-1, 1);
            rage.RenderTransform = new ScaleTransform(-1, 1);
            spirit.RenderTransform = new ScaleTransform(-1, 1);
        }

        // Portrait chip beside the bars, and a dark plate so the HUD reads over bright stages.
        var portrait = new Border
        {
            Width = 74,
            Height = 86,
            BorderBrush = right ? Palette.ToskaBrush : Palette.KunyitBrush,
            BorderThickness = new Thickness(3),
            RenderTransform = new RotateTransform(right ? 3 : -3),
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true,
            Child = new Image { Stretch = Stretch.UniformToFill },
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        if (right)
        {
            row.Children.Add(stack);
            row.Children.Add(portrait);
        }
        else
        {
            row.Children.Add(portrait);
            row.Children.Add(stack);
        }

        var root = new Border
        {
            Child = row,
            Padding = new Thickness(18, 14),
            Margin = new Thickness(18, 14),
            HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            IsVisible = false,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(right ? 1 : 0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(right ? 0 : 1, 0, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#D916130F"), 0), new GradientStop(Color.Parse("#9916130F"), 0.7), new GradientStop(Color.Parse("#0016130F"), 1) },
            },
        };
        var combo = new SablonText(string.Empty, 72) { Misregister = 4, Ink = Palette.KunyitBrush, IsVisible = false };
        var comboLabel = UiKit.Text(Strings.Get("hud.hits"), 22, Palette.KapurBrush, FontWeight.Bold);
        comboLabel.LetterSpacing = 4;
        comboLabel.IsVisible = false;
        return new PlayerPanel { Root = root, Portrait = (Image)portrait.Child!, Name = name, Health = health, RageBar = rage, Spirit = spirit, RageLabel = rageLabel, Info = info, Combo = combo, ComboLabel = comboLabel, Status = status };
    }

    private Control ComboHolder(PlayerPanel p)
    {
        bool right = p == _panels[1];
        return new StackPanel
        {
            HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = right ? new Thickness(0, 230, 70, 0) : new Thickness(70, 230, 0, 0),
            RenderTransform = new RotateTransform(right ? 3 : -3),
            Children = { p.Combo, p.ComboLabel },
        };
    }

    // ───────────────────────────── per frame ─────────────────────────────

    public override void OnFrame(float dt)
    {
        var session = _controller.Session;
        if (session is null)
        {
            return;
        }

        if (_cutLayer.IsVisible)
        {
            UpdateCutscene();
        }

        var world = session.World;
        for (int slot = 0; slot < 2; slot++)
        {
            var panel = _panels[slot];
            var e = session.PlayerEntity(slot);
            if (slot >= session.PlayerCount || !world.IsAlive(e))
            {
                panel.Root.IsVisible = false;
                panel.Combo.IsVisible = panel.ComboLabel.IsVisible = false;
                continue;
            }

            panel.Root.IsVisible = true;
            ref var player = ref world.Get<Player>(e);
            ref var health = ref world.Get<Health>(e);
            ref var rage = ref world.Get<Rage>(e);
            ref var fighter = ref world.Get<Fighter>(e);
            string name = Strings.T(Characters.Get(player.Character).Name) + (player.IsAi ? "  · AI" : string.Empty);
            if (panel.Name.Text != name)
            {
                panel.Name.Text = name;
            }

            if (panel.Shown != player.Character)
            {
                panel.Shown = player.Character;
                panel.Portrait.Source = SetupScreen.Portrait(player.Character);
            }

            panel.Health.Value = health.Fraction;
            panel.Health.Fill = health.Fraction < 0.25f ? Palette.CabaiBrush : Palette.KunyitBrush;
            panel.Health.Tick(dt);
            panel.RageBar.Value = rage.Meter / 100f;
            panel.RageBar.Glow = rage.Meter >= 100 || rage.Active;
            panel.Spirit.Value = rage.Semangat / 100f;
            string rageText = rage.Active ? Strings.Get("hud.rage.on") : rage.Meter >= 100 ? Strings.Get("hud.rage.ready") : Strings.Get("hud.rage");
            if (panel.RageLabel.Text != rageText)
            {
                panel.RageLabel.Text = rageText;
                panel.RageLabel.Foreground = rage.Active || rage.Meter >= 100 ? Palette.KunyitBrush : Palette.KapurDim;
            }

            var weapon = world.Get<WeaponHolder>(e).Weapon;
            string info = $"{Strings.Get("result.score")} {player.Score:N0}   ·   {Strings.Get("hud.lives")} ×{player.Lives}" + (weapon != WeaponId.None ? $"   ·   {Strings.T(Weapons.Get(weapon).Name)}" : string.Empty);
            if (panel.Info.Text != info)
            {
                panel.Info.Text = info;
            }

            string status = fighter.State == FighterState.Downed ? Strings.Get("hud.down") : string.Empty;
            if (panel.Status.Text != status)
            {
                panel.Status.Text = status;
            }

            // Combo counter: pops on change, fades after the chain drops.
            if (player.Combo >= 2)
            {
                if (player.Combo != panel.LastCombo)
                {
                    panel.Combo.Text = player.Combo.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    panel.Combo.Misregister = 7;
                    panel.LastCombo = player.Combo;
                }

                panel.ComboFade = 1f;
            }
            else
            {
                panel.ComboFade = Math.Max(0, panel.ComboFade - dt * 2f);
                panel.LastCombo = 0;
            }

            panel.Combo.Misregister = Math.Max(3, panel.Combo.Misregister - dt * 30);
            panel.Combo.Opacity = panel.ComboLabel.Opacity = panel.ComboFade;
            panel.Combo.IsVisible = panel.ComboLabel.IsVisible = panel.ComboFade > 0;
        }

        // Boss bar.
        var boss = session.BossEntity;
        bool showBoss = world.IsAlive(boss) && world.Get<Boss>(boss).IntroDone && !world.Get<Health>(boss).Dead;
        _bossPanel.IsVisible = showBoss;
        if (showBoss)
        {
            ref var b = ref world.Get<Boss>(boss);
            var def = Bosses.Get(b.Id);
            if (_bossName.Text != Strings.T(def.Name))
            {
                _bossName.Text = Strings.T(def.Name);
                _bossTitle.Text = Strings.T(def.Title).ToUpperInvariant();
            }

            _bossBar.Value = world.Get<Health>(boss).Fraction;
            _bossBar.Fill = b.ShieldTicks > 0 ? Palette.ToskaBrush : Palette.CabaiBrush;
            _bossBar.Tick(dt);
            if (_bossPhases.Children.Count != 3 - b.Phase)
            {
                _bossPhases.Children.Clear();
                for (int i = b.Phase; i < 3; i++)
                {
                    _bossPhases.Children.Add(new Border { Width = 14, Height = 14, Background = Palette.CabaiBrush, RenderTransform = new RotateTransform(45) });
                }
            }
        }

        string wave = session.Stage.Kind == StageKind.SurvivalArena && session.WaveNumber > 0 ? $"{Strings.Get("hud.wave")} {session.WaveNumber}" : string.Empty;
        if (_waveText.Text != wave)
        {
            _waveText.Text = wave;
        }

        // GO arrow blinks after a wave is cleared.
        _goTime += dt;
        _go.IsVisible = session.GoArrow && !session.CameraLocked && (_goTime % 0.8f) < 0.55f;

        Fade(_titleCard, ref _titleTime, dt, 3.2f);
        Fade(_callout, ref _calloutTime, dt, 1.4f);
        Fade(_dialog, ref _dialogTime, dt, 3.6f);
        _waiting.IsVisible = _controller.Waiting;
    }

    private static void Fade(Control c, ref float time, float dt, float hold)
    {
        if (time <= 0)
        {
            c.Opacity = 0;
            return;
        }

        time -= dt;
        float age = hold - time;
        c.Opacity = Math.Clamp(Math.Min(age / 0.2f, time / 0.4f), 0, 1);
    }

    private void Callout(string text, IBrush? ink = null)
    {
        _calloutText.Text = text;
        _calloutText.Ink = ink ?? Palette.KapurBrush;
        _calloutTime = 1.4f;
    }

    private void Dialog(string speaker, string line)
    {
        _dialogName.Text = speaker.ToUpperInvariant();
        _dialogText.Text = line;
        _dialogTime = 3.6f;
    }

    internal void ShowStageTitle()
    {
        var stage = _campaign.CurrentStage;
        _titleSub.Text = stage.Kind switch
        {
            StageKind.BossArena => $"{Strings.Get("menu.bossrush")} {_campaign.StageIndex + 1}/{_campaign.StageCount}",
            StageKind.SurvivalArena => Strings.Get("menu.survival").ToUpperInvariant(),
            _ => Strings.T(stage.Chapter.Subtitle).ToUpperInvariant(),
        };
        _titleMain.Text = Strings.T(stage.Chapter.Name);
        _titleTime = 3.2f;
    }

    // ───────────────────────────── events ─────────────────────────────

    private void OnEvent(GameEvent ev)
    {
        var session = _controller.Session;
        if (session is null)
        {
            return;
        }

        switch (ev.Kind)
        {
            case GameEventKind.AreaEntered when ev.Param > 0:
                _titleSub.Text = Strings.T(session.Stage.Chapter.Name).ToUpperInvariant();
                _titleMain.Text = Strings.T(session.Stage.Chapter.Areas[ev.Param].Name);
                _titleTime = 2.6f;
                break;
            case GameEventKind.Special when !ev.Move.IsNone:
            case GameEventKind.Ultimate when !ev.Move.IsNone:
                Callout(Strings.T(ev.Move.Def.Name), ev.Kind == GameEventKind.Ultimate ? Palette.KunyitBrush : Palette.KapurBrush);
                break;
            case GameEventKind.RageOn:
                Callout(Strings.Get("hud.rage"), Palette.CabaiBrush);
                break;
            case GameEventKind.CoopMove:
                Callout((CoopMove)ev.Param switch
                {
                    CoopMove.DoubleGrab => Strings.T(Moves.DoubleGrabSlam.Def.Name),
                    CoopMove.TeamFinisher => Strings.T(Moves.TeamFinisher.Def.Name),
                    CoopMove.AirLaunchCombo => Strings.Get("coop.airlaunch"),
                    _ => Strings.Get("coop.revive"),
                }, Palette.ToskaBrush);
                break;
            case GameEventKind.ComboMilestone:
                Callout($"{(int)ev.Value} {Strings.Get("hud.hits")}!", Palette.KunyitBrush);
                break;
            case GameEventKind.NotEnoughSpirit:
                ShowToast(Strings.Get("hud.spirit.empty"));
                break;
            case GameEventKind.BossIntro:
            {
                var def = Bosses.Get((BossId)ev.Param);
                _titleSub.Text = Strings.T(def.Title).ToUpperInvariant();
                _titleMain.Text = Strings.T(def.Name);
                _titleTime = 2.8f;
                Dialog(Strings.T(def.Name), Strings.T(def.IntroLine));
                break;
            }

            case GameEventKind.BossTaunt when session.World.IsAlive(session.BossEntity):
            {
                var def = Bosses.Get(session.World.Get<Boss>(session.BossEntity).Id);
                var taunt = def.Phases[Math.Clamp(ev.Param, 0, 2)].Taunt;
                if (!string.IsNullOrEmpty(taunt.Id))
                {
                    Dialog(Strings.T(def.Name), Strings.T(taunt));
                }

                break;
            }

            case GameEventKind.BossDefeated:
            {
                var def = Bosses.Get((BossId)ev.Param);
                Dialog(Strings.T(def.Name), Strings.T(def.DefeatLine));
                break;
            }

            case GameEventKind.Go:
                _goTime = 0;
                break;
            case GameEventKind.WeaponBroken:
                ShowToast(Strings.Format("weapon.broke", Strings.T(Weapons.Get((WeaponId)ev.Param).Name)));
                break;
        }
    }

    // ───────────────────────────── overlays ─────────────────────────────

    private void OpenOverlay(Control content)
    {
        _controller.Paused = true;
        _overlay.Children.Clear();
        _overlay.Children.Add(new Border { Background = new SolidColorBrush(Color.Parse("#B316130F")) });
        _overlay.Children.Add(content);
        _overlay.IsVisible = true;
        Shell.Input.ReleaseAll();
        Avalonia.Threading.Dispatcher.UIThread.Post(base.FocusFirst, Avalonia.Threading.DispatcherPriority.Input);
    }

    private void CloseOverlay()
    {
        _overlay.IsVisible = false;
        _overlay.Children.Clear();
        _controller.Paused = false;
        Shell.Input.ReleaseAll();
        Focus();
    }

    // ───────────────────────────── cutscenes ─────────────────────────────

    /// <summary>The Story chapter opening, played in the engine on the chapter's own stage.</summary>
    private void PlayChapterIntro()
    {
        var chapter = _campaign.CurrentStage.Chapter;
        PlayCutscene(Presentation.Cutscenes.ChapterIntro(chapter, Texts(chapter.Story)), chapter.Story, ShowStageTitle, showTitle: true);
    }

    private static string[] Texts(LocText[] lines)
    {
        var texts = new string[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            texts[i] = Strings.T(lines[i]);
        }

        return texts;
    }

    private void PlayCutscene(Presentation.CutsceneShot[] shots, LocText[] lines, Action after, bool showTitle = false)
    {
        _cutShowsTitle = showTitle;
        _cutLines = lines;
        _cutShownLine = -2;
        _cutAdvanceHeld = true;
        _afterCutscene = after;
        _controller.HoldSimulation = true;
        _hud.IsVisible = false;
        _cutLayer.IsVisible = true;
        Shell.View.PlayCutscene(shots);
        UpdateCutscene();
    }

    private void UpdateCutscene()
    {
        int line = Shell.View.CutsceneLine;
        if (line != _cutShownLine)
        {
            _cutShownLine = line;
            _cutCaption.IsVisible = line >= 0 && line < _cutLines.Length;
            _cutTitle.IsVisible = line < 0 && _cutShowsTitle;
            if (_cutCaption.IsVisible)
            {
                _cutText.Text = Strings.T(_cutLines[line]);
            }
        }

        // Light / Jump on the keyboard sets or a pad advances; Esc / Start skips (through TogglePause).
        var buttons = Shell.Input.Read(0, true);
        bool advance = (buttons & (InputButtons.Light | InputButtons.Jump)) != 0;
        if (advance && !_cutAdvanceHeld)
        {
            Shell.View.AdvanceCutscene();
        }

        _cutAdvanceHeld = advance;
    }

    private void OnCutsceneFinished()
    {
        _cutLayer.IsVisible = false;
        _hud.IsVisible = true;
        _controller.HoldSimulation = false;
        Shell.Input.ReleaseAll();
        var after = _afterCutscene;
        _afterCutscene = null;
        after?.Invoke();
    }

    private void TogglePause()
    {
        if (_cutLayer.IsVisible)
        {
            Shell.View.StopCutscene();
            return;
        }

        // Results stay up until a choice is made.
        if (_controller.Session?.State is SessionState.StageClear or SessionState.GameOver)
        {
            return;
        }

        if (_overlay.IsVisible)
        {
            CloseOverlay();
            return;
        }

        var menu = UiKit.Stack(6,
            new SablonText(Strings.Get("pause.title"), 72),
            UiKit.Sticker(Strings.Get("pause.resume"), CloseOverlay, 32, -1.4),
            UiKit.Sticker(Strings.Get("pause.restart"), () =>
            {
                CloseOverlay();
                _controller.Restart();
                ShowStageTitle();
            }, 32, 1),
            UiKit.Sticker(Strings.Get("pause.quit"), Quit, 32, -1));
        var controls = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto"), ColumnSpacing = 26, RowSpacing = 6 };
        int row = 0;
        foreach (var (action, keys, pad) in Strings.Controls)
        {
            controls.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var a = UiKit.Text(action, 19, Palette.KapurBrush, FontWeight.SemiBold);
            var k = UiKit.Text(keys, 19, Palette.KunyitBrush);
            var g = UiKit.Text(pad, 19, Palette.ToskaBrush);
            Grid.SetRow(a, row);
            Grid.SetRow(k, row);
            Grid.SetColumn(k, 1);
            Grid.SetRow(g, row);
            Grid.SetColumn(g, 2);
            controls.Children.Add(a);
            controls.Children.Add(k);
            controls.Children.Add(g);
            row++;
        }

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(90, 70) };
        layout.Children.Add(menu);
        var controlsBoard = UiKit.Board(UiKit.Stack(12, UiKit.Eyebrow(Strings.Get("controls.title")), controls));
        controlsBoard.Margin = new Thickness(70, 0, 0, 0);
        controlsBoard.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(controlsBoard, 1);
        layout.Children.Add(controlsBoard);
        OpenOverlay(layout);
    }

    private void OnStageEnded(StageResult result)
    {
        Shell.SaveProfile();
        bool more = result.Cleared && _campaign.HasNextStage;
        var stats = result.Stats;
        var headline = new SablonText(Strings.Get(result.Cleared ? "result.clear" : "result.over"), 80) { Misregister = 5, Ink = result.Cleared ? Palette.KunyitBrush : Palette.CabaiBrush };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"), ColumnSpacing = 40, RowSpacing = 4 };
        int row = 0;
        void Stat(string label, string value)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var l = UiKit.Text(label, 22, Palette.KapurDim, FontWeight.SemiBold);
            var v = UiKit.Text(value, 26, Palette.KapurBrush, FontWeight.Bold);
            Grid.SetRow(l, row);
            Grid.SetRow(v, row);
            Grid.SetColumn(v, 1);
            grid.Children.Add(l);
            grid.Children.Add(v);
            row++;
        }

        Stat(Strings.Get("result.score"), result.TotalScore.ToString("N0", System.Globalization.CultureInfo.InvariantCulture));
        Stat(Strings.Get("result.ko"), stats.KnockOuts.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Stat(Strings.Get("result.combo"), stats.MaxCombo.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Stat(Strings.Get("result.time"), result.Time.ToString(@"m\:ss", System.Globalization.CultureInfo.InvariantCulture));
        if (_campaign.CurrentStage.Kind == StageKind.SurvivalArena)
        {
            Stat(Strings.Get("result.waves"), stats.WavesSurvived.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        foreach (var p in result.Players)
        {
            Stat($"{Strings.T(Characters.Get(p.Character).Name)} · {Strings.Get("result.xp")}", $"+{p.Xp}" + (p.LevelAfter > p.LevelBefore ? $"   {Strings.Get("result.level")} {p.LevelAfter}" : string.Empty));
        }

        var extras = new StackPanel { Spacing = 6 };
        if (result.Unlocks.Count > 0)
        {
            extras.Children.Add(UiKit.Eyebrow(Strings.Get("result.unlocks")));
            foreach (var u in result.Unlocks)
            {
                extras.Children.Add(UiKit.Text("★ " + Strings.T(u), 22, Palette.KunyitBrush, FontWeight.SemiBold));
            }
        }

        if (result.NewAchievements.Count > 0)
        {
            extras.Children.Add(UiKit.Eyebrow(Strings.Get("result.achievements")));
            foreach (var a in result.NewAchievements)
            {
                extras.Children.Add(UiKit.Text($"◆ {Strings.T(a.Name)} — {Strings.T(a.Description)}", 20, Palette.ToskaBrush, FontWeight.SemiBold));
            }
        }

        var buttons = new StackPanel { Spacing = 4 };
        if (more)
        {
            buttons.Children.Add(UiKit.Sticker(Strings.Get("result.next") + "  →", () =>
            {
                CloseOverlay();
                _controller.NextStage();
                ShowStageTitle();
            }, 32, -1.3));
        }
        else if (result.Cleared && _campaign.Mode == GameModeKind.Story && _online is null)
        {
            int next = _campaign.CurrentStage.Chapter.Index + 1;
            if (next < Chapters.All.Length)
            {
                // Straight into the next chapter: its in-engine intro plays on the new stage.
                buttons.Children.Add(UiKit.Sticker(Strings.Get("result.nextchapter") + "  →", () =>
                {
                    _overlay.IsVisible = false;
                    Shell.Navigate(new GameScreen(Shell, new Campaign(GameModeKind.Story, _campaign.Profile, _campaign.Players, _campaign.Difficulty, next)));
                }, 32, -1.3));
            }
            else
            {
                buttons.Children.Add(UiKit.Sticker(Strings.Get("result.epilogue") + "  →", () =>
                {
                    CloseOverlay();
                    PlayCutscene(Presentation.Cutscenes.Epilogue(_controller.Session?.CameraX ?? 0, Texts(Chapters.Epilogue)), Chapters.Epilogue, () => Shell.Navigate(new CreditsScreen(Shell)));
                }, 32, -1.3));
            }
        }
        else if (!result.Cleared || _campaign.Mode != GameModeKind.Story)
        {
            buttons.Children.Add(UiKit.Sticker(Strings.Get("result.retry"), () =>
            {
                CloseOverlay();
                _controller.Restart();
                ShowStageTitle();
            }, 32, -1.3));
        }

        buttons.Children.Add(UiKit.Sticker(Strings.Get("result.menu"), Quit, 28, 1));
        var layout = UiKit.Stack(20, headline, UiKit.Row(60, grid, extras), buttons);
        layout.Margin = new Thickness(100, 80);
        OpenOverlay(layout);
    }

    private void Quit()
    {
        _overlay.IsVisible = false;
        Shell.Navigate(new TitleScreen(Shell));
    }

    public override bool OnKey(Key key)
    {
        if (_cutLayer.IsVisible && key is Key.Enter or Key.Space)
        {
            Shell.View.AdvanceCutscene();
            return true;
        }

        // Escape (and the pad's Start) reach the game controller through the input mapper, which toggles pause.
        if (!_overlay.IsVisible || key == Key.Escape)
        {
            return true;
        }

        return base.OnKey(key);
    }

    /// <summary>Used by the screenshot tour.</summary>
    internal void OpenPauseForCapture() => TogglePause();

    internal void CloseOverlayForCapture()
    {
        Shell.View.StopCutscene();
        CloseOverlay();
    }
}

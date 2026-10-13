using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SiJenggo.App.Ui;
using SiJenggo.Game;
using SiJenggo.Game.Data;
using SiJenggo.Presentation.Audio;

namespace SiJenggo.App.Screens;

public enum PartnerKind
{
    None,
    Human,
    Ai,
}

/// <summary>Character select plus the mode's options (chapter / arena, partner, difficulty).</summary>
public sealed class SetupScreen : Screen
{
    private static readonly Dictionary<CharacterId, Bitmap> s_portraits = [];

    private readonly GameModeKind _mode;
    private readonly ChallengeDef? _challenge;
    private CharacterId _p1 = CharacterId.Jenggo;
    private CharacterId _p2 = CharacterId.Bima;
    private PartnerKind _partner = PartnerKind.None;
    private int _chapter;
    private Difficulty _difficulty;
    private readonly StackPanel _body = new() { Spacing = 22 };

    public SetupScreen(MainWindow shell, GameModeKind mode, ChallengeDef? challenge = null) : base(shell)
    {
        _mode = mode;
        _challenge = challenge;
        _difficulty = shell.Profile.Settings.Difficulty;
        _chapter = mode == GameModeKind.Story ? Math.Min(shell.Profile.StoryChapterUnlocked, Chapters.All.Length - 1) : 0;
        if (challenge?.ForcedCharacter is { } forced)
        {
            _p1 = forced;
        }

        var scroll = new ScrollViewer { Content = _body, Margin = new Thickness(72, 48, 72, 32) };
        Root.Children.Add(scroll);
        Rebuild();
    }

    public static Bitmap Portrait(CharacterId id)
    {
        if (!s_portraits.TryGetValue(id, out var bmp))
        {
            using var s = AssetLoader.Open(new Uri($"avares://SiJenggo/Assets/Art/portrait_{Characters.Get(id).Key}.png"));
            bmp = Bitmap.DecodeToWidth(s, 360);
            s_portraits[id] = bmp;
        }

        return bmp;
    }

    private string ModeName => _mode switch
    {
        GameModeKind.Story => Strings.Get("menu.story"),
        GameModeKind.Arcade => Strings.Get("menu.arcade"),
        GameModeKind.Survival => Strings.Get("menu.survival"),
        GameModeKind.BossRush => Strings.Get("menu.bossrush"),
        _ => Strings.Get("menu.challenge"),
    };

    private void Rebuild()
    {
        _body.Children.Clear();
        var header = Header(ModeName, Strings.Get("setup.players"));
        string desc = _challenge is not null ? Strings.T(_challenge.Description) : Strings.Get($"mode.{_mode.ToString().ToLowerInvariant()}.desc");
        _body.Children.Add(UiKit.Row(28, header));
        _body.Children.Add(UiKit.Text(desc, 21, Palette.KapurDim));

        // Character cards.
        var cards = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 18 };
        foreach (var def in Characters.All)
        {
            cards.Children.Add(Card(def));
        }

        _body.Children.Add(cards);

        // Partner.
        if (_challenge?.ForcedCharacter is null)
        {
            var partner = UiKit.Row(10,
                UiKit.Chip(Strings.Get("setup.p2.none"), () => { _partner = PartnerKind.None; Rebuild(); }, _partner == PartnerKind.None),
                UiKit.Chip(Strings.Get("setup.p2.human"), () => { _partner = PartnerKind.Human; Rebuild(); }, _partner == PartnerKind.Human),
                UiKit.Chip(Strings.Get("setup.p2.ai"), () => { _partner = PartnerKind.Ai; Rebuild(); }, _partner == PartnerKind.Ai));
            _body.Children.Add(Section(Strings.Get("setup.p2"), partner));
            if (_partner != PartnerKind.None)
            {
                var p2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                foreach (var def in Characters.All)
                {
                    var chip = UiKit.Chip(Strings.T(def.Name), () => { _p2 = def.Id; Rebuild(); }, _p2 == def.Id);
                    chip.IsEnabled = Shell.Profile.IsUnlocked(def.Id);
                    p2.Children.Add(chip);
                }

                _body.Children.Add(p2);
            }
        }

        // Chapter / arena.
        if (_mode is GameModeKind.Story or GameModeKind.Survival)
        {
            var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var c in Chapters.All)
            {
                bool unlocked = _mode == GameModeKind.Survival || c.Index <= Shell.Profile.StoryChapterUnlocked;
                string label = $"{c.Index + 1}. {Strings.T(c.Name)}" + (Shell.Profile.StoryChaptersCleared.Contains(c.Index) && _mode == GameModeKind.Story ? "  ✓" : string.Empty);
                var chip = UiKit.Chip(unlocked ? label : $"{c.Index + 1}. {Strings.Get("setup.locked")}", () => { _chapter = c.Index; Rebuild(); }, _chapter == c.Index);
                chip.IsEnabled = unlocked;
                chip.Margin = new Thickness(0, 0, 10, 10);
                wrap.Children.Add(chip);
            }

            _body.Children.Add(Section(Strings.Get(_mode == GameModeKind.Story ? "setup.chapter" : "setup.arena"), wrap));
        }

        var diff = UiKit.Row(10,
            Enum.GetValues<Difficulty>().Select(d => (Control)UiKit.Chip(Strings.Get($"diff.{d}"), () => { _difficulty = d; Rebuild(); }, _difficulty == d)).ToArray());
        _body.Children.Add(Section(Strings.Get("setup.difficulty"), diff));

        var start = UiKit.Sticker(Strings.Get("start") + "  →", StartGame, 38, -1.2);
        var back = BackButton(() => Shell.Navigate(new TitleScreen(Shell)));
        _body.Children.Add(UiKit.Row(30, start, back));
    }

    private static Control Section(string title, Control content) => UiKit.Stack(8, UiKit.Eyebrow(title), content);

    private Control Card(CharacterDef def)
    {
        bool unlocked = Shell.Profile.IsUnlocked(def.Id);
        bool isP1 = _p1 == def.Id;
        bool isP2 = _partner != PartnerKind.None && _p2 == def.Id;
        var image = new Image { Source = Portrait(def.Id), Width = 250, Height = 300, Stretch = Stretch.UniformToFill, Opacity = unlocked ? 1 : 0.25 };
        var tag = new Border
        {
            Background = isP1 ? Palette.KunyitBrush : Palette.ToskaBrush,
            Padding = new Thickness(10, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(-6, 10, 0, 0),
            RenderTransform = new RotateTransform(-4),
            IsVisible = isP1 || isP2,
            Child = UiKit.Text(isP1 && isP2 ? "P1 + P2" : isP1 ? "P1" : "P2", 22, Palette.AspalBrush, display: true),
        };
        var name = new SablonText(Strings.T(def.Name), 26) { Misregister = isP1 ? 3 : 1.5 };
        var info = UiKit.Stack(6,
            name,
            UiKit.Text(unlocked ? Strings.T(def.Tagline) : Strings.T(def.UnlockHint), 17, Palette.KapurDim),
            StatRow("stat.power", def.Rating.Power, Palette.CabaiBrush),
            StatRow("stat.speed", def.Rating.Speed, Palette.KunyitBrush),
            StatRow("stat.defense", def.Rating.Defense, Palette.ToskaBrush),
            StatRow("stat.technique", def.Rating.Technique, Palette.KapurBrush));
        info.Margin = new Thickness(14, 10, 14, 14);

        var card = new Border
        {
            Width = 252,
            BorderThickness = new Thickness(3),
            BorderBrush = isP1 ? Palette.KunyitBrush : isP2 ? Palette.ToskaBrush : Palette.SogaBrush,
            Background = Palette.Shade,
            Child = new StackPanel { Children = { new Grid { Children = { image, tag } }, info } },
        };
        var button = new Button { Content = card, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), IsEnabled = unlocked && _challenge?.ForcedCharacter is null, Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) };
        button.Click += (_, _) =>
        {
            Shell.Audio.Play(Sfx.UiConfirm, 0.8f, 0f);
            _p1 = def.Id;
            Rebuild();
        };
        return button;
    }

    private static Control StatRow(string key, int value, IBrush brush)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("92,*") };
        var label = UiKit.Text(Strings.Get(key), 16, Palette.KapurDim);
        var pips = UiKit.Pips(value, brush);
        pips.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(pips, 1);
        grid.Children.Add(label);
        grid.Children.Add(pips);
        return grid;
    }

    public void Configure(CharacterId p1, PartnerKind partner, CharacterId p2, int chapter)
    {
        _p1 = p1;
        _partner = partner;
        _p2 = p2;
        _chapter = chapter;
        Rebuild();
    }

    public void StartGame()
    {
        Shell.Audio.Play(Sfx.UiConfirm, 1f, 0f);
        Shell.Profile.Settings.Difficulty = _difficulty;
        var players = new List<PlayerSlot> { new(_p1, false) };
        if (_partner != PartnerKind.None)
        {
            players.Add(new PlayerSlot(_p2, _partner == PartnerKind.Ai));
        }

        int chapter = _challenge?.Chapter ?? _chapter;
        var campaign = new Campaign(_mode, Shell.Profile, players, _difficulty, chapter, _challenge);
        Shell.Navigate(new GameScreen(Shell, campaign));
    }

    protected override bool Back()
    {
        Shell.Navigate(_challenge is null ? new TitleScreen(Shell) : new ChallengeScreen(Shell));
        return true;
    }
}

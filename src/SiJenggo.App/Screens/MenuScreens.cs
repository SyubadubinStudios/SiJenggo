using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SiJenggo.App.Flow;
using SiJenggo.App.Ui;
using SiJenggo.Game;
using SiJenggo.Game.Data;
using SiJenggo.Game.Net;
using SiJenggo.Game.Persistence;
using SiJenggo.Presentation.Audio;

namespace SiJenggo.App.Screens;

/// <summary>The list of challenge missions.</summary>
public sealed class ChallengeScreen : Screen
{
    public ChallengeScreen(MainWindow shell) : base(shell)
    {
        var list = new StackPanel { Spacing = 10 };
        foreach (var c in Challenges.All)
        {
            bool done = shell.Profile.ChallengesCleared.Contains(c.Key);
            var title = new SablonText(Strings.T(c.Name), 28) { Misregister = 1.5 };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var text = UiKit.Stack(2, title, UiKit.Text(Strings.T(c.Description) + $"  ·  {Strings.T(Chapters.Get(c.Chapter).Name)}", 18, Palette.KapurDim));
            var badge = UiKit.Text(done ? "✓ " + Strings.Get("setup.cleared") : string.Empty, 18, Palette.ToskaBrush, FontWeight.Bold);
            badge.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(badge, 1);
            row.Children.Add(text);
            row.Children.Add(badge);
            var button = new Button { Content = row, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            button.Classes.Add("chip");
            button.Click += (_, _) => Shell.Navigate(new SetupScreen(Shell, GameModeKind.Challenge, c));
            list.Children.Add(button);
        }

        var body = UiKit.Stack(22, Header(Strings.Get("menu.challenge"), Strings.Get("setup.challenge")), new ScrollViewer { Content = list, MaxHeight = 640 }, BackButton(() => Shell.Navigate(new TitleScreen(Shell))));
        body.Margin = new Thickness(72, 48);
        body.MaxWidth = 980;
        body.HorizontalAlignment = HorizontalAlignment.Left;
        Root.Children.Add(body);
    }

    protected override bool Back()
    {
        Shell.Navigate(new TitleScreen(Shell));
        return true;
    }
}

/// <summary>Settings: language, audio, graphics, comfort.</summary>
public sealed class SettingsScreen : Screen
{
    public SettingsScreen(MainWindow shell) : base(shell)
    {
        var s = shell.Profile.Settings;
        var body = new StackPanel { Spacing = 20 };
        body.Children.Add(Header(Strings.Get("menu.settings"), Strings.Get("settings.title")));

        body.Children.Add(Row("settings.language", UiKit.Row(10, Strings.AvailableLanguages
            .Select(l => (Control)UiKit.Chip(Localization.NativeName(l), () => { s.Language = l; Apply(); }, s.Language == l)).ToArray())));
        body.Children.Add(Row("settings.master", Volume(s.MasterVolume, v => s.MasterVolume = v)));
        body.Children.Add(Row("settings.music", Volume(s.MusicVolume, v => s.MusicVolume = v)));
        body.Children.Add(Row("settings.sfx", Volume(s.SfxVolume, v => s.SfxVolume = v)));
        body.Children.Add(Row("settings.quality", UiKit.Row(10, Enumerable.Range(0, 4).Select(q => (Control)UiKit.Chip(Strings.Get($"q.{q}"), () => { s.GraphicsQuality = q; Apply(); }, s.GraphicsQuality == q)).ToArray())));
        body.Children.Add(Row("settings.shake", Toggle(s.ScreenShake, v => s.ScreenShake = v)));
        body.Children.Add(Row("settings.fullscreen", Toggle(s.Fullscreen, v => s.Fullscreen = v)));
        body.Children.Add(Row("settings.fps", Toggle(s.ShowFps, v => s.ShowFps = v)));
        body.Children.Add(BackButton(() => Shell.Navigate(new TitleScreen(Shell))));
        body.Margin = new Thickness(72, 48);
        body.MaxWidth = 900;
        body.HorizontalAlignment = HorizontalAlignment.Left;
        Root.Children.Add(body);
    }

    private void Apply()
    {
        Shell.ApplySettings();
        Shell.SaveProfile();
        Shell.Navigate(new SettingsScreen(Shell));
    }

    private static Control Row(string key, Control control)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("260,*") };
        var label = UiKit.Text(Strings.Get(key), 22, Palette.KapurBrush, FontWeight.SemiBold);
        label.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        grid.Children.Add(label);
        grid.Children.Add(control);
        return grid;
    }

    private Control Volume(float value, Action<float> set)
    {
        var slider = new Slider { Minimum = 0, Maximum = 1, Value = value, Width = 360, HorizontalAlignment = HorizontalAlignment.Left };
        slider.ValueChanged += (_, e) =>
        {
            set((float)e.NewValue);
            Shell.ApplySettings();
        };
        slider.LostFocus += (_, _) => Shell.SaveProfile();
        return slider;
    }

    private Control Toggle(bool value, Action<bool> set) => UiKit.Row(10,
        UiKit.Chip(Strings.Get("settings.on"), () => { set(true); Apply(); }, value),
        UiKit.Chip(Strings.Get("settings.off"), () => { set(false); Apply(); }, !value));

    protected override bool Back()
    {
        Shell.SaveProfile();
        Shell.Navigate(new TitleScreen(Shell));
        return true;
    }
}

/// <summary>Credits, with the studio credit front and centre.</summary>
public sealed class CreditsScreen : Screen
{
    public CreditsScreen(MainWindow shell) : base(shell)
    {
        var made = new SablonText(Strings.Get("credit"), 34) { Misregister = 3 };
        var body = UiKit.Stack(24,
            Header(Strings.Get("menu.credits"), "SI JENGGO"),
            made,
            UiKit.Board(UiKit.Text(Strings.Get("credits.body"), 22)),
            BackButton(() => Shell.Navigate(new TitleScreen(Shell))));
        body.Margin = new Thickness(72, 48);
        body.MaxWidth = 960;
        body.HorizontalAlignment = HorizontalAlignment.Left;
        Root.Children.Add(body);
    }

    protected override bool Back()
    {
        Shell.Navigate(new TitleScreen(Shell));
        return true;
    }
}

/// <summary>Per-character progression: level, skill tree, gear and regional style.</summary>
public sealed class ProgressionScreen : Screen
{
    private CharacterId _character;
    private readonly ContentControl _detail = new();

    public ProgressionScreen(MainWindow shell, CharacterId character = CharacterId.Jenggo) : base(shell)
    {
        _character = character;
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        foreach (var def in Characters.All)
        {
            var chip = UiKit.Chip(Strings.T(def.Name), () => Shell.Navigate(new ProgressionScreen(Shell, def.Id)), def.Id == character);
            chip.IsEnabled = shell.Profile.IsUnlocked(def.Id);
            tabs.Children.Add(chip);
        }

        var body = new StackPanel { Spacing = 18, Margin = new Thickness(72, 40, 72, 24) };
        body.Children.Add(Header(Strings.Get("menu.progress"), Strings.T(Characters.Get(character).Name)));
        body.Children.Add(tabs);
        body.Children.Add(_detail);
        body.Children.Add(BackButton(() => Shell.Navigate(new TitleScreen(Shell))));
        Root.Children.Add(new ScrollViewer { Content = body });
        Refresh();
    }

    private void Refresh()
    {
        var progress = Shell.Profile.For(_character);
        int level = progress.Level;
        int next = Leveling.XpForLevel(level + 1);
        int cur = Leveling.XpForLevel(level);
        var xpBar = new SkewBar { Height = 14, Width = 420, Value = next > cur ? (progress.Xp - cur) / (double)(next - cur) : 1, Fill = Palette.KunyitBrush };
        var summary = UiKit.Row(28,
            new Image { Source = SetupScreen.Portrait(_character), Width = 120, Height = 140, Stretch = Stretch.UniformToFill },
            UiKit.Stack(6,
                new SablonText($"{Strings.Get("progress.level")} {level}", 40),
                xpBar,
                UiKit.Text($"XP {progress.Xp:N0} / {next:N0}   ·   {Strings.Get("progress.points")}: {progress.SkillPointsAvailable}", 20, Palette.KapurDim)));

        // Skill tree: five branches as columns, tiers bottom to top.
        var tree = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*"), ColumnSpacing = 12 };
        foreach (var branch in Enum.GetValues<SkillBranch>())
        {
            var col = new StackPanel { Spacing = 8 };
            col.Children.Add(UiKit.Eyebrow(Strings.Get($"stat.{branch.ToString().ToLowerInvariant()}") is var label && !label.StartsWith("stat.", StringComparison.Ordinal) ? label : branch.ToString()));
            foreach (var node in SkillTree.All.Where(n => n.Branch == branch).OrderBy(n => n.Tier))
            {
                bool owned = progress.Skills.Contains(node.Key);
                bool can = progress.CanUnlock(node);
                var content = UiKit.Stack(2,
                    UiKit.Text(Strings.T(node.Name), 19, owned ? Palette.AspalBrush : Palette.KapurBrush, FontWeight.Bold),
                    UiKit.Text(Strings.T(node.Effect) + (owned ? string.Empty : $"  ·  {Strings.Get("progress.cost")} {node.Cost}"), 15, owned ? Palette.AspalBrush : Palette.KapurDim));
                var b = new Button { Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, IsEnabled = owned || can };
                b.Classes.Add("chip");
                if (owned)
                {
                    b.Classes.Add("selected");
                }

                b.Click += (_, _) =>
                {
                    if (progress.Unlock(node))
                    {
                        Shell.Audio.Play(Sfx.Pickup, 0.9f, 0f);
                        Shell.SaveProfile();
                        Refresh();
                    }
                };
                col.Children.Add(b);
            }

            Grid.SetColumn(col, (int)branch);
            tree.Children.Add(col);
        }

        // Gear slots.
        var gear = new StackPanel { Spacing = 10 };
        foreach (var slot in Enum.GetValues<EquipmentSlot>())
        {
            var row = new WrapPanel();
            row.Children.Add(new TextBlock { Text = slot.ToString(), Width = 130, FontSize = 19, FontWeight = FontWeight.SemiBold, Foreground = Palette.KunyitBrush, VerticalAlignment = VerticalAlignment.Center });
            foreach (var item in Equipment.All.Skip(1).Where(e => e.Slot == slot && Shell.Profile.Inventory.Contains(e.Id)))
            {
                bool on = progress.Equipped.Contains(item.Id);
                var chip = UiKit.Chip(Strings.T(item.Name), () =>
                {
                    progress.Equip(item.Id);
                    Shell.SaveProfile();
                    Refresh();
                }, on);
                ToolTip.SetTip(chip, Strings.T(item.Description));
                chip.Margin = new Thickness(0, 0, 8, 8);
                row.Children.Add(chip);
            }

            gear.Children.Add(row);
        }

        var jurusRow = new WrapPanel();
        if (Shell.Profile.LearnedJurus.Count == 0)
        {
            jurusRow.Children.Add(UiKit.Text(Strings.Get("progress.none"), 19, Palette.KapurDim));
        }

        foreach (var j in Shell.Profile.LearnedJurus.OrderBy(m => m.Value))
        {
            var chip = UiKit.Chip(Strings.T(j.Def.Name), () =>
            {
                progress.EquippedJurus = j;
                Shell.SaveProfile();
                Refresh();
            }, progress.EquippedJurus == j);
            chip.Margin = new Thickness(0, 0, 8, 8);
            jurusRow.Children.Add(chip);
        }

        _detail.Content = UiKit.Stack(20,
            summary,
            UiKit.Board(UiKit.Stack(12, UiKit.Eyebrow(Strings.Get("progress.tree")), tree)),
            new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,*"),
                ColumnSpacing = 18,
                Children =
                {
                    UiKit.Board(UiKit.Stack(12, UiKit.Eyebrow(Strings.Get("progress.gear")), gear)),
                    WithColumn(UiKit.Board(UiKit.Stack(12, UiKit.Eyebrow(Strings.Get("progress.jurus")), jurusRow)), 1),
                },
            });
    }

    private static Control WithColumn(Control c, int column)
    {
        Grid.SetColumn(c, column);
        return c;
    }

    protected override bool Back()
    {
        Shell.Navigate(new TitleScreen(Shell));
        return true;
    }
}

/// <summary>Host or join an online co-op match.</summary>
public sealed class OnlineScreen : Screen
{
    private readonly TextBlock _status;
    private readonly TextBox _address = new() { Text = "127.0.0.1", Width = 260 };
    private readonly TextBox _port = new() { Text = OnlineLink.DefaultPort.ToString(System.Globalization.CultureInfo.InvariantCulture), Width = 120 };
    private readonly StackPanel _hostPanel = new() { Spacing = 12, IsVisible = false };
    private OnlineLink? _link;
    private DispatcherTimer? _pump;
    private CharacterId _character = CharacterId.Jenggo;
    private NetMode _netMode = NetMode.Lockstep;

    public OnlineScreen(MainWindow shell) : base(shell)
    {
        _status = UiKit.Text(Strings.Get("online.note"), 20, Palette.KapurDim);
        _status.MaxWidth = 820;

        var chars = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        foreach (var def in Characters.All)
        {
            var chip = UiKit.Chip(Strings.T(def.Name), () => { _character = def.Id; }, def.Id == _character);
            chip.IsEnabled = shell.Profile.IsUnlocked(def.Id);
            chip.Click += (_, _) =>
            {
                foreach (var c in chars.Children.OfType<Button>())
                {
                    c.Classes.Remove("selected");
                }

                chip.Classes.Add("selected");
            };
            chars.Children.Add(chip);
        }

        // Netcode: the host's choice travels in the match start, so a joining player's pick is only a default.
        var net = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        foreach (var mode in new[] { NetMode.Lockstep, NetMode.Rollback })
        {
            var chip = UiKit.Chip(Strings.Get(mode == NetMode.Lockstep ? "online.lockstep" : "online.rollback"), () =>
            {
                _netMode = mode;
                if (_link is not null)
                {
                    _link.NetMode = mode;
                }
            }, mode == _netMode);
            chip.Click += (_, _) =>
            {
                foreach (var c in net.Children.OfType<Button>())
                {
                    c.Classes.Remove("selected");
                }

                chip.Classes.Add("selected");
            };
            net.Children.Add(chip);
        }

        var hostButton = UiKit.Sticker(Strings.Get("online.host"), Host, 30, -1.2);
        var joinButton = UiKit.Sticker(Strings.Get("online.join"), Join, 30, 1);
        var addressRow = UiKit.Row(12, UiKit.Text(Strings.Get("online.address"), 20), _address, UiKit.Text(Strings.Get("online.port"), 20), _port);
        foreach (var mode in new[] { GameModeKind.Story, GameModeKind.Arcade, GameModeKind.Survival, GameModeKind.BossRush })
        {
            _hostPanel.Children.Add(UiKit.Chip(Strings.Get($"menu.{mode.ToString().ToLowerInvariant()}"), () => StartMatch(mode)));
        }

        var body = UiKit.Stack(20,
            Header(Strings.Get("menu.online"), Strings.Get("online.title")),
            _status,
            UiKit.Stack(8, UiKit.Eyebrow(Strings.Get("setup.p1")), chars),
            UiKit.Stack(8, UiKit.Eyebrow(Strings.Get("online.netcode")), net),
            addressRow,
            UiKit.Row(24, hostButton, joinButton),
            _hostPanel,
            BackButton(Leave));
        body.Margin = new Thickness(72, 48);
        body.HorizontalAlignment = HorizontalAlignment.Left;
        Root.Children.Add(body);
    }

    private int Port => int.TryParse(_port.Text, out int p) ? p : OnlineLink.DefaultPort;

    private void Host()
    {
        try
        {
            _link?.Dispose();
            _link = OnlineLink.Host(Port);
            _link.NetMode = _netMode;
            _status.Text = Strings.Format("online.waiting", _link.Port);
            _link.PeerJoined += () => Dispatcher.UIThread.Post(() =>
            {
                _status.Text = Strings.Get("online.connected");
                _hostPanel.IsVisible = true;
            });
            Wire();
        }
        catch (Exception ex) when (ex is ThreeNet.ThreeNetException or System.Net.Sockets.SocketException)
        {
            _status.Text = Strings.Format("online.failed", ex.Message);
        }
    }

    private void Join()
    {
        try
        {
            _link?.Dispose();
            _link = OnlineLink.Join(_address.Text ?? "127.0.0.1", Port);
            _link.NetMode = _netMode;
            _status.Text = Strings.Format("online.connecting", _address.Text ?? string.Empty);
            _link.PeerJoined += () => Dispatcher.UIThread.Post(() =>
            {
                _status.Text = Strings.Get("online.connected");
                // Through a dedicated relay, player one picks the mode just like a host would.
                _hostPanel.IsVisible = _link?.ViaRelay == true && _link.IsHost && _link.HasPeer;
            });
            _link.MatchStarted += start => Dispatcher.UIThread.Post(() => Launch(start));
            Wire();
        }
        catch (Exception ex) when (ex is ThreeNet.ThreeNetException or System.Net.Sockets.SocketException)
        {
            _status.Text = Strings.Format("online.failed", ex.Message);
        }
    }

    private void Wire()
    {
        _link!.Lost += reason => Dispatcher.UIThread.Post(() => _status.Text = Strings.Format("online.failed", reason));
        _pump?.Stop();
        _pump = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _pump.Tick += (_, _) => _link?.Update();
        _pump.Start();
    }

    private void StartMatch(GameModeKind mode)
    {
        if (_link is null || !_link.HasPeer)
        {
            return;
        }

        // Partner character: the second unlocked fighter different from ours.
        var partner = Characters.All.Select(c => c.Id).First(id => id != _character);
        var start = new MatchStart((ulong)Environment.TickCount64, mode, 0, Shell.Profile.Settings.Difficulty, [_character, partner]);
        _link.SendStart(start);
        Launch(start);
    }

    private void Launch(MatchStart start)
    {
        _pump?.Stop();
        var players = start.Characters.Select(c => new PlayerSlot(c, false)).ToList();
        // Online sessions use base stats on both sides so the simulations match exactly.
        var mode = start.Mode == GameModeKind.Story ? GameModeKind.Arcade : start.Mode;
        var campaign = new Campaign(mode, new Profile(), players, start.Difficulty, start.Chapter, seed: start.Seed);
        var link = _link;
        _link = null;
        Shell.Navigate(new GameScreen(Shell, campaign, link));
    }

    private void Leave()
    {
        _pump?.Stop();
        _link?.Dispose();
        _link = null;
        Shell.Navigate(new TitleScreen(Shell));
    }

    protected override bool Back()
    {
        Leave();
        return true;
    }
}

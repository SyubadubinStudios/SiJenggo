using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SiJenggo.App.Ui;
using SiJenggo.Game;
using SiJenggo.Game.Data;

namespace SiJenggo.App.Screens;

/// <summary>
/// Rolling credits, run like a sheet through a screen-printing press: every display line is printed with
/// its red and teal plates off register, and the plates only snap into register as the line crosses the
/// middle of the screen, then drift apart again on the way out. The roll ends on the studio credit, held
/// in register. Hold Space / J / A to fast-forward; Esc / B goes back.
/// </summary>
public sealed class CreditsScreen : Screen
{
    private const double Speed = 58;          // px per second
    private const double FastSpeed = 360;
    private const double MaxMisregister = 11;

    private readonly Canvas _viewport = new() { ClipToBounds = true };
    private readonly StackPanel _roll = new() { Spacing = 0, Width = 1120 };
    private readonly TranslateTransform _shift = new();
    private readonly List<SablonText> _plates = [];
    private readonly Control _hint;
    private readonly Control _back;
    private double _offset = double.NaN;
    private bool _running;
    private bool _finished;
    private TimeSpan? _lastFrame;

    public CreditsScreen(MainWindow shell) : base(shell)
    {
        // Calm the key art behind the roll: dark asphalt from the centre outwards.
        Root.Children.Add(new Border
        {
            Background = new RadialGradientBrush
            {
                GradientStops = { new GradientStop(Color.Parse("#F216130F"), 0.35), new GradientStop(Color.Parse("#B316130F"), 1) },
                RadiusX = new RelativeScalar(0.75, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(0.9, RelativeUnit.Relative),
            },
        });

        _roll.RenderTransform = _shift;
        _roll.HorizontalAlignment = HorizontalAlignment.Center;
        Build();
        _viewport.Children.Add(_roll);

        // Lines fade in from the bottom edge and out at the top, like ink leaving the screen mesh.
        var frame = new Border
        {
            Child = _viewport,
            OpacityMask = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Colors.Transparent, 0), new GradientStop(Colors.Black, 0.16),
                    new GradientStop(Colors.Black, 0.84), new GradientStop(Colors.Transparent, 1),
                },
            },
        };
        Root.Children.Add(frame);
        _viewport.SizeChanged += (_, _) => Center();

        _hint = UiKit.Text(Strings.Get("credits.hint"), 17, Palette.KapurDim, FontWeight.SemiBold);
        _hint.HorizontalAlignment = HorizontalAlignment.Right;
        _hint.VerticalAlignment = VerticalAlignment.Bottom;
        _hint.Margin = new Thickness(0, 0, 44, 30);
        _hint.IsHitTestVisible = false;
        Root.Children.Add(_hint);

        _back = BackButton(Leave);
        _back.HorizontalAlignment = HorizontalAlignment.Left;
        _back.VerticalAlignment = VerticalAlignment.Bottom;
        _back.Margin = new Thickness(56, 0, 0, 24);
        Root.Children.Add(_back);
    }

    // ───────────────────────────── content ─────────────────────────────

    private void Build()
    {
        // Opening: the title card, already on screen when the roll starts.
        Plate("SI JENGGO", 92, 5, Palette.KapurBrush);
        Line(Strings.Get("credits.subtitle"), 30, Palette.KunyitBrush, FontWeight.Bold, spacing: 4);
        Gap(170);

        Section(Strings.Get("credits.made"));
        Plate("ARIANA MISCHA FADHILA", 58, 4, Palette.KapurBrush);
        Line(Strings.Get("credits.roles"), 22, Palette.KapurDim);
        Gap(40);
        Section(Strings.Get("credits.studio"));
        Plate("SYUBADUBIN STUDIOS", 50, 4, Palette.KunyitBrush);
        Gap(150);

        Section(Strings.Get("credits.cast"));
        Gap(10);
        foreach (var c in Characters.All)
        {
            Fighter(c);
        }

        Gap(110);
        Section(Strings.Get("credits.bosses"));
        Gap(8);
        for (int i = 0; i < Bosses.All.Length; i++)
        {
            var b = Bosses.All[i];
            var chapter = UiKit.Text(Strings.T(Chapters.Get(i).Subtitle).Split('—')[0].Trim().ToUpperInvariant(), 15, Palette.CabaiBrush, FontWeight.Bold);
            chapter.LetterSpacing = 3;
            chapter.HorizontalAlignment = HorizontalAlignment.Center;
            _roll.Children.Add(chapter);
            Plate(Strings.T(b.Name).ToUpperInvariant(), 36, 3, Palette.KapurBrush);
            Line(Strings.T(b.Title), 20, Palette.KapurDim);
            Gap(26);
        }

        Gap(110);
        Section(Strings.Get("credits.tech"));
        Credit(Strings.Get("credits.engine"), "ThreeNet — Gravicode Studios");
        Credit(Strings.Get("credits.ui"), "Avalonia UI");
        Credit(Strings.Get("credits.runtime"), ".NET 10");

        Gap(90);
        Section(Strings.Get("credits.assets"));
        Credit(Strings.Get("credits.models"), "Rodin");
        Credit(Strings.Get("credits.rig"), "Blender");
        Credit(Strings.Get("credits.voices"), "ElevenLabs v3");
        Credit(Strings.Get("credits.music"), Strings.Get("credits.music.how"));

        Gap(90);
        Section(Strings.Get("credits.type"));
        Credit("Bungee", "David Jonathan Ross");
        Credit("Barlow Condensed", "Jeremy Tribby");
        Line("SIL Open Font License", 18, Palette.KapurDim);

        Gap(90);
        Section(Strings.Get("credits.languages"));
        Line("Bahasa Indonesia  ·  English  ·  Bahasa Melayu", 26, Palette.KapurBrush, FontWeight.SemiBold);

        Gap(150);
        Line(Strings.Get("credits.thanks"), 26, Palette.KapurBrush, FontWeight.Medium);
        Gap(160);

        // Closing: a truck-tailgate motto, then the studio credit, which the roll stops on.
        var motto = Plate(Strings.Get("credits.motto").ToUpperInvariant(), 44, 4, Palette.KunyitBrush);
        motto.RenderTransform = new RotateTransform(-3);
        Gap(200);
        var final = new SablonText(Strings.Get("credit"), 26) { Misregister = MaxMisregister, Ink = Palette.KapurBrush, HorizontalAlignment = HorizontalAlignment.Center };
        _plates.Add(final);
        _roll.Children.Add(final);
        Gap(40);
    }

    private SablonText Plate(string text, double size, double misregister, IBrush ink)
    {
        var plate = new SablonText(text, size) { Misregister = misregister, Ink = ink, HorizontalAlignment = HorizontalAlignment.Center };
        _plates.Add(plate);
        _roll.Children.Add(plate);
        return plate;
    }

    private void Section(string text)
    {
        var eyebrow = UiKit.Eyebrow(text.ToUpperInvariant());
        eyebrow.HorizontalAlignment = HorizontalAlignment.Center;
        eyebrow.Margin = new Thickness(0, 0, 0, 12);
        _roll.Children.Add(eyebrow);
    }

    private void Line(string text, double size, IBrush brush, FontWeight weight = FontWeight.Medium, double spacing = 0)
    {
        var line = UiKit.Text(text, size, brush, weight);
        line.HorizontalAlignment = HorizontalAlignment.Center;
        line.TextAlignment = TextAlignment.Center;
        line.TextWrapping = TextWrapping.Wrap;
        line.MaxWidth = 900;
        line.LetterSpacing = spacing;
        _roll.Children.Add(line);
    }

    /// <summary>Role on the left, name on the right, meeting at the centre line.</summary>
    private void Credit(string role, string name)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,36,*"), Margin = new Thickness(0, 6) };
        var r = UiKit.Text(role, 22, Palette.KapurDim, FontWeight.SemiBold);
        r.HorizontalAlignment = HorizontalAlignment.Right;
        r.TextAlignment = TextAlignment.Right;
        var n = UiKit.Text(name, 24, Palette.KapurBrush, FontWeight.Bold);
        Grid.SetColumn(n, 2);
        grid.Children.Add(r);
        grid.Children.Add(n);
        _roll.Children.Add(grid);
    }

    /// <summary>A fighter: the portrait as a tilted sticker beside the name and tagline, alternating sides.</summary>
    private void Fighter(CharacterDef c)
    {
        bool flip = (int)c.Id % 2 == 1;
        var portrait = new Border
        {
            Width = 150,
            Height = 176,
            BorderBrush = flip ? Palette.ToskaBrush : Palette.KunyitBrush,
            BorderThickness = new Thickness(4),
            ClipToBounds = true,
            RenderTransform = new RotateTransform(flip ? 4 : -4),
            Child = new Image { Source = Portrait(c.Id), Stretch = Stretch.UniformToFill },
        };
        var name = new SablonText(Strings.T(c.Name).ToUpperInvariant(), 44) { Misregister = MaxMisregister };
        _plates.Add(name);
        var tagline = UiKit.Text(Strings.T(c.Tagline), 22, Palette.KapurDim, FontWeight.SemiBold);
        var words = UiKit.Stack(4, name, tagline);
        words.VerticalAlignment = VerticalAlignment.Center;
        words.HorizontalAlignment = flip ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        foreach (var child in words.Children)
        {
            child.HorizontalAlignment = words.HorizontalAlignment;
        }

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions(flip ? "*,32,Auto" : "Auto,32,*"), Margin = new Thickness(60, 22) };
        Grid.SetColumn(portrait, flip ? 2 : 0);
        Grid.SetColumn(words, flip ? 0 : 2);
        row.Children.Add(portrait);
        row.Children.Add(words);
        _roll.Children.Add(row);
    }

    private static Bitmap Portrait(CharacterId id)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://SiJenggo/Assets/Art/portrait_{id.ToString().ToLowerInvariant()}.png"));
        return new Bitmap(stream);
    }

    private void Gap(double height) => _roll.Children.Add(new Border { Height = height });

    // ───────────────────────────── the roll ─────────────────────────────

    public override void OnShown()
    {
        Shell.Audio.PlayMusic(MusicStyle.GamelanMetal);
        _running = true;
        _lastFrame = null;
        TopLevel.GetTopLevel(Shell)?.RequestAnimationFrame(Tick);
    }

    public override void OnHidden()
    {
        _running = false;
        Shell.Audio.StopMusic();
    }

    /// <summary>First layout: the title card starts a third of the way down, so the screen is never empty.</summary>
    private void Center()
    {
        Canvas.SetLeft(_roll, Math.Max(0, (_viewport.Bounds.Width - _roll.Width) / 2));
        if (double.IsNaN(_offset) && _viewport.Bounds.Height > 0)
        {
            _offset = -_viewport.Bounds.Height * 0.3;
            Apply();
        }
    }

    private void Tick(TimeSpan now)
    {
        if (!_running)
        {
            return;
        }

        float dt = _lastFrame is { } last ? (float)Math.Clamp((now - last).TotalSeconds, 0, 0.1) : 0f;
        _lastFrame = now;
        if (!double.IsNaN(_offset) && !_finished)
        {
            bool fast = (Shell.Input.Read(0, true) & (InputButtons.Jump | InputButtons.Light)) != 0;
            _offset += (fast ? FastSpeed : Speed) * dt;

            // Stop with the studio credit on the centre line.
            var final = _plates[^1];
            double finalCentre = MidY(final);
            double stop = finalCentre - _viewport.Bounds.Height / 2;
            if (final.Bounds.Height > 0 && _offset >= stop)
            {
                _offset = stop;
                _finished = true;
                _hint.IsVisible = false;
                _back.Focus();
            }

            Apply();
        }

        Shell.Audio.UpdateMix(MusicState.Victory, 0.6f, dt, false);
        TopLevel.GetTopLevel(Shell)?.RequestAnimationFrame(Tick);
    }

    private void Apply()
    {
        _shift.Y = -_offset;
        // The plates register exactly on the centre line and drift apart with distance from it.
        double centre = _viewport.Bounds.Height / 2 + _offset;
        double reach = _viewport.Bounds.Height * 0.45;
        foreach (var plate in _plates)
        {
            double mid = MidY(plate);
            double k = Math.Clamp(Math.Abs(mid - centre) / reach, 0, 1);
            plate.Misregister = 0.6 + (MaxMisregister - 0.6) * k * k;
        }
    }

    /// <summary>Vertical centre of a plate in roll coordinates (fighter names sit inside nested panels).</summary>
    private double MidY(Control c) => c.TranslatePoint(new Point(0, c.Bounds.Height / 2), _roll)?.Y ?? 0;

    /// <summary>Used by the screenshot tour: jump the roll forward by some pixels.</summary>
    internal void AdvanceForCapture(double pixels)
    {
        if (!double.IsNaN(_offset) && !_finished)
        {
            _offset += pixels;
            Apply();
        }
    }

    private void Leave() => Shell.Navigate(new TitleScreen(Shell));

    protected override bool Back()
    {
        Leave();
        return true;
    }
}

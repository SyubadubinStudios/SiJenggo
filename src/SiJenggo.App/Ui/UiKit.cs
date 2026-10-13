using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SiJenggo.App.Ui;

/// <summary>Palette shared by code-built UI (mirrors Styles/Theme.axaml).</summary>
public static class Palette
{
    public static readonly Color Aspal = Color.Parse("#16130F");
    public static readonly Color Kunyit = Color.Parse("#F2B705");
    public static readonly Color Cabai = Color.Parse("#D7261E");
    public static readonly Color Toska = Color.Parse("#1FA39A");
    public static readonly Color Kapur = Color.Parse("#EFE6D2");
    public static readonly Color Soga = Color.Parse("#6B3E26");

    public static readonly IBrush AspalBrush = new SolidColorBrush(Aspal).ToImmutable();
    public static readonly IBrush KunyitBrush = new SolidColorBrush(Kunyit).ToImmutable();
    public static readonly IBrush CabaiBrush = new SolidColorBrush(Cabai).ToImmutable();
    public static readonly IBrush ToskaBrush = new SolidColorBrush(Toska).ToImmutable();
    public static readonly IBrush KapurBrush = new SolidColorBrush(Kapur).ToImmutable();
    public static readonly IBrush KapurDim = new SolidColorBrush(Color.Parse("#99EFE6D2")).ToImmutable();
    public static readonly IBrush SogaBrush = new SolidColorBrush(Soga).ToImmutable();
    public static readonly IBrush Shade = new SolidColorBrush(Color.Parse("#CC16130F")).ToImmutable();

    public static FontFamily Display => (FontFamily)Application.Current!.FindResource("DisplayFont")!;
    public static FontFamily Body => (FontFamily)Application.Current!.FindResource("BodyFont")!;
}

/// <summary>
/// The signature element: display type printed in three plates — chili red and market teal slightly
/// off register behind the lime-wash white — like a cheap screen-printed angkot sticker.
/// <see cref="Misregister"/> animates the offset (menus widen it on focus).
/// </summary>
public sealed class SablonText : Grid
{
    private readonly TextBlock _red = new();
    private readonly TextBlock _teal = new();
    private readonly TextBlock _top = new();
    private double _misregister = 3;

    public SablonText(string text = "", double size = 48)
    {
        IsHitTestVisible = false;
        foreach (var tb in new[] { _red, _teal, _top })
        {
            tb.FontFamily = Palette.Display;
            tb.FontSize = size;
            tb.Text = text;
            Children.Add(tb);
        }

        _red.Foreground = Palette.CabaiBrush;
        _teal.Foreground = Palette.ToskaBrush;
        _top.Foreground = Palette.KapurBrush;
        Misregister = 3;
    }

    public string Text
    {
        get => _top.Text ?? string.Empty;
        set => _red.Text = _teal.Text = _top.Text = value;
    }

    public IBrush Ink
    {
        get => _top.Foreground!;
        set => _top.Foreground = value;
    }

    public double FontSize
    {
        set => _red.FontSize = _teal.FontSize = _top.FontSize = value;
    }

    public double Misregister
    {
        get => _misregister;
        set
        {
            _misregister = value;
            _red.RenderTransform = new TranslateTransform(-value, -value * 0.6);
            _teal.RenderTransform = new TranslateTransform(value, value * 0.6);
        }
    }

    public TextAlignment TextAlignment
    {
        set => _red.TextAlignment = _teal.TextAlignment = _top.TextAlignment = value;
    }
}

/// <summary>
/// A slanted health/meter bar drawn directly (no templates, cheap to redraw every frame).
/// A pale "ghost" segment trails behind damage so big hits read clearly.
/// </summary>
public sealed class SkewBar : Control
{
    private double _value = 1, _ghost = 1;

    public IBrush Fill { get; set; } = Palette.KunyitBrush;
    public IBrush Track { get; set; } = new SolidColorBrush(Color.Parse("#55000000")).ToImmutable();
    public IBrush GhostBrush { get; set; } = new SolidColorBrush(Color.Parse("#CCEFE6D2")).ToImmutable();
    public double Slant { get; set; } = 10;
    public bool Glow { get; set; }

    /// <summary>0..1.</summary>
    public double Value
    {
        get => _value;
        set
        {
            value = Math.Clamp(value, 0, 1);
            if (Math.Abs(value - _value) < 0.0005)
            {
                return;
            }

            if (value > _ghost)
            {
                _ghost = value;
            }

            _value = value;
            InvalidateVisual();
        }
    }

    /// <summary>Call once per frame to let the ghost segment drain.</summary>
    public void Tick(double dt)
    {
        if (_ghost > _value)
        {
            _ghost = Math.Max(_value, _ghost - dt * 0.6);
            InvalidateVisual();
        }
    }

    private StreamGeometry Shape(double fraction)
    {
        double w = Bounds.Width, h = Bounds.Height;
        double x1 = Slant + (w - Slant) * fraction;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(Slant, 0), true);
            ctx.LineTo(new Point(Math.Max(Slant, x1), 0));
            ctx.LineTo(new Point(Math.Max(0, x1 - Slant), h));
            ctx.LineTo(new Point(0, h));
            ctx.EndFigure(true);
        }

        return g;
    }

    public override void Render(DrawingContext context)
    {
        context.DrawGeometry(Track, null, Shape(1));
        if (_ghost > _value)
        {
            context.DrawGeometry(GhostBrush, null, Shape(_ghost));
        }

        if (_value > 0)
        {
            context.DrawGeometry(Fill, Glow ? new Pen(Palette.KapurBrush, 1.5) : null, Shape(_value));
        }
    }
}

public static class UiKit
{
    public static TextBlock Text(string text, double size = 20, IBrush? brush = null, FontWeight weight = FontWeight.Medium, bool display = false, string? classes = null)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight,
            Foreground = brush ?? Palette.KapurBrush,
            FontFamily = display ? Palette.Display : Palette.Body,
            TextWrapping = TextWrapping.Wrap,
        };
        if (classes is not null)
        {
            tb.Classes.Add(classes);
        }

        return tb;
    }

    public static TextBlock Eyebrow(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = 15,
        FontWeight = FontWeight.SemiBold,
        LetterSpacing = 2.5,
        Foreground = Palette.KunyitBrush,
        FontFamily = Palette.Body,
    };

    /// <summary>A tilted sticker menu item; its print plates spread when it has focus.</summary>
    public static Button Sticker(string text, Action onClick, double size = 34, double tilt = -1.5)
    {
        var label = new SablonText(text, size) { Misregister = 2 };
        var b = new Button { Content = label, RenderTransform = new RotateTransform(tilt) };
        b.Classes.Add("sticker");
        b.Click += (_, _) => onClick();
        void Hot(bool on)
        {
            label.Misregister = on ? 5 : 2;
            label.Ink = on ? Palette.AspalBrush : Palette.KapurBrush;
        }

        b.GotFocus += (_, _) => Hot(true);
        b.LostFocus += (_, _) => Hot(b.IsPointerOver);
        b.PointerEntered += (_, _) => Hot(true);
        b.PointerExited += (_, _) => Hot(b.IsFocused);
        return b;
    }

    public static Button Chip(string text, Action onClick, bool selected = false)
    {
        var b = new Button { Content = text };
        b.Classes.Add("chip");
        if (selected)
        {
            b.Classes.Add("selected");
        }

        b.Click += (_, _) => onClick();
        return b;
    }

    public static Border Board(Control child, Thickness? padding = null) => new()
    {
        Child = child,
        Classes = { "board" },
        Padding = padding ?? new Thickness(22),
    };

    public static StackPanel Stack(double spacing, params Control[] children)
    {
        var s = new StackPanel { Spacing = spacing };
        foreach (var c in children)
        {
            s.Children.Add(c);
        }

        return s;
    }

    public static StackPanel Row(double spacing, params Control[] children)
    {
        var s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
        foreach (var c in children)
        {
            s.Children.Add(c);
        }

        return s;
    }

    /// <summary>Five-pip rating (character select stats).</summary>
    public static Control Pips(int value, IBrush fill)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        for (int i = 0; i < 5; i++)
        {
            row.Children.Add(new Border
            {
                Width = 18,
                Height = 8,
                Background = i < value ? fill : new SolidColorBrush(Color.Parse("#33EFE6D2")),
                RenderTransform = new SkewTransform(-20, 0),
            });
        }

        return row;
    }
}

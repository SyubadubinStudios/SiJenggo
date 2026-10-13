using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using SiJenggo.App.Ui;
using SiJenggo.Presentation.Audio;

namespace SiJenggo.App.Screens;

/// <summary>Base for every screen: layout root, toast, keyboard/gamepad menu navigation, back handling.</summary>
public abstract class Screen : UserControl
{
    private readonly Border _toast;
    private readonly TextBlock _toastText;
    private DispatcherTimer? _toastTimer;

    protected Screen(MainWindow shell)
    {
        Shell = shell;
        Root = new Grid();
        _toastText = UiKit.Text(string.Empty, 20, Palette.AspalBrush, Avalonia.Media.FontWeight.SemiBold);
        _toast = new Border
        {
            Background = Palette.KunyitBrush,
            Padding = new Thickness(18, 8),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 48),
            Child = _toastText,
            IsVisible = false,
            RenderTransform = new Avalonia.Media.RotateTransform(-1),
        };
        Content = new Grid { Children = { Root, _toast } };
    }

    protected MainWindow Shell { get; }
    protected Grid Root { get; }

    /// <summary>True for the gameplay screen: the 3D viewport renders instead of the key art.</summary>
    public virtual bool Uses3D => false;

    /// <summary>True while a menu overlay (pause, results) takes the gamepad.</summary>
    public virtual bool ModalOpen => false;

    public virtual void OnShown()
    {
    }

    public virtual void OnHidden()
    {
    }

    public virtual void OnFrame(float dt)
    {
    }

    /// <summary>Keyboard shortcut handling; return true when handled.</summary>
    public virtual bool OnKey(Key key)
    {
        if (key == Key.Escape && Back())
        {
            return true;
        }

        if (key is Key.Up or Key.Down or Key.Left or Key.Right)
        {
            Shell.Audio.Play(Sfx.UiMove, 0.6f, 0f);
        }

        return false;
    }

    /// <summary>Gamepad navigation arrives as synthetic keys.</summary>
    public virtual void OnMenuKey(Key key)
    {
        if (OnKey(key))
        {
            return;
        }

        var focus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        switch (key)
        {
            case Key.Enter when focus is Button b:
                Shell.Audio.Play(Sfx.UiConfirm, 0.8f, 0f);
                b.Command?.Execute(b.CommandParameter);
                b.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                break;
            case Key.Up or Key.Left:
                MoveFocus(NavigationDirection.Previous);
                break;
            case Key.Down or Key.Right:
                MoveFocus(NavigationDirection.Next);
                break;
        }
    }

    /// <summary>Go back one screen; false if there is nowhere to go.</summary>
    protected virtual bool Back() => false;

    private void MoveFocus(NavigationDirection direction)
    {
        var manager = TopLevel.GetTopLevel(this)?.FocusManager;
        if (manager?.GetFocusedElement() is null)
        {
            FocusFirst();
            return;
        }

        manager.TryMoveFocus(direction, new FindNextElementOptions());
    }

    public virtual void FocusFirst()
    {
        var first = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(this)
            .OfType<Button>()
            .FirstOrDefault(b => b.IsEffectivelyEnabled && b.IsEffectivelyVisible);
        first?.Focus(NavigationMethod.Directional);
    }

    public void ShowToast(string message)
    {
        _toastText.Text = message;
        _toast.IsVisible = true;
        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
        _toastTimer.Tick += (_, _) =>
        {
            _toast.IsVisible = false;
            _toastTimer?.Stop();
        };
        _toastTimer.Start();
    }

    /// <summary>Standard header: eyebrow + sablon title, top-left.</summary>
    protected static Control Header(string eyebrow, string title) => UiKit.Stack(4, UiKit.Eyebrow(eyebrow), new SablonText(title, 54));

    protected Button BackButton(Action onBack) => UiKit.Sticker("← " + Strings.Get("back"), () =>
    {
        Shell.Audio.Play(Sfx.UiBack, 0.8f, 0f);
        onBack();
    }, 24, 0);
}

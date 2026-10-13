using Android.Content;
using Android.Graphics;
using Android.Views;
using SiJenggo.Game;

namespace SiJenggo.Android;

/// <summary>
/// Virtual pad drawn over the game: a floating stick on the left half of the screen and seven action
/// buttons on the right, laid out like the keyboard set (light / heavy / jump on the bottom row).
/// Multi-touch: every pointer is mapped to the stick or to the button under it, and the result is a
/// plain <see cref="InputButtons"/> mask the render thread reads each tick.
/// </summary>
public sealed class TouchControls : View
{
    private readonly record struct PadButton(InputButtons Button, string Label, float X, float Y, uint Color);

    // Positions as fractions of the view, measured from the bottom-right corner.
    private static readonly PadButton[] s_buttons =
    [
        new(InputButtons.Light, "J", 0.20f, 0.16f, 0xFFF2B705),
        new(InputButtons.Heavy, "K", 0.12f, 0.27f, 0xFFD7263D),
        new(InputButtons.Jump, "L", 0.28f, 0.30f, 0xFF1B998B),
        new(InputButtons.Special, "I", 0.20f, 0.44f, 0xFFF2E9DC),
        new(InputButtons.Grab, "U", 0.36f, 0.16f, 0xFF8C5E3C),
        new(InputButtons.Dodge, "SH", 0.06f, 0.48f, 0xFF6C7A89),
        new(InputButtons.Rage, "O", 0.34f, 0.48f, 0xFFF26419),
    ];

    private readonly Paint _fill = new() { AntiAlias = true };
    private readonly Paint _text = new() { AntiAlias = true, TextAlign = Paint.Align.Center, FakeBoldText = true };
    private readonly Dictionary<int, InputButtons> _pointerButtons = [];
    private int _stickPointer = -1;
    private float _stickX, _stickY, _knobX, _knobY;
    private volatile int _state;

    public TouchControls(Context context) : base(context)
    {
    }

    /// <summary>Buttons currently held (thread-safe snapshot).</summary>
    public InputButtons State => (InputButtons)_state;

    /// <summary>Raised on the UI thread when the pause corner is tapped.</summary>
    public event Action? PauseTapped;

    private float Radius => Math.Min(Width, Height) * 0.075f;

    private (float X, float Y) ButtonCentre(in PadButton b) => (Width - b.X * Height * 1.25f, Height - b.Y * Height * 1.25f);

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e is null)
        {
            return false;
        }

        int index = e.ActionIndex;
        int id = e.GetPointerId(index);
        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
            case MotionEventActions.PointerDown:
            {
                float x = e.GetX(index), y = e.GetY(index);
                if (x > Width * 0.9f && y < Height * 0.12f)
                {
                    PauseTapped?.Invoke();
                }
                else if (x < Width * 0.45f && _stickPointer < 0)
                {
                    _stickPointer = id;
                    _stickX = _knobX = x;
                    _stickY = _knobY = y;
                }
                else
                {
                    _pointerButtons[id] = HitButton(x, y);
                }

                break;
            }

            case MotionEventActions.Move:
                for (int i = 0; i < e.PointerCount; i++)
                {
                    int pid = e.GetPointerId(i);
                    if (pid == _stickPointer)
                    {
                        _knobX = e.GetX(i);
                        _knobY = e.GetY(i);
                    }
                    else if (_pointerButtons.ContainsKey(pid))
                    {
                        _pointerButtons[pid] = HitButton(e.GetX(i), e.GetY(i));
                    }
                }

                break;
            case MotionEventActions.Up:
            case MotionEventActions.PointerUp:
            case MotionEventActions.Cancel:
                if (e.ActionMasked == MotionEventActions.Cancel)
                {
                    _pointerButtons.Clear();
                    _stickPointer = -1;
                }
                else if (id == _stickPointer)
                {
                    _stickPointer = -1;
                }
                else
                {
                    _pointerButtons.Remove(id);
                }

                break;
        }

        Publish();
        Invalidate();
        return true;
    }

    private InputButtons HitButton(float x, float y)
    {
        float r = Radius * 1.25f;
        foreach (var b in s_buttons)
        {
            var (cx, cy) = ButtonCentre(b);
            if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r)
            {
                return b.Button;
            }
        }

        return InputButtons.None;
    }

    private void Publish()
    {
        var state = InputButtons.None;
        foreach (var b in _pointerButtons.Values)
        {
            state |= b;
        }

        if (_stickPointer >= 0)
        {
            float dx = _knobX - _stickX, dy = _knobY - _stickY;
            float dead = Radius * 0.35f;
            if (dx < -dead) state |= InputButtons.Left;
            if (dx > dead) state |= InputButtons.Right;
            // Screen up = into the screen (+Z), as on the keyboard.
            if (dy < -dead) state |= InputButtons.Up;
            if (dy > dead) state |= InputButtons.Down;
        }

        _state = (int)state;
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        float r = Radius;
        var held = State;
        _text.TextSize = r * 0.7f;
        foreach (var b in s_buttons)
        {
            var (cx, cy) = ButtonCentre(b);
            bool down = (held & b.Button) != 0;
            _fill.Color = new Color((int)(down ? b.Color : (b.Color & 0x00FFFFFF) | 0x66000000));
            canvas.DrawCircle(cx, cy, r, _fill);
            _text.Color = Color.Argb(down ? 255 : 200, 22, 19, 15);
            canvas.DrawText(b.Label, cx, cy + r * 0.25f, _text);
        }

        if (_stickPointer >= 0)
        {
            _fill.Color = Color.Argb(70, 242, 233, 220);
            canvas.DrawCircle(_stickX, _stickY, r * 1.6f, _fill);
            _fill.Color = Color.Argb(170, 242, 183, 5);
            canvas.DrawCircle(_knobX, _knobY, r * 0.7f, _fill);
        }

        // Pause corner.
        _text.TextSize = r * 0.55f;
        _text.Color = Color.Argb(200, 242, 233, 220);
        canvas.DrawText("II", Width * 0.95f, Height * 0.08f, _text);
    }
}

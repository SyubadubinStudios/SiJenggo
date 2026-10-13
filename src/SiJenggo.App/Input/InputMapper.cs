using System.Numerics;
using Avalonia.Input;
using SiJenggo.Game;
using ThreeNet;
using Key = Avalonia.Input.Key;

namespace SiJenggo.App.Input;

/// <summary>
/// Keyboard (two players on one keyboard) plus up to two gamepads, folded into the simulation's
/// <see cref="InputButtons"/>. Analog sticks are quantised here, before anything reaches the
/// deterministic simulation.
/// </summary>
public sealed class InputMapper : IDisposable
{
    private readonly HashSet<Key> _down = [];
    private readonly Gamepads? _pads;

    public InputMapper()
    {
        try
        {
            _pads = new Gamepads { DeadZone = 0.3f };
        }
        catch (Exception ex) when (ex is ThreeNetException or DllNotFoundException)
        {
            _pads = null;
        }
    }

    private static readonly (Key Key, InputButtons Button)[] s_p1 =
    [
        (Key.A, InputButtons.Left), (Key.D, InputButtons.Right), (Key.W, InputButtons.Up), (Key.S, InputButtons.Down),
        (Key.J, InputButtons.Light), (Key.K, InputButtons.Heavy), (Key.L, InputButtons.Jump), (Key.Space, InputButtons.Jump),
        (Key.I, InputButtons.Special), (Key.U, InputButtons.Grab), (Key.O, InputButtons.Rage), (Key.LeftShift, InputButtons.Dodge),
        (Key.Escape, InputButtons.Pause),
    ];

    private static readonly (Key Key, InputButtons Button)[] s_p2 =
    [
        (Key.Left, InputButtons.Left), (Key.Right, InputButtons.Right), (Key.Up, InputButtons.Up), (Key.Down, InputButtons.Down),
        (Key.NumPad1, InputButtons.Light), (Key.NumPad2, InputButtons.Heavy), (Key.NumPad3, InputButtons.Jump),
        (Key.NumPad4, InputButtons.Special), (Key.NumPad5, InputButtons.Grab), (Key.NumPad6, InputButtons.Rage), (Key.NumPad0, InputButtons.Dodge),
        (Key.RightShift, InputButtons.Dodge),
    ];

    public bool GamepadsAvailable => _pads is not null;

    public void KeyDown(Key key) => _down.Add(key);

    public void KeyUp(Key key) => _down.Remove(key);

    public void ReleaseAll() => _down.Clear();

    public void Poll() => _pads?.Update();

    /// <summary>Buttons for a local player slot (0 or 1). In single player the arrow keys also drive player one.</summary>
    public InputButtons Read(int slot, bool singleKeyboardPlayer)
    {
        var b = InputButtons.None;
        foreach (var (key, button) in slot == 0 ? s_p1 : s_p2)
        {
            if (_down.Contains(key))
            {
                b |= button;
            }
        }

        if (slot == 0 && singleKeyboardPlayer)
        {
            foreach (var (key, button) in s_p2)
            {
                if (_down.Contains(key) && (button & InputButtons.Directions) != 0)
                {
                    b |= button;
                }
            }
        }

        if (_pads is not null && slot < _pads.SlotCount)
        {
            var pad = _pads.Get(slot);
            if (pad.IsConnected)
            {
                b |= FromPad(pad);
            }
        }

        // Opposing directions cancel.
        if ((b & (InputButtons.Left | InputButtons.Right)) == (InputButtons.Left | InputButtons.Right))
        {
            b &= ~(InputButtons.Left | InputButtons.Right);
        }

        if ((b & (InputButtons.Up | InputButtons.Down)) == (InputButtons.Up | InputButtons.Down))
        {
            b &= ~(InputButtons.Up | InputButtons.Down);
        }

        return b;
    }

    private static InputButtons FromPad(GamepadState pad)
    {
        var b = InputButtons.None;
        Vector2 stick = pad.LeftStick;
        if (stick.X < -0.4f || pad.IsDown(GamepadButton.DPadLeft)) b |= InputButtons.Left;
        if (stick.X > 0.4f || pad.IsDown(GamepadButton.DPadRight)) b |= InputButtons.Right;
        if (stick.Y > 0.4f || pad.IsDown(GamepadButton.DPadUp)) b |= InputButtons.Up;
        if (stick.Y < -0.4f || pad.IsDown(GamepadButton.DPadDown)) b |= InputButtons.Down;
        if (pad.IsDown(GamepadButton.South)) b |= InputButtons.Jump;
        if (pad.IsDown(GamepadButton.West)) b |= InputButtons.Light;
        if (pad.IsDown(GamepadButton.North)) b |= InputButtons.Heavy;
        if (pad.IsDown(GamepadButton.East)) b |= InputButtons.Special;
        if (pad.IsDown(GamepadButton.RightBumper)) b |= InputButtons.Grab;
        if (pad.IsDown(GamepadButton.LeftBumper)) b |= InputButtons.Dodge;
        if (pad.RightTrigger > 0.5f || pad.IsDown(GamepadButton.RightTrigger)) b |= InputButtons.Rage;
        if (pad.IsDown(GamepadButton.Start)) b |= InputButtons.Pause;
        return b;
    }

    /// <summary>Menu navigation from any gamepad: returns a key to inject (arrows, Enter, Escape) on press edges.</summary>
    public Key? MenuKeyFromPads()
    {
        if (_pads is null)
        {
            return null;
        }

        for (int slot = 0; slot < Math.Min(2, _pads.SlotCount); slot++)
        {
            var pad = _pads.Get(slot);
            if (!pad.IsConnected)
            {
                continue;
            }

            if (pad.WasPressed(GamepadButton.DPadUp)) return Key.Up;
            if (pad.WasPressed(GamepadButton.DPadDown)) return Key.Down;
            if (pad.WasPressed(GamepadButton.DPadLeft)) return Key.Left;
            if (pad.WasPressed(GamepadButton.DPadRight)) return Key.Right;
            if (pad.WasPressed(GamepadButton.South)) return Key.Enter;
            if (pad.WasPressed(GamepadButton.East)) return Key.Escape;
        }

        return null;
    }

    public void Dispose() => _pads?.Dispose();
}

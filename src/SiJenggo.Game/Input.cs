namespace SiJenggo.Game;

/// <summary>
/// One player's digital input for one simulation tick. Analog sticks are quantised to directions
/// before they reach the simulation, so a frame is two bytes on the wire and replays exactly.
/// </summary>
[Flags]
public enum InputButtons : ushort
{
    None = 0,
    Left = 1 << 0,
    Right = 1 << 1,
    /// <summary>Into the screen (+Z, away from the camera).</summary>
    Up = 1 << 2,
    /// <summary>Towards the camera (-Z).</summary>
    Down = 1 << 3,
    Light = 1 << 4,
    Heavy = 1 << 5,
    Jump = 1 << 6,
    Special = 1 << 7,
    /// <summary>Grab an enemy, pick up / throw a prop or weapon, revive a partner.</summary>
    Grab = 1 << 8,
    /// <summary>Activate Rage Nusantara when full; while active, unleash the ultimate.</summary>
    Rage = 1 << 9,
    Dodge = 1 << 10,
    Pause = 1 << 11,

    Directions = Left | Right | Up | Down,
}

public readonly record struct InputFrame(InputButtons Buttons)
{
    public bool Down(InputButtons b) => (Buttons & b) != 0;
}

/// <summary>Current and previous frame, so systems can ask for edges.</summary>
public struct InputState
{
    public InputButtons Current;
    public InputButtons Previous;

    public readonly bool Held(InputButtons b) => (Current & b) != 0;
    public readonly bool Pressed(InputButtons b) => (Current & b) != 0 && (Previous & b) == 0;
    public readonly bool Released(InputButtons b) => (Current & b) == 0 && (Previous & b) != 0;

    /// <summary>Movement intent on the ground plane: X = right, Y = into screen.</summary>
    public readonly System.Numerics.Vector2 Axis
    {
        get
        {
            float x = (Held(InputButtons.Right) ? 1 : 0) - (Held(InputButtons.Left) ? 1 : 0);
            float y = (Held(InputButtons.Up) ? 1 : 0) - (Held(InputButtons.Down) ? 1 : 0);
            var v = new System.Numerics.Vector2(x, y);
            return x != 0 && y != 0 ? v * 0.70710677f : v;
        }
    }

    public void Push(InputButtons next)
    {
        Previous = Current;
        Current = next;
    }
}

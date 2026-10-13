using System.Numerics;
using SiJenggo.Game.Data;

namespace SiJenggo.Presentation;

/// <summary>
/// One camera move of an in-engine cutscene, in render space: the camera glides from
/// (<see cref="FromPos"/>, <see cref="FromLook"/>) to (<see cref="ToPos"/>, <see cref="ToLook"/>).
/// <see cref="Line"/> is the story line shown as a subtitle during the shot (-1 for none).
/// </summary>
public readonly record struct CutsceneShot(Vector3 FromPos, Vector3 FromLook, Vector3 ToPos, Vector3 ToLook, float Duration, int Line);

/// <summary>
/// Camera scripts for the cutscenes that open each Story chapter (and the epilogue). They tour the
/// chapter's real stage: a wide establishing shot, one tracking shot per area the story passes
/// through, a low push into the boss arena, and finally the heroes at their starting line.
/// </summary>
public static class Cutscenes
{
    /// <summary>Seconds a subtitle stays up: a base plus reading time.</summary>
    public static float LineSeconds(string text) => 2.6f + text.Length * 0.045f;

    public static CutsceneShot[] ChapterIntro(ChapterDef chapter, IReadOnlyList<string> lines)
    {
        var shots = new List<CutsceneShot>(lines.Count + 1);
        var areas = chapter.Areas;
        for (int i = 0; i < lines.Count; i++)
        {
            float seconds = LineSeconds(lines[i]);
            if (i == 0)
            {
                float sx = areas[0].StartX;
                shots.Add(new CutsceneShot(new Vector3(sx + 2, 7.5f, 18), new Vector3(sx + 10, 1, 0), new Vector3(sx + 16, 6, 15), new Vector3(sx + 22, 1.2f, 0), seconds, i));
            }
            else if (i == lines.Count - 1 && lines.Count > 1)
            {
                float bx = chapter.BossArenaX;
                shots.Add(new CutsceneShot(new Vector3(bx + 1, 1.0f, 9), new Vector3(bx + 11, 2.2f, -1), new Vector3(bx + 5, 1.7f, 6), new Vector3(bx + 11, 2.8f, -1), seconds, i));
            }
            else
            {
                // Spread the middle lines over the middle areas.
                int area = Math.Clamp(1 + (i - 1) * Math.Max(1, areas.Length - 2) / Math.Max(1, lines.Count - 2), 0, areas.Length - 1);
                float x = (areas[area].StartX + areas[area].EndX) * 0.5f;
                shots.Add(new CutsceneShot(new Vector3(x - 7, 2.2f, 7.5f), new Vector3(x - 1, 1.5f, -2), new Vector3(x + 5, 2.8f, 8), new Vector3(x + 9, 1.5f, -2), seconds, i));
            }
        }

        // The heroes at the start line (players spawn at x 3..4.2 facing +X): low and close, from the front.
        shots.Add(new CutsceneShot(new Vector3(7.2f, 1.1f, 3.6f), new Vector3(3.6f, 1.3f, 0), new Vector3(5.6f, 1.7f, 5.8f), new Vector3(3.6f, 1.4f, 0), 3.2f, -1));
        return [.. shots];
    }

    /// <summary>The epilogue plays where the last fight ended: slow orbit around the spot, then a rise to the sky.</summary>
    public static CutsceneShot[] Epilogue(float x, IReadOnlyList<string> lines)
    {
        var shots = new CutsceneShot[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            float a = i * 1.1f;
            var from = new Vector3(x + MathF.Sin(a) * 7, 2 + i * 1.5f, MathF.Cos(a) * 7 + 2);
            var to = new Vector3(x + MathF.Sin(a + 0.8f) * 8, 3 + i * 2f, MathF.Cos(a + 0.8f) * 8 + 2);
            shots[i] = new CutsceneShot(from, new Vector3(x, 1.4f + i * 0.8f, 0), to, new Vector3(x, 1.8f + i * 1.2f, 0), LineSeconds(lines[i]), i);
        }

        return shots;
    }
}

using System.Numerics;
using SiJenggo.Core;
using SiJenggo.Game.Data;
using ThreeNet;
using ThreeNet.Effects;

namespace SiJenggo.Presentation.Scenery;

/// <summary>Lights, fire sources and other things the stage keeps alive while it is shown.</summary>
public sealed class StageDressing
{
    public List<Vector3> LampAnchors { get; } = [];
    public List<Vector3> LampColors { get; } = [];
    public List<FireEffect> Fires { get; } = [];
    public WaterSurface? Water { get; set; }
    public required Node Sun { get; init; }
    public required SceneEnvironment Environment { get; init; }
    public float SunIntensity { get; init; }
    public required ChapterDef Chapter { get; init; }
}

/// <summary>
/// Builds each chapter's street out of merged low-poly geometry: one draw call per material per stage,
/// so a full 190 m street costs a few dozen draw calls on an integrated GPU.
/// Render space: X along the street, the walkable lane is Z ∈ [-3.2, 3.2], the back of the stage is -Z.
/// </summary>
public sealed class EnvironmentBuilder
{
    private readonly Scene _scene;
    private readonly Materials _m;
    private readonly Dictionary<Material, MeshBuilder> _batches = [];
    private DeterministicRandom _rng;
    private Node _root = null!;

    public EnvironmentBuilder(Scene scene, Materials materials)
    {
        _scene = scene;
        _m = materials;
    }

    private MeshBuilder B(Material material)
    {
        if (!_batches.TryGetValue(material, out var b))
        {
            b = new MeshBuilder();
            _batches[material] = b;
        }

        return b;
    }

    public StageDressing Build(ChapterDef chapter, Node parent, int quality)
    {
        _batches.Clear();
        _rng = new DeterministicRandom((ulong)(chapter.Index + 1) * 977);
        _root = parent.CreateChild("stage");
        float length = chapter.Length + 30f;
        float x0 = -15f;

        var env = SceneEnvironment.Default;
        Node sun = _scene.AddLight(ThreeNet.Light.Directional(Vector3.One, 2f), _root, "sun");
        var dressing = new StageDressing { Sun = sun, Environment = env, Chapter = chapter, SunIntensity = 2f };

        switch (chapter.Theme)
        {
            case EnvironmentTheme.JakartaKumuh: Jakarta(chapter, x0, length, dressing); break;
            case EnvironmentTheme.Pelabuhan: Harbour(chapter, x0, length, dressing); break;
            case EnvironmentTheme.PantaiSelatan: Beach(chapter, x0, length, dressing, quality); break;
            case EnvironmentTheme.HutanKalimantan: Forest(chapter, x0, length, dressing); break;
            case EnvironmentTheme.GunungBerapi: Volcano(chapter, x0, length, dressing, quality); break;
            case EnvironmentTheme.CandiKuno: Temple(chapter, x0, length, dressing, quality); break;
            default: FutureCity(chapter, x0, length, dressing); break;
        }

        foreach (var (mat, b) in _batches)
        {
            b.AddTo(_scene, mat, _root, castShadow: true);
        }

        _batches.Clear();
        return dressing;
    }

    private static void Lamp(StageDressing d, Vector3 pos, uint color)
    {
        d.LampAnchors.Add(pos);
        var c = MathHelpers.FromHex(color);
        d.LampColors.Add(new Vector3(c.X, c.Y, c.Z));
    }

    private static Light SunLight(Vector3 color, float intensity) => new()
    {
        Type = LightType.Directional,
        Color = color,
        Intensity = intensity,
        CastShadow = true,
        Enabled = true,
        ShadowStrength = 0.75f,
        ShadowBias = 0.002f,
        ShadowNormalBias = 0.02f,
    };

    private static void SetSun(StageDressing d, Vector3 dir, Vector3 color, float intensity)
    {
        d.Sun.Light = SunLight(color, intensity);
        d.Sun.Position = -Vector3.Normalize(dir) * 30f;
        d.Sun.LookAt(Vector3.Zero);
    }

    private void Ground(float x0, float length, uint road, uint verge, float roadDepth = 7.4f)
    {
        B(_m.Pbr(road, 0.92f)).Floor(new Vector3(x0 + length / 2, 0, 0), length, roadDepth, 0.5f);
        B(_m.Pbr(verge, 0.95f)).Floor(new Vector3(x0 + length / 2, -0.01f, -9f), length, 11f, 0.25f);
        B(_m.Pbr(verge, 0.95f)).Floor(new Vector3(x0 + length / 2, -0.01f, 7.5f), length, 8f, 0.25f);
    }

    private string AreaAt(ChapterDef c, float x)
    {
        foreach (var a in c.Areas)
        {
            if (x >= a.StartX && x < a.EndX)
            {
                return a.Dressing;
            }
        }

        return c.Areas[^1].Dressing;
    }

    // ───────────────────────────── Chapter 1: Jakarta slums at night ─────────────────────────────
    private void Jakarta(ChapterDef c, float x0, float length, StageDressing d)
    {
        var env = d.Environment;
        env.Sky = SkyMode.Color;
        env.Background = MathHelpers.FromHex(0x0B0D1A);
        env.AmbientColor = new Vector3(0.3f, 0.32f, 0.5f);
        env.AmbientIntensity = 0.16f;
        env.FogColor = new Vector3(0.05f, 0.045f, 0.09f);
        env.FogDensity = 0.012f;
        _scene.Environment = env;
        SetSun(d, new Vector3(0.3f, -1f, -0.4f), new Vector3(0.55f, 0.6f, 0.9f), 0.8f);

        Ground(x0, length, 0x2A2B30, 0x1F2024);
        // Road markings and gutter.
        for (float x = x0; x < x0 + length; x += 4f)
        {
            B(_m.Pbr(0xC9B458, 0.8f)).Floor(new Vector3(x, 0.005f, 0.2f), 1.8f, 0.12f);
        }

        B(_m.Pbr(0x55575C, 0.9f)).Box(new Vector3(x0 + length / 2, 0, -3.75f), new Vector3(length, 0.18f, 0.5f));

        uint[] walls = [0xB9A88A, 0x8FA3A0, 0xC48B6B, 0x9C8DB0, 0xD1C59B, 0x7F9C83];
        uint[] neon = [0xFF2E88, 0x2EE6FF, 0xFFD23F, 0x7CFF4F, 0xFF7A1A];
        uint[] awnings = [0xD7261E, 0x1F6FB2, 0x2E9E5B, 0xF2B705];
        for (float x = x0; x < x0 + length;)
        {
            string area = AreaAt(c, x);
            float w = area == "rusun" ? _rng.Range(9f, 13f) : _rng.Range(4f, 7f);
            float h = area switch { "rusun" => _rng.Range(14f, 22f), "terminal" => _rng.Range(3.5f, 5f), _ => _rng.Range(4f, 8f) };
            float depth = area == "rusun" ? 6f : 4f;
            var wall = _m.Pbr(walls[_rng.Range(0, walls.Length)], 0.9f);
            float z = -4.6f - depth / 2;
            B(wall).Box(new Vector3(x + w / 2, 0, z), new Vector3(w - 0.3f, h, depth));
            // Corrugated roof lip.
            B(_m.Pbr(0x6E6A62, 0.6f, 0.6f)).Box(new Vector3(x + w / 2, h, z + 0.4f), new Vector3(w, 0.15f, depth + 1f));
            // Windows: warm ones lit, some dark.
            var lit = _m.Glow(0xFFC56B, 1.6f);
            var dark = _m.Pbr(0x1A1C24, 0.3f);
            for (float wy = 1.4f; wy < h - 0.8f; wy += 2.2f)
            {
                for (float wx = x + 0.9f; wx < x + w - 0.9f; wx += 1.6f)
                {
                    B(_rng.Chance(0.45f) ? lit : dark).Box(new Vector3(wx, wy, z + depth / 2 + 0.02f), new Vector3(0.8f, 1.1f, 0.05f));
                }
            }

            if (area is "gang" or "pasar")
            {
                // Warung awning and a hand-painted sign.
                var awn = _m.Pbr(awnings[_rng.Range(0, awnings.Length)], 0.7f);
                B(awn).Box(new Vector3(x + w / 2, 2.6f, -4.1f), new Vector3(w * 0.8f, 0.08f, 1.6f), 0);
                B(_m.Glow(neon[_rng.Range(0, neon.Length)], 2.6f)).Box(new Vector3(x + w / 2, 3.2f, -4.3f), new Vector3(w * 0.5f, 0.55f, 0.08f));
            }

            if (area == "pasar")
            {
                // String lights between poles.
                var bulb = _m.Glow(neon[_rng.Range(0, 3)], 4f);
                for (float bx = x; bx < x + w; bx += 0.6f)
                {
                    float sag = MathF.Sin((bx - x) / w * MathF.PI) * 0.5f;
                    B(bulb).Sphere(new Vector3(bx, 4.2f - sag, -3.6f), new Vector3(0.06f), 2, 4);
                }

                B(_m.Pbr(0xEDE3C8, 0.8f)).Box(new Vector3(x + w / 2, 0, -3.3f), new Vector3(1.6f, 0.9f, 0.7f));
            }

            if (area == "terminal" && _rng.Chance(0.6f))
            {
                // A parked angkot: blue minibus with windows.
                var body = _m.Pbr(0x1E5AA8, 0.4f, 0.3f);
                B(body).Box(new Vector3(x + w / 2, 0.35f, -3.9f), new Vector3(3.6f, 1.5f, 1.6f));
                B(_m.Pbr(0x9FC3D9, 0.1f)).Box(new Vector3(x + w / 2, 1.15f, -3.08f), new Vector3(3.2f, 0.5f, 0.02f));
                B(_m.Pbr(0x111111, 0.7f)).Cylinder(new Vector3(x + w / 2 - 1.1f, 0.35f, -3.08f), 0.35f, 0.35f, 0.08f, 10);
                B(_m.Pbr(0x111111, 0.7f)).Cylinder(new Vector3(x + w / 2 + 1.1f, 0.35f, -3.08f), 0.35f, 0.35f, 0.08f, 10);
            }

            if (area == "rusun")
            {
                // Balconies with laundry.
                for (float wy = 3f; wy < h - 1f; wy += 2.8f)
                {
                    B(_m.Pbr(0x8C8C8C, 0.9f)).Box(new Vector3(x + w / 2, wy, z + depth / 2 + 0.5f), new Vector3(w - 0.5f, 0.12f, 1f));
                    for (float lx = x + 0.8f; lx < x + w - 0.8f; lx += 0.7f)
                    {
                        if (_rng.Chance(0.4f))
                        {
                            B(_m.Pbr(awnings[_rng.Range(0, awnings.Length)], 0.9f)).Box(new Vector3(lx, wy + 0.5f, z + depth / 2 + 0.95f), new Vector3(0.45f, 0.6f, 0.02f));
                        }
                    }
                }
            }

            // Street lamp every other building.
            if (_rng.Chance(0.5f))
            {
                B(_m.Pbr(0x3A3D42, 0.5f, 0.8f)).Cylinder(new Vector3(x + 0.3f, 0, -3.9f), 0.06f, 0.05f, 4.6f, 6);
                B(_m.Glow(0xFFD68A, 5f)).Sphere(new Vector3(x + 0.3f, 4.6f, -3.7f), new Vector3(0.18f, 0.12f, 0.18f), 3, 6);
                Lamp(d, new Vector3(x + 0.3f, 4.2f, -3.2f), 0xFFC27A);
            }

            x += w;
        }

        // Neon accent lights.
        for (float x = x0 + 8; x < x0 + length; x += 17f)
        {
            Lamp(d, new Vector3(x, 3.2f, -3.6f), neon[_rng.Range(0, neon.Length)]);
        }

        Foreground(x0, length, 0x2D2A26);
    }

    // ───────────────────────────── Chapter 2: harbour in the rain ─────────────────────────────
    private void Harbour(ChapterDef c, float x0, float length, StageDressing d)
    {
        var env = d.Environment;
        env.Sky = SkyMode.Procedural;
        env.SunDirection = Vector3.Normalize(new Vector3(-0.6f, 0.08f, -0.5f));
        env.SkyIntensity = 0.22f;
        env.SkyHaze = 0.8f;
        env.SkyClouds = 0.9f;
        env.AmbientColor = new Vector3(0.45f, 0.5f, 0.6f);
        env.AmbientIntensity = 0.2f;
        env.FogColor = new Vector3(0.05f, 0.06f, 0.075f);
        env.FogDensity = 0.016f;
        _scene.Environment = env;
        SetSun(d, new Vector3(0.6f, -0.5f, -0.5f), new Vector3(0.8f, 0.75f, 0.7f), 1.2f);

        Ground(x0, length, 0x55585C, 0x404347);
        uint[] containers = [0xB03A2E, 0x1F618D, 0x1E8449, 0xD68910, 0x6C3483, 0x7B7D7D];
        for (float x = x0; x < x0 + length;)
        {
            string area = AreaAt(c, x);
            if (area == "kontainer")
            {
                int stack = _rng.Range(1, 4);
                for (int s = 0; s < stack; s++)
                {
                    var col = _m.Pbr(containers[_rng.Range(0, containers.Length)], 0.6f, 0.4f);
                    B(col).Box(new Vector3(x + 3f, s * 2.6f, -6.5f), new Vector3(6f, 2.55f, 2.45f));
                    for (float rx = x + 0.4f; rx < x + 6f; rx += 0.5f)
                    {
                        B(col).Box(new Vector3(rx, s * 2.6f + 0.1f, -5.25f), new Vector3(0.08f, 2.35f, 0.05f));
                    }
                }

                x += 6.4f;
            }
            else if (area == "gudang")
            {
                B(_m.Pbr(0x7C7F82, 0.7f, 0.5f)).Box(new Vector3(x + 6f, 0, -8f), new Vector3(12f, 9f, 6f));
                B(_m.Pbr(0x3B3D40, 0.6f, 0.6f)).Box(new Vector3(x + 6f, 0, -4.95f), new Vector3(4.5f, 5f, 0.1f));
                B(_m.Glow(0xFFE6A8, 3f)).Box(new Vector3(x + 6f, 6f, -4.9f), new Vector3(1.5f, 0.3f, 0.1f));
                Lamp(d, new Vector3(x + 6f, 5.5f, -4f), 0xFFE0A0);
                x += 12.5f;
            }
            else
            {
                // The freighter: a wall of hull with a red waterline.
                B(_m.Pbr(0x1C2833, 0.5f, 0.6f)).Box(new Vector3(x + 5f, 0, -9f), new Vector3(10.2f, 10f, 5f));
                B(_m.Pbr(0x922B21, 0.6f, 0.4f)).Box(new Vector3(x + 5f, 0, -6.45f), new Vector3(10.2f, 1.5f, 0.1f));
                for (float px = x + 1; px < x + 10; px += 1.8f)
                {
                    B(_m.Glow(0xFFF0C0, 2f)).Cylinder(new Vector3(px, 6.5f, -6.45f), 0.25f, 0.25f, 0.06f, 8);
                }

                x += 10.2f;
            }

            if (_rng.Chance(0.25f))
            {
                // Crane mast.
                B(_m.Pbr(0xD4AC0D, 0.5f, 0.6f)).Box(new Vector3(x, 0, -11f), new Vector3(0.8f, 18f, 0.8f));
                B(_m.Pbr(0xD4AC0D, 0.5f, 0.6f)).Box(new Vector3(x + 4f, 17f, -11f), new Vector3(10f, 0.7f, 0.7f));
            }
        }

        for (float x = x0 + 6; x < x0 + length; x += 14f)
        {
            B(_m.Pbr(0x3A3D42, 0.5f, 0.8f)).Cylinder(new Vector3(x, 0, -3.9f), 0.07f, 0.06f, 5.2f, 6);
            B(_m.Glow(0xFFF2CC, 5f)).Box(new Vector3(x, 5.2f, -3.6f), new Vector3(0.5f, 0.12f, 0.3f));
            Lamp(d, new Vector3(x, 4.8f, -3.2f), 0xE8F0FF);
        }

        // Bollards along the quay at the front.
        for (float x = x0; x < x0 + length; x += 5f)
        {
            B(_m.Pbr(0x2B2B2B, 0.5f, 0.7f)).Cylinder(new Vector3(x, 0, 4.4f), 0.2f, 0.16f, 0.6f, 8);
        }

        B(_m.Pbr(0x0E2A3B, 0.15f, 0.2f)).Floor(new Vector3(x0 + length / 2, -0.3f, 14f), length + 40, 14f);
    }

    // ───────────────────────────── Chapter 3: southern shore storm ─────────────────────────────
    private void Beach(ChapterDef c, float x0, float length, StageDressing d, int quality)
    {
        var env = d.Environment;
        env.Sky = SkyMode.Procedural;
        env.SunDirection = Vector3.Normalize(new Vector3(0.2f, 0.12f, -0.8f));
        env.SkyIntensity = 0.12f;
        env.SkyHaze = 0.9f;
        env.SkyClouds = 1f;
        env.AmbientColor = new Vector3(0.4f, 0.48f, 0.6f);
        env.AmbientIntensity = 0.18f;
        env.FogColor = new Vector3(0.03f, 0.04f, 0.055f);
        env.FogDensity = 0.012f;
        _scene.Environment = env;
        SetSun(d, new Vector3(-0.3f, -0.6f, -0.6f), new Vector3(0.6f, 0.68f, 0.85f), 0.8f);

        B(_m.Pbr(0x8C7A5A, 0.95f)).Floor(new Vector3(x0 + length / 2, 0, 0), length, 7.4f, 0.4f);
        B(_m.Pbr(0x7A6A4E, 0.95f)).Floor(new Vector3(x0 + length / 2, -0.01f, 7.5f), length, 8f);
        B(_m.Pbr(0x6E6047, 0.95f)).Floor(new Vector3(x0 + length / 2, -0.02f, -6f), length, 6f);

        // The sea behind the beach.
        if (quality >= 2)
        {
            d.Water = new WaterSurface(_scene, new Vector3(x0 + length / 2, -0.15f, -26f), 60f, -2f) { Style = WaterStyle.Ocean };
        }
        else
        {
            B(_m.Pbr(0x1B4F72, 0.08f, 0.1f)).Floor(new Vector3(x0 + length / 2, -0.15f, -30f), length + 60, 42f);
        }

        for (float x = x0; x < x0 + length; x += _rng.Range(3f, 7f))
        {
            string area = AreaAt(c, x);
            if (area is "pantai" or "tanjung")
            {
                Palm(new Vector3(x, 0, _rng.Range(-8f, -4.5f)));
                if (_rng.Chance(0.5f))
                {
                    B(_m.Pbr(0x5D5A55, 0.95f)).Sphere(new Vector3(x + 1.5f, 0.4f, _rng.Range(-7f, -4.5f)), new Vector3(_rng.Range(0.6f, 1.6f), _rng.Range(0.5f, 1.2f), 0.9f), 3, 6);
                }
            }
            else
            {
                // Fishing huts on stilts and jukung outrigger boats.
                B(_m.Pbr(0x8B6B4A, 0.9f)).Box(new Vector3(x, 1f, -7f), new Vector3(3.2f, 2.6f, 3f));
                B(_m.Pbr(0x5C3A1E, 0.8f)).Box(new Vector3(x, 3.6f, -7f), new Vector3(3.8f, 0.2f, 3.6f), 0);
                for (int s = -1; s <= 1; s += 2)
                {
                    B(_m.Pbr(0x5C3A1E, 0.8f)).Cylinder(new Vector3(x + s * 1.4f, 0, -5.6f), 0.08f, 0.08f, 1f, 6);
                }

                B(_m.Pbr(0xE8E2D0, 0.6f)).Box(new Vector3(x + 2.5f, 0, -4.6f), new Vector3(4f, 0.45f, 0.6f));
                B(_m.Pbr(0xD7261E, 0.6f)).Box(new Vector3(x + 2.5f, 0.45f, -4.6f), new Vector3(4f, 0.08f, 0.62f));
                if (_rng.Chance(0.5f))
                {
                    var flame = new FireEffect(_scene, new Vector3(x - 2f, 0, -5f), new Vector3(0.6f, 1.2f, 0.6f), _root) { Style = FireStyle.Torch };
                    d.Fires.Add(flame);
                    Lamp(d, new Vector3(x - 2f, 1.2f, -4.6f), 0xFF9A3C);
                }
            }
        }

        Lamp(d, new Vector3(x0 + length * 0.5f, 6f, -4f), 0x9FB8FF);
    }

    private void Palm(Vector3 at)
    {
        var trunk = _m.Pbr(0x7A5C3A, 0.9f);
        var leaf = _m.Pbr(0x2E7D32, 0.7f);
        float lean = _rng.Range(-0.4f, 0.4f);
        var p = at;
        for (int i = 0; i < 6; i++)
        {
            B(trunk).Cylinder(p, 0.22f - i * 0.02f, 0.2f - i * 0.02f, 1.05f, 7, false);
            p += new Vector3(lean * 0.25f * (i + 1) * 0.3f, 1f, 0);
        }

        for (int i = 0; i < 7; i++)
        {
            float a = i / 7f * MathF.Tau;
            B(leaf).Box(p + new Vector3(MathF.Cos(a) * 1.1f, -0.25f, MathF.Sin(a) * 1.1f), new Vector3(2.4f, 0.06f, 0.5f), -a);
        }
    }

    // ───────────────────────────── Chapter 4: Borneo rainforest ─────────────────────────────
    private void Forest(ChapterDef c, float x0, float length, StageDressing d)
    {
        var env = d.Environment;
        env.Sky = SkyMode.Procedural;
        env.SunDirection = Vector3.Normalize(new Vector3(0.3f, 0.6f, -0.4f));
        env.SkyIntensity = 0.7f;
        env.SkyHaze = 1f;
        env.AmbientColor = new Vector3(0.45f, 0.6f, 0.45f);
        env.AmbientIntensity = 0.25f;
        env.FogColor = new Vector3(0.09f, 0.13f, 0.1f);
        env.FogDensity = 0.03f;
        _scene.Environment = env;
        SetSun(d, new Vector3(-0.3f, -0.8f, -0.3f), new Vector3(1f, 0.95f, 0.8f), 1.5f);

        Ground(x0, length, 0x4A3B2A, 0x2F4A26);
        var bark = _m.Pbr(0x5B4632, 0.95f);
        var canopy = _m.Pbr(0x1E5631, 0.85f);
        var canopy2 = _m.Pbr(0x2D7A3E, 0.85f);
        var fern = _m.Pbr(0x3E8E41, 0.8f);
        for (float x = x0; x < x0 + length; x += _rng.Range(2.5f, 5.5f))
        {
            string area = AreaAt(c, x);
            float z = _rng.Range(-11f, -4.6f);
            bool giant = area == "raksasa" && _rng.Chance(0.6f);
            float r = giant ? _rng.Range(1.2f, 2f) : _rng.Range(0.3f, 0.6f);
            float h = giant ? 26f : _rng.Range(8f, 14f);
            if (area == "kamp" && _rng.Chance(0.5f))
            {
                // Felled logs and stumps: what the loggers left.
                B(bark).Cylinder(new Vector3(x, 0, z), r * 1.2f, r * 1.1f, 0.6f, 9);
                for (int l = 0; l < 3; l++)
                {
                    B(bark).Box(new Vector3(x + 2f, l * 0.55f, -4.8f + (l % 2) * 0.1f), new Vector3(4f, 0.5f, 0.55f));
                }

                B(_m.Pbr(0xF2B705, 0.6f)).Box(new Vector3(x + 5f, 0, -6.5f), new Vector3(4f, 2.2f, 2f));
                continue;
            }

            B(bark).Cylinder(new Vector3(x, 0, z), r * 1.3f, r, h, 9, false);
            B(_rng.Chance(0.5f) ? canopy : canopy2).Sphere(new Vector3(x, h, z), new Vector3(r * 4f, r * 2.2f + 1.5f, r * 4f), 4, 8);
            if (giant)
            {
                // Buttress roots.
                for (int k = 0; k < 4; k++)
                {
                    B(bark).Box(new Vector3(x, 0, z), new Vector3(r * 4f, 1.6f, 0.3f), k * MathF.PI / 4);
                }
            }

            B(fern).Sphere(new Vector3(x + 1.5f, 0.2f, -4.2f), new Vector3(0.9f, 0.5f, 0.6f), 3, 6);
        }

        // Undergrowth in the foreground for depth.
        for (float x = x0; x < x0 + length; x += _rng.Range(4f, 9f))
        {
            B(fern).Sphere(new Vector3(x, 0f, 6.5f), new Vector3(0.9f, 0.35f, 0.6f), 3, 6);
        }

        for (float x = x0 + 10; x < x0 + length; x += 20f)
        {
            Lamp(d, new Vector3(x, 6f, -2f), 0xD6FFB0);
        }
    }

    // ───────────────────────────── Chapter 5: the volcano ─────────────────────────────
    private void Volcano(ChapterDef c, float x0, float length, StageDressing d, int quality)
    {
        var env = d.Environment;
        env.Sky = SkyMode.Color;
        env.Background = MathHelpers.FromHex(0x2A120C);
        env.AmbientColor = new Vector3(0.7f, 0.35f, 0.25f);
        env.AmbientIntensity = 0.14f;
        env.FogColor = new Vector3(0.06f, 0.02f, 0.012f);
        env.FogDensity = 0.018f;
        _scene.Environment = env;
        SetSun(d, new Vector3(0.4f, -0.6f, -0.4f), new Vector3(1f, 0.55f, 0.35f), 0.9f);

        Ground(x0, length, 0x2B2523, 0x1A1614);
        var rock = _m.Pbr(0x2E2724, 0.95f);
        var lava = _m.Glow(0xFF5A1F, 4f);
        // Lava river behind the path.
        B(lava).Floor(new Vector3(x0 + length / 2, 0.02f, -10f), length, 4f);
        for (float x = x0; x < x0 + length; x += _rng.Range(3f, 6f))
        {
            string area = AreaAt(c, x);
            B(rock).Cylinder(new Vector3(x, 0, _rng.Range(-16f, -12.5f)), _rng.Range(1.5f, 3f), 0.2f, _rng.Range(6f, 16f), 6);
            B(rock).Sphere(new Vector3(x + 1, 0.3f, -5.2f), new Vector3(_rng.Range(0.6f, 1.4f), 0.7f, 0.8f), 3, 6);
            // Glowing cracks in the road.
            if (_rng.Chance(0.5f))
            {
                B(lava).Box(new Vector3(x, -0.02f, _rng.Range(-2.8f, 2.8f)), new Vector3(_rng.Range(0.8f, 2f), 0.03f, 0.08f), _rng.Range(-0.6f, 0.6f));
            }

            if (area == "tambang" && _rng.Chance(0.5f))
            {
                var wood = _m.Pbr(0x6E4826, 0.85f);
                B(wood).Box(new Vector3(x, 0, -5f), new Vector3(0.2f, 4f, 0.2f));
                B(wood).Box(new Vector3(x + 2.4f, 0, -5f), new Vector3(0.2f, 4f, 0.2f));
                B(wood).Box(new Vector3(x + 1.2f, 4f, -5f), new Vector3(2.8f, 0.25f, 0.3f));
                B(_m.Glow(0xFFD27F, 3f)).Sphere(new Vector3(x + 1.2f, 3.6f, -4.8f), new Vector3(0.12f), 2, 5);
                Lamp(d, new Vector3(x + 1.2f, 3.4f, -4.4f), 0xFFC070);
            }

            if (_rng.Chance(quality >= 2 ? 0.25f : 0.1f))
            {
                d.Fires.Add(new FireEffect(_scene, new Vector3(x, 0, -8.5f), new Vector3(1.2f, 2.4f, 1.2f), _root) { Style = FireStyle.Campfire });
            }
        }

        for (float x = x0 + 5; x < x0 + length; x += 12f)
        {
            Lamp(d, new Vector3(x, 1.5f, -8f), 0xFF5A1F);
        }
    }

    // ───────────────────────────── Chapter 6: the ancient temple ─────────────────────────────
    private void Temple(ChapterDef c, float x0, float length, StageDressing d, int quality)
    {
        var env = d.Environment;
        env.Sky = SkyMode.Procedural;
        env.SunDirection = Vector3.Normalize(new Vector3(-0.5f, 0.2f, -0.6f));
        env.SkyIntensity = 0.5f;
        env.SkyHaze = 0.7f;
        env.SkyClouds = 0.4f;
        env.AmbientColor = new Vector3(0.6f, 0.55f, 0.7f);
        env.AmbientIntensity = 0.22f;
        env.FogColor = new Vector3(0.1f, 0.09f, 0.13f);
        env.FogDensity = 0.02f;
        _scene.Environment = env;
        SetSun(d, new Vector3(0.5f, -0.4f, -0.6f), new Vector3(1f, 0.8f, 0.6f), 1.6f);

        var stone = _m.Pbr(0x6F6A5F, 0.9f);
        var stoneDark = _m.Pbr(0x575248, 0.92f);
        var moss = _m.Pbr(0x4F6B3A, 0.9f);
        B(_m.Pbr(0x8A8273, 0.9f)).Floor(new Vector3(x0 + length / 2, 0, 0), length, 7.4f, 0.8f);
        B(moss).Floor(new Vector3(x0 + length / 2, -0.01f, -9f), length, 11f);
        B(moss).Floor(new Vector3(x0 + length / 2, -0.01f, 7.5f), length, 8f);
        for (float x = x0; x < x0 + length; x += 3f)
        {
            string area = AreaAt(c, x);
            if (area == "relief")
            {
                // Relief wall: panels with carved bands.
                B(stone).Box(new Vector3(x + 1.5f, 0, -5.5f), new Vector3(3f, 4.5f, 1.2f));
                B(stoneDark).Box(new Vector3(x + 1.5f, 1.2f, -4.88f), new Vector3(2.6f, 0.9f, 0.05f));
                B(stoneDark).Box(new Vector3(x + 1.5f, 2.6f, -4.88f), new Vector3(2.6f, 0.9f, 0.05f));
                B(stone).Box(new Vector3(x + 1.5f, 4.5f, -5.5f), new Vector3(3.2f, 0.4f, 1.4f));
            }
            else if (area == "stupa" || _rng.Chance(0.35f))
            {
                // Borobudur-style bell stupa: base, bell, square harmika, spire.
                var at = new Vector3(x + 1.5f, 0, _rng.Range(-8f, -5f));
                B(stone).Box(at, new Vector3(2.2f, 0.4f, 2.2f));
                B(stone).Sphere(at + new Vector3(0, 0.9f, 0), new Vector3(1f, 1f, 1f), 5, 10);
                B(stoneDark).Box(at + new Vector3(0, 1.8f, 0), new Vector3(0.5f, 0.35f, 0.5f));
                B(stone).Cylinder(at + new Vector3(0, 2.15f, 0), 0.22f, 0.02f, 1f, 6);
            }

            if (_rng.Chance(0.3f))
            {
                B(stone).Box(new Vector3(x, 0, -4.4f), new Vector3(0.6f, 3f, 0.6f));
                if (_rng.Chance(0.5f))
                {
                    d.Fires.Add(new FireEffect(_scene, new Vector3(x, 3f, -4.4f), new Vector3(0.35f, 0.8f, 0.35f), _root) { Style = FireStyle.Torch });
                    Lamp(d, new Vector3(x, 3.5f, -4f), 0xFFA040);
                }
            }
        }

        // The great terraced temple at the end.
        float end = c.Length - 10f;
        for (int t = 0; t < 5; t++)
        {
            B(stone).Box(new Vector3(end + 8f, t * 2.2f, -18f), new Vector3(30f - t * 5f, 2.2f, 14f - t * 2f));
        }

        B(stone).Sphere(new Vector3(end + 8f, 12.5f, -18f), new Vector3(2.5f, 2.6f, 2.5f), 5, 10);
    }

    // ───────────────────────────── Chapter 7: Garuda Hitam's future city ─────────────────────────────
    private void FutureCity(ChapterDef c, float x0, float length, StageDressing d)
    {
        var env = d.Environment;
        env.Sky = SkyMode.Color;
        env.Background = MathHelpers.FromHex(0x06040F);
        env.AmbientColor = new Vector3(0.4f, 0.3f, 0.7f);
        env.AmbientIntensity = 0.1f;
        env.FogColor = new Vector3(0.02f, 0.01f, 0.035f);
        env.FogDensity = 0.012f;
        _scene.Environment = env;
        SetSun(d, new Vector3(0.2f, -1f, -0.3f), new Vector3(0.6f, 0.5f, 1f), 0.6f);

        B(_m.Pbr(0x1B1E27, 0.35f, 0.7f)).Floor(new Vector3(x0 + length / 2, 0, 0), length, 7.4f, 0.5f);
        B(_m.Pbr(0x101219, 0.4f, 0.6f)).Floor(new Vector3(x0 + length / 2, -0.01f, -9f), length, 11f);
        B(_m.Pbr(0x101219, 0.4f, 0.6f)).Floor(new Vector3(x0 + length / 2, -0.01f, 7.5f), length, 8f);
        var redLine = _m.Glow(0xFF1744, 4f);
        var cyanLine = _m.Glow(0x00E5FF, 4f);
        B(redLine).Box(new Vector3(x0 + length / 2, 0.01f, -3.6f), new Vector3(length, 0.02f, 0.06f));
        B(cyanLine).Box(new Vector3(x0 + length / 2, 0.01f, 3.6f), new Vector3(length, 0.02f, 0.06f));

        var tower = _m.Pbr(0x1C1F2B, 0.3f, 0.8f);
        uint[] holo = [0xFF1744, 0x00E5FF, 0xFFD600, 0xD500F9];
        for (float x = x0; x < x0 + length; x += _rng.Range(5f, 9f))
        {
            string area = AreaAt(c, x);
            float h = area == "menara" ? _rng.Range(30f, 50f) : _rng.Range(12f, 35f);
            float w = _rng.Range(3f, 6f);
            float z = _rng.Range(-14f, -6f);
            B(tower).Box(new Vector3(x, 0, z), new Vector3(w, h, w));
            var strip = _m.Glow(holo[_rng.Range(0, holo.Length)], 3.5f);
            for (float y = 2f; y < h; y += _rng.Range(2.5f, 5f))
            {
                B(strip).Box(new Vector3(x, y, z + w / 2 + 0.02f), new Vector3(w * 0.9f, 0.08f, 0.04f));
            }

            if (_rng.Chance(0.35f))
            {
                // Holographic billboard.
                B(_m.Glow(holo[_rng.Range(0, holo.Length)], 2f, 0.6f, true)).Panel(new Vector3(x, h * 0.5f, z + w / 2 + 0.5f), w * 1.2f, 3f);
            }

            if (area == "pabrik" && _rng.Chance(0.5f))
            {
                B(_m.Pbr(0x3A3F4B, 0.4f, 0.9f)).Box(new Vector3(x + 2, 0, -5f), new Vector3(2.5f, 3.5f, 1.5f));
                B(_m.Glow(0xFF1744, 3f)).Box(new Vector3(x + 2, 2.8f, -4.2f), new Vector3(1.8f, 0.15f, 0.05f));
            }
        }

        // The Garuda emblem over the final arena.
        float gx = c.Length - 8f;
        var gold = _m.Pbr(0xC9A227, 0.3f, 1f, 0xFFB300, 0.4f);
        B(gold).Box(new Vector3(gx, 9f, -12f), new Vector3(1.4f, 4f, 0.5f));
        for (int i = 0; i < 6; i++)
        {
            float span = 3f + i * 1.3f;
            B(gold).Box(new Vector3(gx - span * 0.5f - 0.5f, 12f - i * 0.45f, -12f), new Vector3(span, 0.45f, 0.4f), 0.15f);
            B(gold).Box(new Vector3(gx + span * 0.5f + 0.5f, 12f - i * 0.45f, -12f), new Vector3(span, 0.45f, 0.4f), -0.15f);
        }

        for (float x = x0 + 4; x < x0 + length; x += 10f)
        {
            Lamp(d, new Vector3(x, 3f, -3.5f), holo[_rng.Range(0, holo.Length)]);
        }
    }

    private void Foreground(float x0, float length, uint color)
    {
        // Low objects between the camera and the lane give the side view some depth.
        // Kerb and the odd drain cover just in front of the lane: low, so they never hide a fighter.
        B(_m.Pbr(color, 0.9f)).Box(new Vector3(x0 + length / 2, 0, 4.1f), new Vector3(length, 0.16f, 0.4f));
        for (float x = x0; x < x0 + length; x += _rng.Range(6f, 12f))
        {
            B(_m.Pbr(0x1C1C1E, 0.6f, 0.7f)).Box(new Vector3(x, 0, 3.7f), new Vector3(0.7f, 0.03f, 0.35f));
        }
    }
}

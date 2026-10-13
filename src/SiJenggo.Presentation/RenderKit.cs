using System.Numerics;
using ThreeNet;

namespace SiJenggo.Presentation;

/// <summary>Simulation ↔ render space. The sim's +Z (into the screen) is the renderer's -Z, so the camera sits on +Z.</summary>
public static class Space
{
    public static Vector3 ToRender(Vector3 sim) => new(sim.X, sim.Y, -sim.Z);

    /// <summary>Models face +Z (towards the camera); turn them to face along the street, a little towards the viewer.</summary>
    public static Quaternion Facing(float facing, float towardsCamera = 0.28f)
        => Quaternion.CreateFromAxisAngle(Vector3.UnitY, facing >= 0 ? MathF.PI / 2 - towardsCamera : -MathF.PI / 2 + towardsCamera);
}

/// <summary>Material cache keyed by look, so the whole game uses a few dozen materials.</summary>
public sealed class Materials
{
    private readonly Scene _scene;
    private readonly Dictionary<(uint, float, float, uint, float, bool), Material> _cache = [];

    public Materials(Scene scene) => _scene = scene;

    public Material Pbr(uint hex, float roughness = 0.8f, float metallic = 0f, uint emissive = 0, float emissiveIntensity = 0f, bool transparent = false, float alpha = 1f)
    {
        var key = (hex, roughness, metallic, emissive, emissiveIntensity + alpha * 1000, transparent);
        if (_cache.TryGetValue(key, out var m))
        {
            return m;
        }

        var o = MaterialOptions.Pbr(MathHelpers.FromHex(hex, alpha), metallic, roughness);
        o.CullMode = CullMode.None;
        if (emissive != 0)
        {
            var e = MathHelpers.FromHex(emissive);
            o.Emissive = new Vector3(e.X, e.Y, e.Z);
            o.EmissiveIntensity = emissiveIntensity;
        }

        if (transparent)
        {
            o.AlphaMode = AlphaMode.Blend;
            o.DepthWrite = false;
        }

        m = _scene.CreateMaterial(o);
        _cache[key] = m;
        return m;
    }

    /// <summary>Unlit glowing colour (neon, lava, telegraphs). Not cached: callers often animate it.</summary>
    public Material Glow(uint hex, float intensity = 3f, float alpha = 1f, bool additive = false)
    {
        var c = MathHelpers.FromHex(hex, alpha);
        var o = MaterialOptions.Basic(c);
        o.Emissive = new Vector3(c.X, c.Y, c.Z);
        o.EmissiveIntensity = intensity;
        o.CullMode = CullMode.None;
        if (alpha < 1f || additive)
        {
            o.AlphaMode = additive ? AlphaMode.Additive : AlphaMode.Blend;
            o.DepthWrite = false;
        }

        return _scene.CreateMaterial(o);
    }
}

/// <summary>Where the game's assets live (models exported by the Blender pipeline, fonts, etc.).</summary>
public static class AssetPaths
{
    private static string? s_root;

    public static string Root
    {
        get
        {
            if (s_root is not null)
            {
                return s_root;
            }

            // SIJENGGO_ASSETS overrides; else next to the executable, else the repo's assets/ folder.
            var env = Environment.GetEnvironmentVariable("SIJENGGO_ASSETS");
            if (!string.IsNullOrEmpty(env) && Directory.Exists(env))
            {
                return s_root = env;
            }

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "assets");
                if (Directory.Exists(Path.Combine(candidate, "models")))
                {
                    return s_root = candidate;
                }

                dir = dir.Parent;
            }

            return s_root = Path.Combine(AppContext.BaseDirectory, "assets");
        }
    }

    public static string Model(string key) => Path.Combine(Root, "models", key + ".glb");

    public static bool HasModel(string key) => File.Exists(Model(key));
}

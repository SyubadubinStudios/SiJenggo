using System.Numerics;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;
using ThreeNet;

namespace SiJenggo.Presentation.Actors;

/// <summary>
/// Low-poly models built in code for objects that have no exported asset yet (props, weapons, food,
/// projectiles, hazard telegraphs) and as a fallback for any missing character model. Each kind is
/// built once; instances share the geometry and materials.
/// </summary>
public sealed class ProceduralModels
{
    private readonly Scene _scene;
    private readonly Materials _mats;
    private readonly Dictionary<string, List<(Geometry Geometry, Material Material)>> _parts = [];

    public ProceduralModels(Scene scene, Materials mats)
    {
        _scene = scene;
        _mats = mats;
    }

    /// <summary>"prop.TongSampah" → "prop_tong_sampah": the file name the Blender pipeline exports.</summary>
    public static string AssetName(string key)
    {
        var sb = new System.Text.StringBuilder(key.Length + 8);
        foreach (char ch in key)
        {
            if (ch == '.')
            {
                sb.Append('_');
            }
            else if (char.IsUpper(ch))
            {
                if (sb.Length > 0 && sb[^1] != '_')
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Creates an instance of a model under <paramref name="parent"/>: the exported glTF when the asset
    /// pipeline produced one (assets/models), otherwise the cached procedural version.
    /// </summary>
    public Node Instance(string key, Node parent)
    {
        string asset = AssetName(key);
        if (AssetPaths.HasModel(asset))
        {
            var holder = parent.CreateChild(key);
            var model = _scene.LoadModelCached(AssetPaths.Model(asset), holder);
            model.Root.SetShadowsRecursive(true, true);
            return holder;
        }

        if (!_parts.TryGetValue(key, out var parts))
        {
            parts = Build(key);
            _parts[key] = parts;
        }

        var root = parent.CreateChild(key);
        foreach (var (g, m) in parts)
        {
            var child = root.CreateChild();
            child.AttachMesh(g, m);
            child.CastShadow = true;
            child.ReceiveShadow = true;
        }

        return root;
    }

    public static string PropKey(PropKind kind) => "prop." + kind;
    public static string PickupKey(PickupKind kind, WeaponId weapon) => kind == PickupKind.Weapon ? "weapon." + weapon : "pickup." + kind;
    public static string ProjectileKey(ProjectileKind kind, WeaponId weapon) => kind == ProjectileKind.Weapon ? "weapon." + weapon : "projectile." + kind;

    private List<(Geometry, Material)> Build(string key)
    {
        var parts = new Dictionary<Material, MeshBuilder>();
        MeshBuilder B(Material m)
        {
            if (!parts.TryGetValue(m, out var b))
            {
                b = new MeshBuilder();
                parts[m] = b;
            }

            return b;
        }

        var m = _mats;
        switch (key)
        {
            // ── props ──
            case "prop.Kursi":
            {
                var red = m.Pbr(0xC8231E, 0.45f);
                B(red).Cylinder(new(0, 0.42f, 0), 0.22f, 0.22f, 0.05f, 14);
                foreach (var (x, z) in new[] { (-0.14f, -0.14f), (0.14f, -0.14f), (-0.14f, 0.14f), (0.14f, 0.14f) })
                {
                    B(red).Cylinder(new(x, 0, z), 0.025f, 0.03f, 0.43f, 6);
                }

                break;
            }

            case "prop.Motor":
            {
                var body = m.Pbr(0xB3261E, 0.35f, 0.4f);
                var dark = m.Pbr(0x1B1B1F, 0.6f);
                var chrome = m.Pbr(0xC9CCD2, 0.2f, 1f);
                B(body).Box(new(0, 0.42f, 0), new(1.1f, 0.32f, 0.3f));
                B(dark).Box(new(-0.15f, 0.74f, 0), new(0.55f, 0.1f, 0.28f));
                B(body).Box(new(0.48f, 0.55f, 0), new(0.18f, 0.45f, 0.22f));
                B(chrome).Box(new(0.55f, 1.0f, 0), new(0.06f, 0.06f, 0.6f));
                foreach (float x in new[] { -0.55f, 0.6f })
                {
                    B(dark).Cylinder(new(x, 0.3f, -0.07f), 0.3f, 0.3f, 0.14f, 14);
                }

                break;
            }

            case "prop.Gerobak":
            {
                var wood = m.Pbr(0x2AA198, 0.6f);
                var glass = m.Pbr(0xBFE6FF, 0.1f, 0f, transparent: true, alpha: 0.35f);
                var roof = m.Pbr(0xF2B705, 0.7f);
                var dark = m.Pbr(0x1B1B1F, 0.6f);
                B(wood).Box(new(0, 0.55f, 0), new(1.5f, 0.55f, 0.8f));
                B(glass).Box(new(0, 1.1f, 0), new(1.3f, 0.5f, 0.6f));
                B(roof).Box(new(0, 1.75f, 0), new(1.8f, 0.06f, 1.0f));
                foreach (float x in new[] { -0.72f, 0.72f })
                {
                    B(wood).Box(new(x, 1.1f, 0.35f), new(0.06f, 0.65f, 0.06f));
                    B(wood).Box(new(x, 1.1f, -0.35f), new(0.06f, 0.65f, 0.06f));
                }

                B(dark).Cylinder(new(-0.35f, 0.32f, -0.48f), 0.32f, 0.32f, 0.08f, 14);
                B(dark).Cylinder(new(-0.35f, 0.32f, 0.4f), 0.32f, 0.32f, 0.08f, 14);
                break;
            }

            case "prop.TongSampah":
            {
                var blue = m.Pbr(0x2B5BA8, 0.5f, 0.5f);
                B(blue).Cylinder(new(0, 0, 0), 0.3f, 0.33f, 0.82f, 14);
                B(m.Pbr(0x1E3F78, 0.5f, 0.5f)).Cylinder(new(0, 0.82f, 0), 0.35f, 0.3f, 0.08f, 14);
                break;
            }

            case "prop.Bambu":
            {
                var green = m.Pbr(0x7FA33A, 0.5f);
                var node = m.Pbr(0x5C7A26, 0.6f);
                for (int i = 0; i < 3; i++)
                {
                    B(green).Box(new(0, 0.08f + i * 0.13f, -0.12f + i * 0.12f), new(2.4f, 0.11f, 0.11f));
                    B(node).Box(new(-0.6f, 0.07f + i * 0.13f, -0.12f + i * 0.12f), new(0.05f, 0.13f, 0.13f));
                    B(node).Box(new(0.6f, 0.07f + i * 0.13f, -0.12f + i * 0.12f), new(0.05f, 0.13f, 0.13f));
                }

                break;
            }

            case "prop.Pagar":
            {
                var metal = m.Pbr(0x6B6F75, 0.5f, 0.8f);
                for (int i = -2; i <= 2; i++)
                {
                    B(metal).Box(new(i * 0.32f, 0, 0), new(0.05f, 1.1f, 0.05f));
                }

                B(metal).Box(new(0, 0.95f, 0), new(1.4f, 0.06f, 0.05f));
                B(metal).Box(new(0, 0.3f, 0), new(1.4f, 0.06f, 0.05f));
                break;
            }

            case "prop.PetiKayu":
            {
                var wood = m.Pbr(0x9A6B3D, 0.8f);
                var dark = m.Pbr(0x6E4826, 0.85f);
                B(wood).Box(Vector3.Zero, new(0.8f, 0.8f, 0.8f));
                B(dark).Box(new(0, 0, 0.405f), new(0.82f, 0.1f, 0.02f));
                B(dark).Box(new(0, 0.7f, 0.405f), new(0.82f, 0.1f, 0.02f));
                B(dark).Box(new(0, 0.05f, 0.405f), new(0.1f, 0.75f, 0.02f), 0);
                break;
            }

            case "prop.BatuBesar":
                B(m.Pbr(0x6D6A64, 0.95f)).Sphere(new(0, 0.55f, 0), new(0.8f, 0.6f, 0.7f), 4, 7);
                B(m.Pbr(0x5A5853, 0.95f)).Sphere(new(0.35f, 0.3f, 0.2f), new(0.4f, 0.35f, 0.4f), 3, 6);
                break;

            // ── weapons (held along +Y from the grip) ──
            case "weapon.Golok":
                Blade(B, m.Pbr(0xC9CCD2, 0.25f, 1f), m.Pbr(0x5C3A1E, 0.7f), 0.5f, 0.07f);
                break;
            case "weapon.Keris":
                Blade(B, m.Pbr(0x8C8F99, 0.3f, 1f), m.Pbr(0xC79A2E, 0.4f, 0.8f), 0.38f, 0.035f);
                break;
            case "weapon.Kujang":
                Blade(B, m.Pbr(0xB8BCC4, 0.25f, 1f), m.Pbr(0x3B2414, 0.7f), 0.32f, 0.06f);
                B(m.Pbr(0xB8BCC4, 0.25f, 1f)).Box(new(0.05f, 0.3f, 0), new(0.1f, 0.04f, 0.012f));
                break;
            case "weapon.Celurit":
                Blade(B, m.Pbr(0xA0A4AC, 0.3f, 1f), m.Pbr(0x4A2C17, 0.7f), 0.22f, 0.03f);
                B(m.Pbr(0xA0A4AC, 0.3f, 1f)).Box(new(0.12f, 0.36f, 0), new(0.26f, 0.03f, 0.012f), 0);
                break;
            case "weapon.Rencong":
                Blade(B, m.Pbr(0xB0B3BA, 0.25f, 1f), m.Pbr(0xE8DCC0, 0.5f), 0.3f, 0.03f);
                break;
            case "weapon.TombakDayak":
                B(m.Pbr(0x6B4423, 0.7f)).Cylinder(new(0, -0.6f, 0), 0.02f, 0.02f, 1.8f, 6);
                B(m.Pbr(0xC9CCD2, 0.25f, 1f)).Cylinder(new(0, 1.2f, 0), 0.05f, 0f, 0.3f, 6);
                B(m.Pbr(0xC0392B, 0.6f)).Box(new(0, 1.1f, 0), new(0.06f, 0.08f, 0.06f));
                break;
            case "weapon.Mandau":
                Blade(B, m.Pbr(0xC9CCD2, 0.25f, 1f), m.Pbr(0xEDE3C8, 0.5f), 0.62f, 0.08f);
                B(m.Pbr(0xC0392B, 0.6f)).Box(new(0, 0.02f, 0), new(0.05f, 0.05f, 0.05f));
                break;
            case "weapon.PipaBesi":
                B(m.Pbr(0x7D8189, 0.4f, 0.9f)).Cylinder(new(0, -0.15f, 0), 0.03f, 0.03f, 0.95f, 8);
                break;

            // ── food and loot ──
            case "pickup.NasiPadang":
                B(m.Pbr(0x3E8E2E, 0.6f)).Box(new(0, 0.02f, 0), new(0.5f, 0.03f, 0.4f));
                B(m.Pbr(0xF4F1E8, 0.7f)).Sphere(new(-0.05f, 0.08f, 0), new(0.16f, 0.1f, 0.14f));
                B(m.Pbr(0x7A2E12, 0.5f)).Sphere(new(0.14f, 0.07f, 0.05f), new(0.08f, 0.05f, 0.07f));
                B(m.Pbr(0xC9461A, 0.5f)).Sphere(new(0.12f, 0.07f, -0.1f), new(0.06f, 0.04f, 0.06f));
                break;
            case "pickup.Sate":
                for (int i = 0; i < 3; i++)
                {
                    B(m.Pbr(0xC9A472, 0.7f)).Box(new(0, 0.04f, -0.08f + i * 0.08f), new(0.5f, 0.012f, 0.012f));
                    for (int j = 0; j < 4; j++)
                    {
                        B(m.Pbr(0x6B2E14, 0.6f)).Sphere(new(-0.12f + j * 0.07f, 0.05f, -0.08f + i * 0.08f), new(0.03f, 0.03f, 0.03f), 3, 5);
                    }
                }

                break;
            case "pickup.EsTeh":
                B(m.Pbr(0xB5651D, 0.1f, 0f, transparent: true, alpha: 0.8f)).Cylinder(Vector3.Zero, 0.08f, 0.1f, 0.3f, 10);
                B(m.Pbr(0xFFFFFF, 0.1f, 0f, transparent: true, alpha: 0.5f)).Cylinder(new(0, 0.2f, 0), 0.09f, 0.095f, 0.06f, 10, false);
                break;
            case "pickup.KopiTubruk":
                B(m.Pbr(0xF2F2F2, 0.3f)).Cylinder(Vector3.Zero, 0.09f, 0.1f, 0.14f, 10);
                B(m.Pbr(0x2B170C, 0.4f)).Disc(new(0, 0.13f, 0), 0.09f, 10, Vector3.UnitY);
                B(m.Glow(0xFF6A00, 1.5f)).Sphere(new(0, 0.3f, 0), new(0.04f, 0.04f, 0.04f), 3, 5);
                break;
            case "pickup.Uang":
                B(m.Pbr(0x2E8B57, 0.6f)).Box(Vector3.Zero, new(0.32f, 0.06f, 0.16f));
                B(m.Pbr(0xC0392B, 0.6f)).Box(new(0.08f, 0.06f, 0), new(0.32f, 0.04f, 0.16f), 0.4f);
                break;

            // ── projectiles ──
            case "projectile.Bottle":
                B(m.Pbr(0x2E7D32, 0.1f, 0f, transparent: true, alpha: 0.8f)).Cylinder(new(0, -0.15f, 0), 0.05f, 0.05f, 0.22f, 8);
                B(m.Pbr(0x2E7D32, 0.1f, 0f, transparent: true, alpha: 0.8f)).Cylinder(new(0, 0.07f, 0), 0.05f, 0.02f, 0.1f, 8);
                break;
            case "projectile.Harpoon":
                B(m.Pbr(0x5D4037, 0.7f)).Box(Vector3.Zero, new(1.2f, 0.05f, 0.05f));
                B(m.Pbr(0xC9CCD2, 0.25f, 1f)).Box(new(0.65f, -0.02f, 0), new(0.18f, 0.09f, 0.03f), 0.785f);
                break;
            case "projectile.WindBlade":
                B(m.Glow(0x7FFFD4, 4f, 0.7f, true)).Box(Vector3.Zero, new(0.2f, 1.2f, 0.1f));
                break;
            case "projectile.Shuriken":
                B(m.Pbr(0x2B2B2B, 0.3f, 1f)).Box(Vector3.Zero, new(0.3f, 0.02f, 0.08f));
                B(m.Pbr(0x2B2B2B, 0.3f, 1f)).Box(Vector3.Zero, new(0.08f, 0.02f, 0.3f));
                break;
            case "projectile.Gadget":
                B(m.Glow(0x4FC3F7, 6f)).Sphere(Vector3.Zero, new(0.12f, 0.12f, 0.12f));
                break;
            case "projectile.Laser":
                B(m.Glow(0xFF1744, 8f, 0.9f, true)).Box(new(0, -0.05f, 0), new(1.6f, 0.1f, 0.1f));
                break;
            case "projectile.Boulder":
                B(m.Pbr(0x4E3B30, 0.95f, 0f, 0xFF5722, 1.5f)).Sphere(Vector3.Zero, new(0.45f, 0.4f, 0.42f), 4, 7);
                break;
            case "projectile.Peci":
                B(m.Pbr(0x111111, 0.6f)).Cylinder(new(0, -0.05f, 0), 0.13f, 0.12f, 0.1f, 12);
                break;

            default:
                B(m.Pbr(0xFF00FF)).Box(Vector3.Zero, new(0.4f, 0.4f, 0.4f));
                break;
        }

        var list = new List<(Geometry, Material)>();
        foreach (var (mat, builder) in parts)
        {
            if (!builder.IsEmpty)
            {
                list.Add((builder.Build(_scene), mat));
            }
        }

        return list;
    }

    private static void Blade(Func<Material, MeshBuilder> b, Material steel, Material grip, float length, float width)
    {
        b(grip).Cylinder(new(0, -0.14f, 0), 0.022f, 0.025f, 0.14f, 6);
        b(steel).Box(new(0, 0f, 0), new(width, length, 0.012f));
        b(steel).Cylinder(new(0, length, 0), width * 0.5f, 0f, width * 1.2f, 4, false);
    }

    /// <summary>A crude but readable humanoid for any fighter whose model has not been exported yet.</summary>
    public Node Mannequin(Node parent, uint shirt, uint pants, uint skin, float scale)
    {
        var root = parent.CreateChild("mannequin");
        var b = new Dictionary<Material, MeshBuilder>();
        void Add(Material mat, Action<MeshBuilder> draw)
        {
            if (!b.TryGetValue(mat, out var mb))
            {
                mb = new MeshBuilder();
                b[mat] = mb;
            }

            draw(mb);
        }

        var s = _mats.Pbr(shirt, 0.7f);
        var p = _mats.Pbr(pants, 0.8f);
        var k = _mats.Pbr(skin, 0.6f);
        Add(p, mb => { mb.Box(new(-0.1f, 0, 0), new(0.16f, 0.9f, 0.2f)); mb.Box(new(0.1f, 0, 0), new(0.16f, 0.9f, 0.2f)); });
        Add(s, mb => { mb.Box(new(0, 0.88f, 0), new(0.48f, 0.62f, 0.26f)); mb.Box(new(-0.31f, 0.95f, 0), new(0.13f, 0.5f, 0.13f)); mb.Box(new(0.31f, 0.95f, 0), new(0.13f, 0.5f, 0.13f)); });
        Add(k, mb => { mb.Sphere(new(0, 1.66f, 0), new(0.13f, 0.15f, 0.14f)); mb.Box(new(-0.31f, 0.8f, 0), new(0.1f, 0.14f, 0.1f)); mb.Box(new(0.31f, 0.8f, 0), new(0.1f, 0.14f, 0.1f)); });
        foreach (var (mat, mb) in b)
        {
            var n = root.CreateChild();
            n.AttachMesh(mb.Build(_scene), mat);
            n.CastShadow = true;
        }

        root.Scale = new Vector3(scale);
        return root;
    }
}

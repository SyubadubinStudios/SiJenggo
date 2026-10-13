using System.Numerics;
using ThreeNet;
using ThreeNet.Interop;

namespace SiJenggo.Presentation;

/// <summary>
/// Accumulates many primitive shapes into one vertex/index buffer so a whole street of buildings,
/// windows or lamp posts in one colour becomes a single draw call (the integrated-GPU budget matters).
/// </summary>
public sealed class MeshBuilder
{
    private readonly List<Vertex> _vertices = new(1024);
    private readonly List<uint> _indices = new(2048);

    public int VertexCount => _vertices.Count;
    public bool IsEmpty => _vertices.Count == 0;

    public void Clear()
    {
        _vertices.Clear();
        _indices.Clear();
    }

    private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 uvScale)
    {
        uint i = (uint)_vertices.Count;
        _vertices.Add(new Vertex(a, normal, new Vector2(0, uvScale.Y)));
        _vertices.Add(new Vertex(b, normal, new Vector2(uvScale.X, uvScale.Y)));
        _vertices.Add(new Vertex(c, normal, new Vector2(uvScale.X, 0)));
        _vertices.Add(new Vertex(d, normal, new Vector2(0, 0)));
        _indices.Add(i);
        _indices.Add(i + 1);
        _indices.Add(i + 2);
        _indices.Add(i);
        _indices.Add(i + 2);
        _indices.Add(i + 3);
    }

    /// <summary>Axis-aligned box (optionally rotated about Y) with its base centre at <paramref name="baseCentre"/>.</summary>
    public void Box(Vector3 baseCentre, Vector3 size, float yaw = 0, float uvPerMetre = 0.5f)
    {
        var rot = Matrix4x4.CreateRotationY(yaw);
        var h = size / 2;
        Vector3 P(float x, float y, float z) => baseCentre + Vector3.Transform(new Vector3(x * h.X, y * size.Y, z * h.Z), rot);
        Vector3 N(Vector3 n) => Vector3.TransformNormal(n, rot);
        var uvX = new Vector2(size.X, size.Y) * uvPerMetre;
        var uvZ = new Vector2(size.Z, size.Y) * uvPerMetre;
        var uvY = new Vector2(size.X, size.Z) * uvPerMetre;
        Quad(P(-1, 0, 1), P(1, 0, 1), P(1, 1, 1), P(-1, 1, 1), N(Vector3.UnitZ), uvX);    // front (+Z)
        Quad(P(1, 0, -1), P(-1, 0, -1), P(-1, 1, -1), P(1, 1, -1), N(-Vector3.UnitZ), uvX); // back
        Quad(P(1, 0, 1), P(1, 0, -1), P(1, 1, -1), P(1, 1, 1), N(Vector3.UnitX), uvZ);     // right
        Quad(P(-1, 0, -1), P(-1, 0, 1), P(-1, 1, 1), P(-1, 1, -1), N(-Vector3.UnitX), uvZ); // left
        Quad(P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1), N(Vector3.UnitY), uvY);   // top
        Quad(P(-1, 0, -1), P(1, 0, -1), P(1, 0, 1), P(-1, 0, 1), N(-Vector3.UnitY), uvY);  // bottom
    }

    /// <summary>Upright cylinder or cone (topRadius 0) with its base centre at <paramref name="baseCentre"/>.</summary>
    public void Cylinder(Vector3 baseCentre, float bottomRadius, float topRadius, float height, int segments = 10, bool caps = true)
    {
        uint start = (uint)_vertices.Count;
        float slope = (bottomRadius - topRadius) / Math.Max(0.001f, height);
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * MathF.Tau;
            var dir = new Vector3(MathF.Cos(a), 0, MathF.Sin(a));
            var n = Vector3.Normalize(new Vector3(dir.X, slope, dir.Z));
            _vertices.Add(new Vertex(baseCentre + dir * bottomRadius, n, new Vector2(i / (float)segments, 1)));
            _vertices.Add(new Vertex(baseCentre + dir * topRadius + new Vector3(0, height, 0), n, new Vector2(i / (float)segments, 0)));
        }

        for (int i = 0; i < segments; i++)
        {
            uint b = start + (uint)(i * 2);
            _indices.Add(b);
            _indices.Add(b + 1);
            _indices.Add(b + 3);
            _indices.Add(b);
            _indices.Add(b + 3);
            _indices.Add(b + 2);
        }

        if (!caps)
        {
            return;
        }

        if (topRadius > 0)
        {
            Disc(baseCentre + new Vector3(0, height, 0), topRadius, segments, Vector3.UnitY);
        }

        Disc(baseCentre, bottomRadius, segments, -Vector3.UnitY);
    }

    public void Disc(Vector3 centre, float radius, int segments, Vector3 normal)
    {
        uint c = (uint)_vertices.Count;
        _vertices.Add(new Vertex(centre, normal, new Vector2(0.5f, 0.5f)));
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * MathF.Tau;
            _vertices.Add(new Vertex(centre + new Vector3(MathF.Cos(a), 0, MathF.Sin(a)) * radius, normal, new Vector2(0.5f + MathF.Cos(a) * 0.5f, 0.5f + MathF.Sin(a) * 0.5f)));
        }

        bool up = normal.Y > 0;
        for (int i = 0; i < segments; i++)
        {
            _indices.Add(c);
            _indices.Add(up ? c + (uint)i + 2 : c + (uint)i + 1);
            _indices.Add(up ? c + (uint)i + 1 : c + (uint)i + 2);
        }
    }

    /// <summary>Low-poly sphere, squashed by <paramref name="scale"/>.</summary>
    public void Sphere(Vector3 centre, Vector3 scale, int rings = 6, int segments = 10)
    {
        uint start = (uint)_vertices.Count;
        for (int r = 0; r <= rings; r++)
        {
            float v = r / (float)rings;
            float phi = v * MathF.PI;
            for (int s = 0; s <= segments; s++)
            {
                float u = s / (float)segments;
                float theta = u * MathF.Tau;
                var n = new Vector3(MathF.Sin(phi) * MathF.Cos(theta), MathF.Cos(phi), MathF.Sin(phi) * MathF.Sin(theta));
                _vertices.Add(new Vertex(centre + n * scale, Vector3.Normalize(n / scale), new Vector2(u, v)));
            }
        }

        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                uint a = start + (uint)(r * (segments + 1) + s);
                uint b = a + (uint)segments + 1;
                _indices.Add(a);
                _indices.Add(a + 1);
                _indices.Add(b);
                _indices.Add(a + 1);
                _indices.Add(b + 1);
                _indices.Add(b);
            }
        }
    }

    /// <summary>Flat quad facing +Z (a sign, a billboard) with its bottom edge centre at <paramref name="baseCentre"/>.</summary>
    public void Panel(Vector3 baseCentre, float width, float height, float yaw = 0)
    {
        var rot = Matrix4x4.CreateRotationY(yaw);
        Vector3 P(float x, float y) => baseCentre + Vector3.Transform(new Vector3(x * width / 2, y * height, 0), rot);
        var n = Vector3.TransformNormal(Vector3.UnitZ, rot);
        Quad(P(-1, 0), P(1, 0), P(1, 1), P(-1, 1), n, Vector2.One);
    }

    /// <summary>Ground strip lying flat (Y up).</summary>
    public void Floor(Vector3 centre, float width, float depth, float uvPerMetre = 0.25f)
    {
        var h = new Vector3(width / 2, 0, depth / 2);
        Quad(centre + new Vector3(-h.X, 0, h.Z), centre + new Vector3(h.X, 0, h.Z), centre + new Vector3(h.X, 0, -h.Z), centre + new Vector3(-h.X, 0, -h.Z), Vector3.UnitY, new Vector2(width, depth) * uvPerMetre);
    }

    public Geometry Build(Scene scene)
    {
        var geometry = scene.CreateGeometry(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_vertices), System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_indices));
        return geometry;
    }

    /// <summary>Builds and adds the mesh as one node; returns null when nothing was added.</summary>
    public Node? AddTo(Scene scene, Material material, Node? parent = null, string? name = null, bool castShadow = true)
    {
        if (IsEmpty)
        {
            return null;
        }

        var node = scene.AddMesh(Build(scene), material, parent, name);
        node.CastShadow = castShadow;
        node.ReceiveShadow = true;
        return node;
    }
}

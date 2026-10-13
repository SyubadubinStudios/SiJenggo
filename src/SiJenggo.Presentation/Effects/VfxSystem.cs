using System.Numerics;
using SiJenggo.Core;
using SiJenggo.Core.Ecs;
using SiJenggo.Game;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;
using ThreeNet;
using ThreeNet.Effects;

namespace SiJenggo.Presentation.Effects;

/// <summary>One CPU particle system = one draw call. A handful of shared systems cover every effect.</summary>
internal sealed class ParticleChannel
{
    public required ParticleEffect Effect;
    public required Geometry Geometry;
    public required Node Node;
    public bool Dirty = true;

    public void Emit(Vector3 at, int count, Vector3? direction = null)
    {
        Effect.Origin = at;
        if (direction is { } d)
        {
            Effect.Direction = d;
        }

        Effect.Burst(count);
        Dirty = true;
    }
}

/// <summary>Expanding glowing ring for shockwaves, slams and phase changes.</summary>
internal sealed class Ring
{
    public required Node Node;
    public required Material Material;
    public float Age;
    public float Life;
    public float Radius;
    public Vector3 Color;
    public bool Active;
}

/// <summary>
/// Turns gameplay events into spectacle: hit sparks per <see cref="HitFx"/>, slash flashes,
/// shockwave rings, telegraphed hazard markers, rage auras and the chapter's weather.
/// Everything is pooled; nothing is allocated per hit.
/// </summary>
public sealed class VfxSystem : IDisposable
{
    private readonly Scene _scene;
    private readonly Materials _mats;
    private readonly Node _root;
    private readonly ParticleChannel _sparks, _slash, _fire, _electric, _dust, _aura, _weather;
    private readonly Ring[] _rings = new Ring[10];
    private readonly ParticleChannel[] _channels;
    private readonly Dictionary<int, HazardView> _hazards = [];
    private readonly List<int> _stale = [];
    private readonly Geometry _disc, _bolt, _rock, _crate, _trunk, _boar, _spike;
    private readonly Node[] _flashLights = new Node[2];
    private readonly float[] _flashLife = new float[2];
    private int _flashNext;
    private DeterministicRandom _rng = new(99);
    private WeatherKind _weather0;
    private float _time;

    private sealed class HazardView
    {
        public required Entity Entity;
        public required Node Root;
        public required Node Marker;
        public required Material MarkerMaterial;
        public Node? Body;
        public HazardKind Kind;
        public bool Seen;
    }

    public VfxSystem(Scene scene, Materials mats)
    {
        _scene = scene;
        _mats = mats;
        _root = scene.CreateNode(null, "vfx");
        var glow = scene.CreateShader(EffectShaders.Particle, ShaderLanguage.Wgsl, "glow");
        var smoke = scene.CreateShader(EffectShaders.Smoke, ShaderLanguage.Wgsl, "smoke");

        _sparks = Channel(600, ParticleEffect.GlowMaterial(glow, new(1f, 0.85f, 0.4f), new(1f, 0.25f, 0.05f), 3.2f), e =>
        {
            e.Lifetime = 0.35f; e.Size = 0.07f; e.Shape = EmitterShape.Sphere; e.EmitterRadius = 0.05f; e.Spread = 1f; e.Rise = 0;
            e.Gravity = new(0, -12, 0); e.Drag = 2f; e.StretchBySpeed = 0.04f; e.Radial = true; e.Variation = 0.5f;
        });
        _slash = Channel(300, ParticleEffect.GlowMaterial(glow, new(0.85f, 0.95f, 1f), new(0.3f, 0.6f, 1f), 3.5f), e =>
        {
            e.Lifetime = 0.22f; e.Size = 0.12f; e.Shape = EmitterShape.Disc; e.EmitterRadius = 0.5f; e.Spread = 0.3f; e.Gravity = Vector3.Zero; e.Drag = 6f; e.StretchBySpeed = 0.08f;
        });
        _fire = Channel(500, ParticleEffect.GlowMaterial(glow, new(1f, 0.75f, 0.2f), new(0.9f, 0.1f, 0.0f), 3f), e =>
        {
            e.Lifetime = 0.6f; e.Size = 0.22f; e.Growth = 0.4f; e.Shape = EmitterShape.Sphere; e.EmitterRadius = 0.25f; e.Spread = 0.6f; e.Rise = 2.5f; e.Gravity = new(0, 2, 0);
            e.Turbulence = 2f; e.TurbulenceScale = 1.5f; e.Drag = 1.5f;
        });
        _electric = Channel(300, ParticleEffect.GlowMaterial(glow, new(0.85f, 0.9f, 1f), new(0.3f, 0.5f, 1f), 4.5f), e =>
        {
            e.Lifetime = 0.25f; e.Size = 0.06f; e.Shape = EmitterShape.Sphere; e.EmitterRadius = 0.3f; e.Spread = 1f; e.Gravity = Vector3.Zero; e.Turbulence = 8f; e.TurbulenceScale = 4f; e.StretchBySpeed = 0.05f;
        });
        _dust = Channel(400, ParticleEffect.SmokeMaterial(smoke, new(0.55f, 0.5f, 0.45f), 0.45f), e =>
        {
            e.Lifetime = 0.8f; e.Size = 0.35f; e.Growth = 1.2f; e.Shape = EmitterShape.Disc; e.EmitterRadius = 0.5f; e.Spread = 0.4f; e.Rise = 0.6f; e.Gravity = new(0, 0.3f, 0); e.Drag = 2.5f;
            e.FloorHeight = 0f;
        });
        _aura = Channel(500, ParticleEffect.GlowMaterial(glow, new(1f, 0.82f, 0.25f), new(0.85f, 0.05f, 0.02f), 2.6f), e =>
        {
            e.Lifetime = 0.7f; e.Size = 0.18f; e.Growth = -0.1f; e.Shape = EmitterShape.Ring; e.EmitterRadius = 0.45f; e.Spread = 0.15f; e.Rise = 2.2f;
            e.Turbulence = 1.6f; e.TurbulenceScale = 2f; e.Gravity = new(0, 1, 0);
        });
        _weather = Channel(1800, ParticleEffect.GlowMaterial(glow, new(0.7f, 0.75f, 0.85f), new(0.5f, 0.55f, 0.65f), 0.35f), e =>
        {
            e.Shape = EmitterShape.Box; e.EmitterSize = new(30, 0.5f, 14); e.Rate = 0;
        });

        _channels = [_sparks, _slash, _fire, _electric, _dust, _aura, _weather];
        var ringGeometry = scene.CreateTorusGeometry(1f, 0.025f, 5, 48);
        for (int i = 0; i < _rings.Length; i++)
        {
            var mat = mats.Glow(0xFFD27F, 3f, 0.9f, additive: true);
            var node = scene.AddMesh(ringGeometry, mat, _root, "ring");
            node.Visible = false;
            node.CastShadow = false;
            _rings[i] = new Ring { Node = node, Material = mat };
        }

        var mb = new MeshBuilder();
        mb.Cylinder(Vector3.Zero, 1f, 1f, 0.02f, 32);
        _disc = mb.Build(scene);
        mb.Clear();
        mb.Box(Vector3.Zero, new Vector3(0.18f, 14f, 0.18f));
        _bolt = mb.Build(scene);
        mb.Clear();
        mb.Sphere(Vector3.Zero, new Vector3(0.7f, 0.6f, 0.65f), 4, 7);
        _rock = mb.Build(scene);
        mb.Clear();
        mb.Box(new Vector3(0, -0.6f, 0), new Vector3(1.4f, 1.2f, 1.4f));
        _crate = mb.Build(scene);
        mb.Clear();
        mb.Cylinder(Vector3.Zero, 0.45f, 0.35f, 9f, 9);
        mb.Sphere(new Vector3(0, 9f, 0), new Vector3(2.2f, 1.6f, 2.2f), 4, 8);
        _trunk = mb.Build(scene);
        mb.Clear();
        mb.Box(new Vector3(0, 0.35f, 0), new Vector3(1.3f, 0.7f, 0.6f));
        mb.Box(new Vector3(0.75f, 0.45f, 0), new Vector3(0.45f, 0.45f, 0.45f));
        mb.Box(new Vector3(0.98f, 0.38f, 0.12f), new Vector3(0.12f, 0.06f, 0.06f));
        mb.Box(new Vector3(0.98f, 0.38f, -0.12f), new Vector3(0.12f, 0.06f, 0.06f));
        foreach (var (x, z) in new[] { (-0.45f, 0.2f), (0.45f, 0.2f), (-0.45f, -0.2f), (0.45f, -0.2f) })
        {
            mb.Box(new Vector3(x, 0, z), new Vector3(0.12f, 0.35f, 0.12f));
        }

        _boar = mb.Build(scene);
        mb.Clear();
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * MathF.Tau;
            mb.Cylinder(new Vector3(MathF.Cos(a) * 0.5f, 0, MathF.Sin(a) * 0.5f), 0.08f, 0f, 1.4f, 5);
        }

        _spike = mb.Build(scene);

        for (int i = 0; i < _flashLights.Length; i++)
        {
            _flashLights[i] = scene.AddLight(Light.Point(new Vector3(1f, 0.8f, 0.5f), 0f, 8f), _root, "flash");
        }
    }

    private ParticleChannel Channel(int capacity, MaterialOptions material, Action<ParticleEffect> setup)
    {
        var effect = new ParticleEffect(capacity, capacity * 7 + _rng.Range(0, 1000));
        setup(effect);
        var geometry = effect.CreateGeometry(_scene);
        var node = _scene.AddMesh(geometry, _scene.CreateMaterial(material), _root, "particles");
        node.CastShadow = false;
        node.ReceiveShadow = false;
        return new ParticleChannel { Effect = effect, Geometry = geometry, Node = node };
    }

    /// <summary>Sets up the chapter's weather emitter (rain, ash, mist, spray, neon dust).</summary>
    public void SetWeather(WeatherKind weather, int quality)
    {
        _weather0 = weather;
        var e = _weather.Effect;
        e.Clear();
        float q = quality switch { 0 => 0.35f, 1 => 0.65f, _ => 1f };
        switch (weather)
        {
            case WeatherKind.TropicalRain or WeatherKind.Thunderstorm:
                e.Rate = (weather == WeatherKind.Thunderstorm ? 900 : 600) * q;
                e.Lifetime = 0.9f; e.Size = 0.025f; e.Direction = new Vector3(0.15f, -1, 0); e.Spread = 0.02f; e.Gravity = new(0, -30, 0);
                e.StretchBySpeed = 0.06f; e.Rise = 0; e.Variation = 0.3f;
                break;
            case WeatherKind.VolcanicAsh:
                e.Rate = 160 * q; e.Lifetime = 4f; e.Size = 0.06f; e.Direction = new Vector3(0.2f, -1, 0); e.Spread = 0.4f; e.Gravity = new(0, -0.8f, 0);
                e.Turbulence = 0.6f; e.StretchBySpeed = 0; e.Wind = new Vector3(0.8f, 0, 0); e.WindInfluence = 1f;
                break;
            case WeatherKind.Mist:
                e.Rate = 60 * q; e.Lifetime = 5f; e.Size = 0.08f; e.Direction = new Vector3(1, 0.05f, 0); e.Spread = 0.6f; e.Gravity = new(0, -0.05f, 0);
                e.Turbulence = 0.4f; e.StretchBySpeed = 0;
                break;
            case WeatherKind.NightNeon:
                e.Rate = 40 * q; e.Lifetime = 5f; e.Size = 0.035f; e.Direction = new Vector3(0, 0.2f, 0); e.Spread = 1f; e.Gravity = new(0, -0.02f, 0);
                e.Turbulence = 0.5f; e.StretchBySpeed = 0;
                break;
            default:
                e.Rate = 0;
                break;
        }
    }

    public void Handle(in GameEvent ev, World world)
    {
        var at = Space.ToRender(ev.Position);
        switch (ev.Kind)
        {
            case GameEventKind.Hit:
            {
                int count = (int)Math.Clamp(6 + ev.Value * 1.2f, 6, 40);
                switch (ev.Fx)
                {
                    case HitFx.Slash:
                        _slash.Emit(at, count, new Vector3(_rng.Range(-1f, 1f), _rng.Range(-0.3f, 0.6f), 0));
                        _sparks.Emit(at, count / 2);
                        break;
                    case HitFx.Fire:
                        _fire.Emit(at, count);
                        _sparks.Emit(at, count / 3);
                        break;
                    case HitFx.Lightning or HitFx.Gadget:
                        _electric.Emit(at, count + 10);
                        Flash(at, new Vector3(0.6f, 0.75f, 1f), 9f);
                        break;
                    case HitFx.Wind:
                        _slash.Emit(at, count, Vector3.UnitY);
                        _dust.Emit(at with { Y = 0.1f }, 4);
                        break;
                    case HitFx.Shockwave:
                        _sparks.Emit(at, count);
                        Ring(at with { Y = 0.05f }, 2.5f, 0.35f, 0xFFD27F);
                        _dust.Emit(at with { Y = 0.1f }, 10);
                        break;
                    case HitFx.Heavy:
                        _sparks.Emit(at, count + 8);
                        Ring(at, 0.9f, 0.18f, 0xFFF3C4);
                        Flash(at, new Vector3(1f, 0.7f, 0.4f), 6f);
                        break;
                    default:
                        _sparks.Emit(at, count);
                        break;
                }

                if (ev.Param == 1)
                {
                    // Knock-out blow: a bigger flash.
                    Ring(at, 1.6f, 0.3f, 0xFFFFFF);
                    Flash(at, new Vector3(1f, 0.9f, 0.7f), 10f);
                }

                break;
            }

            case GameEventKind.Land when ev.Value > 6f:
                _dust.Emit(at with { Y = 0.05f }, (int)Math.Clamp(ev.Value, 6, 20));
                break;
            case GameEventKind.Special:
                _aura.Emit(at + new Vector3(0, 0.2f, 0), 30);
                break;
            case GameEventKind.Ultimate:
            case GameEventKind.RageOn:
                Ring(at + new Vector3(0, 0.05f, 0), 4.5f, 0.6f, 0xFF5A1F);
                _fire.Emit(at + new Vector3(0, 1f, 0), 60);
                Flash(at + new Vector3(0, 1.5f, 0), new Vector3(1f, 0.4f, 0.1f), 14f);
                break;
            case GameEventKind.BossPhase:
                Ring(at with { Y = 0.05f }, 6f, 0.7f, 0xFF1744);
                _dust.Emit(at with { Y = 0.1f }, 40);
                break;
            case GameEventKind.PropBroken:
                _dust.Emit(at + new Vector3(0, 0.4f, 0), 18);
                _sparks.Emit(at + new Vector3(0, 0.5f, 0), 12);
                break;
            case GameEventKind.PickupTaken:
                _electric.Emit(at + new Vector3(0, 0.6f, 0), 16);
                break;
            case GameEventKind.Counter:
                Ring(at, 1.3f, 0.25f, 0x00E5FF);
                _electric.Emit(at, 24);
                break;
            case GameEventKind.CoopMove:
                Ring(at with { Y = 0.05f }, 3f, 0.5f, 0x7CFF4F);
                break;
            case GameEventKind.HazardStrike:
                switch ((HazardKind)ev.Param)
                {
                    case HazardKind.Lightning:
                        _electric.Emit(at + new Vector3(0, 0.3f, 0), 50);
                        Flash(at + new Vector3(0, 2f, 0), new Vector3(0.7f, 0.8f, 1f), 30f);
                        break;
                    case HazardKind.Shockwave:
                        Ring(at with { Y = 0.05f }, ev.Value, 0.35f, 0xFFD27F);
                        _dust.Emit(at with { Y = 0.1f }, 16);
                        break;
                    case HazardKind.LaserSweep:
                        _fire.Emit(at + new Vector3(0, 0.2f, 0), 25);
                        Flash(at + new Vector3(0, 1f, 0), new Vector3(1f, 0.2f, 0.2f), 12f);
                        break;
                    default:
                        _dust.Emit(at with { Y = 0.1f }, 25);
                        _sparks.Emit(at + new Vector3(0, 0.3f, 0), 15);
                        break;
                }

                break;
        }
    }

    private void Ring(Vector3 at, float radius, float life, uint color)
    {
        foreach (var r in _rings)
        {
            if (r.Active)
            {
                continue;
            }

            r.Active = true;
            r.Age = 0;
            r.Life = life;
            r.Radius = radius;
            r.Node.Position = at;
            r.Node.Visible = true;
            var c = MathHelpers.FromHex(color);
            r.Color = new Vector3(c.X, c.Y, c.Z);
            return;
        }
    }

    private void Flash(Vector3 at, Vector3 color, float intensity)
    {
        int i = _flashNext;
        _flashNext = (_flashNext + 1) % _flashLights.Length;
        _flashLights[i].Position = at;
        _flashLights[i].Light = Light.Point(color, intensity, 9f);
        _flashLife[i] = 0.12f;
    }

    public void Update(GameSession session, Node camera, Vector3 cameraFocus, float dt)
    {
        _time += dt;
        var world = session.World;

        // Rage auras around raging players.
        for (int s = 0; s < session.PlayerCount; s++)
        {
            var e = session.PlayerEntity(s);
            if (world.IsAlive(e) && world.Get<Rage>(e).Active && _rng.Chance(0.9f))
            {
                _aura.Effect.Origin = Space.ToRender(world.Get<Transform>(e).Position) + new Vector3(0, 0.1f, 0);
                _aura.Effect.Burst(3);
                _aura.Dirty = true;
            }
        }

        UpdateHazards(world, dt);

        // Weather follows the camera.
        if (_weather.Effect.Rate > 0)
        {
            _weather.Effect.Origin = cameraFocus + new Vector3(0, _weather0 is WeatherKind.TropicalRain or WeatherKind.Thunderstorm ? 12f : 6f, 0);
            _weather.Dirty = true;
        }

        var camWorld = camera.WorldMatrix;
        foreach (var ch in _channels)
        {
            if (ch.Effect.Alive > 0 || ch.Dirty || ch.Effect.Rate > 0)
            {
                ch.Effect.Update(dt, _time);
                ch.Effect.Upload(ch.Geometry, camWorld);
                ch.Dirty = ch.Effect.Alive > 0;
            }
        }

        foreach (var r in _rings)
        {
            if (!r.Active)
            {
                continue;
            }

            r.Age += dt;
            float t = r.Age / r.Life;
            if (t >= 1f)
            {
                r.Active = false;
                r.Node.Visible = false;
                continue;
            }

            float radius = MathX.Lerp(0.2f, r.Radius, MathX.SmoothStep(t * 1.4f));
            r.Node.Scale = new Vector3(radius, radius, 1f + radius * 0.3f);
            r.Node.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2);
            var alpha = (1f - t) * (1f - t);
            var c = r.Color * alpha;
            r.Material.Update(o =>
            {
                o.EmissiveIntensity = 3f * alpha;
                o.BaseColor = new Vector4(c, alpha);
                o.Emissive = c;
                return o;
            });
        }

        for (int i = 0; i < _flashLights.Length; i++)
        {
            if (_flashLife[i] > 0)
            {
                _flashLife[i] -= dt;
                var l = _flashLights[i].Light!.Value;
                l.Intensity *= MathF.Max(0, _flashLife[i] / 0.12f);
                _flashLights[i].Light = l;
            }
        }
    }

    private void UpdateHazards(World world, float dt)
    {
        foreach (var v in _hazards.Values)
        {
            v.Seen = false;
        }

        var pool = world.Pool<Hazard>();
        var ids = pool.EntityIds;
        for (int i = 0; i < ids.Length; i++)
        {
            var e = world.Handle(ids[i]);
            ref var h = ref pool.Get(e.Id);
            if (!_hazards.TryGetValue(e.Id, out var view) || view.Entity != e)
            {
                if (view is not null)
                {
                    view.Root.Remove();
                }

                view = CreateHazard(e, h);
                _hazards[e.Id] = view;
            }

            view.Seen = true;
            var pos = Space.ToRender(world.Get<Transform>(e).Position);
            view.Root.Position = pos;
            float warnT = h.WarnTicks <= 0 ? 1f : Math.Clamp(h.Ticks / (float)h.WarnTicks, 0f, 1f);
            bool striking = h.Ticks >= h.WarnTicks;

            // Pulsing telegraph that tightens as the strike approaches.
            float pulse = 0.55f + 0.45f * MathF.Sin(_time * (8f + warnT * 18f));
            view.Marker.Scale = new Vector3(h.Radius, 1f, h.Radius);
            view.Marker.Visible = h.Kind is not HazardKind.WildBoar && (!striking || h.Persistent);
            float glow = h.Kind == HazardKind.LavaPool && striking ? 3f : (1f + warnT * 3f) * pulse;
            view.MarkerMaterial.Update(o =>
            {
                o.EmissiveIntensity = glow;
                return o;
            });

            if (view.Body is not null)
            {
                switch (h.Kind)
                {
                    case HazardKind.FallingRock or HazardKind.CrateDrop:
                        view.Body.Position = new Vector3(0, striking ? 0.6f : MathX.Lerp(12f, 0.6f, warnT * warnT), 0);
                        view.Body.Rotation = Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1, 0.3f, 0.2f)), _time * 3f);
                        break;
                    case HazardKind.FallingTree:
                    {
                        float fall = striking ? 1f : warnT * warnT;
                        view.Body.Position = new Vector3(0, 0, -6f);
                        view.Body.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, fall * MathF.PI / 2 * 0.98f);
                        break;
                    }

                    case HazardKind.Lightning or HazardKind.LaserSweep:
                        view.Body.Visible = striking || warnT > 0.92f;
                        break;
                    case HazardKind.SpearTrap:
                        view.Body.Position = new Vector3(0, striking ? 0f : -1.5f, 0);
                        break;
                    case HazardKind.WildBoar:
                    {
                        var vel = world.Get<Motion>(e).Velocity;
                        view.Body.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, vel.X >= 0 ? 0 : MathF.PI);
                        view.Body.Position = new Vector3(0, MathF.Abs(MathF.Sin(_time * 22f)) * 0.08f, 0);
                        if (_rng.Chance(0.3f))
                        {
                            _dust.Emit(pos with { Y = 0.05f }, 1);
                        }

                        break;
                    }
                }
            }

            if (h.Kind == HazardKind.LavaPool && striking && _rng.Chance(dt * 12f))
            {
                _fire.Emit(pos + new Vector3(_rng.Range(-h.Radius, h.Radius) * 0.7f, 0.1f, _rng.Range(-h.Radius, h.Radius) * 0.7f), 3);
            }
        }

        _stale.Clear();
        foreach (var (id, v) in _hazards)
        {
            if (!v.Seen)
            {
                _stale.Add(id);
            }
        }

        foreach (int id in _stale)
        {
            _hazards[id].Root.Remove();
            _hazards.Remove(id);
        }
    }

    private HazardView CreateHazard(Entity e, in Hazard h)
    {
        var root = _root.CreateChild("hazard");
        uint color = h.Team == TeamId.Players ? 0x00E5FFu : h.Kind == HazardKind.LavaPool ? 0xFF6A00u : 0xFF1744u;
        var markerMat = _mats.Glow(color, 2f, 0.55f, additive: true);
        var marker = _scene.AddMesh(_disc, markerMat, root, "marker");
        marker.CastShadow = false;
        Node? body = h.Kind switch
        {
            HazardKind.Lightning => _scene.AddMesh(_bolt, _mats.Glow(0xD6E4FF, 9f, 0.95f, additive: true), root),
            HazardKind.LaserSweep => _scene.AddMesh(_bolt, _mats.Glow(0xFF1744, 8f, 0.9f, additive: true), root),
            HazardKind.FallingRock => _scene.AddMesh(_rock, _mats.Pbr(0x4E3B30, 0.95f, 0f, 0xFF5722, 0.6f), root),
            HazardKind.CrateDrop => _scene.AddMesh(_crate, _mats.Pbr(0x9A6B3D, 0.8f), root),
            HazardKind.FallingTree => _scene.AddMesh(_trunk, _mats.Pbr(0x4A3B2A, 0.9f), root),
            HazardKind.WildBoar => _scene.AddMesh(_boar, _mats.Pbr(0x4B3621, 0.9f), root),
            HazardKind.SpearTrap => _scene.AddMesh(_spike, _mats.Pbr(0xA0A4AC, 0.3f, 1f), root),
            HazardKind.Trap => _scene.AddMesh(_disc, _mats.Glow(0x00E5FF, 5f), root),
            _ => null,
        };
        if (body is not null)
        {
            body.CastShadow = true;
            if (h.Kind == HazardKind.Trap)
            {
                body.Scale = new Vector3(0.25f, 3f, 0.25f);
            }
        }

        return new HazardView { Entity = e, Root = root, Marker = marker, MarkerMaterial = markerMat, Body = body, Kind = h.Kind };
    }

    public void Clear()
    {
        foreach (var v in _hazards.Values)
        {
            v.Root.Remove();
        }

        _hazards.Clear();
        foreach (var ch in _channels)
        {
            ch.Effect.Clear();
            ch.Dirty = true;
        }

        foreach (var r in _rings)
        {
            r.Active = false;
            r.Node.Visible = false;
        }
    }

    public void Dispose() => Clear();
}

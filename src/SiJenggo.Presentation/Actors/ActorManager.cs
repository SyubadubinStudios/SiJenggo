using System.Numerics;
using SiJenggo.Core;
using SiJenggo.Core.Ecs;
using SiJenggo.Game;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;
using ThreeNet;

namespace SiJenggo.Presentation.Actors;

/// <summary>One rendered entity: a root node placed every frame plus, for fighters, an animator.</summary>
internal sealed class ActorView
{
    public required Entity Entity;
    /// <summary>Model key for rigged fighters; such views are pooled instead of destroyed.</summary>
    public string? PoolKey;
    public required Node Root;
    public required Node Body;
    public required VisualKind Kind;
    public ActorAnimator? Animator;
    public Node? WeaponNode;
    public WeaponId WeaponShown;
    public Node? HandBone;
    public float Spin;
    public bool Seen;
    public float Fade = 1f;
}

/// <summary>
/// Mirrors the ECS world into the ThreeNet scene: creates a view when an entity with a
/// <see cref="Visual"/> appears, interpolates it every render frame, removes it when the entity dies.
/// </summary>
public sealed class ActorManager : IDisposable
{
    private readonly Scene _scene;
    private readonly ProceduralModels _procedural;
    private readonly Node _root;
    private readonly Dictionary<int, ActorView> _views = [];
    private readonly List<int> _stale = [];
    private readonly Dictionary<string, Stack<ActorView>> _pool = [];

    public ActorManager(Scene scene, ProceduralModels procedural)
    {
        _scene = scene;
        _procedural = procedural;
        _root = scene.CreateNode(null, "actors");
    }

    public int Count => _views.Count;

    /// <summary>The model file for a fighter visual, e.g. "jenggo", "preman_pisau", "bang_kodir".</summary>
    public static string? ModelKey(in Visual v) => v.Kind switch
    {
        VisualKind.Character => ((CharacterId)v.Variant) switch
        {
            CharacterId.Jenggo => "jenggo",
            CharacterId.Bima => "bima",
            CharacterId.Rani => "rani",
            _ => "ucup",
        },
        VisualKind.Enemy => ((EnemyKind)v.Variant) switch
        {
            EnemyKind.PremanGang => "preman_gang",
            EnemyKind.PremanPisau => "preman_pisau",
            EnemyKind.TukangPukul => "tukang_pukul",
            EnemyKind.JagoanSilat => "jagoan_silat",
            EnemyKind.PelemparBotol => "pelempar_botol",
            EnemyKind.NinjaGaruda => "ninja_garuda",
            EnemyKind.RobotGaruda => "robot_garuda",
            _ => "ninja_garuda",
        },
        VisualKind.Boss => ((BossId)v.Variant) switch
        {
            BossId.BangKodir => "bang_kodir",
            BossId.KaptenBadai => "kapten_badai",
            BossId.NyiAnginLaut => "nyi_angin_laut",
            BossId.RajaPenebang => "raja_penebang",
            BossId.JagalMerapi => "jagal_merapi",
            BossId.PatihAbadi => "patih_abadi",
            _ => "jenderal_garuda",
        },
        _ => null,
    };

    /// <summary>Loads a rigged model and collects the clips that belong to this instance.</summary>
    internal (Node Root, ActorAnimator? Animator, Node? Hand) LoadRigged(string key, Node parent)
    {
        int before = _scene.Animations.Count;
        var result = _scene.LoadModelCached(AssetPaths.Model(key), parent);
        var clips = new Dictionary<string, AnimationClip>();
        var all = _scene.Animations;
        for (int i = before; i < all.Count; i++)
        {
            clips[all[i].Name] = all[i];
        }

        result.Root.SetShadowsRecursive(true, false);
        return (result.Root, clips.Count > 0 ? new ActorAnimator(clips) : null, FindNode(result.Root, "hand.R"));
    }

    private static Node? FindNode(Node node, string name)
    {
        if (node.Name == name)
        {
            return node;
        }

        foreach (var c in node.Children)
        {
            var found = FindNode(c, name);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    public void Sync(GameSession session, float alpha, float dt)
    {
        var world = session.World;
        foreach (var v in _views.Values)
        {
            v.Seen = false;
        }

        var visuals = world.Pool<Visual>();
        var ids = visuals.EntityIds;
        var comps = visuals.Components;
        for (int i = 0; i < ids.Length; i++)
        {
            var entity = world.Handle(ids[i]);
            if (!_views.TryGetValue(entity.Id, out var view) || view.Entity != entity)
            {
                if (view is not null)
                {
                    Remove(view);
                }

                view = Create(entity, comps[i]);
                _views[entity.Id] = view;
            }

            view.Seen = true;
            Place(world, view, comps[i], alpha, dt);
        }

        _stale.Clear();
        foreach (var (id, v) in _views)
        {
            if (!v.Seen)
            {
                _stale.Add(id);
            }
        }

        foreach (int id in _stale)
        {
            Remove(_views[id]);
            _views.Remove(id);
        }
    }

    private ActorView Create(Entity e, in Visual visual)
    {
        var key0 = ModelKey(visual);
        if (key0 is not null && _pool.TryGetValue(key0, out var stack) && stack.Count > 0)
        {
            // Reuse a hidden instance: loading is avoided and its clips stay valid.
            var reused = stack.Pop();
            reused.Entity = e;
            reused.Root.Visible = true;
            reused.Root.Scale = new Vector3(MathF.Max(0.1f, visual.Scale));
            reused.Animator?.Reset();
            if (reused.Animator is not null)
            {
                reused.Animator.IsBoss = visual.Kind == VisualKind.Boss;
            }

            return reused;
        }

        var root = _root.CreateChild("actor");
        Node body;
        ActorAnimator? animator = null;
        Node? hand = null;
        var key = ModelKey(visual);
        if (key is not null && AssetPaths.HasModel(key))
        {
            (body, animator, hand) = LoadRigged(key, root);
            if (animator is not null)
            {
                animator.IsBoss = visual.Kind == VisualKind.Boss;
            }
        }
        else if (key is not null)
        {
            body = _procedural.Mannequin(root, 0x333333, 0x22334A, 0xC68642, 1f);
        }
        else
        {
            var procKey = visual.Kind switch
            {
                VisualKind.Prop => ProceduralModels.PropKey((PropKind)visual.Variant),
                VisualKind.Pickup => visual.Variant >= 100 ? "weapon." + (WeaponId)(visual.Variant - 100) : "pickup." + (PickupKind)visual.Variant,
                VisualKind.Projectile => visual.Variant >= 100 ? "weapon." + (WeaponId)(visual.Variant - 100) : "projectile." + (ProjectileKind)visual.Variant,
                _ => null,
            };
            body = procKey is null ? root.CreateChild("empty") : _procedural.Instance(procKey, root);
            if (visual.Kind == VisualKind.Pickup && visual.Variant >= 100)
            {
                body.EulerAngles = new Vector3(0, 0, MathF.PI / 2);
                body.Position = new Vector3(0, 0.05f, 0);
            }
        }

        root.Scale = new Vector3(visual.Kind is VisualKind.Hazard ? 1f : MathF.Max(0.1f, visual.Scale));
        return new ActorView { Entity = e, Root = root, Body = body, Kind = visual.Kind, Animator = animator, HandBone = hand, PoolKey = animator is not null ? key : null };
    }

    private void Place(World world, ActorView view, in Visual visual, float alpha, float dt)
    {
        var e = view.Entity;
        ref var t = ref world.Get<Transform>(e);
        var pos = Vector3.Lerp(t.PreviousPosition, t.Position, alpha);
        view.Root.Position = Space.ToRender(pos);

        switch (view.Kind)
        {
            case VisualKind.Character or VisualKind.Enemy or VisualKind.Boss:
            {
                ref var f = ref world.Get<Fighter>(e);
                view.Root.Rotation = Space.Facing(t.Facing);
                var vel = world.Get<Motion>(e).Velocity;
                bool rage = world.Has<Rage>(e) && world.Get<Rage>(e).Active;
                if (view.Animator is not null)
                {
                    view.Animator.Update(f, MathF.Sqrt(vel.X * vel.X + vel.Z * vel.Z), rage, dt);
                }
                else
                {
                    view.Body.EulerAngles = f.State is FighterState.Knockdown or FighterState.Dead or FighterState.Downed ? new Vector3(-MathF.PI / 2, 0, 0) : Vector3.Zero;
                }

                // Knocked-out enemies blink out.
                if (world.Has<Enemy>(e) && world.Get<Health>(e).Dead && f.State == FighterState.Dead)
                {
                    int ticks = world.Get<Enemy>(e).DeathTicks;
                    view.Root.Visible = ticks < 60 || (ticks / 4) % 2 == 0;
                }
                else if (f.InvulnTicks > 60 && world.Has<Player>(e))
                {
                    view.Root.Visible = (f.InvulnTicks / 4) % 2 == 0;
                }
                else
                {
                    view.Root.Visible = !(world.Has<Player>(e) && f.State == FighterState.Dead);
                }

                SyncWeapon(world, view);
                break;
            }

            case VisualKind.Prop:
            {
                ref var pr = ref world.Get<Prop>(e);
                var m = world.Get<Motion>(e);
                if (pr.Thrown)
                {
                    view.Spin += dt * 9f * MathF.Sign(m.Velocity.X == 0 ? 1 : m.Velocity.X);
                    view.Root.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -view.Spin);
                }
                else if (world.IsAlive(pr.Carrier))
                {
                    view.Root.Rotation = Quaternion.Identity;
                }
                else
                {
                    view.Spin = 0;
                    view.Root.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (e.Id % 7) * 0.4f);
                }

                break;
            }

            case VisualKind.Pickup:
                view.Spin += dt * 1.8f;
                view.Root.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, view.Spin);
                view.Root.Position += new Vector3(0, 0.12f + 0.06f * MathF.Sin(view.Spin * 2f), 0);
                break;

            case VisualKind.Projectile:
            {
                var kind = (ProjectileKind)Math.Min(visual.Variant, (int)ProjectileKind.Weapon);
                float dir = t.Facing;
                view.Spin += dt * 18f;
                view.Root.Rotation = visual.Variant >= 100 || kind is ProjectileKind.Shuriken or ProjectileKind.Peci
                    ? Quaternion.CreateFromAxisAngle(kind == ProjectileKind.Peci || kind == ProjectileKind.Shuriken ? Vector3.UnitY : Vector3.UnitZ, -view.Spin * dir)
                    : kind is ProjectileKind.Bottle or ProjectileKind.Boulder
                        ? Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -view.Spin * 0.6f * dir)
                        : Quaternion.CreateFromAxisAngle(Vector3.UnitY, dir >= 0 ? 0 : MathF.PI);
                break;
            }
        }
    }

    private void SyncWeapon(World world, ActorView view)
    {
        var weapon = world.Has<WeaponHolder>(view.Entity) ? world.Get<WeaponHolder>(view.Entity).Weapon : WeaponId.None;
        if (weapon == view.WeaponShown)
        {
            return;
        }

        view.WeaponNode?.Remove();
        view.WeaponNode = null;
        view.WeaponShown = weapon;
        if (weapon == WeaponId.None)
        {
            return;
        }

        var parent = view.HandBone ?? view.Body;
        view.WeaponNode = _procedural.Instance("weapon." + weapon, parent);
        if (view.HandBone is null)
        {
            view.WeaponNode.Position = new Vector3(0.35f, 1.0f, 0.2f);
            view.WeaponNode.EulerAngles = new Vector3(MathF.PI / 2, 0, 0);
        }
        else
        {
            // Bone space: the hand bone points along +Y; lay the blade across the fist.
            view.WeaponNode.Position = new Vector3(0, 0.06f, 0.02f);
            view.WeaponNode.EulerAngles = new Vector3(MathF.PI / 2, 0, 0);
        }
    }

    /// <summary>World position of an entity's view (for effects that need the rendered spot).</summary>
    public Vector3? RenderPosition(Entity e) => _views.TryGetValue(e.Id, out var v) && v.Entity == e ? v.Root.WorldPosition : null;

    private void Remove(ActorView view)
    {
        if (view.PoolKey is not null)
        {
            view.Root.Visible = false;
            view.WeaponNode?.Remove();
            view.WeaponNode = null;
            view.WeaponShown = WeaponId.None;
            view.Animator?.Reset();
            if (!_pool.TryGetValue(view.PoolKey, out var stack))
            {
                stack = new Stack<ActorView>();
                _pool[view.PoolKey] = stack;
            }

            stack.Push(view);
            return;
        }

        view.Root.Remove();
    }

    /// <summary>Hides every actor (rigged ones go back to the pool for the next stage).</summary>
    public void Clear()
    {
        foreach (var v in _views.Values)
        {
            Remove(v);
        }

        _views.Clear();
    }

    public void Dispose() => Clear();
}

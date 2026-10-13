using System.Numerics;
using SiJenggo.Core.Ecs;
using SiJenggo.Game;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;

namespace SiJenggo.Tests;

internal static class TestHelpers
{
    public static GameSession NewSession(CharacterId character = CharacterId.Jenggo, int players = 1, bool ai = false, ulong seed = 42, int chapter = 0, StageKind kind = StageKind.Chapter)
    {
        var loadouts = new PlayerLoadout[players];
        for (int i = 0; i < players; i++)
        {
            loadouts[i] = PlayerLoadout.Base(i == 0 ? character : CharacterId.Bima, ai);
        }

        var config = new SessionConfig { Players = loadouts, Seed = seed };
        return new GameSession(config, new StagePlan { Kind = kind, Chapter = Chapters.Get(chapter) });
    }

    public static void Run(this GameSession session, int ticks, InputButtons p1 = InputButtons.None)
    {
        Span<InputFrame> inputs = stackalloc InputFrame[GameSession.MaxPlayers];
        inputs[0] = new InputFrame(p1);
        for (int i = 0; i < ticks; i++)
        {
            session.Step(inputs[..session.PlayerCount]);
        }
    }

    /// <summary>One tick with the buttons held, then one tick released (a clean press).</summary>
    public static void Tap(this GameSession session, InputButtons buttons)
    {
        session.Run(1, buttons);
        session.Run(1);
    }

    public static Entity Player(this GameSession session) => session.PlayerEntity(0);

    public static ref Transform T(this GameSession s, Entity e) => ref s.World.Get<Transform>(e);

    public static ref Fighter F(this GameSession s, Entity e) => ref s.World.Get<Fighter>(e);

    public static ref Health H(this GameSession s, Entity e) => ref s.World.Get<Health>(e);

    /// <summary>An enemy standing right in front of player one, AI asleep so it does not interfere.</summary>
    public static Entity DummyInFront(this GameSession session, float distance = 0.95f, EnemyKind kind = EnemyKind.TukangPukul)
    {
        var p = session.T(session.Player()).Position;
        var e = session.SpawnEnemy(kind, p + new Vector3(distance, 0, 0));
        ref var brain = ref session.World.Get<AiBrain>(e);
        brain.AttackCooldown = 100000;
        brain.ThinkTicks = 100000;
        session.F(e).Armor = 0;
        session.F(e).ArmorMax = 0;
        return e;
    }
}

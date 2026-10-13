using SiJenggo.Core;
using SiJenggo.Core.Ecs;

namespace SiJenggo.Tests;

public class EcsTests
{
    private struct Pos { public int X; }
    private struct Vel { public int V; }

    [Fact]
    public void Destroyed_entity_handles_go_stale_and_ids_are_recycled()
    {
        var world = new World(8);
        var a = world.Create();
        world.Destroy(a);
        world.Flush();
        Assert.False(world.IsAlive(a));

        var b = world.Create();
        Assert.Equal(a.Id, b.Id);
        Assert.NotEqual(a.Version, b.Version);
        Assert.True(world.IsAlive(b));
        Assert.False(world.IsAlive(a));
    }

    [Fact]
    public void Removing_a_component_keeps_the_others_packed_and_addressable()
    {
        var world = new World(8);
        var es = Enumerable.Range(0, 5).Select(i =>
        {
            var e = world.Create();
            world.Add(e, new Pos { X = i });
            return e;
        }).ToArray();

        world.Remove<Pos>(es[1]);
        Assert.Equal(4, world.Pool<Pos>().Count);
        Assert.Equal(4, world.Get<Pos>(es[4]).X);
        Assert.Equal(0, world.Get<Pos>(es[0]).X);
        Assert.False(world.Has<Pos>(es[1]));
    }

    [Fact]
    public void Query_joins_two_pools()
    {
        var world = new World(8);
        for (int i = 0; i < 6; i++)
        {
            var e = world.Create();
            world.Add(e, new Pos { X = i });
            if (i % 2 == 0)
            {
                world.Add(e, new Vel { V = i });
            }
        }

        int count = 0;
        foreach (var e in world.Query<Pos, Vel>())
        {
            Assert.Equal(world.Get<Pos>(e).X, world.Get<Vel>(e).V);
            count++;
        }

        Assert.Equal(3, count);
    }

    [Fact]
    public void Deterministic_random_repeats_for_the_same_seed()
    {
        var a = new DeterministicRandom(1234);
        var b = new DeterministicRandom(1234);
        var c = new DeterministicRandom(4321);
        bool differs = false;
        for (int i = 0; i < 100; i++)
        {
            uint va = a.NextUInt();
            Assert.Equal(va, b.NextUInt());
            differs |= va != c.NextUInt();
        }

        Assert.True(differs);
    }

    [Fact]
    public void Fixed_step_clock_never_spirals()
    {
        var clock = new FixedStepClock();
        Assert.Equal(1, clock.Advance(1 / 60.0));
        Assert.Equal(clock.MaxTicksPerFrame, clock.Advance(5.0));
    }
}

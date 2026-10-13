using System.Numerics;
using SiJenggo.Game;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;

namespace SiJenggo.Tests;

public class CombatTests
{
    [Fact]
    public void Jab_hits_an_enemy_in_front_but_not_one_behind()
    {
        var s = TestHelpers.NewSession();
        var front = s.DummyInFront();
        var behind = s.DummyInFront(-1.5f);
        float frontHp = s.H(front).Current, behindHp = s.H(behind).Current;

        s.Tap(InputButtons.Light);
        s.Run(20);

        Assert.True(s.H(front).Current < frontHp);
        Assert.Equal(behindHp, s.H(behind).Current);
        Assert.Contains(s.Events.Items.ToArray(), e => e.Kind == GameEventKind.Hit);
    }

    [Fact]
    public void Light_presses_advance_the_combo_chain_to_a_knockdown_finisher()
    {
        var s = TestHelpers.NewSession();
        var dummy = s.DummyInFront();
        var chain = Characters.Jenggo.LightChain;
        bool sawFinisher = false;
        for (int i = 0; i < 40; i++)
        {
            s.Tap(InputButtons.Light);
            s.Run(3);
            if (s.F(s.Player()).Move == chain[^1])
            {
                sawFinisher = true;
            }
        }

        Assert.True(sawFinisher);
        Assert.True(s.World.Get<Player>(s.Player()).BestCombo >= chain.Length);
        s.Run(30);
        Assert.Contains(s.F(dummy).State, new[] { FighterState.Juggle, FighterState.Knockdown, FighterState.GetUp, FighterState.Idle });
    }

    [Fact]
    public void Heavy_kick_launches_then_knocks_down()
    {
        var s = TestHelpers.NewSession();
        var dummy = s.DummyInFront(1.1f, EnemyKind.PremanGang);
        s.Tap(InputButtons.Heavy);
        bool juggled = false, knocked = false;
        for (int i = 0; i < 120; i++)
        {
            s.Run(1);
            juggled |= s.F(dummy).State == FighterState.Juggle;
            knocked |= s.F(dummy).State == FighterState.Knockdown;
        }

        Assert.True(juggled);
        Assert.True(knocked);
    }

    [Fact]
    public void Full_rage_activates_and_a_second_press_unleashes_the_ultimate()
    {
        var s = TestHelpers.NewSession();
        var p = s.Player();
        s.World.Get<Rage>(p).Meter = 100;

        s.Tap(InputButtons.Rage);
        Assert.True(s.World.Get<Rage>(p).Active);

        s.Tap(InputButtons.Rage);
        Assert.Equal(Characters.Jenggo.Ultimate, s.F(p).Move);
        Assert.False(s.World.Get<Rage>(p).Active);
        Assert.Equal(1, s.Stats.UltimatesUsed);
    }

    [Fact]
    public void Taking_damage_fills_the_rage_meter()
    {
        var s = TestHelpers.NewSession();
        var p = s.Player();
        var enemy = s.DummyInFront(1f, EnemyKind.PremanGang);
        s.T(enemy).Facing = -1;
        var spec = new HitSpec { Attacker = enemy, Damage = 20, Direction = 1, HitStun = 10, Knockback = new Vector2(1, 0) };
        s.ApplyHit(p, ref spec);
        Assert.True(s.World.Get<Rage>(p).Meter > 0);
    }

    [Fact]
    public void Specials_spend_semangat_and_fail_when_empty()
    {
        var s = TestHelpers.NewSession();
        var p = s.Player();
        ref var rage = ref s.World.Get<Rage>(p);
        rage.Semangat = 100;
        s.Tap(InputButtons.Special);
        Assert.Equal(Characters.Jenggo.Specials[0], s.F(p).Move);
        Assert.True(s.World.Get<Rage>(p).Semangat < 100);

        s.Run(80);
        s.World.Get<Rage>(p).Semangat = 0;
        s.Tap(InputButtons.Special);
        Assert.NotEqual(FighterState.Attack, s.F(p).State);
        Assert.Contains(s.Events.Items.ToArray(), e => e.Kind == GameEventKind.NotEnoughSpirit);
    }

    [Fact]
    public void Bima_counter_reverses_an_incoming_attack()
    {
        var s = TestHelpers.NewSession(CharacterId.Bima);
        var p = s.Player();
        var enemy = s.DummyInFront(1.1f, EnemyKind.PremanGang);
        s.T(enemy).Facing = -1;
        float hp = s.H(p).Current;

        s.Tap(InputButtons.Dodge | InputButtons.Light);
        Assert.Equal(Characters.Bima.Counter, s.F(p).Move);
        // Enemy swings into the counter window.
        ref var ef = ref s.F(enemy);
        ef.State = FighterState.Attack;
        ef.Move = Moves.PremanJab;
        ef.MoveClock = ef.MoveTick = Moves.PremanJab.Def.Startup - 1;
        s.Run(6);

        Assert.Equal(hp, s.H(p).Current);
        Assert.Equal(1, s.Stats.CountersLanded);
    }

    [Fact]
    public void Grab_then_throw_damages_and_throws_the_enemy()
    {
        var s = TestHelpers.NewSession();
        var p = s.Player();
        var enemy = s.DummyInFront(0.8f, EnemyKind.PremanGang);
        float hp = s.H(enemy).Current;

        s.Tap(InputButtons.Grab);
        Assert.Equal(FighterState.Grabbing, s.F(p).State);
        Assert.Equal(FighterState.Grabbed, s.F(enemy).State);

        s.Tap(InputButtons.Light);
        s.Run(20);
        Assert.True(s.H(enemy).Current < hp);
        Assert.Equal(FighterState.Grabbed, s.F(enemy).State);

        s.Tap(InputButtons.Heavy);
        s.Run(14);
        Assert.Contains(s.F(enemy).State, new[] { FighterState.Thrown, FighterState.Juggle, FighterState.Knockdown });
    }

    [Fact]
    public void Picking_up_a_weapon_switches_to_its_combo()
    {
        var s = TestHelpers.NewSession();
        var p = s.Player();
        s.SpawnPickup(PickupKind.Weapon, s.T(p).Position + new Vector3(0.3f, 0, 0), WeaponId.Golok);
        s.Tap(InputButtons.Grab);
        Assert.Equal(WeaponId.Golok, s.World.Get<WeaponHolder>(p).Weapon);

        s.Tap(InputButtons.Light);
        Assert.Equal(Weapons.Get(WeaponId.Golok).Chain[0], s.F(p).Move);
    }

    [Fact]
    public void Props_can_be_lifted_and_thrown_into_enemies()
    {
        var s = TestHelpers.NewSession();
        var p = s.Player();
        var chair = s.SpawnProp(PropKind.Kursi, s.T(p).Position + new Vector3(0.5f, 0, 0));
        s.Tap(InputButtons.Grab);
        Assert.Equal(FighterState.Carrying, s.F(p).State);

        var enemy = s.DummyInFront(3.5f, EnemyKind.PremanGang);
        float hp = s.H(enemy).Current;
        s.Tap(InputButtons.Light);
        s.Run(40);
        Assert.True(s.H(enemy).Current < hp);
        Assert.False(s.World.IsAlive(chair));
    }

    [Fact]
    public void Knocked_out_enemies_award_score_and_xp()
    {
        var s = TestHelpers.NewSession();
        var p = s.Player();
        var enemy = s.DummyInFront(1f, EnemyKind.PremanGang);
        s.H(enemy).Current = 1;
        s.Tap(InputButtons.Light);
        s.Run(60);
        Assert.True(s.H(enemy).Dead);
        Assert.Equal(1, s.Stats.KnockOuts);
        Assert.True(s.World.Get<Player>(p).Xp > 0);
        Assert.True(s.World.Get<Player>(p).Score >= Enemies.Get(EnemyKind.PremanGang).Score);
    }

    [Fact]
    public void Every_move_has_sane_frame_data()
    {
        foreach (var m in Moves.All.Skip(1))
        {
            Assert.True(m.Startup >= 1, m.Key);
            Assert.True(m.Active >= 1, m.Key);
            Assert.True(m.TotalTicks < 200, m.Key);
            Assert.True(m.Damage >= 0, m.Key);
        }

        Assert.Equal(Moves.Count, Moves.All.Select(m => m?.Key).Where(k => k is not null).Distinct().Count());
    }
}

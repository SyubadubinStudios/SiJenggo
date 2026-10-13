using System.Numerics;
using SiJenggo.Core;
using SiJenggo.Core.Ecs;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;

namespace SiJenggo.Game;

/// <summary>
/// The stage director: camera locks, enemy waves, the boss arena, ambient hazards, area titles,
/// endless survival waves, and the end of the stage.
/// </summary>
public sealed partial class GameSession
{
    private readonly List<EnemyKind> _spawnQueue = new(32);
    private int _waveIndex;
    private bool _waveActive;
    private int _waveMaxAlive;
    private int _spawnTimer;
    private float _lockCenter;
    private bool _bossTriggered;
    private bool _bossDefeated;
    private int _areaIndex = -1;
    private int _ambientTimer;
    private int _endTicks;
    private int _survivalWave;
    private int _survivalRest;

    public int WaveNumber => Stage.Kind == StageKind.SurvivalArena ? _survivalWave : _waveIndex;
    public bool BossDefeated => _bossDefeated;
    public int AreaIndex => _areaIndex;

    private void InitStage()
    {
        var chapter = Stage.Chapter;
        _ambientTimer = chapter.AmbientHazardInterval;
        CameraX = ViewHalfWidth;

        switch (Stage.Kind)
        {
            case StageKind.Chapter:
                foreach (var p in chapter.Props)
                {
                    SpawnProp(p.Kind, new Vector3(p.X, 0, p.Z));
                }

                foreach (var p in chapter.Pickups)
                {
                    SpawnPickup(p.Kind, new Vector3(p.X, 0, p.Z), p.Weapon);
                }

                break;

            case StageKind.SurvivalArena:
            case StageKind.BossArena:
            {
                // Arenas reuse the chapter's boss area.
                float centre = Math.Clamp(chapter.BossArenaX + ViewHalfWidth, ViewHalfWidth, chapter.Length - ViewHalfWidth);
                LockCamera(centre);
                CameraX = centre;
                for (int s = 0; s < PlayerCount; s++)
                {
                    var e = _players[s];
                    ref var t = ref World.Get<Transform>(e);
                    t.Position = t.PreviousPosition = new Vector3(centre - 5f + s * 1.2f, 0, s % 2 == 0 ? -0.8f : 0.8f);
                }

                int propCount = 4;
                for (int i = 0; i < propCount; i++)
                {
                    var kind = chapter.Props.Length > 0 ? chapter.Props[i % chapter.Props.Length].Kind : PropKind.PetiKayu;
                    SpawnProp(kind, new Vector3(centre - 6f + i * 4f, 0, i % 2 == 0 ? 2.6f : -2.6f));
                }

                if (Stage.Kind == StageKind.BossArena)
                {
                    _bossTriggered = true;
                    SpawnBossNow(Stage.Boss ?? chapter.Boss, centre + 5f);
                }
                else
                {
                    _survivalRest = 120;
                }

                break;
            }
        }
    }

    private void LockCamera(float centre)
    {
        CameraLocked = true;
        _lockCenter = Math.Clamp(centre, ViewHalfWidth, Stage.Chapter.Length - ViewHalfWidth);
        GoArrow = false;
    }

    private void UnlockCamera()
    {
        CameraLocked = false;
        GoArrow = true;
        Emit(GameEventKind.Go, Entity.Null, Entity.Null, new Vector3(CameraX, 0, 0));
    }

    private float LeadPlayerX()
    {
        float x = float.MinValue;
        for (int s = 0; s < PlayerCount; s++)
        {
            var e = _players[s];
            if (World.IsAlive(e) && World.Get<Fighter>(e).State != FighterState.Dead)
            {
                x = MathF.Max(x, World.Get<Transform>(e).Position.X);
            }
        }

        return x == float.MinValue ? CameraX : x;
    }

    private void UpdateDirector()
    {
        if (State is SessionState.StageClear or SessionState.GameOver)
        {
            return;
        }

        var chapter = Stage.Chapter;
        float lead = LeadPlayerX();

        // Area titles ("Terminal Liar", "Pasar Malam", ...).
        if (Stage.Kind == StageKind.Chapter)
        {
            for (int i = chapter.Areas.Length - 1; i > _areaIndex; i--)
            {
                if (lead >= chapter.Areas[i].StartX)
                {
                    _areaIndex = i;
                    Emit(GameEventKind.AreaEntered, Entity.Null, Entity.Null, new Vector3(lead, 0, 0), param: i);
                    break;
                }
            }
        }

        // Ambient hazards: storm lightning, falling rocks, boars, temple spears, drone lasers.
        if (chapter.AmbientHazard is { } ambient && Stage.Kind != StageKind.BossArena && --_ambientTimer <= 0)
        {
            _ambientTimer = (int)(chapter.AmbientHazardInterval * _rng.Range(0.7f, 1.3f));
            if (ambient == HazardKind.WildBoar)
            {
                SpawnBoar();
            }
            else
            {
                ScatterHazards(ambient, 2, ambient == HazardKind.SpearTrap ? 1f : 1.3f, 12, 70);
            }
        }

        switch (Stage.Kind)
        {
            case StageKind.Chapter:
                UpdateChapterWaves(chapter, lead);
                break;
            case StageKind.SurvivalArena:
                UpdateSurvival(chapter);
                break;
        }

        StreamSpawns();
        UpdateBossOutcome();
    }

    private void UpdateChapterWaves(ChapterDef chapter, float lead)
    {
        if (!_waveActive && !_bossTriggered && _waveIndex < chapter.Waves.Length && lead >= chapter.Waves[_waveIndex].TriggerX)
        {
            var wave = chapter.Waves[_waveIndex];
            StartWave(wave.Groups, wave.MaxAlive, wave.TriggerX + 3f);
        }

        if (_waveActive && _spawnQueue.Count == 0 && AliveEnemyCount() == 0)
        {
            _waveActive = false;
            _waveIndex++;
            Stats.WavesSurvived++;
            UnlockCamera();
        }

        if (!_waveActive && !_bossTriggered && lead >= chapter.BossArenaX)
        {
            _bossTriggered = true;
            float centre = chapter.Length - ViewHalfWidth;
            LockCamera(centre);
            SpawnBossNow(Stage.Boss ?? chapter.Boss, centre + ViewHalfWidth - 2.5f);
        }

        if (GoArrow && CameraX > _lockCenter + 6f)
        {
            GoArrow = false;
        }
    }

    private void StartWave(EnemyGroup[] groups, int maxAlive, float lockCentre)
    {
        _spawnQueue.Clear();
        foreach (var g in groups)
        {
            int count = g.Count + (PlayerCount > 1 ? (g.Count + 1) / 2 : 0);
            for (int i = 0; i < count; i++)
            {
                _spawnQueue.Add(g.Kind);
            }
        }

        // Deterministic shuffle so waves feel mixed.
        for (int i = _spawnQueue.Count - 1; i > 0; i--)
        {
            int j = _rng.Range(0, i + 1);
            (_spawnQueue[i], _spawnQueue[j]) = (_spawnQueue[j], _spawnQueue[i]);
        }

        _waveActive = true;
        _waveMaxAlive = maxAlive + (PlayerCount > 1 ? 1 : 0);
        _spawnTimer = 0;
        LockCamera(lockCentre);
        Emit(GameEventKind.WaveStart, Entity.Null, Entity.Null, new Vector3(lockCentre, 0, 0), param: _waveIndex);
    }

    private void StreamSpawns()
    {
        if (_spawnQueue.Count == 0 || --_spawnTimer > 0)
        {
            return;
        }

        if (AliveEnemyCount() >= _waveMaxAlive)
        {
            _spawnTimer = 20;
            return;
        }

        var kind = _spawnQueue[^1];
        _spawnQueue.RemoveAt(_spawnQueue.Count - 1);
        float side = _rng.Chance(0.5f) ? -1 : 1;
        float centre = CameraLocked ? _lockCenter : CameraX;
        var pos = new Vector3(centre + side * (ViewHalfWidth + 1.5f), 0, _rng.Range(-2.6f, 2.6f));
        SpawnEnemy(kind, pos);
        _spawnTimer = _rng.Range(25, 55);
    }

    private void UpdateSurvival(ChapterDef chapter)
    {
        if (_waveActive)
        {
            if (_spawnQueue.Count == 0 && AliveEnemyCount() == 0)
            {
                _waveActive = false;
                Stats.WavesSurvived = _survivalWave;
                _survivalRest = 150;
                // A breather: a snack on the floor between waves.
                if (_survivalWave % 2 == 0)
                {
                    SpawnPickup(_survivalWave % 4 == 0 ? PickupKind.NasiPadang : PickupKind.Sate, new Vector3(_lockCenter + _rng.Range(-4f, 4f), 3f, _rng.Range(-2f, 2f))).Also(this, dropped: true);
                }
            }

            return;
        }

        if (--_survivalRest > 0)
        {
            return;
        }

        _survivalWave++;
        // Every fifth wave brings a boss at reduced health.
        if (_survivalWave % 5 == 0)
        {
            var bossId = (BossId)(((_survivalWave / 5) - 1) % Bosses.All.Length);
            var boss = SpawnBoss(bossId, new Vector3(_lockCenter + ViewHalfWidth + 1f, 0, 0));
            ref var hp = ref World.Get<Health>(boss);
            hp.Max = hp.Current = hp.Max * 0.5f;
            World.Get<Fighter>(boss).State = FighterState.Idle;
            World.Get<Boss>(boss).IntroDone = true;
            _waveActive = true;
            _waveMaxAlive = 3;
            Emit(GameEventKind.WaveStart, Entity.Null, Entity.Null, new Vector3(_lockCenter, 0, 0), param: _survivalWave);
            return;
        }

        int budget = 3 + _survivalWave;
        var pool = SurvivalPool(_survivalWave);
        var groups = new EnemyGroup[Math.Min(budget, 12)];
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i] = new EnemyGroup(pool[_rng.Range(0, pool.Length)], 1);
        }

        StartWave(groups, Math.Min(3 + _survivalWave / 2, 7), _lockCenter);
    }

    private static EnemyKind[] SurvivalPool(int wave) => wave switch
    {
        <= 2 => [EnemyKind.PremanGang, EnemyKind.PremanPisau],
        <= 4 => [EnemyKind.PremanGang, EnemyKind.PremanPisau, EnemyKind.PelemparBotol, EnemyKind.JagoanSilat],
        <= 8 => [EnemyKind.PremanPisau, EnemyKind.JagoanSilat, EnemyKind.TukangPukul, EnemyKind.PelemparBotol, EnemyKind.NinjaGaruda],
        _ => [EnemyKind.JagoanSilat, EnemyKind.TukangPukul, EnemyKind.NinjaGaruda, EnemyKind.RobotGaruda],
    };

    private void SpawnBossNow(BossId id, float x)
    {
        var boss = SpawnBoss(id, new Vector3(x, 0, 0));
        World.Get<Transform>(boss).Facing = -1;
        StartCinematic(170);
        Emit(GameEventKind.BossIntro, boss, Entity.Null, World.Get<Transform>(boss).Position, param: (int)id);
    }

    private void UpdateBossOutcome()
    {
        if (!_bossTriggered || _bossDefeated || !World.IsAlive(BossEntity) || Stage.Kind == StageKind.SurvivalArena)
        {
            return;
        }

        if (!World.Get<Health>(BossEntity).Dead)
        {
            return;
        }

        _bossDefeated = true;
        Stats.BossesDefeated++;
        // The boss falls: every remaining minion runs off (counted as KOs, no score).
        var enemies = World.Pool<Enemy>();
        foreach (int id in enemies.EntityIds)
        {
            var e = World.Handle(id);
            if (e != BossEntity && !World.Get<Health>(e).Dead)
            {
                ref var h = ref World.Get<Health>(e);
                h.Current = 0;
                h.Dead = true;
                ref var f = ref World.Get<Fighter>(e);
                SetState(ref f, FighterState.Juggle);
                ref var m = ref World.Get<Motion>(e);
                m.Velocity = new Vector3(_rng.Range(-4f, 4f), 6f, 0);
                m.Grounded = false;
            }
        }

        foreach (int id in World.Pool<Hazard>().EntityIds)
        {
            World.Destroy(World.Handle(id));
        }

        Emit(GameEventKind.BossDefeated, BossEntity, Entity.Null, World.Get<Transform>(BossEntity).Position, param: (int)World.Get<Boss>(BossEntity).Id);
        _endTicks = 240;
    }

    private void UpdateCamera()
    {
        float length = Stage.Chapter.Length;
        if (CameraLocked)
        {
            CameraX = MathX.MoveTowards(CameraX, _lockCenter, 8f * Dt);
            return;
        }

        // Follow the players' average, never scrolling back (classic beat 'em up rule).
        float sum = 0;
        int n = 0;
        float lead = float.MinValue;
        for (int s = 0; s < PlayerCount; s++)
        {
            var e = _players[s];
            if (World.IsAlive(e) && World.Get<Fighter>(e).State != FighterState.Dead)
            {
                float x = World.Get<Transform>(e).Position.X;
                sum += x;
                n++;
                lead = MathF.Max(lead, x);
            }
        }

        if (n == 0)
        {
            return;
        }

        float target = MathF.Max(sum / n + 2f, lead - ViewHalfWidth + 3f);
        float limit = _bossTriggered ? length - ViewHalfWidth : length - ViewHalfWidth;
        if (!_bossTriggered && _waveIndex < Stage.Chapter.Waves.Length)
        {
            // Do not scroll past the next wave's lock point before it triggers.
            limit = MathF.Min(limit, Stage.Chapter.Waves[_waveIndex].TriggerX + 3f);
        }

        target = Math.Clamp(target, ViewHalfWidth, limit);
        if (target > CameraX)
        {
            CameraX = MathX.MoveTowards(CameraX, target, 7f * Dt);
        }
    }

    private void UpdateMusic()
    {
        var music = MusicState.Explore;
        float intensity = 0.2f;
        int alive = AliveEnemyCount();
        if (alive > 0)
        {
            music = MusicState.Combat;
            intensity = MathF.Min(1f, 0.35f + alive * 0.1f);
        }

        if (World.IsAlive(BossEntity) && !_bossDefeated && Stage.Kind != StageKind.SurvivalArena)
        {
            music = MusicState.Boss;
            intensity = 0.6f + 0.2f * World.Get<Boss>(BossEntity).Phase;
        }

        for (int s = 0; s < PlayerCount; s++)
        {
            if (World.IsAlive(_players[s]) && World.Get<Rage>(_players[s]).Active)
            {
                music = MusicState.Rage;
                intensity = 1f;
            }
        }

        if (State == SessionState.StageClear || _bossDefeated)
        {
            music = MusicState.Victory;
        }
        else if (State == SessionState.GameOver)
        {
            music = MusicState.Defeat;
        }

        Music = music;
        MusicIntensity = MathX.MoveTowards(MusicIntensity, intensity, Dt * 0.5f);
    }

    private void UpdateChallenge()
    {
        if (Stage.Challenge is not { } challenge || ChallengeResult is not null)
        {
            return;
        }

        var result = challenge.Evaluate(Stats, _bossDefeated);
        if (result is null)
        {
            return;
        }

        ChallengeResult = result;
        if (result == true)
        {
            _endTicks = 90;
            _bossDefeated = _bossDefeated || Stage.Kind != StageKind.BossArena;
        }
        else
        {
            State = SessionState.GameOver;
            Emit(GameEventKind.GameOver, Entity.Null, Entity.Null, new Vector3(CameraX, 0, 0), param: 1);
        }
    }

    private void UpdateEndOfStage()
    {
        if (_endTicks <= 0 || State is SessionState.StageClear or SessionState.GameOver)
        {
            return;
        }

        if (--_endTicks == 0)
        {
            State = SessionState.StageClear;
            for (int s = 0; s < PlayerCount; s++)
            {
                var e = _players[s];
                if (World.IsAlive(e) && World.Get<Fighter>(e).State is FighterState.Idle or FighterState.Walk)
                {
                    SetState(ref World.Get<Fighter>(e), FighterState.Victory);
                    World.Get<Motion>(e).Velocity = Vector3.Zero;
                }
            }

            Emit(GameEventKind.StageClear, Entity.Null, Entity.Null, new Vector3(CameraX, 0, 0));
        }
    }

    private void AdvanceEndTimers()
    {
        // After the stage ends the world keeps animating (bodies land, victory poses) without new logic.
        CollectFighters();
        UpdateFighters();
        UpdateMotion();
        World.Flush();
    }
}

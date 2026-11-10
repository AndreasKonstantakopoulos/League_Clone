using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using static Fx;

public class MonsterDef
{
    public string Name;
    public float Health, Damage, Armor, MR, Range, Interval, Speed, Scale, Aggro, Leash, SlamRadius, SlamDamage, Gold, Xp;
    public Color Color;
}

// A big objective in the middle of the jungle. Killing it buffs the killer's whole team.
public class Objective
{
    public string Name, BuffName;
    public Vector3 Pos;
    public MonsterDef Monster;
    public Stats Buff;
    public float BuffSeconds, TeamGold, NextSpawn, Respawn;
    public Unit Alive;
    public bool RemoteAlive; // clients: reported by the host
    public bool IsAlive => Net.IsClientOnly ? RemoteAlive : Alive != null && Alive.Alive;
}

public struct ActiveBuff { public string Name; public Team Team; public float Until; }

// Spawns and respawns the four neutral camps and the two big objectives (markers placed by the map builder).
public class JungleManager : MonoBehaviour
{
    static readonly MonsterDef Brute = new MonsterDef { Name = "Brute", Health = 900, Damage = 45, Armor = 15, MR = 15, Range = 2.5f, Interval = 1.4f, Speed = 3.5f, Scale = 1.4f, Aggro = 6, Leash = 12, Gold = 55, Xp = 90, Color = new Color(0.55f, 0.4f, 0.25f) };
    static readonly MonsterDef Imp = new MonsterDef { Name = "Imp", Health = 400, Damage = 25, Armor = 5, MR = 5, Range = 2f, Interval = 1.2f, Speed = 4f, Scale = 0.8f, Aggro = 6, Leash = 12, Gold = 25, Xp = 40, Color = new Color(0.45f, 0.6f, 0.3f) };
    static readonly MonsterDef Drake = new MonsterDef { Name = "Ember Drake", Health = 2800, Damage = 80, Armor = 30, MR = 30, Range = 3.5f, Interval = 1.5f, Speed = 3f, Scale = 2.6f, Aggro = 5, Leash = 14, SlamRadius = 4.5f, SlamDamage = 90, Gold = 150, Xp = 250, Color = new Color(1f, 0.45f, 0.15f) };
    static readonly MonsterDef Titan = new MonsterDef { Name = "Void Titan", Health = 5200, Damage = 110, Armor = 45, MR = 45, Range = 4f, Interval = 1.6f, Speed = 3f, Scale = 3.4f, Aggro = 5, Leash = 14, SlamRadius = 5.5f, SlamDamage = 140, Gold = 300, Xp = 400, Color = new Color(0.5f, 0.2f, 0.8f) };

    class Camp
    {
        public Vector3 Pos;
        public readonly List<Unit> Members = new List<Unit>();
        public float RespawnAt = Rules.JungleFirstSpawn; // < 0 means "alive, waiting to be cleared"
    }

    public readonly List<Objective> Objectives = new List<Objective>();
    public readonly List<ActiveBuff> ActiveBuffs = new List<ActiveBuff>();
    readonly List<Camp> camps = new List<Camp>();

    void Awake()
    {
        if (!Net.IsClientOnly) // clients never spawn monsters
        foreach (var side in new[] { "Blue", "Red" })
            foreach (var lane in new[] { "Top", "Bottom" })
            {
                var marker = GameObject.Find($"Camp_{side}_{lane}");
                if (marker != null) camps.Add(new Camp { Pos = marker.transform.position });
            }
        AddObjective("BigBuff_Top", Drake, "Drake's Fury", St(ad: 20, ap: 30, ms: 0.6f), 120f, 100f, Rules.DrakeFirst, Rules.DrakeRespawn);
        AddObjective("BigBuff_Bottom", Titan, "Void Might", St(hp: 400, armor: 25, mr: 25, ad: 30, ap: 45, hpr: 2f), 180f, 200f, Rules.TitanFirst, Rules.TitanRespawn);
        if (camps.Count == 0 && Objectives.Count == 0) Debug.LogWarning("No jungle markers found. Rebuild the map with Tools > Build Greybox Map.");
    }

    void OnEnable() { Unit.AnyDied += OnDied; }
    void OnDisable() { Unit.AnyDied -= OnDied; }

    void AddObjective(string marker, MonsterDef m, string buffName, Stats buff, float seconds, float teamGold, float first, float respawn)
    {
        var g = GameObject.Find(marker);
        if (g == null) return;
        Objectives.Add(new Objective
        {
            Name = m.Name, BuffName = buffName, Pos = g.transform.position, Monster = m, Buff = buff,
            BuffSeconds = seconds, TeamGold = teamGold, NextSpawn = first, Respawn = respawn
        });
    }

    void Update()
    {
        var gm = GameManager.I;
        if (gm == null || gm.State != MatchState.Playing || Net.IsClientOnly) return;
        float now = gm.Clock;

        foreach (var camp in camps)
        {
            camp.Members.RemoveAll(m => m == null || !m.Alive);
            if (camp.Members.Count > 0) continue;
            if (camp.RespawnAt < 0f) camp.RespawnAt = now + Rules.CampRespawn; // just cleared: start the timer
            else if (now >= camp.RespawnAt)
            {
                camp.Members.Add(Spawn(Brute, camp.Pos));
                camp.Members.Add(Spawn(Imp, camp.Pos + new Vector3(2f, 0f, 1f)));
                camp.Members.Add(Spawn(Imp, camp.Pos + new Vector3(-2f, 0f, 1f)));
                camp.RespawnAt = -1f;
            }
        }

        foreach (var o in Objectives)
        {
            if (o.Alive != null && o.Alive.Alive) continue;
            o.Alive = null;
            if (now >= o.NextSpawn) o.Alive = Spawn(o.Monster, o.Pos);
        }
    }

    static Unit Spawn(MonsterDef d, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Monster_" + d.Name;
        go.transform.localScale = Vector3.one * d.Scale;
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().material.color = d.Color;
        go.transform.position = GameManager.Snap(pos);

        var agent = go.AddComponent<NavMeshAgent>();
        agent.baseOffset = d.Scale; // capsule pivot is its centre
        agent.radius = 0.4f * d.Scale;
        agent.acceleration = 30f;
        var u = go.AddComponent<Unit>();
        u.Init(Team.Neutral, UnitKind.Monster, new Stats { Health = d.Health, AD = d.Damage, Armor = d.Armor, MR = d.MR, MoveSpeed = d.Speed }, 0.5f * d.Scale);
        u.GoldBounty = d.Gold;
        u.XpBounty = d.Xp;
        go.AddComponent<Monster>().Init(d, go.transform.position);
        return u;
    }

    // Killing an objective buffs every champion on the killer's team and pays them gold.
    void OnDied(Unit victim, Unit killer)
    {
        foreach (var o in Objectives)
        {
            if (victim != o.Alive) continue;
            o.NextSpawn = GameManager.I.Clock + o.Respawn;
            if (killer == null) return;
            Team team = killer.Team;
            foreach (var c in Champion.All)
                if (c.U.Team == team)
                {
                    c.U.AddBuff(o.Buff, o.BuffSeconds); // shortcut: champions that are dead right now miss the buff
                    c.Gold += o.TeamGold;
                }
            ActiveBuffs.RemoveAll(b => b.Until <= Time.time);
            ActiveBuffs.Add(new ActiveBuff { Name = o.BuffName, Team = team, Until = Time.time + o.BuffSeconds });
            GameManager.I.Announce($"{team} team slew the {o.Name}!  {o.BuffName} for {o.BuffSeconds:0}s, +{o.TeamGold:0} gold each");
            return;
        }
    }
}

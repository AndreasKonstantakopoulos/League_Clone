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

// A boss monster. Killing it buffs the killer's whole team.
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

// A capture point. Stand on it alone to take it; it then locks for a while.
public class Altar
{
    public string Name;
    public Vector3 Pos;
    public Team Owner = Team.Neutral, Capturer = Team.Neutral;
    public float LockedUntil = Rules.AltarUnlock, Progress; // LockedUntil is match-clock time; Progress is seconds stood on it
    public Renderer Rend;
}

public struct ActiveBuff { public string Name; public Team Team; public float Until; }

// The jungle, Twisted Treeline style: three camps per side, Vilemaw, two altars and the health relic
// (all placed on markers made by the map builder). The host runs the logic; clients only show the state they are sent.
public class JungleManager : MonoBehaviour
{
    static MonsterDef M(string name, float hp, float dmg, float scale, float gold, float xp, Color c, float range = 2.3f) => new MonsterDef
    {
        Name = name, Health = hp, Damage = dmg, Armor = 10, MR = 10, Range = range, Interval = 1.3f, Speed = 3.8f,
        Scale = scale, Aggro = 6, Leash = 12, Gold = gold, Xp = xp, Color = c
    };
    static readonly MonsterDef Wolf = M("Giant Wolf", 950, 42, 1.3f, 50, 90, new Color(0.45f, 0.5f, 0.6f));
    static readonly MonsterDef Pup = M("Wolf", 380, 20, 0.8f, 18, 35, new Color(0.5f, 0.55f, 0.65f));
    static readonly MonsterDef Wraith = M("Wraith", 800, 48, 1.2f, 48, 85, new Color(0.4f, 0.85f, 0.8f), 5f);
    static readonly MonsterDef Wisp = M("Lesser Wraith", 300, 22, 0.7f, 16, 30, new Color(0.5f, 0.9f, 0.85f), 5f);
    static readonly MonsterDef Golem = M("Golem", 1200, 55, 1.6f, 60, 110, new Color(0.5f, 0.42f, 0.35f));
    static readonly MonsterDef Vilemaw = new MonsterDef
    {
        Name = "Vilemaw", Health = 5500, Damage = 110, Armor = 40, MR = 40, Range = 4f, Interval = 1.5f, Speed = 3f, Scale = 3.4f,
        Aggro = 6, Leash = 13, SlamRadius = 5.5f, SlamDamage = 130, Gold = 250, Xp = 400, Color = new Color(0.45f, 0.75f, 0.2f)
    };

    class Camp
    {
        public Vector3 Pos;
        public MonsterDef[] Spawns;
        public readonly List<Unit> Members = new List<Unit>();
        public float RespawnAt = Rules.JungleFirstSpawn; // < 0 means "alive, waiting to be cleared"
    }

    public readonly List<Objective> Objectives = new List<Objective>();
    public readonly List<ActiveBuff> ActiveBuffs = new List<ActiveBuff>();
    public readonly List<Altar> Altars = new List<Altar>();
    public bool RelicUp;
    readonly List<Camp> camps = new List<Camp>();
    Vector3 relicPos;
    GameObject relicOrb;
    float relicNext = Rules.AltarUnlock;

    void Awake()
    {
        if (!Net.IsClientOnly) // clients never spawn monsters
            foreach (var side in new[] { "Blue", "Red" })
            {
                AddCamp($"Camp_{side}_Wolves", Wolf, Pup, Pup);
                AddCamp($"Camp_{side}_Wraiths", Wraith, Wisp, Wisp);
                AddCamp($"Camp_{side}_Golems", Golem, Golem);
            }

        var lair = GameObject.Find("Vilemaw");
        if (lair != null)
            Objectives.Add(new Objective
            {
                Name = "Vilemaw", BuffName = "Crest of Crushing Wrath", Pos = lair.transform.position, Monster = Vilemaw,
                Buff = St(ad: 15, ap: 25, aspd: 0.2f, cdr: 0.1f, hpr: 3f, mpr: 2f), BuffSeconds = 150f, TeamGold = 190f,
                NextSpawn = Rules.VilemawFirst, Respawn = Rules.VilemawRespawn
            });

        foreach (var name in new[] { "Altar_West", "Altar_East" })
        {
            var g = GameObject.Find(name);
            if (g != null) Altars.Add(new Altar { Name = name == "Altar_West" ? "West altar" : "East altar", Pos = g.transform.position, Rend = g.GetComponent<Renderer>() });
        }

        var relic = GameObject.Find("Relic");
        if (relic != null)
        {
            relicPos = relic.transform.position;
            relicOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            relicOrb.name = "RelicOrb";
            Destroy(relicOrb.GetComponent<Collider>());
            relicOrb.transform.position = relicPos + Vector3.up * 1.2f;
            relicOrb.GetComponent<Renderer>().material.color = new Color(1f, 0.45f, 0.75f);
            relicOrb.SetActive(false);
        }
        if (Objectives.Count == 0 || Altars.Count == 0) Debug.LogWarning("Jungle markers missing. Rebuild the map with Tools > Build Greybox Map.");
    }

    void AddCamp(string marker, params MonsterDef[] spawns)
    {
        var g = GameObject.Find(marker);
        if (g != null) camps.Add(new Camp { Pos = g.transform.position, Spawns = spawns });
    }

    void OnEnable() { Unit.AnyDied += OnDied; }
    void OnDisable() { Unit.AnyDied -= OnDied; }

    public int AltarsOf(Team t)
    {
        int n = 0;
        foreach (var a in Altars) if (a.Owner == t) n++;
        return n;
    }

    // One altar: extra gold for every minion or monster your team kills.
    public float AltarGold(Team t) { return AltarsOf(t) >= 1 ? Rules.AltarGoldPerKill : 0f; }

    void Update()
    {
        var gm = GameManager.I;
        if (gm == null) return;
        ShowState(gm);
        if (gm.State != MatchState.Playing || Net.IsClientOnly) return;
        float now = gm.Clock;

        foreach (var camp in camps)
        {
            camp.Members.RemoveAll(m => m == null || !m.Alive);
            if (camp.Members.Count > 0) continue;
            if (camp.RespawnAt < 0f) camp.RespawnAt = now + Rules.CampRespawn; // just cleared: start the timer
            else if (now >= camp.RespawnAt)
            {
                for (int i = 0; i < camp.Spawns.Length; i++)
                    camp.Members.Add(Spawn(camp.Spawns[i], camp.Pos + new Vector3((i - (camp.Spawns.Length - 1) / 2f) * 2.4f, 0f, i % 2)));
                camp.RespawnAt = -1f;
            }
        }

        foreach (var o in Objectives)
        {
            if (o.Alive != null && o.Alive.Alive) continue;
            o.Alive = null;
            if (now >= o.NextSpawn) o.Alive = Spawn(o.Monster, o.Pos);
        }

        foreach (var a in Altars) TickAltar(a, now);
        TickRelic(now);
    }

    // Colours and the relic orb, on every machine.
    void ShowState(GameManager gm)
    {
        foreach (var a in Altars)
        {
            if (a.Rend == null) continue;
            Color c = a.Owner == Team.Neutral ? new Color(0.5f, 0.55f, 0.55f) : Vfx.TeamColor(a.Owner);
            if (gm.Clock < a.LockedUntil) c *= 0.55f;                                           // locked: dimmed
            else if (a.Progress > 0f) c = Color.Lerp(c, Vfx.TeamColor(a.Capturer), a.Progress / Rules.AltarCaptureTime); // being taken
            a.Rend.material.color = c;
        }
        if (relicOrb != null && relicOrb.activeSelf != RelicUp) relicOrb.SetActive(RelicUp);
    }

    // A team captures an unlocked altar by having champions on it, with no enemy champion on it, for AltarCaptureTime seconds.
    void TickAltar(Altar a, float now)
    {
        if (now < a.LockedUntil) { a.Progress = 0f; return; }
        bool blue = false, red = false;
        foreach (var c in Champion.All)
        {
            if (!c.U.Alive || c.U.DistTo(a.Pos) > Rules.AltarRadius) continue;
            if (c.U.Team == Team.Blue) blue = true; else red = true;
        }
        Team taker = blue == red ? Team.Neutral : blue ? Team.Blue : Team.Red; // nobody, or contested
        if (taker == Team.Neutral || taker == a.Owner) { a.Progress = 0f; return; }
        if (taker != a.Capturer) { a.Capturer = taker; a.Progress = 0f; }
        a.Progress += Time.deltaTime;
        if (a.Progress < Rules.AltarCaptureTime) return;

        a.Owner = taker;
        a.Progress = 0f;
        a.LockedUntil = now + Rules.AltarLock;
        ApplyAltarBonuses();
        Vfx.Ring(a.Pos, 3.5f, Vfx.TeamColor(taker));
        int n = AltarsOf(taker);
        GameManager.I.Announce($"{TeamText.Name(taker)} team captured the {a.Name}!  " +
            (n >= 2 ? "Both altars: +10% attack damage and ability power" : $"+{Rules.AltarGoldPerKill:0} gold per minion and monster kill"));
    }

    // Two altars: +10% attack damage and ability power for the whole team.
    public void ApplyAltarBonuses()
    {
        foreach (var c in Champion.All)
        {
            c.U.PowerMult = AltarsOf(c.U.Team) >= 2 ? 1.1f : 1f;
            c.U.Recalc();
        }
    }

    // The relic heals whoever walks over it and gives a short burst of speed, then comes back later.
    void TickRelic(float now)
    {
        if (relicOrb == null) return;
        if (!RelicUp) { if (now >= relicNext) RelicUp = true; return; }
        foreach (var c in Champion.All)
        {
            if (!c.U.Alive || c.U.DistTo(relicPos) > 2f) continue;
            c.U.Heal(c.U.Total.Health * 0.2f);
            c.U.Mana = Mathf.Min(c.U.Total.Mana, c.U.Mana + c.U.Total.Mana * 0.2f);
            c.U.AddBuff(St(ms: 2f), 4f, "Relic speed");
            Vfx.Ring(relicPos, 2f, new Color(1f, 0.45f, 0.75f));
            RelicUp = false;
            relicNext = now + Rules.RelicRespawn;
            return;
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

    // Killing a boss buffs every champion on the killer's team and pays them gold.
    void OnDied(Unit victim, Unit killer)
    {
        foreach (var o in Objectives)
        {
            if (victim != o.Alive) continue;
            o.NextSpawn = GameManager.I.Clock + o.Respawn;
            if (killer == null || killer.Team == Team.Neutral) return;
            Team team = killer.Team;
            foreach (var c in Champion.All)
                if (c.U.Team == team)
                {
                    c.U.AddBuff(o.Buff, o.BuffSeconds, o.BuffName); // shortcut: champions that are dead right now miss the buff
                    c.Gold += o.TeamGold;
                }
            ActiveBuffs.RemoveAll(b => b.Until <= Time.time);
            ActiveBuffs.Add(new ActiveBuff { Name = o.BuffName, Team = team, Until = Time.time + o.BuffSeconds });
            GameManager.I.Announce($"{TeamText.Name(team)} team slew {o.Name}!  {o.BuffName} for {o.BuffSeconds:0}s, +{o.TeamGold:0} gold each");
            return;
        }
    }
}

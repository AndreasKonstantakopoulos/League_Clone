using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public enum MatchState { Waiting, Playing, Over } // Waiting: host is waiting for every player's map to load

public static class Rules
{
    public const float WaveInterval = 20f;  // a minion wave spawns every 20 seconds
    public const float FirstWave = 5f;
    public const float PassiveGold = 2f;    // gold per second
    public const int MeleePerWave = 3, CastersPerWave = 1;
    // Jungle timings are short on purpose so objectives are easy to test; raise them for real matches.
    public const float JungleFirstSpawn = 15f, CampRespawn = 60f;
    public const float DrakeFirst = 90f, DrakeRespawn = 180f, TitanFirst = 240f, TitanRespawn = 300f;
}

// Wires the greybox map up at runtime (towers, Nexus, waypoints found by name), spawns waves, handles rewards and the win.
public class GameManager : MonoBehaviour
{
    public static GameManager I;
    public MatchState State = MatchState.Waiting;
    public float Clock;
    public Team Winner;
    public JungleManager Jungle;
    public string Banner;
    public float BannerUntil;

    public void Announce(string text)
    {
        Banner = text;
        BannerUntil = Time.time + 6f;
        if (Net.IsHost) Net.FxAnnounce(text);
    }

    static readonly string[] Lanes = { "Top", "Bottom" };
    readonly Dictionary<string, List<Vector3>> lanePaths = new Dictionary<string, List<Vector3>>(); // blue base -> red base
    readonly Unit[] nexus = new Unit[2];
    readonly Transform[] spawn = new Transform[2];
    float nextWave = Rules.FirstWave;

    readonly Transform[] shopNpc = new Transform[2];

    public Vector3 SpawnOf(Team t) => spawn[(int)t].position;
    public Transform ShopNpc(Team t) => shopNpc[(int)t];

    void Awake()
    {
        I = this;
        string problem = Content.Validate();
        if (problem != null) Debug.LogError("Content check failed: " + problem);
        else Debug.Log($"Content OK: {Content.Champions.Count} champions, {Content.Champions.Count * 4} abilities, {Content.Items.Count} items");
        SetupMap();
        gameObject.AddComponent<Hud>();
        Jungle = gameObject.AddComponent<JungleManager>();
        gameObject.AddComponent<Fog>();
        Unit.AnyDied += OnDied;
    }

    void OnDestroy()
    {
        Unit.AnyDied -= OnDied;
        if (I == this) I = null;
    }

    void Start()
    {
        if (!Net.Online) { StartMatch(Session.Selected ?? Content.Champions[0]); return; } // solo: champion from the ChampSelect scene
        if (Net.IsHost) StartCoroutine(HostStart());
        else { State = MatchState.Playing; Net.SendReady(); } // clients just show what the host sends
    }

    // Host: wait (up to 20 s) until everyone's map has loaded, then put each player's champion on their team's spawn.
    IEnumerator HostStart()
    {
        float t0 = Time.time;
        while (!Net.AllReady && Time.time - t0 < 20f) yield return null;
        var slot = new int[2];
        foreach (var p in Net.Players)
            SpawnChampion(Content.Champions[p.ChampIndex], p.Team, slot[(int)p.Team]++, p.Id, p.Id == Net.MyId);
        State = MatchState.Playing;
    }

    static GameObject Find(string name) =>
        GameObject.Find(name) ?? throw new System.Exception($"Missing map object {name}. Run Tools > Build Greybox Map.");

    void SetupMap()
    {
        foreach (Team team in new[] { Team.Blue, Team.Red })
        {
            string tn = team.ToString();
            spawn[(int)team] = Find($"Spawn_{tn}").transform;
            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule); // clickable shopkeeper standing on the shop pad
            npc.name = $"ShopNpc_{tn}";
            Destroy(npc.GetComponent<Collider>());
            npc.transform.position = Find($"Shop_{tn}").transform.position + Vector3.up * 1.1f;
            npc.GetComponent<Renderer>().material.color = new Color(1f, 0.85f, 0.2f);
            shopNpc[(int)team] = npc.transform;
            if (Net.IsClientOnly) continue; // clients get towers and Nexus from the host's spawn messages
            nexus[(int)team] = MakeStructure(Find($"Nexus_{tn}"), team, UnitKind.Nexus, 3500f, 3f);
            foreach (var lane in Lanes)
            {
                var outer = MakeStructure(Find($"Tower_{tn}_{lane}_Outer"), team, UnitKind.Tower, 2500f, 1.2f);
                var inner = MakeStructure(Find($"Tower_{tn}_{lane}_Inner"), team, UnitKind.Tower, 2500f, 1.2f);
                inner.Requires.Add(outer);            // outer tower falls first
                nexus[(int)team].Requires.Add(inner); // then both inner towers, then the Nexus
            }
        }
        if (Net.IsClientOnly) return;
        foreach (var lane in Lanes)
        {
            var path = new List<Vector3>();
            for (int i = 0; i < 9; i++) path.Add(Find($"Waypoint_{lane}_{i:00}").transform.position);
            lanePaths[lane] = path;
        }
    }

    static Unit MakeStructure(GameObject go, Team team, UnitKind kind, float hp, float radius)
    {
        var u = go.AddComponent<Unit>();
        u.Init(team, kind, new Stats { Health = hp, Armor = 40f, MR = 40f }, radius);
        if (kind == UnitKind.Tower)
        {
            u.GoldBounty = 150f;
            u.XpBounty = 200f;
            go.AddComponent<Tower>();
        }
        return u;
    }

    public static Vector3 Snap(Vector3 p) =>
        NavMesh.SamplePosition(p, out var hit, 5f, NavMesh.AllAreas) ? hit.position : p;

    // slot spreads teammates along the spawn pad so they do not stand on each other
    Champion SpawnChampion(ChampionDef def, Team team, int slot, ulong owner, bool local)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Player_" + def.Name;
        var ch = go.AddComponent<Champion>();
        ch.OwnerId = owner;
        ch.Init(def, team, Snap(SpawnOf(team) + new Vector3(0f, 0f, (slot - 1) * 2.5f)), local);
        if (local)
        {
            var cam = Camera.main;
            if (cam != null) cam.gameObject.AddComponent<CameraFollow>().Target = go.transform;
        }
        if (Net.IsHost) Net.RegisterChampion(owner, ch);
        return ch;
    }

    public void StartMatch(ChampionDef def)
    {
        SpawnChampion(def, Team.Blue, 1, 0, true);
        State = MatchState.Playing;
    }

    void Update()
    {
        if (Net.IsClientOnly && Net.Lost) { Net.Shutdown(); SceneManager.LoadScene("ChampSelect"); return; } // host went away
        if (State != MatchState.Playing || Net.IsClientOnly) return; // clients get the clock from the host
        Clock += Time.deltaTime;
        if (Clock < nextWave) return;
        nextWave += Rules.WaveInterval;
        foreach (Team team in new[] { Team.Blue, Team.Red })
            foreach (var lane in Lanes)
                StartCoroutine(SpawnWave(team, lane));
    }

    IEnumerator SpawnWave(Team team, string lane)
    {
        int total = Rules.MeleePerWave + Rules.CastersPerWave;
        for (int i = 0; i < total; i++)
        {
            if (State != MatchState.Playing) yield break;
            SpawnMinion(team, lane, i >= Rules.MeleePerWave); // casters trail the melee line
            yield return new WaitForSeconds(0.6f);
        }
    }

    void SpawnMinion(Team team, string lane, bool caster)
    {
        var path = new List<Vector3>(lanePaths[lane]);
        if (team == Team.Red) path.Reverse();

        float scale = caster ? 0.8f : 0.9f;
        var go = GameObject.CreatePrimitive(caster ? PrimitiveType.Cube : PrimitiveType.Capsule);
        go.name = $"Minion_{team}_{lane}";
        go.transform.localScale = Vector3.one * scale;
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().material.color = team == Team.Blue ? new Color(0.4f, 0.6f, 1f) : new Color(1f, 0.45f, 0.45f);
        go.transform.position = Snap(path[0]);

        var agent = go.AddComponent<NavMeshAgent>();
        agent.baseOffset = scale * (caster ? 0.5f : 1f); // primitives pivot at their centre
        agent.radius = 0.4f;
        agent.acceleration = 30f;
        agent.avoidancePriority = Random.Range(30, 70);
        var u = go.AddComponent<Unit>();
        u.Init(team, UnitKind.Minion, new Stats
        {
            Health = caster ? 300f : 450f, AD = caster ? 28f : 22f, MoveSpeed = 3.8f
        }, 0.5f);
        u.GoldBounty = caster ? 16f : 20f;
        u.XpBounty = caster ? 30f : 40f;
        go.AddComponent<Minion>().Init(path, nexus[team == Team.Blue ? 1 : 0].transform.position, caster);
    }

    void OnDied(Unit victim, Unit killer)
    {
        if (victim.Kind == UnitKind.Champion) victim.GetComponent<Champion>().OnDeath();
        if (victim.Kind == UnitKind.Nexus)
        {
            Finish(victim.Team == Team.Blue ? Team.Red : Team.Blue);
            return;
        }
        // last hit gets the gold; nearby enemy champions share the XP
        var k = killer != null ? killer.GetComponent<Champion>() : null;
        if (k != null) k.Gold += victim.GoldBounty;
        foreach (var c in Champion.All)
            if (c.U.Team != victim.Team && c.U.Alive && c.U.DistTo(victim.transform.position) < 25f)
                c.GainXp((int)victim.XpBounty);
    }

    void Finish(Team winner)
    {
        if (State == MatchState.Over) return;
        State = MatchState.Over;
        Winner = winner;
    }

    public void Rematch() { SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
    public void ToChampSelect()
    {
        if (Net.Online) Net.Shutdown();
        SceneManager.LoadScene("ChampSelect");
    }
}

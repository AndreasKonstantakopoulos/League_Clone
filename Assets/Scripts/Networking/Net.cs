using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum NetMode { Solo, Host, Client }

// Networking layer on top of Netcode's transport and custom messages (no network prefabs needed).
//   The host runs the whole simulation exactly like solo play (minions, towers, jungle, every champion).
//   Clients never simulate: they send commands (move, cast, buy...) and mirror what the host sends.
//   Host -> clients: "spawn" once per entity, "snap" ~15 times a second (positions, health, objectives),
//   "own" private state for each player's champion (gold, cooldowns, items) and "fx" for particle effects.
public static class Net
{
    public class Player { public ulong Id; public int ChampIndex; public Team Team; public bool Ready; }

    enum Cmd : byte { Move, Attack, Cast, Recall, Rank, Buy, Sell }
    enum FxType : byte { Auto, Cast, Tower, Ring, Slam, Announce }

    public const ushort DefaultPort = 7777;
    public const int MaxPlayers = 6;
    const NetworkDelivery Delivery = NetworkDelivery.ReliableFragmentedSequenced;
    const float SendInterval = 1f / 15f;

    public static NetMode Mode = NetMode.Solo;
    public static bool Online => Mode != NetMode.Solo;
    public static bool IsHost => Mode == NetMode.Host;
    public static bool IsClientOnly => Mode == NetMode.Client; // the simulation runs on the host, not here
    public static string Status = "";
    public static bool Lost;          // the connection to the host dropped
    public static bool MatchStarted;
    public static readonly List<Player> Players = new List<Player>(); // same list on every peer, host is the authority

    static NetworkManager nm;
    static GameObject root;
    static float sendTimer, pruneTimer;
    static int nextId;
    static readonly Dictionary<int, Unit> byId = new Dictionary<int, Unit>();                    // host: real units, client: mirrors
    static readonly Dictionary<ulong, HashSet<int>> seen = new Dictionary<ulong, HashSet<int>>(); // host: which spawns each client has
    static readonly Dictionary<ulong, Champion> champOf = new Dictionary<ulong, Champion>();

    public static ulong MyId => nm != null ? nm.LocalClientId : 0UL;
    public static Player Me => Players.Find(p => p.Id == MyId);
    public static bool AllReady => Players.TrueForAll(p => p.Ready);

    static string localIp;
    public static string LocalAddress()
    {
        if (localIp != null) return localIp;
        localIp = "127.0.0.1";
        try
        {
            foreach (var a in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
                if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) { localIp = a.ToString(); break; }
        }
        catch { /* offline machines just keep the loopback address */ }
        return localIp;
    }

    // ---------------- connection ----------------
    public static string JoinCode = "";   // Relay games: share this instead of an IP address
    static bool busy;

    // Direct connection (LAN or port-forwarded). This exposes the host's IP to the players.
    public static void StartHost(ushort port) { Direct("127.0.0.1", port, true); }
    public static void Join(string ip, ushort port) { Direct(ip.Trim(), port, false); }

    // Builds the NetworkManager and transport but does not start them.
    static UnityTransport Prepare(bool host)
    {
        Shutdown();
        Application.runInBackground = true; // a window that loses focus must keep simulating or the peers drift apart
        Lost = false;
        MatchStarted = false;
        JoinCode = "";
        root = new GameObject("Net");
        root.SetActive(false); // configure before the NetworkManager wakes up
        Object.DontDestroyOnLoad(root);
        nm = root.AddComponent<NetworkManager>();
        var transport = root.AddComponent<UnityTransport>();
        nm.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
        root.AddComponent<NetDriver>();
        root.SetActive(true);
        Mode = host ? NetMode.Host : NetMode.Client;
        nm.OnClientConnectedCallback += OnConnected;
        nm.OnClientDisconnectCallback += OnDisconnected;
        return transport;
    }

    static void Direct(string ip, ushort port, bool host)
    {
        var transport = Prepare(host);
        transport.SetConnectionData(ip, port, host ? "0.0.0.0" : null);
        bool ok = host ? nm.StartHost() : nm.StartClient();
        if (!ok) { Shutdown(); Status = "Could not start networking"; return; }
        Begin(host, host ? $"Hosting on {LocalAddress()}:{port}" : "Connecting...");
    }

    // Registers the message handlers and, for the host, adds the host as the first player.
    static void Begin(bool host, string status)
    {
        RegisterHandlers();
        Players.Clear();
        if (host)
        {
            Players.Add(new Player { Id = nm.LocalClientId, Ready = true });
            AssignTeams();
        }
        Status = status;
    }

    static async System.Threading.Tasks.Task SignIn()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    // Relay: Unity's Multiplayer Services create the session and a short join code and start the NetworkManager
    // for us. Players only ever see the code, never an IP address.
    public static async void HostRelay()
    {
        if (busy) return;
        busy = true;
        try
        {
            Prepare(true);
            Status = "Creating Relay game...";
            await SignIn();
            var session = await MultiplayerService.Instance.CreateSessionAsync(new SessionOptions { MaxPlayers = MaxPlayers }.WithRelayNetwork());
            if (nm == null || !nm.IsListening) throw new System.Exception("The Multiplayer Services SDK did not start the host");
            JoinCode = session.Code;
            Begin(true, "Hosting via Relay");
        }
        catch (System.Exception e) { Debug.LogException(e); Shutdown(); Status = "Relay failed: " + e.Message; }
        finally { busy = false; }
    }

    public static async void JoinRelay(string code)
    {
        if (busy) return;
        busy = true;
        try
        {
            Prepare(false);
            Status = "Joining...";
            await SignIn();
            await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
            if (nm == null || !nm.IsClient) throw new System.Exception("The Multiplayer Services SDK did not start the client");
            Begin(false, "Connecting...");
        }
        catch (System.Exception e) { Debug.LogException(e); Shutdown(); Status = "Could not join: " + e.Message; }
        finally { busy = false; }
    }

    public static void Shutdown()
    {
        if (nm != null) nm.Shutdown();
        if (root != null) Object.Destroy(root);
        nm = null;
        root = null;
        Mode = NetMode.Solo;
        MatchStarted = false;
        JoinCode = "";
        Players.Clear();
        byId.Clear();
        seen.Clear();
        champOf.Clear();
        nextId = 0;
    }

    static void OnConnected(ulong id)
    {
        if (IsClientOnly) { Status = "Connected to host"; return; }
        if (Players.Exists(p => p.Id == id)) return; // the host's own connection
        if (Players.Count >= MaxPlayers || MatchStarted) { nm.DisconnectClient(id); return; }
        Players.Add(new Player { Id = id });
        AssignTeams();
        BroadcastLobby();
    }

    static void OnDisconnected(ulong id)
    {
        if (IsClientOnly) { Lost = true; Status = "Disconnected from host"; return; }
        if (MatchStarted) return; // the champion simply stops responding
        Players.RemoveAll(p => p.Id == id);
        AssignTeams();
        BroadcastLobby();
    }

    static void AssignTeams()
    {
        for (int i = 0; i < Players.Count; i++) Players[i].Team = i % 2 == 0 ? Team.Blue : Team.Red;
    }

    // ---------------- sending helpers ----------------
    static void SendTo(ulong id, string name, System.Action<FastBufferWriter> fill)
    {
        if (nm == null) return;
        using (var w = new FastBufferWriter(256, Allocator.Temp, 1 << 20))
        {
            fill(w);
            nm.CustomMessagingManager.SendNamedMessage(name, id, w, Delivery);
        }
    }

    static void ToHost(string name, System.Action<FastBufferWriter> fill)
    {
        if (nm != null && nm.IsConnectedClient) SendTo(NetworkManager.ServerClientId, name, fill);
    }

    // Host -> every other player (only those whose map has loaded when readyOnly is set).
    // `to` limits the recipients by team (used so fog of war also hides information, not just pictures).
    static void ToClients(string name, System.Action<FastBufferWriter> fill, bool readyOnly, System.Func<Team, bool> to = null)
    {
        if (nm == null || !IsHost) return;
        using (var w = new FastBufferWriter(256, Allocator.Temp, 1 << 20))
        {
            fill(w);
            foreach (var p in Players)
                if (p.Id != MyId && (!readyOnly || p.Ready) && (to == null || to(p.Team))) nm.CustomMessagingManager.SendNamedMessage(name, p.Id, w, Delivery);
        }
    }

    static void RegisterHandlers()
    {
        var mm = nm.CustomMessagingManager;
        mm.RegisterNamedMessageHandler("pick", OnPick);
        mm.RegisterNamedMessageHandler("ready", OnReady);
        mm.RegisterNamedMessageHandler("cmd", OnCmd);
        mm.RegisterNamedMessageHandler("lobby", OnLobby);
        mm.RegisterNamedMessageHandler("start", OnStart);
        mm.RegisterNamedMessageHandler("spawn", OnSpawn);
        mm.RegisterNamedMessageHandler("snap", OnSnap);
        mm.RegisterNamedMessageHandler("own", OnOwn);
        mm.RegisterNamedMessageHandler("fx", OnFx);
    }

    // ---------------- lobby ----------------
    public static void Pick(int champIndex)
    {
        if (IsHost)
        {
            var me = Me;
            if (me != null) me.ChampIndex = champIndex;
            BroadcastLobby();
        }
        else if (IsClientOnly) ToHost("pick", w => w.WriteValueSafe(champIndex));
    }

    static void OnPick(ulong from, FastBufferReader r)
    {
        r.ReadValueSafe(out int idx);
        var p = Players.Find(x => x.Id == from);
        if (!IsHost || p == null || idx < 0 || idx >= Content.Champions.Count) return;
        p.ChampIndex = idx;
        BroadcastLobby();
    }

    static void WriteLobby(FastBufferWriter w)
    {
        w.WriteValueSafe(Players.Count);
        foreach (var p in Players)
        {
            w.WriteValueSafe(p.Id);
            w.WriteValueSafe(p.ChampIndex);
            w.WriteValueSafe((byte)p.Team);
        }
    }

    static void ReadLobby(FastBufferReader r)
    {
        r.ReadValueSafe(out int n);
        var list = new List<Player>();
        for (int i = 0; i < n; i++)
        {
            r.ReadValueSafe(out ulong id);
            r.ReadValueSafe(out int champ);
            r.ReadValueSafe(out byte team);
            list.Add(new Player { Id = id, ChampIndex = Mathf.Clamp(champ, 0, Content.Champions.Count - 1), Team = (Team)team });
        }
        Players.Clear();
        Players.AddRange(list);
    }

    static void BroadcastLobby() { ToClients("lobby", WriteLobby, false); }
    static void OnLobby(ulong from, FastBufferReader r) { if (IsClientOnly) ReadLobby(r); }

    // Host presses start: everyone loads the map, then the host waits for all "ready" before simulating.
    public static void StartMatch()
    {
        if (!IsHost || MatchStarted) return;
        MatchStarted = true;
        foreach (var p in Players) p.Ready = p.Id == MyId;
        ToClients("start", WriteLobby, false);
        SceneManager.LoadScene("Map");
    }

    static void OnStart(ulong from, FastBufferReader r)
    {
        if (!IsClientOnly) return;
        ReadLobby(r);
        MatchStarted = true;
        SceneManager.LoadScene("Map");
    }

    public static void SendReady() { ToHost("ready", w => w.WriteValueSafe((byte)1)); }

    static void OnReady(ulong from, FastBufferReader r)
    {
        r.ReadValueSafe(out byte _);
        var p = Players.Find(x => x.Id == from);
        if (IsHost && p != null) p.Ready = true;
    }

    // ---------------- unit registry ----------------
    public static int NewId() { return ++nextId; }
    public static void Track(Unit u) { byId[u.NetId] = u; }
    public static void Untrack(Unit u) { if (u.NetId != 0 && byId.TryGetValue(u.NetId, out var cur) && cur == u) byId.Remove(u.NetId); }
    static Unit Find(int id) { return byId.TryGetValue(id, out var u) && u != null ? u : null; }
    public static void RegisterChampion(ulong owner, Champion c) { champOf[owner] = c; }

    // ---------------- commands: client -> host ----------------
    public static void SendMove(Vector3 p) { ToHost("cmd", w => { w.WriteValueSafe((byte)Cmd.Move); w.WriteValueSafe(p.x); w.WriteValueSafe(p.z); }); }
    public static void SendAttack(Unit t) { ToHost("cmd", w => { w.WriteValueSafe((byte)Cmd.Attack); w.WriteValueSafe(t.NetId); }); }
    public static void SendCast(int slot, Vector3 aim) { ToHost("cmd", w => { w.WriteValueSafe((byte)Cmd.Cast); w.WriteValueSafe((byte)slot); w.WriteValueSafe(aim.x); w.WriteValueSafe(aim.z); }); }
    public static void SendRecall() { ToHost("cmd", w => w.WriteValueSafe((byte)Cmd.Recall)); }
    public static void SendRank(int slot) { ToHost("cmd", w => { w.WriteValueSafe((byte)Cmd.Rank); w.WriteValueSafe((byte)slot); }); }
    public static void SendBuy(ItemDef it) { ToHost("cmd", w => { w.WriteValueSafe((byte)Cmd.Buy); w.WriteValueSafe(Content.Items.IndexOf(it)); }); }
    public static void SendSell(int index) { ToHost("cmd", w => { w.WriteValueSafe((byte)Cmd.Sell); w.WriteValueSafe((byte)index); }); }

    static bool Sane(float v) { return !float.IsNaN(v) && !float.IsInfinity(v) && Mathf.Abs(v) < 1000f; }

    // Clients are untrusted: every value is range-checked and the champion itself re-validates the action.
    static void OnCmd(ulong from, FastBufferReader r)
    {
        if (!IsHost || !champOf.TryGetValue(from, out var ch) || ch == null) return;
        r.ReadValueSafe(out byte type);
        switch ((Cmd)type)
        {
            case Cmd.Move:
            {
                r.ReadValueSafe(out float x); r.ReadValueSafe(out float z);
                if (Sane(x) && Sane(z)) ch.CmdMove(new Vector3(x, 0f, z));
                break;
            }
            case Cmd.Attack:
            {
                r.ReadValueSafe(out int id);
                var t = Find(id);
                if (t != null) ch.CmdAttack(t);
                break;
            }
            case Cmd.Cast:
            {
                r.ReadValueSafe(out byte slot); r.ReadValueSafe(out float x); r.ReadValueSafe(out float z);
                if (slot < 4 && Sane(x) && Sane(z)) ch.CmdCast(slot, new Vector3(x, 0f, z));
                break;
            }
            case Cmd.Recall: ch.CmdRecall(); break;
            case Cmd.Rank:
            {
                r.ReadValueSafe(out byte slot);
                if (slot < 4) ch.Rank(slot);
                break;
            }
            case Cmd.Buy:
            {
                r.ReadValueSafe(out int index);
                if (index >= 0 && index < Content.Items.Count) ch.Buy(Content.Items[index]);
                break;
            }
            case Cmd.Sell:
            {
                r.ReadValueSafe(out byte index);
                ch.Sell(index);
                break;
            }
        }
    }

    // ---------------- host tick: spawns, snapshot, private state ----------------
    public static void HostTick(float dt)
    {
        var gm = GameManager.I;
        if (!IsHost || nm == null || gm == null || gm.State == MatchState.Waiting) return;
        pruneTimer += dt;
        if (pruneTimer > 2f)
        {
            pruneTimer = 0f;
            var dead = new List<int>();
            foreach (var kv in byId) if (kv.Value == null) dead.Add(kv.Key);
            foreach (var id in dead) byId.Remove(id);
        }
        sendTimer += dt;
        if (sendTimer < SendInterval) return;
        sendTimer = 0f;

        foreach (var p in Players)
        {
            if (p.Id == MyId || !p.Ready) continue;
            if (!seen.TryGetValue(p.Id, out var set)) seen[p.Id] = set = new HashSet<int>();
            foreach (var u in Unit.All)
            {
                if (u.NetId == 0 || !(u.Alive || u.Kind == UnitKind.Champion)) continue;
                if (!Vision.Reports(p.Team, u)) { set.Remove(u.NetId); continue; } // in the fog: the client drops it, and it is re-sent when seen again
                if (set.Add(u.NetId)) SendSpawn(p.Id, u);
            }
        }
        foreach (Team team in new[] { Team.Blue, Team.Red }) // each team gets a snapshot of only what it can see
            ToClients("snap", w => WriteSnapshot(w, gm, team), true, t => t == team);
        foreach (var p in Players)
            if (p.Id != MyId && p.Ready && champOf.TryGetValue(p.Id, out var ch) && ch != null) SendOwner(p.Id, ch);
    }

    static void SendSpawn(ulong to, Unit u)
    {
        var go = u.gameObject;
        var mf = go.GetComponent<MeshFilter>();
        var rend = go.GetComponent<Renderer>();
        var ch = go.GetComponent<Champion>();
        Color c = rend != null ? rend.material.color : Color.white;
        SendTo(to, "spawn", w =>
        {
            w.WriteValueSafe(u.NetId);
            w.WriteValueSafe((byte)u.Kind);
            w.WriteValueSafe((byte)u.Team);
            w.WriteValueSafe(u.Radius);
            w.WriteValueSafe(go.name);
            w.WriteValueSafe(mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "Capsule");
            w.WriteValueSafe(go.transform.localScale.x);
            w.WriteValueSafe(c.r); w.WriteValueSafe(c.g); w.WriteValueSafe(c.b);
            w.WriteValueSafe(ch != null ? Content.Champions.IndexOf(ch.Def) : -1);
            w.WriteValueSafe(ch != null ? ch.OwnerId : 0UL);
        });
    }

    static void WriteSnapshot(FastBufferWriter w, GameManager gm, Team viewer)
    {
        w.WriteValueSafe(gm.Clock);
        w.WriteValueSafe((byte)gm.State);
        w.WriteValueSafe((byte)gm.Winner);
        var list = new List<Unit>();
        foreach (var u in Unit.All)
            if (u.NetId != 0 && (u.Alive || u.Kind == UnitKind.Champion) && Vision.Reports(viewer, u)) list.Add(u);
        w.WriteValueSafe(list.Count);
        foreach (var u in list)
        {
            Vector3 p = u.transform.position;
            w.WriteValueSafe(u.NetId);
            w.WriteValueSafe(p.x); w.WriteValueSafe(p.y); w.WriteValueSafe(p.z);
            w.WriteValueSafe((byte)(Mathf.Repeat(u.transform.eulerAngles.y, 360f) / 360f * 255f));
            w.WriteValueSafe(u.Health); w.WriteValueSafe(u.Total.Health); w.WriteValueSafe(u.Shield);
            bool champ = u.Kind == UnitKind.Champion;
            w.WriteValueSafe((byte)((u.Alive ? 1 : 0) | (u.Invulnerable ? 2 : 0) | (champ ? 4 : 0) | (u.Stealthed ? 8 : 0)));
            if (champ) { w.WriteValueSafe(u.Mana); w.WriteValueSafe(u.Total.Mana); } // champions only: their mana bar
        }
        var j = gm.Jungle;
        w.WriteValueSafe(j != null ? j.Objectives.Count : 0);
        if (j != null)
            foreach (var o in j.Objectives) { w.WriteValueSafe((byte)(o.IsAlive ? 1 : 0)); w.WriteValueSafe(o.NextSpawn); }
        var buffs = new List<ActiveBuff>();
        if (j != null) foreach (var b in j.ActiveBuffs) if (b.Until > Time.time) buffs.Add(b);
        w.WriteValueSafe(buffs.Count);
        foreach (var b in buffs) { w.WriteValueSafe(b.Name); w.WriteValueSafe((byte)b.Team); w.WriteValueSafe(b.Until - Time.time); }
    }

    static void SendOwner(ulong to, Champion ch)
    {
        SendTo(to, "own", w =>
        {
            w.WriteValueSafe(ch.Level); w.WriteValueSafe(ch.Xp); w.WriteValueSafe(ch.SkillPoints); w.WriteValueSafe(ch.Gold);
            for (int i = 0; i < 4; i++) w.WriteValueSafe((byte)ch.Ranks[i]);
            for (int i = 0; i < 4; i++) w.WriteValueSafe(Mathf.Max(0f, ch.CdEnd[i] - Time.time));
            w.WriteValueSafe((byte)ch.Items.Count);
            foreach (var it in ch.Items) w.WriteValueSafe(it.Name);
            w.WriteValueSafe((byte)(ch.Dead ? 1 : 0));
            w.WriteValueSafe(Mathf.Max(0f, ch.RespawnAt - Time.time));
            w.WriteValueSafe(ch.Recalling ? ch.RecallProgress : -1f);
            w.WriteValueSafe((byte)ch.PassiveStacks);
            w.WriteValueSafe(ch.U.Mana);
            var s = ch.U.Total;
            w.WriteValueSafe(s.Health); w.WriteValueSafe(s.Mana); w.WriteValueSafe(s.AD); w.WriteValueSafe(s.AP); w.WriteValueSafe(s.Armor);
            w.WriteValueSafe(s.MR); w.WriteValueSafe(s.AttackSpeed); w.WriteValueSafe(s.MoveSpeed); w.WriteValueSafe(s.HealthRegen);
            w.WriteValueSafe(s.ManaRegen); w.WriteValueSafe(s.CDR);
        });
    }

    // ---------------- client: apply what the host sent ----------------
    static void OnSpawn(ulong from, FastBufferReader r)
    {
        r.ReadValueSafe(out int id); r.ReadValueSafe(out byte kind); r.ReadValueSafe(out byte team); r.ReadValueSafe(out float radius);
        r.ReadValueSafe(out string name); r.ReadValueSafe(out string mesh); r.ReadValueSafe(out float scale);
        r.ReadValueSafe(out float cr); r.ReadValueSafe(out float cg); r.ReadValueSafe(out float cb);
        r.ReadValueSafe(out int champ); r.ReadValueSafe(out ulong owner);
        if (!IsClientOnly || GameManager.I == null || byId.ContainsKey(id)) return;

        var k = (UnitKind)kind;
        GameObject go = k == UnitKind.Tower || k == UnitKind.Nexus ? GameObject.Find(name) : null; // structures already exist in the scene
        if (go == null)
        {
            go = GameObject.CreatePrimitive(mesh == "Cube" ? PrimitiveType.Cube : mesh == "Cylinder" ? PrimitiveType.Cylinder : PrimitiveType.Capsule);
            go.name = name;
            go.transform.localScale = Vector3.one * scale;
            Object.Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().material.color = new Color(cr, cg, cb);
            go.AddComponent<NetGhost>();
        }
        var u = go.AddComponent<Unit>();
        u.Mirror = true;
        u.NetId = id;
        u.Init((Team)team, k, new Stats { Health = 1f }, radius); // real numbers arrive with the next snapshot
        byId[id] = u;
        if (champ >= 0 && champ < Content.Champions.Count)
            go.AddComponent<Champion>().InitMirror(Content.Champions[champ], u, owner, owner == MyId);
    }

    static void OnSnap(ulong from, FastBufferReader r)
    {
        if (!IsClientOnly) return; // only the host sends snapshots; never let a client overwrite host state
        var gm = GameManager.I;
        r.ReadValueSafe(out float clock); r.ReadValueSafe(out byte state); r.ReadValueSafe(out byte winner);
        r.ReadValueSafe(out int n);
        var present = new HashSet<int>();
        for (int i = 0; i < n; i++)
        {
            r.ReadValueSafe(out int id);
            r.ReadValueSafe(out float x); r.ReadValueSafe(out float y); r.ReadValueSafe(out float z);
            r.ReadValueSafe(out byte yaw);
            r.ReadValueSafe(out float hp); r.ReadValueSafe(out float maxHp); r.ReadValueSafe(out float shield);
            r.ReadValueSafe(out byte flags);
            float mana = 0f, maxMana = 0f;
            if ((flags & 4) != 0) { r.ReadValueSafe(out mana); r.ReadValueSafe(out maxMana); }
            present.Add(id);
            var u = Find(id);
            if (u == null) continue;
            u.Health = hp;
            u.Total.Health = maxHp;
            u.Shield = shield;
            if ((flags & 4) != 0) { u.Mana = mana; u.Total.Mana = maxMana; }
            u.MirrorInvulnerable = (flags & 2) != 0;
            u.Stealthed = (flags & 8) != 0;
            var g = u.GetComponent<NetGhost>();
            if (g != null) g.SetTarget(new Vector3(x, y, z), yaw / 255f * 360f);
            var ch = u.GetComponent<Champion>();
            if (ch != null) ch.SetMirrorVisible((flags & 1) != 0);
        }
        r.ReadValueSafe(out int objectives);
        var j = gm != null ? gm.Jungle : null;
        for (int i = 0; i < objectives; i++)
        {
            r.ReadValueSafe(out byte alive); r.ReadValueSafe(out float next);
            if (j != null && i < j.Objectives.Count) { j.Objectives[i].RemoteAlive = alive != 0; j.Objectives[i].NextSpawn = next; }
        }
        r.ReadValueSafe(out int nb);
        if (j != null) j.ActiveBuffs.Clear();
        for (int i = 0; i < nb; i++)
        {
            r.ReadValueSafe(out string bn); r.ReadValueSafe(out byte bt); r.ReadValueSafe(out float left);
            if (j != null) j.ActiveBuffs.Add(new ActiveBuff { Name = bn, Team = (Team)bt, Until = Time.time + left });
        }
        if (gm == null || !IsClientOnly) return;
        gm.Clock = clock;
        gm.State = (MatchState)state;
        gm.Winner = (Team)winner;

        // anything the host no longer lists is gone (died, destroyed)
        var gone = new List<int>();
        foreach (var kv in byId) if (!present.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (var id in gone)
        {
            if (byId[id] != null) Object.Destroy(byId[id].gameObject);
            byId.Remove(id);
        }
    }

    static void OnOwn(ulong from, FastBufferReader r)
    {
        r.ReadValueSafe(out int level); r.ReadValueSafe(out int xp); r.ReadValueSafe(out int sp); r.ReadValueSafe(out float gold);
        var ranks = new int[4]; var cd = new float[4];
        for (int i = 0; i < 4; i++) { r.ReadValueSafe(out byte rk); ranks[i] = rk; }
        for (int i = 0; i < 4; i++) r.ReadValueSafe(out cd[i]);
        r.ReadValueSafe(out byte nItems);
        var items = new List<string>();
        for (int i = 0; i < nItems; i++) { r.ReadValueSafe(out string nm2); items.Add(nm2); }
        r.ReadValueSafe(out byte dead); r.ReadValueSafe(out float respawnLeft); r.ReadValueSafe(out float recall); r.ReadValueSafe(out byte passive); r.ReadValueSafe(out float mana);
        var s = new Stats();
        r.ReadValueSafe(out s.Health); r.ReadValueSafe(out s.Mana); r.ReadValueSafe(out s.AD); r.ReadValueSafe(out s.AP); r.ReadValueSafe(out s.Armor);
        r.ReadValueSafe(out s.MR); r.ReadValueSafe(out s.AttackSpeed); r.ReadValueSafe(out s.MoveSpeed); r.ReadValueSafe(out s.HealthRegen);
        r.ReadValueSafe(out s.ManaRegen); r.ReadValueSafe(out s.CDR);
        var ch = Champion.Local;
        if (!IsClientOnly || ch == null) return;
        ch.U.Total = s;
        ch.U.Mana = mana;
        ch.ApplyMirror(level, xp, sp, gold, ranks, cd, items, dead != 0, respawnLeft, recall, passive);
    }

    // ---------------- effects: host -> clients ----------------
    static void WriteV3(FastBufferWriter w, Vector3 v) { w.WriteValueSafe(v.x); w.WriteValueSafe(v.y); w.WriteValueSafe(v.z); }
    static Vector3 ReadV3(FastBufferReader r) { r.ReadValueSafe(out float x); r.ReadValueSafe(out float y); r.ReadValueSafe(out float z); return new Vector3(x, y, z); }
    static void WriteColor(FastBufferWriter w, Color c) { w.WriteValueSafe(c.r); w.WriteValueSafe(c.g); w.WriteValueSafe(c.b); }
    static Color ReadColor(FastBufferReader r) { r.ReadValueSafe(out float cr); r.ReadValueSafe(out float cg); r.ReadValueSafe(out float cb); return new Color(cr, cg, cb); }

    public static void FxAuto(Unit caster, Color accent, bool melee, Unit target)
    {
        if (!IsHost || caster.NetId == 0 || target.NetId == 0) return;
        ToClients("fx", w =>
        {
            w.WriteValueSafe((byte)FxType.Auto); w.WriteValueSafe(caster.NetId); w.WriteValueSafe(target.NetId);
            w.WriteValueSafe((byte)(melee ? 1 : 0)); WriteColor(w, accent);
        }, true, t => Vision.Sees(t, caster) || Vision.Sees(t, target));
    }

    public static void FxCast(AbilityDef a, Unit caster, Vector3 pos, Vector3 dir, Vector3 point, float dist, List<Unit> hit)
    {
        var ch = caster.GetComponent<Champion>();
        if (!IsHost || ch == null || caster.NetId == 0) return;
        int slot = System.Array.IndexOf(ch.Def.Abilities, a);
        if (slot < 0) return;
        ToClients("fx", w =>
        {
            w.WriteValueSafe((byte)FxType.Cast); w.WriteValueSafe(caster.NetId); w.WriteValueSafe((byte)slot);
            WriteV3(w, pos); WriteV3(w, dir); WriteV3(w, point); w.WriteValueSafe(dist);
            w.WriteValueSafe(hit.Count);
            foreach (var h in hit) w.WriteValueSafe(h.NetId);
        }, true, t => Vision.Sees(t, caster) || Vision.SeesPoint(t, point));
    }

    public static void FxTower(Unit tower, Unit target)
    {
        if (!IsHost || tower.NetId == 0 || target.NetId == 0) return;
        ToClients("fx", w => { w.WriteValueSafe((byte)FxType.Tower); w.WriteValueSafe(tower.NetId); w.WriteValueSafe(target.NetId); },
            true, t => Vision.SeesPoint(t, tower.transform.position) || Vision.SeesPoint(t, target.transform.position));
    }

    public static void FxRing(Vector3 center, float radius, Color c)
    {
        if (!IsHost) return;
        ToClients("fx", w => { w.WriteValueSafe((byte)FxType.Ring); WriteV3(w, center); w.WriteValueSafe(radius); WriteColor(w, c); }, true, t => Vision.SeesPoint(t, center));
    }

    public static void FxSlam(Vector3 center, float radius, Color c, float power)
    {
        if (!IsHost) return;
        ToClients("fx", w => { w.WriteValueSafe((byte)FxType.Slam); WriteV3(w, center); w.WriteValueSafe(radius); WriteColor(w, c); w.WriteValueSafe(power); }, true, t => Vision.SeesPoint(t, center));
    }

    public static void FxAnnounce(string text)
    {
        if (!IsHost) return;
        ToClients("fx", w => { w.WriteValueSafe((byte)FxType.Announce); w.WriteValueSafe(text); }, true);
    }

    static void OnFx(ulong from, FastBufferReader r)
    {
        r.ReadValueSafe(out byte type);
        switch ((FxType)type)
        {
            case FxType.Auto:
            {
                r.ReadValueSafe(out int cid); r.ReadValueSafe(out int tid); r.ReadValueSafe(out byte melee);
                var accent = ReadColor(r);
                var c = Find(cid); var t = Find(tid);
                if (IsClientOnly && c != null && t != null) Vfx.Auto(c, accent, melee != 0, t);
                break;
            }
            case FxType.Cast:
            {
                r.ReadValueSafe(out int cid); r.ReadValueSafe(out byte slot);
                var pos = ReadV3(r); var dir = ReadV3(r); var point = ReadV3(r);
                r.ReadValueSafe(out float dist); r.ReadValueSafe(out int count);
                var hit = new List<Unit>();
                for (int i = 0; i < count; i++) { r.ReadValueSafe(out int hid); var h = Find(hid); if (h != null) hit.Add(h); }
                var cu = Find(cid);
                var ch = cu != null ? cu.GetComponent<Champion>() : null;
                if (IsClientOnly && ch != null && slot < 4)
                    Vfx.Cast(ch.Def.Abilities[slot], cu, ch.Def.Color, pos, dir, point, dist, hit, ch == Champion.Local);
                break;
            }
            case FxType.Tower:
            {
                r.ReadValueSafe(out int tw); r.ReadValueSafe(out int tg);
                var tower = Find(tw); var target = Find(tg);
                if (IsClientOnly && tower != null && target != null) Vfx.TowerShot(tower, target, 0f); // visual only: the host applies the damage
                break;
            }
            case FxType.Ring:
            {
                var center = ReadV3(r); r.ReadValueSafe(out float radius); var c = ReadColor(r);
                if (IsClientOnly) Vfx.Ring(center, radius, c);
                break;
            }
            case FxType.Slam:
            {
                var center = ReadV3(r); r.ReadValueSafe(out float radius); var c = ReadColor(r); r.ReadValueSafe(out float power);
                if (IsClientOnly) Vfx.MonsterSlam(center, radius, c, power);
                break;
            }
            case FxType.Announce:
            {
                r.ReadValueSafe(out string text);
                if (IsClientOnly && GameManager.I != null) GameManager.I.Announce(text);
                break;
            }
        }
    }
}

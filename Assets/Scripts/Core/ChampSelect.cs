using UnityEngine;
using UnityEngine.SceneManagement;

// Champion-select scene: roster with icons on the left, a spinning 3D preview in the middle,
// stats and ability descriptions on the right, and a lobby panel to host or join a game with friends.
// Solo play: pick and press Start. Online: the host presses Start once everyone has picked.
public class ChampSelect : MonoBehaviour
{
    ChampionDef sel;
    GameObject preview;
    Vector2 listScroll;
    string ip = "127.0.0.1", port = Net.DefaultPort.ToString(), code = "", notice = "";
    // Button presses are queued and run in Update, because changing the UI mid-OnGUI breaks IMGUI layout.
    bool pendingHost, pendingJoin, pendingHostRelay, pendingJoinRelay, pendingLeave, pendingStart, pendingSolo;

    void Start()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.transform.position = new Vector3(0f, -0.05f, 0f);
        floor.transform.localScale = new Vector3(3f, 0.05f, 3f);
        floor.GetComponent<Renderer>().material.color = new Color(0.2f, 0.22f, 0.28f);

        preview = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        preview.transform.position = new Vector3(0f, 1f, 0f);
        var visor = GameObject.CreatePrimitive(PrimitiveType.Cube); // makes the spin visible
        visor.transform.SetParent(preview.transform, false);
        visor.transform.localPosition = new Vector3(0f, 0.45f, 0.4f);
        visor.transform.localScale = new Vector3(0.6f, 0.2f, 0.2f);
        visor.GetComponent<Renderer>().material.color = Color.black;

        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 1.8f, -5.5f);
            cam.transform.LookAt(new Vector3(0f, 1f, 0f));
        }
        Select(Session.Selected ?? Content.Champions[0]);
    }

    void Select(ChampionDef d)
    {
        sel = d;
        preview.GetComponent<Renderer>().material.color = d.Color;
        if (Net.Online) Net.Pick(Content.Champions.IndexOf(d)); // the lobby shows everyone's pick
    }

    ushort Port() { return ushort.TryParse(port, out var p) ? p : Net.DefaultPort; }

    void Update()
    {
        preview.transform.Rotate(0f, 40f * Time.deltaTime, 0f);
        if (Net.Lost) { notice = Net.Status; Net.Shutdown(); }
        if (pendingHost) { pendingHost = false; notice = ""; Net.StartHost(Port()); Net.Pick(Content.Champions.IndexOf(sel)); }
        if (pendingJoin) { pendingJoin = false; notice = ""; Net.Join(ip, Port()); }
        if (pendingHostRelay) { pendingHostRelay = false; notice = ""; Net.HostRelay(); }
        if (pendingJoinRelay) { pendingJoinRelay = false; notice = ""; Net.JoinRelay(code); }
        if (pendingLeave) { pendingLeave = false; Net.Shutdown(); }
        if (pendingStart) { pendingStart = false; Net.StartMatch(); }
        if (pendingSolo) { pendingSolo = false; Session.Selected = sel; SceneManager.LoadScene("Map"); }
        // keep the lobby's record of my pick equal to the one on screen (covers a freshly connected client and a Relay host that finished setting up later)
        if (Net.Online)
        {
            var me = Net.Me;
            int want = Content.Champions.IndexOf(sel);
            if (me == null ? Net.IsClientOnly : me.ChampIndex != want) Net.Pick(want);
        }
    }

    void OnGUI()
    {
        float h = Screen.height - 40f;

        GUILayout.BeginArea(new Rect(20f, 20f, 320f, h), GUI.skin.box);
        GUILayout.Label("Choose your champion");
        listScroll = GUILayout.BeginScrollView(listScroll);
        foreach (var d in Content.Champions)
        {
            Rect row = GUILayoutUtility.GetRect(280f, 58f);
            var old = GUI.color;
            if (d == sel) GUI.color = new Color(1f, 1f, 0.6f);
            if (GUI.Button(row, GUIContent.none)) Select(d);
            GUI.color = old;
            Icons.Draw(new Rect(row.x + 4f, row.y + 4f, 50f, 50f), d);
            GUI.Label(new Rect(row.x + 62f, row.y + 8f, 210f, 22f), d.Name);
            GUI.Label(new Rect(row.x + 62f, row.y + 30f, 210f, 22f), d.Role.ToString());
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        GUILayout.BeginArea(new Rect(Screen.width - 460f, 20f, 440f, h), GUI.skin.box);
        GUILayout.Label($"{sel.Name} - {sel.Role}");
        var b = sel.Base;
        GUILayout.Label($"HP {b.Health:0}   Mana {b.Mana:0}   AD {b.AD:0}   Armor {b.Armor:0}   MR {b.MR:0}\n" +
                        $"Attack speed {b.AttackSpeed:0.00}   Move speed {b.MoveSpeed:0.0}   Attack range {sel.AttackRange:0.#}");
        for (int i = 0; i < 4; i++)
        {
            var a = sel.Abilities[i];
            GUILayout.Space(6f);
            GUILayout.Label($"{"QWER"[i]} - {a.Name}\n{a.Describe()}");
        }
        GUILayout.EndArea();

        DrawLobby();
    }

    void DrawLobby()
    {
        GUILayout.BeginArea(new Rect(Screen.width / 2f - 260f, Screen.height - 330f, 520f, 310f), GUI.skin.box);
        if (!Net.Online)
        {
            GUILayout.Label("Play with friends (Unity Relay: you share a code, never your IP)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Host a game", GUILayout.Height(30f))) pendingHostRelay = true;
            GUILayout.Label("Code", GUILayout.Width(36f));
            code = GUILayout.TextField(code, 8, GUILayout.Width(100f));
            if (GUILayout.Button("Join", GUILayout.Height(30f), GUILayout.Width(80f))) pendingJoinRelay = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            GUILayout.Label("Direct connection (LAN only, this shares the host's IP):");
            GUILayout.BeginHorizontal();
            GUILayout.Label("IP", GUILayout.Width(24f));
            ip = GUILayout.TextField(ip, GUILayout.Width(150f));
            GUILayout.Label("Port", GUILayout.Width(34f));
            port = GUILayout.TextField(port, GUILayout.Width(60f));
            if (GUILayout.Button("Host")) pendingHost = true;
            if (GUILayout.Button("Join")) pendingJoin = true;
            GUILayout.EndHorizontal();
            if (notice.Length > 0) GUILayout.Label(notice);
            else if (Net.Status.Length > 0) GUILayout.Label(Net.Status + " (see the Console for details)"); // e.g. hosting failed
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Start solo as " + sel.Name, GUILayout.Height(40f))) pendingSolo = true;
        }
        else
        {
            GUILayout.Label(Net.Status);
            if (Net.JoinCode.Length > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Join code:  " + Net.JoinCode);
                if (GUILayout.Button("Copy", GUILayout.Width(70f))) GUIUtility.systemCopyBuffer = Net.JoinCode;
                GUILayout.EndHorizontal();
            }
            foreach (var p in Net.Players)
                GUILayout.Label($"{(p.Id == Net.MyId ? "You" : "Player " + p.Id)}  -  {p.Team}  -  {Content.Champions[p.ChampIndex].Name}");
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Leave", GUILayout.Height(40f), GUILayout.Width(100f))) pendingLeave = true;
            if (Net.IsHost && Net.Me != null)
            {
                if (GUILayout.Button($"Start match ({Net.Players.Count} players)", GUILayout.Height(40f))) pendingStart = true;
            }
            else GUILayout.Label(Net.IsHost ? "Setting up..." : "Waiting for the host to start...");
            GUILayout.EndHorizontal();
        }
        GUILayout.EndArea();
    }
}

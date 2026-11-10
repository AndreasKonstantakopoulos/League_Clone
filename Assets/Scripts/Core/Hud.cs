using UnityEngine;

// Placeholder IMGUI interface: champion select, health bars, ability bar, shop, end screen.
// Button clicks are queued and applied in Update, because changing controls mid-OnGUI breaks IMGUI layout.
public class Hud : MonoBehaviour
{
    static readonly Color BlueCol = new Color(0.3f, 0.55f, 1f), RedCol = new Color(1f, 0.35f, 0.35f);

    bool shopOpen;
    Vector2 scroll;
    ItemDef pendingBuy;
    int pendingSell = -1, pendingRank = -1;

    static bool AtBase(Champion c) =>
        !c.Dead && c.U.DistTo(GameManager.I.SpawnOf(c.U.Team)) < 10f;

    void Update()
    {
        var c = Champion.Local;
        if (c == null) return;
        if (GameInput.KeyDown('B')) shopOpen = !shopOpen;
        if (shopOpen && !AtBase(c)) shopOpen = false;

        if (pendingBuy != null) { c.Buy(pendingBuy); pendingBuy = null; }
        if (pendingSell >= 0) { c.Sell(pendingSell); pendingSell = -1; }
        if (pendingRank >= 0) { c.Rank(pendingRank); pendingRank = -1; }
    }

    void OnGUI()
    {
        var gm = GameManager.I;
        if (gm == null) return;
        if (gm.State == MatchState.Select) { DrawSelect(gm); return; }
        DrawWorldBars();
        DrawBottom(gm);
        if (shopOpen && Champion.Local != null) DrawShop();
        GUI.Label(new Rect(10, 10, 600, 24), "Right-click: move / attack    Q W E R: abilities    B: shop (at your base)");
        if (gm.State == MatchState.Over) DrawEnd(gm);
    }

    void DrawSelect(GameManager gm)
    {
        GUILayout.BeginArea(new Rect(Screen.width / 2 - 320, 30, 640, Screen.height - 60), GUI.skin.box);
        GUILayout.Label("Choose your champion");
        scroll = GUILayout.BeginScrollView(scroll);
        foreach (var d in Content.Champions)
        {
            var a = d.Abilities;
            string label = $"{d.Name}  [{d.Role}]\nQ {a[0].Name} | W {a[1].Name} | E {a[2].Name} | R {a[3].Name}";
            if (GUILayout.Button(label, GUILayout.Height(54))) gm.StartMatch(d);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void DrawWorldBars()
    {
        var cam = Camera.main;
        if (cam == null) return;
        foreach (var u in Unit.All)
        {
            if (!u.Alive) continue;
            var p = cam.WorldToScreenPoint(u.transform.position + Vector3.up * (u.IsStructure ? 3.5f : 1.8f));
            if (p.z <= 0f) continue;
            float w = u.IsStructure ? 80f : u.Kind == UnitKind.Champion ? 60f : 36f;
            var r = new Rect(p.x - w / 2f, Screen.height - p.y, w, 5f);
            Color col = u.Invulnerable ? Color.gray : u.Team == Team.Blue ? BlueCol : RedCol; // grey = protected
            Bar(r, u.Health / u.Total.Health, col);
            if (u.Shield > 0f) Bar(new Rect(r.x, r.y - 3f, w, 3f), u.Shield / u.Total.Health, Color.white);
        }
    }

    static void Bar(Rect r, float frac, Color c)
    {
        var old = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = c;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    void DrawBottom(GameManager gm)
    {
        var c = Champion.Local;
        if (c == null) return;
        const float w = 640f, h = 170f;
        GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, Screen.height - h - 8f, w, h), GUI.skin.box);
        string clock = $"{(int)(gm.Clock / 60f)}:{(int)(gm.Clock % 60f):00}";
        GUILayout.Label($"{c.Def.Name}  Lv {c.Level}   HP {c.U.Health:0}/{c.U.Total.Health:0}   MP {c.U.Mana:0}/{c.U.Total.Mana:0}   Gold {c.Gold:0}   XP {c.Xp}/{c.XpToNext}   {clock}");
        if (c.Dead) GUILayout.Label($"Respawning in {Mathf.Max(0f, c.RespawnAt - Time.time):0}s");
        GUILayout.BeginHorizontal();
        for (int i = 0; i < 4; i++)
        {
            float cd = Mathf.Max(0f, c.CdEnd[i] - Time.time);
            string status = c.Ranks[i] == 0 ? "not learned" : cd > 0f ? $"{cd:0.0}s" : "ready";
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(150));
            GUILayout.Label($"{"QWER"[i]}: {c.Def.Abilities[i].Name}\nrank {c.Ranks[i]}  {status}");
            if (GUILayout.Button("+ rank") && c.CanRank(i)) pendingRank = i; // always drawn, so layout stays stable
            GUILayout.EndVertical();
        }
        GUILayout.EndHorizontal();
        var names = c.Items.ConvertAll(x => x.Name);
        GUILayout.Label("Items: " + (names.Count == 0 ? "none" : string.Join(", ", names)) + (c.SkillPoints > 0 ? $"    Skill points: {c.SkillPoints}" : ""));
        GUILayout.EndArea();
    }

    void DrawShop()
    {
        var c = Champion.Local;
        GUILayout.BeginArea(new Rect(20, 40, 400, Screen.height - 240), GUI.skin.box);
        GUILayout.Label($"Shop   Gold {c.Gold:0}   (B to close)");
        scroll = GUILayout.BeginScrollView(scroll);
        foreach (var it in Content.Items)
        {
            string recipe = it.Components.Length > 0 ? "  [" + string.Join(" + ", it.Components) + "]" : "";
            if (GUILayout.Button($"{it.Name} - {c.PriceOf(it)}g   {it.Bonus}{recipe}")) pendingBuy = it;
        }
        GUILayout.EndScrollView();
        GUILayout.Label("Click an owned item to sell it (70%):");
        for (int i = 0; i < Champion.MaxItems; i++)
        {
            string label = i < c.Items.Count ? c.Items[i].Name : "(empty)";
            if (GUILayout.Button(label) && i < c.Items.Count) pendingSell = i;
        }
        GUILayout.EndArea();
    }

    void DrawEnd(GameManager gm)
    {
        GUILayout.BeginArea(new Rect(Screen.width / 2 - 130, Screen.height / 2 - 70, 260, 140), GUI.skin.box);
        GUILayout.Label(gm.Winner == Team.Blue ? "VICTORY" : "DEFEAT");
        GUILayout.Label($"Match time {(int)(gm.Clock / 60f)}:{(int)(gm.Clock % 60f):00}");
        if (GUILayout.Button("Play again", GUILayout.Height(40))) gm.Restart();
        GUILayout.EndArea();
    }
}

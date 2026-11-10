using System.Text;
using UnityEngine;

// Placeholder IMGUI interface: champion select, health bars, ability bar, shop, end screen.
// Button clicks are queued and applied in Update, because changing controls mid-OnGUI breaks IMGUI layout.
public class Hud : MonoBehaviour
{
    static readonly Color BlueCol = new Color(0.3f, 0.55f, 1f), RedCol = new Color(1f, 0.35f, 0.35f);

    GUIStyle tipStyle;
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
        DrawWorldBars();
        DrawBottom(gm);
        if (shopOpen && Champion.Local != null) DrawShop();
        GUI.Label(new Rect(10, 10, 600, 24), "Right-click: move / attack    Q W E R: abilities    B: shop (at your base)");
        if (gm.State == MatchState.Over) DrawEnd(gm);
        DrawTooltip(); // last, so it sits on top
    }

    // What an item does to this champion's abilities, using the item's real stat bonuses.
    static string ItemTip(ItemDef it, Champion c)
    {
        var sb = new StringBuilder($"<b>{it.Name}</b>  {it.Cost}g\n{it.Bonus}\n");
        if (it.Components.Length > 0) sb.Append("Builds from: ").Append(string.Join(", ", it.Components)).Append('\n');
        bool scales = false;
        for (int i = 0; i < 4; i++)
        {
            var a = c.Def.Abilities[i];
            float ad = 0f, ap = 0f;
            foreach (var e in a.Effects) { ad += e.AD; ap += e.AP; }
            string part = "";
            if (it.Bonus.AD > 0f && ad > 0f) part += $" +{ad * it.Bonus.AD:0} from AD";
            if (it.Bonus.AP > 0f && ap > 0f) part += $" +{ap * it.Bonus.AP:0} from AP";
            if (part == "") continue;
            sb.Append($"{"QWER"[i]} {a.Name}:{part}\n");
            scales = true;
        }
        if (it.Bonus.CDR > 0f) sb.Append("Shortens all your ability cooldowns\n");
        else if ((it.Bonus.AD > 0f || it.Bonus.AP > 0f) && !scales) sb.Append("None of your abilities scale with this stat\n");
        return sb.ToString();
    }

    void DrawTooltip()
    {
        if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(GUI.tooltip)) return;
        if (tipStyle == null)
            tipStyle = new GUIStyle(GUI.skin.box) { wordWrap = true, richText = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(8, 8, 6, 6) };
        var content = new GUIContent(GUI.tooltip);
        const float w = 340f;
        float h = tipStyle.CalcHeight(content, w);
        var m = Event.current.mousePosition;
        var r = new Rect(Mathf.Clamp(m.x + 16f, 4f, Screen.width - w - 4f), Mathf.Clamp(m.y - h - 8f, 4f, Screen.height - h - 4f), w, h);
        GUI.Box(r, content, tipStyle);
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
        float x = (Screen.width - w) / 2f, y = Screen.height - h - 8f;
        Icons.Draw(new Rect(x - 64f, y + 4f, 58f, 58f), c.Def);
        GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.box);
        string clock = $"{(int)(gm.Clock / 60f)}:{(int)(gm.Clock % 60f):00}";
        GUILayout.Label($"{c.Def.Name}  Lv {c.Level}   HP {c.U.Health:0}/{c.U.Total.Health:0}   MP {c.U.Mana:0}/{c.U.Total.Mana:0}   Gold {c.Gold:0}   XP {c.Xp}/{c.XpToNext}   {clock}");
        if (c.Dead) GUILayout.Label($"Respawning in {Mathf.Max(0f, c.RespawnAt - Time.time):0}s");
        GUILayout.BeginHorizontal();
        for (int i = 0; i < 4; i++)
        {
            float cd = Mathf.Max(0f, c.CdEnd[i] - Time.time);
            string status = c.Ranks[i] == 0 ? "not learned" : cd > 0f ? $"{cd:0.0}s" : "ready";
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(150));
            GUILayout.Label(new GUIContent($"{"QWER"[i]}: {c.Def.Abilities[i].Name}\nrank {c.Ranks[i]}  {status}",
                c.Def.Abilities[i].Tooltip(c.U, c.Ranks[i])));
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
            if (GUILayout.Button(new GUIContent($"{it.Name} - {c.PriceOf(it)}g   {it.Bonus}{recipe}", ItemTip(it, c)))) pendingBuy = it;
        }
        GUILayout.EndScrollView();
        GUILayout.Label("Click an owned item to sell it (70%):");
        for (int i = 0; i < Champion.MaxItems; i++)
        {
            string label = i < c.Items.Count ? c.Items[i].Name : "(empty)";
            if (GUILayout.Button(new GUIContent(label, i < c.Items.Count ? ItemTip(c.Items[i], c) : "")) && i < c.Items.Count) pendingSell = i;
        }
        GUILayout.EndArea();
    }

    void DrawEnd(GameManager gm)
    {
        GUILayout.BeginArea(new Rect(Screen.width / 2 - 130, Screen.height / 2 - 90, 260, 180), GUI.skin.box);
        GUILayout.Label(gm.Winner == Team.Blue ? "VICTORY" : "DEFEAT");
        GUILayout.Label($"Match time {(int)(gm.Clock / 60f)}:{(int)(gm.Clock % 60f):00}");
        if (GUILayout.Button("Rematch (same champion)", GUILayout.Height(36))) gm.Rematch();
        if (GUILayout.Button("Champion select", GUILayout.Height(36))) gm.ToChampSelect();
        GUILayout.EndArea();
    }
}

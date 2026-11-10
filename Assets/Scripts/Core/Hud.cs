using System.Text;
using UnityEngine;

// Placeholder IMGUI interface: champion select, health bars, ability bar, shop, end screen.
// Button clicks are queued and applied in Update, because changing controls mid-OnGUI breaks IMGUI layout.
public class Hud : MonoBehaviour
{
    static readonly Color BlueCol = new Color(0.3f, 0.55f, 1f), RedCol = new Color(1f, 0.35f, 0.35f), NeutralCol = new Color(1f, 0.85f, 0.3f);

    GUIStyle tipStyle;
    bool shopOpen;
    Vector2 scroll;
    ItemDef pendingBuy;
    int pendingSell = -1, pendingRank = -1;

    void Update()
    {
        var c = Champion.Local;
        if (c == null) return;
        if (GameInput.KeyDown('P')) shopOpen = !shopOpen; // browsing works anywhere; buying needs the base (checked in Champion)
        if (GameInput.LeftClickDown && !shopOpen)
        {
            var npc = GameManager.I.ShopNpc(c.U.Team);
            if (npc != null && Unit.FlatDist(GameInput.MouseGround(), npc.position) < 1.5f) shopOpen = true; // click the shopkeeper
        }

        if (pendingBuy != null) { c.RequestBuy(pendingBuy); pendingBuy = null; }
        if (pendingSell >= 0) { c.RequestSell(pendingSell); pendingSell = -1; }
        if (pendingRank >= 0) { c.RequestRank(pendingRank); pendingRank = -1; }
    }

    void OnGUI()
    {
        var gm = GameManager.I;
        if (gm == null) return;
        DrawWorldBars();
        DrawShopLabel(gm);
        DrawJungleInfo(gm);
        DrawBottom(gm);
        if (shopOpen && Champion.Local != null) DrawShop();
        GUI.Label(new Rect(10, 10, 600, 24), "Right-click: move / attack    Q W E R: abilities    B: recall    P: shop (buy at base only)");
        if (gm.State == MatchState.Over) DrawEnd(gm);
        if (gm.State == MatchState.Waiting)
            GUI.Box(new Rect(Screen.width / 2f - 150f, Screen.height / 2f - 20f, 300f, 40f), "Waiting for players...");
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
            var p = cam.WorldToScreenPoint(u.transform.position + Vector3.up * (u.IsStructure ? 3.5f : 1.8f + (u.Kind == UnitKind.Monster ? u.Radius * 3f : 0f)));
            if (p.z <= 0f) continue;
            float w = u.IsStructure ? 80f : u.Kind == UnitKind.Champion ? 60f : u.Kind == UnitKind.Monster ? Mathf.Clamp(u.Radius * 70f, 36f, 100f) : 36f;
            var r = new Rect(p.x - w / 2f, Screen.height - p.y, w, 5f);
            Color col = u.Invulnerable ? Color.gray : u.Team == Team.Neutral ? NeutralCol : u.Team == Team.Blue ? BlueCol : RedCol; // grey = protected
            Bar(r, u.Health / u.Total.Health, col);
            if (u.Kind == UnitKind.Champion && u.Total.Mana > 0f) // mana bar under the health bar
                Bar(new Rect(r.x, r.y + 6f, w, 4f), u.Mana / u.Total.Mana, new Color(0.35f, 0.9f, 1f));
            if (u.Shield > 0f) Bar(new Rect(r.x, r.y - 3f, w, 3f), u.Shield / u.Total.Health, Color.white);
        }
    }

    // Objective timers, your team's active buffs, and the "slain" banner.
    void DrawJungleInfo(GameManager gm)
    {
        var c = Champion.Local;
        if (gm.Jungle == null) return;
        var sb = new StringBuilder();
        foreach (var o in gm.Jungle.Objectives)
            sb.AppendLine(o.IsAlive ? $"{o.Name}: alive" : $"{o.Name}: spawns in {Mathf.Max(0f, o.NextSpawn - gm.Clock):0}s");
        if (c != null)
            foreach (var b in gm.Jungle.ActiveBuffs)
                if (b.Team == c.U.Team && b.Until > Time.time) sb.AppendLine($"BUFF {b.Name}: {b.Until - Time.time:0}s left");
        GUI.Label(new Rect(10, 34, 460, 90), sb.ToString());
        if (Time.time < gm.BannerUntil) GUI.Box(new Rect(Screen.width / 2f - 280f, 50f, 560f, 34f), gm.Banner);
    }

    void DrawShopLabel(GameManager gm)
    {
        var cam = Camera.main;
        var c = Champion.Local;
        if (cam == null || c == null) return;
        var p = cam.WorldToScreenPoint(gm.ShopNpc(c.U.Team).position + Vector3.up * 1.6f);
        if (p.z > 0f) GUI.Label(new Rect(p.x - 60f, Screen.height - p.y, 120f, 22f), "SHOP (click / P)");
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

    // ---------- bottom panel: portrait, abilities, health / mana / xp bars, items, stats ----------
    GUIStyle keyStyle, nameStyle, cdStyle, numStyle, smallStyle;

    void EnsureStyles()
    {
        if (keyStyle != null) return;
        GUIStyle Make(int size, TextAnchor anchor, FontStyle font, bool wrap = false) => new GUIStyle(GUI.skin.label)
        {
            fontSize = size, alignment = anchor, fontStyle = font, wordWrap = wrap, richText = true, clipping = TextClipping.Clip, normal = { textColor = Color.white }
        };
        keyStyle = Make(28, TextAnchor.MiddleCenter, FontStyle.Bold);
        nameStyle = Make(9, TextAnchor.MiddleCenter, FontStyle.Normal, true);
        cdStyle = Make(22, TextAnchor.MiddleCenter, FontStyle.Bold);
        numStyle = Make(11, TextAnchor.MiddleCenter, FontStyle.Bold);
        smallStyle = Make(12, TextAnchor.UpperLeft, FontStyle.Normal);
    }

    static void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    static Rect Inset(Rect r, float d) { return new Rect(r.x + d, r.y + d, r.width - 2f * d, r.height - 2f * d); }

    void LabeledBar(Rect r, float frac, Color col, string text)
    {
        Fill(r, new Color(0f, 0f, 0f, 0.8f));
        Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), col);
        if (text != null) GUI.Label(r, text, numStyle);
    }

    static string Initials(string name)
    {
        string s = "";
        foreach (var word in name.Split(' ', '\''))
            if (word.Length > 0 && s.Length < 2) s += char.ToUpper(word[0]);
        return s;
    }

    void DrawBottom(GameManager gm)
    {
        var c = Champion.Local;
        if (c == null) return;
        EnsureStyles();
        const float w = 720f, h = 140f;
        float x = (Screen.width - w) / 2f, y = Screen.height - h - 8f;
        var u = c.U;

        if (c.Recalling)
        {
            GUI.Label(new Rect(x + 96f, y - 44f, 400f, 20f), "Recalling... (move, cast or take damage to cancel)");
            LabeledBar(new Rect(x + 96f, y - 22f, 288f, 8f), c.RecallProgress, new Color(0.6f, 0.85f, 1f), null);
        }

        Fill(new Rect(x, y, w, h), new Color(0.05f, 0.06f, 0.09f, 0.9f));

        // portrait with the level in a corner badge
        Icons.Draw(new Rect(x + 10f, y + 16f, 72f, 72f), c.Def);
        Fill(new Rect(x + 56f, y + 70f, 26f, 18f), new Color(0f, 0f, 0f, 0.9f));
        GUI.Label(new Rect(x + 56f, y + 70f, 26f, 18f), c.Level.ToString(), numStyle);

        if (c.SkillPoints > 0)
            GUI.Label(new Rect(x + 96f, y + 2f, 280f, 18f), $"<b>{c.SkillPoints}</b> skill point{(c.SkillPoints > 1 ? "s" : "")} to spend", smallStyle);

        for (int i = 0; i < 4; i++) DrawAbility(c, i, new Rect(x + 96f + i * 72f, y + 24f, 64f, 64f));

        // health, mana and xp
        float bx = x + 96f, bw = 280f;
        LabeledBar(new Rect(bx, y + 94f, bw, 18f), u.Health / u.Total.Health, Color.Lerp(new Color(0.85f, 0.2f, 0.2f), new Color(0.3f, 0.8f, 0.35f), u.Health / u.Total.Health * 1.4f),
            $"{u.Health:0} / {u.Total.Health:0}");
        LabeledBar(new Rect(bx, y + 114f, bw, 14f), u.Total.Mana > 0f ? u.Mana / u.Total.Mana : 0f, new Color(0.25f, 0.55f, 1f), $"{u.Mana:0} / {u.Total.Mana:0}");
        LabeledBar(new Rect(bx, y + 130f, bw, 5f), (float)c.Xp / c.XpToNext, new Color(0.7f, 0.4f, 0.95f), null);

        // items
        for (int i = 0; i < Champion.MaxItems; i++)
        {
            var r = new Rect(x + 400f + (i % 3) * 46f, y + 16f + (i / 3) * 46f, 42f, 42f);
            Fill(r, new Color(0.02f, 0.02f, 0.03f, 1f));
            if (i >= c.Items.Count) continue;
            var it = c.Items[i];
            int hash = 0;
            foreach (char ch in it.Name) hash = hash * 31 + ch;
            Fill(Inset(r, 3f), Color.HSVToRGB((hash & 0xFF) / 255f, 0.45f, 0.6f));
            GUI.Label(r, Initials(it.Name), numStyle);
            GUI.Label(r, new GUIContent("", ItemTip(it, c)));
        }
        GUI.Label(new Rect(x + 400f, y + 110f, 150f, 22f), $"<b>Gold {c.Gold:0}</b>", smallStyle);

        // stats and clock
        var t = u.Total;
        string clock = $"{(int)(gm.Clock / 60f)}:{(int)(gm.Clock % 60f):00}";
        GUI.Label(new Rect(x + 560f, y + 16f, 150f, 110f),
            $"AD {t.AD:0}   AP {t.AP:0}\nArmor {t.Armor:0}   MR {t.MR:0}\nAtk speed {t.AttackSpeed:0.00}\nMove {t.MoveSpeed:0.0}\nCooldown -{Mathf.Min(t.CDR, 0.4f) * 100f:0}%\nTime {clock}", smallStyle);

        if (c.Dead)
        {
            Fill(new Rect(x, y, w, h), new Color(0f, 0f, 0f, 0.6f));
            GUI.Label(new Rect(x, y, w, h), $"Respawning in {Mathf.Max(0f, c.RespawnAt - Time.time):0}s", cdStyle);
        }
    }

    // One ability slot: key, name, rank pips, cooldown sweep with seconds, a blue tint when you lack the mana, and a pulsing + when you can level it.
    void DrawAbility(Champion c, int i, Rect r)
    {
        var a = c.Def.Abilities[i];
        int rank = c.Ranks[i];
        Color tint = a.Tint ?? Vfx.ColorOf(a);
        float total = a.CooldownAt(Mathf.Max(1, rank), c.U.Total.CDR);
        float cd = Mathf.Max(0f, c.CdEnd[i] - Time.time);
        bool ready = rank > 0 && cd <= 0f && c.U.Mana >= a.Mana;

        Fill(r, ready ? Color.Lerp(tint, Color.white, 0.2f) : new Color(0.02f, 0.02f, 0.03f, 1f)); // bright frame when castable
        Fill(Inset(r, 3f), rank > 0 ? tint * 0.55f : new Color(0.2f, 0.2f, 0.22f));
        GUI.Label(new Rect(r.x, r.y + 6f, r.width, 32f), "QWER"[i].ToString(), keyStyle);
        Fill(new Rect(r.x + 3f, r.yMax - 27f, r.width - 6f, 24f), new Color(0f, 0f, 0f, 0.6f));
        GUI.Label(new Rect(r.x + 3f, r.yMax - 27f, r.width - 6f, 24f), a.Name, nameStyle);
        for (int p = 0; p < a.MaxRank; p++) // rank pips
            Fill(new Rect(r.x + 5f + p * 9f, r.y + 5f, 7f, 4f), p < rank ? new Color(1f, 0.85f, 0.3f) : new Color(0.1f, 0.1f, 0.12f));

        if (rank == 0) Fill(Inset(r, 3f), new Color(0f, 0f, 0f, 0.55f));
        else if (cd > 0f)
        {
            Fill(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, (r.height - 6f) * Mathf.Clamp01(cd / total)), new Color(0f, 0f, 0f, 0.7f));
            GUI.Label(r, cd >= 10f ? cd.ToString("0") : cd.ToString("0.0"), cdStyle);
        }
        else if (c.U.Mana < a.Mana) Fill(Inset(r, 3f), new Color(0.1f, 0.2f, 0.9f, 0.5f));

        GUI.Label(r, new GUIContent("", a.Tooltip(c.U, rank))); // hover tooltip

        if (!c.CanRank(i)) return;
        var old = GUI.color;
        GUI.color = Color.Lerp(Color.white, new Color(1f, 0.9f, 0.3f), Mathf.PingPong(Time.time * 2f, 1f));
        if (GUI.Button(new Rect(r.x + 20f, r.y - 24f, 24f, 20f), "+")) pendingRank = i;
        GUI.color = old;
    }

    void DrawShop()
    {
        var c = Champion.Local;
        GUILayout.BeginArea(new Rect(20, 40, 400, Screen.height - 240), GUI.skin.box);
        GUILayout.Label($"Shop   Gold {c.Gold:0}   (P to close)");
        GUILayout.Label(c.AtBase ? "At base: you can buy and sell." : "Not at base: browse only. Press B to recall, then buy.");
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
        var me = Champion.Local;
        bool won = me != null ? gm.Winner == me.U.Team : gm.Winner == Team.Blue; // your own team, not always blue
        GUILayout.Label(won ? "VICTORY" : "DEFEAT");
        GUILayout.Label($"Match time {(int)(gm.Clock / 60f)}:{(int)(gm.Clock % 60f):00}");
        if (!Net.Online && GUILayout.Button("Rematch (same champion)", GUILayout.Height(36))) gm.Rematch();
        if (GUILayout.Button("Champion select", GUILayout.Height(36))) gm.ToChampSelect();
        GUILayout.EndArea();
    }
}

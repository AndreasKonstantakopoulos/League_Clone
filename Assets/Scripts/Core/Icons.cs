using System.Collections.Generic;
using UnityEngine;

// Champion icons. Uses Assets/Resources/Icons/<Champion Name>.png when it exists,
// otherwise draws a placeholder: a disc in the champion's colour, ringed in the role colour, with initials on top.
public static class Icons
{
    static readonly Dictionary<ChampionDef, (Texture2D tex, bool art)> cache = new Dictionary<ChampionDef, (Texture2D, bool)>();
    static GUIStyle initialsStyle;

    public static Color RoleColor(Role r)
    {
        switch (r)
        {
            case Role.Tank: return new Color(0.3f, 0.5f, 0.9f);
            case Role.Bruiser: return new Color(0.9f, 0.5f, 0.2f);
            case Role.Assassin: return new Color(0.7f, 0.25f, 0.85f);
            case Role.Marksman: return new Color(0.95f, 0.8f, 0.2f);
            case Role.Mage: return new Color(0.3f, 0.8f, 0.95f);
            default: return new Color(0.4f, 0.9f, 0.5f); // Support
        }
    }

    static (Texture2D tex, bool art) Get(ChampionDef d)
    {
        if (cache.TryGetValue(d, out var hit) && hit.tex != null) return hit;
        var art = Resources.Load<Texture2D>("Icons/" + d.Name);
        hit = art != null ? (art, true) : (Generate(d), false);
        cache[d] = hit;
        return hit;
    }

    static Texture2D Generate(ChampionDef d)
    {
        const int n = 64;
        var ring = RoleColor(d.Role);
        var px = new Color32[n * n];
        float c = (n - 1) / 2f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dist = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                Color col = dist > c ? Color.clear
                          : dist > c - 6f ? ring
                          : Color.Lerp(d.Color * 0.6f, d.Color, y / (float)n); // light at the top
                col.a = dist > c ? 0f : 1f;
                px[y * n + x] = col;
            }
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // Call from OnGUI.
    public static void Draw(Rect r, ChampionDef d)
    {
        var (tex, art) = Get(d);
        GUI.DrawTexture(r, tex);
        if (art) return;
        if (initialsStyle == null)
            initialsStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        initialsStyle.fontSize = Mathf.RoundToInt(r.height * 0.36f);
        initialsStyle.normal.textColor = Color.white;
        string initials = "";
        foreach (var w in d.Name.Split(' ')) if (w.Length > 0) initials += w[0];
        GUI.Label(r, initials, initialsStyle);
    }
}

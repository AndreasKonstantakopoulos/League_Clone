using UnityEngine;

// Minimap in the bottom-right corner. A top-down camera renders the whole map to a texture (so the fog overlay and
// hidden enemies show up exactly as in the world) and unit icons are drawn on top. Click it with either mouse
// button and your champion walks there. The white box shows what the main camera currently sees.
public class Minimap : MonoBehaviour
{
    static float worldW = 160f, worldH = 96f;
    static Vector3 center;
    RenderTexture rt;

    // Where it sits on screen (GUI coordinates, origin top-left). Hud uses this to keep clicks from reaching the world.
    public static Rect Area
    {
        get
        {
            float w = Mathf.Clamp(Screen.width * 0.2f, 220f, 320f), h = w * worldH / worldW;
            return new Rect(Screen.width - w - 10f, Screen.height - h - 10f, w, h);
        }
    }

    static Vector2 ToMinimap(Vector3 world, Rect r)
    {
        float u = (world.x - center.x) / worldW + 0.5f, v = 0.5f - (world.z - center.z) / worldH; // top of the minimap = +z
        return new Vector2(r.x + u * r.width, r.y + v * r.height);
    }

    static Vector3 ToWorld(Vector2 gui, Rect r)
    {
        float u = (gui.x - r.x) / r.width, v = (gui.y - r.y) / r.height;
        return new Vector3(center.x + (u - 0.5f) * worldW, 0f, center.z + (0.5f - v) * worldH);
    }

    void Start()
    {
        var ground = GameObject.Find("Ground"); // the map is measured from the ground, like the fog
        if (ground != null)
        {
            worldW = ground.transform.lossyScale.x;
            worldH = ground.transform.lossyScale.z;
            center = ground.transform.position;
        }
        rt = new RenderTexture(512, Mathf.RoundToInt(512f * worldH / worldW), 16);
        var go = new GameObject("MinimapCamera");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = worldH / 2f;
        cam.aspect = worldW / worldH;
        cam.transform.position = new Vector3(center.x, 60f, center.z);
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // looking straight down, +z is up on screen
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 120f;
        cam.targetTexture = rt;
    }

    void OnDestroy() { if (rt != null) rt.Release(); }

    void Update()
    {
        var me = Champion.Local;
        var gm = GameManager.I;
        if (me == null || gm == null || gm.State != MatchState.Playing) return;
        if (!GameInput.LeftClickDown && !GameInput.RightClickDown) return;
        Vector2 m = GameInput.MousePos;
        m.y = Screen.height - m.y; // the mouse has its origin bottom-left, the GUI top-left
        var r = Area;
        if (r.Contains(m)) me.RequestMove(ToWorld(m, r));
    }

    static void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    static void Outline(Rect r, Color c, float t)
    {
        Fill(new Rect(r.x, r.y, r.width, t), c);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c);
        Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    void OnGUI()
    {
        var gm = GameManager.I;
        if (gm == null || rt == null) return;
        var r = Area;
        Fill(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), new Color(0.8f, 0.65f, 0.25f)); // gold frame
        GUI.DrawTexture(r, rt, ScaleMode.StretchToFill, false);

        var me = Champion.Local;
        var viewer = me != null ? me.U.Team : Team.Blue;
        foreach (var u in Unit.All)
        {
            if (!u.Alive || !Vision.Sees(viewer, u)) continue; // only what you can see
            float s;
            Color c = u.Team == Team.Neutral ? new Color(1f, 0.85f, 0.3f) : u.Team == Team.Blue ? new Color(0.35f, 0.65f, 1f) : new Color(1f, 0.35f, 0.3f);
            switch (u.Kind)
            {
                case UnitKind.Champion: s = 9f; break;
                case UnitKind.Minion: s = 3f; break;
                case UnitKind.Tower: s = 7f; break;
                case UnitKind.Nexus: s = 12f; break;
                default: s = 5f; break;
            }
            var p = ToMinimap(u.transform.position, r);
            var icon = new Rect(p.x - s / 2f, p.y - s / 2f, s, s);
            if (me != null && u == me.U) Outline(new Rect(icon.x - 2f, icon.y - 2f, icon.width + 4f, icon.height + 4f), Color.white, 1.5f);
            Fill(icon, c);
        }

        // what the main camera is looking at
        var cam = Camera.main;
        if (cam == null) return;
        var plane = new Plane(Vector3.up, Vector3.zero);
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var corner in new[] { new Vector2(0f, 0f), new Vector2(Screen.width, 0f), new Vector2(Screen.width, Screen.height), new Vector2(0f, Screen.height) })
        {
            var ray = cam.ScreenPointToRay(corner);
            if (!plane.Raycast(ray, out float d)) continue;
            var p = ToMinimap(ray.GetPoint(d), r);
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
        }
        if (minX < maxX)
        {
            var view = new Rect(Mathf.Max(minX, r.x), Mathf.Max(minY, r.y), 0f, 0f);
            view.xMax = Mathf.Min(maxX, r.xMax);
            view.yMax = Mathf.Min(maxY, r.yMax);
            if (view.width > 2f && view.height > 2f) Outline(view, Color.white, 1.5f);
        }
    }
}

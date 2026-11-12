using UnityEngine;

// Draws the fog: a dark, soft-edged overlay on the ground wherever your team has no vision,
// and hides enemy minions and jungle monsters standing in it. (Champions hide themselves in Champion.ApplyStealthLook.)
public class Fog : MonoBehaviour
{
    const float Interval = 0.1f, Darkness = 0.62f, EdgeSoftness = 2.5f;
    Texture2D tex;
    Color32[] px;
    float[] clear;
    int w, h;
    float cellX, cellZ, halfX, halfZ, next;
    Vector3 origin;
    static readonly Team[] Teams = { Team.Blue, Team.Red };

    void Start()
    {
        var ground = GameObject.Find("Ground");
        if (ground == null) { enabled = false; return; }
        var size = ground.transform.lossyScale;
        origin = ground.transform.position;
        halfX = size.x / 2f;
        halfZ = size.z / 2f;
        w = 160;
        h = Mathf.Max(8, Mathf.RoundToInt(w * size.z / size.x));
        cellX = size.x / w;
        cellZ = size.z / h;
        clear = new float[w * h];
        px = new Color32[w * h];
        tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); // lies flat just above the ground and bases
        quad.name = "FogOfWar";
        Destroy(quad.GetComponent<Collider>());
        quad.transform.position = new Vector3(origin.x, 0.32f, origin.z);
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = new Vector3(size.x, size.z, 1f);
        quad.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
        var walls = new System.Collections.Generic.List<Transform>();
        foreach (var c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            if (c.name.StartsWith("JungleWall")) walls.Add(c.transform);
        Vision.SetWalls(walls);
        var brush = new System.Collections.Generic.List<Transform>();
        foreach (var c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            if (c.name.StartsWith("Bush")) brush.Add(c.transform);
        Vision.SetBushes(brush);
        Vision.Refresh();
        Rebuild(Viewer());
    }

    void OnDestroy() { if (tex != null) Destroy(tex); }

    static Team Viewer() { return Champion.Local != null ? Champion.Local.U.Team : Team.Blue; }

    void Update()
    {
        var viewer = Viewer();
        if (Time.time >= next)
        {
            next = Time.time + Interval;
            Vision.Refresh();
            if (tex != null) Rebuild(viewer);
        }
        // every frame, so a unit never flashes into view before the next refresh
        foreach (var u in Unit.All)
            if (u.Kind != UnitKind.Champion && u.Rend != null) u.Rend.enabled = Vision.Sees(viewer, u); // minions, monsters and buildings
        var gm = GameManager.I;
        if (gm == null) return;
        foreach (var t in Teams) // the enemy shopkeeper stays hidden until you scout their base
        {
            var npc = gm.ShopNpc(t);
            var r = npc != null ? npc.GetComponent<Renderer>() : null;
            if (r != null) r.enabled = t == viewer || Vision.SeesPoint(viewer, npc.position);
        }
    }

    // clear[] holds 1 where the team sees well and fades to 0 at the edge of each vision circle.
    void Rebuild(Team viewer)
    {
        System.Array.Clear(clear, 0, clear.Length);
        foreach (var u in Unit.All)
        {
            if (!u.Alive || u.Team != viewer) continue;
            float r = Vision.RadiusOf(u);
            if (r <= 0f) continue;
            Vector3 p = u.transform.position;
            int cx = Mathf.FloorToInt((p.x - origin.x + halfX) / cellX), cz = Mathf.FloorToInt((p.z - origin.z + halfZ) / cellZ);
            int rx = Mathf.CeilToInt(r / cellX) + 1, rz = Mathf.CeilToInt(r / cellZ) + 1;
            for (int z = Mathf.Max(0, cz - rz); z <= Mathf.Min(h - 1, cz + rz); z++)
                for (int x = Mathf.Max(0, cx - rx); x <= Mathf.Min(w - 1, cx + rx); x++)
                {
                    float dx = origin.x - halfX + (x + 0.5f) * cellX - p.x, dz = origin.z - halfZ + (z + 0.5f) * cellZ - p.z;
                    float v = Mathf.Clamp01((r - Mathf.Sqrt(dx * dx + dz * dz)) / EdgeSoftness);
                    if (v <= clear[z * w + x]) continue;
                    if (Vision.CanSee(p, r, new Vector3(p.x + dx, 0f, p.z + dz))) clear[z * w + x] = v; // walls cast a shadow
                }
        }
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, (byte)((1f - clear[i]) * Darkness * 255f));
        tex.SetPixels32(px);
        tex.Apply();
    }
}

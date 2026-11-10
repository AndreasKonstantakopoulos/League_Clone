using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Tools > Build Greybox Map. Rebuilds Assets/Scenes/Map.unity and ChampSelect.unity from scratch (overwrites them).
// Layout (like Twisted Treeline): an oval field inside a 160 x 96 m rectangle. Blue base at the left end, red at the right.
// Two lanes curve along the top and bottom edges; between them is the jungle, a maze of walls with camps and the two big pits.
public static class BuildGreyboxMap
{
    const float K = 1.6f;                      // scales the base, shop and Nexus positions
    const float HalfW = 80f, HalfD = 48f;      // the rectangle the oval sits in (this is the Ground object)
    const float OvalA = 78f, OvalB = 46f;      // the oval playing field (semi-axes)
    const float LaneA = 70f, LaneB = 36f;      // lane centre lines: a smaller oval inside it
    const float RimA = 63f, RimB = 29f;        // the jungle wall that runs along the inside of each lane
    const string ScenePath = "Assets/Scenes/Map.unity";
    const string SelectScenePath = "Assets/Scenes/ChampSelect.unity";
    const string NavDataPath = "Assets/Scenes/Map_NavMesh.asset";
    const string MatDir = "Assets/Data/Greybox";
    static readonly string[] LaneName = { "Top", "Bottom" };
    static readonly string[] TeamName = { "Blue", "Red" }; // index 0 = side -1 (left), index 1 = side +1 (right)

    static Material voidMat, floorMat, ground, lane, blue, red;
    static Transform root;
    static Vector3[][] lanePts = new Vector3[2][];
    static int towers, nexuses, camps, pits, waypoints, wallCount;

    [MenuItem("Tools/Build Greybox Map")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        towers = nexuses = camps = pits = waypoints = wallCount = 0;

        voidMat = Mat("Void", new Color(0.04f, 0.05f, 0.06f));
        floorMat = Mat("Floor", new Color(0.2f, 0.21f, 0.18f));
        ground = Mat("Ground", new Color(0.25f, 0.27f, 0.25f));
        lane = Mat("Lane", new Color(0.55f, 0.55f, 0.5f));
        blue = Mat("Blue", new Color(0.2f, 0.4f, 0.9f));
        red = Mat("Red", new Color(0.9f, 0.25f, 0.25f));

        root = new GameObject("Map").transform;
        // The Ground object keeps its name and size: the fog and the minimap measure the map from it.
        Box("Ground", PrimitiveType.Cube, new Vector3(0, -0.5f, 0), new Vector3(2 * HalfW, 1, 2 * HalfD), voidMat);

        BuildBoundary();
        BuildLanes();
        BuildJungleWalls();
        BuildJungleMarkers();
        for (int t = 0; t < 2; t++) BuildTeam(t, t == 0 ? -1 : 1);

        // Top-down-ish camera so the whole map is visible on open.
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0, 70 * K, -40 * K);
            cam.transform.rotation = Quaternion.Euler(60, 0, 0);
        }

        var surface = root.gameObject.AddComponent<NavMeshSurface>();
        surface.BuildNavMesh();
        // The baked data must be a saved asset or it is lost when the scene reloads.
        AssetDatabase.DeleteAsset(NavDataPath);
        AssetDatabase.CreateAsset(surface.navMeshData, NavDataPath);

        EditorSceneManager.SaveScene(scene, ScenePath);
        BuildChampSelect(); // also registers both scenes in Build Settings, which scene loading needs
        // Symmetry check: expected 8 towers, 2 nexuses, 4 camps, 2 pits, 18 waypoints.
        bool ok = towers == 8 && nexuses == 2 && camps == 4 && pits == 2 && waypoints == 18;
        Debug.Log($"Greybox map {(ok ? "OK" : "COUNT MISMATCH")} ({2 * HalfW:0}x{2 * HalfD:0} m): towers={towers} nexuses={nexuses} camps={camps} pits={pits} waypoints={waypoints} walls={wallCount}");
    }

    // Separate scene for champion select. The runtime ChampSelect script adds the UI when it finds the marker object.
    static void BuildChampSelect()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        new GameObject("ChampSelectRoot");
        var cam = Camera.main;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
        cam.transform.position = new Vector3(0f, 1.8f, -5.5f);
        cam.transform.LookAt(new Vector3(0f, 1f, 0f));
        EditorSceneManager.SaveScene(scene, SelectScenePath);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(SelectScenePath, true), // index 0: the game starts here
            new EditorBuildSettingsScene(ScenePath, true),
        };
    }

    // ---------- helpers ----------
    // Point on an ellipse with semi-axes a (x) and b (z); angle in degrees from +x towards +z.
    static Vector3 Ell(float a, float b, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        return new Vector3(a * Mathf.Cos(r), 0f, b * Mathf.Sin(r));
    }

    // A box lying along the segment p -> q (rotated about Y so its long side follows the segment).
    static GameObject Seg(string name, Vector3 p, Vector3 q, float height, float thick, Material mat, float centerY)
    {
        Vector3 d = q - p;
        d.y = 0f;
        var go = Box(name, PrimitiveType.Cube, new Vector3((p.x + q.x) / 2f, centerY, (p.z + q.z) / 2f),
            new Vector3(d.magnitude + 0.3f, height, thick), mat);
        go.transform.localRotation = Quaternion.Euler(0f, Mathf.Atan2(-d.z, d.x) * Mathf.Rad2Deg, 0f);
        return go;
    }

    // ---------- map parts ----------
    static void BuildBoundary()
    {
        Box("Wall_N", PrimitiveType.Cube, new Vector3(0, 1.5f, HalfD + 0.5f), new Vector3(2 * HalfW + 2, 3, 1), ground);
        Box("Wall_S", PrimitiveType.Cube, new Vector3(0, 1.5f, -HalfD - 0.5f), new Vector3(2 * HalfW + 2, 3, 1), ground);
        Box("Wall_W", PrimitiveType.Cube, new Vector3(-HalfW - 0.5f, 1.5f, 0), new Vector3(1, 3, 2 * HalfD + 2), ground);
        Box("Wall_E", PrimitiveType.Cube, new Vector3(HalfW + 0.5f, 1.5f, 0), new Vector3(1, 3, 2 * HalfD + 2), ground);

        // The floor is an oval; a ring of walls closes it, and everything outside is dark void nobody can reach.
        Box("OvalFloor", PrimitiveType.Cylinder, new Vector3(0, 0.03f, 0), new Vector3(2 * OvalA, 0.03f, 2 * OvalB), floorMat);
        const int n = 72;
        for (int i = 0; i < n; i++)
            Seg($"Wall_Ring_{i}", Ell(OvalA + 0.75f, OvalB + 0.75f, i * 360f / n), Ell(OvalA + 0.75f, OvalB + 0.75f, (i + 1) * 360f / n), 3f, 1.5f, ground, 1.5f);
    }

    // Two lanes that curve along the top and bottom of the oval, base to base, as 9 waypoints each.
    static void BuildLanes()
    {
        for (int i = 0; i < 2; i++)
        {
            var pts = new Vector3[9];
            for (int n = 0; n < 9; n++)
            {
                pts[n] = Ell(LaneA, LaneB, 150f - n * 15f);
                if (i == 1) pts[n].z = -pts[n].z;
                // One shared ordered list, blue base -> red base. Red minions walk it in reverse.
                Marker($"Waypoint_{LaneName[i]}_{n:00}", pts[n]);
            }
            lanePts[i] = pts;
            waypoints += 9;
            for (int n = 0; n < 8; n++)
                Seg($"Lane_{LaneName[i]}_{n}", pts[n], pts[n + 1], 0.1f, 8f, lane, 0.05f);
        }
    }

    // Jungle walls: one along the inside of each lane with gaps for entrances (the names start with
    // "JungleWall" so vision treats them as sight blockers), plus a maze of short walls in the middle.
    static void BuildJungleWalls()
    {
        float[][] chunks = { new[] { 24f, 52f }, new[] { 62f, 84f }, new[] { 96f, 118f }, new[] { 128f, 156f } };
        foreach (int sign in new[] { 1, -1 })
            foreach (var ch in chunks)
                for (float a = ch[0]; a < ch[1] - 0.01f; a += 7f)
                {
                    var p = Ell(RimA, RimB, a);
                    var q = Ell(RimA, RimB, Mathf.Min(a + 7f, ch[1]));
                    p.z *= sign;
                    q.z *= sign;
                    Seg($"JungleWall_Rim_{(sign > 0 ? "N" : "S")}_{wallCount++}", p, q, 2f, 1f, ground, 1f);
                }

        // x, z, length along x, thickness along z. Each is mirrored into all four quarters of the map.
        float[][] maze = { new[] { 22f, 5f, 10f, 1.6f }, new[] { 8f, 16f, 1.6f, 9f }, new[] { 44f, 8f, 1.6f, 10f } };
        foreach (var m in maze)
            foreach (int sx in new[] { -1, 1 })
                foreach (int sz in new[] { -1, 1 })
                    Box($"JungleWall_Maze_{wallCount++}", PrimitiveType.Cube, new Vector3(sx * m[0], 1f, sz * m[1]), new Vector3(m[2], 2f, m[3]), ground);
    }

    static void BuildJungleMarkers()
    {
        // Empty-ish named markers; the jungle manager spawns camps and the two big objectives on them.
        foreach (int side in new[] { -1, 1 })
            for (int i = 0; i < 2; i++)
            {
                Box($"Camp_{TeamName[side < 0 ? 0 : 1]}_{LaneName[i]}", PrimitiveType.Cylinder,
                    new Vector3(side * 32f, 0.1f, i == 0 ? 16f : -16f), new Vector3(2, 0.1f, 2), lane);
                camps++;
            }
        for (int i = 0; i < 2; i++)
        {
            Box($"BigBuff_{LaneName[i]}", PrimitiveType.Cylinder, new Vector3(0, 0.1f, i == 0 ? 10f : -10f), new Vector3(4, 0.1f, 4), lane);
            pits++;
        }
    }

    static void BuildTeam(int t, int side)
    {
        var mat = t == 0 ? blue : red;
        string team = TeamName[t];
        Box($"Base_{team}", PrimitiveType.Cube, new Vector3(side * 43 * K, 0.1f, 0), new Vector3(14 * K, 0.2f, 20 * K), mat);
        Box($"Spawn_{team}", PrimitiveType.Cube, new Vector3(side * 40 * K, 0.25f, 0), new Vector3(4, 0.1f, 4), mat);
        Box($"Shop_{team}", PrimitiveType.Cube, new Vector3(side * 40 * K, 0.25f, 7 * K), new Vector3(3, 0.1f, 3), mat); // the shop NPC stands here
        Box($"Nexus_{team}", PrimitiveType.Cylinder, new Vector3(side * 45 * K, 2, 0), new Vector3(4, 2, 4), mat);
        nexuses++;

        for (int i = 0; i < 2; i++)
        {
            // Lane points run blue -> red: the outer tower is on the third point from this team's end, the inner one near its base.
            var pts = lanePts[i];
            Vector3 outer = side < 0 ? pts[2] : pts[6];
            Vector3 inner = side < 0 ? (pts[0] + pts[1]) / 2f : (pts[8] + pts[7]) / 2f;
            Box($"Tower_{team}_{LaneName[i]}_Outer", PrimitiveType.Cylinder, new Vector3(outer.x, 1.5f, outer.z), new Vector3(1.5f, 1.5f, 1.5f), mat);
            Box($"Tower_{team}_{LaneName[i]}_Inner", PrimitiveType.Cylinder, new Vector3(inner.x, 1.5f, inner.z), new Vector3(1.5f, 1.5f, 1.5f), mat);
            towers += 2;
        }
    }

    static GameObject Box(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static void Marker(string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
    }

    static Material Mat(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Data", "Greybox");
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        m = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}

using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Tools > Build Greybox Map. Rebuilds Assets/Scenes/Map.unity and ChampSelect.unity from scratch (overwrites them).
// Layout follows Twisted Treeline: a wide 160 x 96 m field, left-right mirrored.
//   - Bases at both ends. Each has its Nexus and an S-shaped ridge closing off its inner side, so a base opens onto the two lanes.
//   - Top lane: leaves the base, runs down and across under the big alcove at the top centre (the Vilemaw-style pit).
//   - Bottom lane: runs flat along the bottom edge.
//   - Between the lanes is the jungle: diagonal walls, a centre wall, and a row of walls above the bottom lane, with camps and the second pit.
public static class BuildGreyboxMap
{
    const float HalfW = 80f, HalfD = 48f;
    const string ScenePath = "Assets/Scenes/Map.unity";
    const string SelectScenePath = "Assets/Scenes/ChampSelect.unity";
    const string NavDataPath = "Assets/Scenes/Map_NavMesh.asset";
    const string MatDir = "Assets/Data/Greybox";
    static readonly string[] LaneName = { "Top", "Bottom" };
    static readonly string[] TeamName = { "Blue", "Red" }; // index 0 = side -1 (left), index 1 = side +1 (right)

    // Lane centre lines as (x, z), blue base -> red base.
    static readonly Vector2[][] LanePath =
    {
        new[] { new Vector2(-66, 27), new Vector2(-50, 25), new Vector2(-36, 18), new Vector2(-22, 10), new Vector2(0, 5),
                new Vector2(22, 10), new Vector2(36, 18), new Vector2(50, 25), new Vector2(66, 27) },
        new[] { new Vector2(-66, -30), new Vector2(-52, -35), new Vector2(-36, -36), new Vector2(-18, -36), new Vector2(0, -36),
                new Vector2(18, -36), new Vector2(36, -36), new Vector2(52, -35), new Vector2(66, -30) },
    };

    static Material floorMat, wallMat, lane, blue, red;
    static Transform root;
    static Vector3[][] lanePts = new Vector3[2][];
    static int towers, nexuses, camps, pits, waypoints, wallCount;

    [MenuItem("Tools/Build Greybox Map")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        towers = nexuses = camps = pits = waypoints = wallCount = 0;

        floorMat = Mat("Floor", new Color(0.13f, 0.27f, 0.31f));
        wallMat = Mat("Wall", new Color(0.22f, 0.16f, 0.3f));
        lane = Mat("Lane", new Color(0.3f, 0.45f, 0.5f));
        blue = Mat("Blue", new Color(0.2f, 0.4f, 0.9f));
        red = Mat("Red", new Color(0.9f, 0.25f, 0.25f));

        root = new GameObject("Map").transform;
        // The Ground object keeps its name and size: the fog and the minimap measure the map from it.
        Box("Ground", PrimitiveType.Cube, new Vector3(0, -0.5f, 0), new Vector3(2 * HalfW, 1, 2 * HalfD), floorMat);

        BuildBoundary();
        BuildLanes();
        BuildJungleWalls();
        BuildJungleMarkers();
        for (int t = 0; t < 2; t++) BuildTeam(t, t == 0 ? -1 : 1);

        // Top-down-ish camera so the whole map is visible on open.
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0, 112, -64);
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

    // A jungle wall along p -> q. The name must start with "JungleWall": vision treats those as sight blockers.
    static void Wall(Vector3 p, Vector3 q)
    {
        Seg($"JungleWall_{wallCount++}", p, q, 2.2f, 1.6f, wallMat, 1.1f);
    }

    static Vector3 V(float x, float z) { return new Vector3(x, 0f, z); }

    // Builds a wall in both halves of the map (x mirrored).
    static void WallPair(Vector3 p, Vector3 q)
    {
        Wall(p, q);
        Wall(new Vector3(-p.x, 0f, p.z), new Vector3(-q.x, 0f, q.z));
    }

    // ---------- map parts ----------
    static void BuildBoundary()
    {
        // outer walls, plus a dark border strip along the top and bottom like the reference map
        Box("Wall_N", PrimitiveType.Cube, new Vector3(0, 1.5f, HalfD + 0.5f), new Vector3(2 * HalfW + 2, 3, 1), wallMat);
        Box("Wall_S", PrimitiveType.Cube, new Vector3(0, 1.5f, -HalfD - 0.5f), new Vector3(2 * HalfW + 2, 3, 1), wallMat);
        Box("Wall_W", PrimitiveType.Cube, new Vector3(-HalfW - 0.5f, 1.5f, 0), new Vector3(1, 3, 2 * HalfD + 2), wallMat);
        Box("Wall_E", PrimitiveType.Cube, new Vector3(HalfW + 0.5f, 1.5f, 0), new Vector3(1, 3, 2 * HalfD + 2), wallMat);
        Box("Border_N", PrimitiveType.Cube, new Vector3(0, 1.2f, HalfD - 2f), new Vector3(2 * HalfW, 2.4f, 3f), wallMat);
        Box("Border_S", PrimitiveType.Cube, new Vector3(0, 1.2f, -HalfD + 2f), new Vector3(2 * HalfW, 2.4f, 3f), wallMat);
    }

    static void BuildLanes()
    {
        for (int i = 0; i < 2; i++)
        {
            var pts = new Vector3[9];
            for (int n = 0; n < 9; n++)
            {
                pts[n] = V(LanePath[i][n].x, LanePath[i][n].y);
                // One shared ordered list, blue base -> red base. Red minions walk it in reverse.
                Marker($"Waypoint_{LaneName[i]}_{n:00}", pts[n]);
            }
            lanePts[i] = pts;
            waypoints += 9;
            for (int n = 0; n < 8; n++)
                Seg($"Lane_{LaneName[i]}_{n}", pts[n], pts[n + 1], 0.1f, 8f, lane, 0.05f);
        }
    }

    static void BuildJungleWalls()
    {
        // Base ridge: an S-shaped wall closing off the inner side of each base, open at the top and bottom onto the lanes.
        Vector3[] ridge = { V(-55, 19), V(-50, 11), V(-47, 2), V(-47, -8), V(-51, -17), V(-57, -23) };
        for (int i = 0; i < ridge.Length - 1; i++) WallPair(ridge[i], ridge[i + 1]);

        // The alcove at the top centre (where the Vilemaw-style pit sits): side walls and a curved front with an opening in the middle.
        WallPair(V(-27, 44), V(-24, 15));
        for (float a = 195f; a < 345f - 0.01f; a += 15f)
        {
            if (a >= 255f && a < 285f) continue; // the entrance
            float r1 = a * Mathf.Deg2Rad, r2 = (a + 15f) * Mathf.Deg2Rad;
            Wall(V(24f * Mathf.Cos(r1), 17f + 6f * Mathf.Sin(r1)), V(24f * Mathf.Cos(r2), 17f + 6f * Mathf.Sin(r2)));
        }

        // Centre jungle: a wall just under the top lane, two diagonals that meet near the bottom, and a row above the bottom lane.
        Wall(V(-18, -3), V(-3, -3));
        Wall(V(3, -3), V(18, -3));
        WallPair(V(-41, 2), V(-12, -20));
        WallPair(V(-52, -26), V(-36, -26)); // starts clear of the base ridge so there is a proper gap
        WallPair(V(-32, -26), V(-22, -26));
    }

    static void BuildJungleMarkers()
    {
        // Empty-ish named markers; the jungle manager spawns camps and the two big objectives on them.
        foreach (int side in new[] { -1, 1 })
        {
            Box($"Camp_{TeamName[side < 0 ? 0 : 1]}_Top", PrimitiveType.Cylinder, new Vector3(side * 43f, 0.1f, 12f), new Vector3(2, 0.1f, 2), lane);
            Box($"Camp_{TeamName[side < 0 ? 0 : 1]}_Bottom", PrimitiveType.Cylinder, new Vector3(side * 34f, 0.1f, -12f), new Vector3(2, 0.1f, 2), lane);
            camps += 2;
        }
        Box("BigBuff_Top", PrimitiveType.Cylinder, new Vector3(0, 0.1f, 29f), new Vector3(4, 0.1f, 4), lane);    // inside the alcove
        Box("BigBuff_Bottom", PrimitiveType.Cylinder, new Vector3(0, 0.1f, -15f), new Vector3(4, 0.1f, 4), lane); // in the pocket between the diagonals
        pits += 2;
    }

    static void BuildTeam(int t, int side)
    {
        var mat = t == 0 ? blue : red;
        string team = TeamName[t];
        Box($"Base_{team}", PrimitiveType.Cube, new Vector3(side * 62f, 0.1f, 0), new Vector3(26f, 0.2f, 34f), mat);
        Box($"Spawn_{team}", PrimitiveType.Cube, new Vector3(side * 68f, 0.25f, 0), new Vector3(4, 0.1f, 4), mat);
        Box($"Shop_{team}", PrimitiveType.Cube, new Vector3(side * 68f, 0.25f, 11f), new Vector3(3, 0.1f, 3), mat); // the shop NPC stands here
        Box($"Nexus_{team}", PrimitiveType.Cylinder, new Vector3(side * 58f, 2, 0), new Vector3(4, 2, 4), mat);
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

using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Tools > Build Greybox Map. Rebuilds Assets/Scenes/Map.unity and ChampSelect.unity from scratch (overwrites them).
// Layout: blue base on the left, red on the right, two lanes (top and bottom), jungle between.
// Change K to resize the whole map (1 = 100x60 m, 1.6 = 160x96 m). Everything else scales with it.
public static class BuildGreyboxMap
{
    const float K = 1.6f;
    const string ScenePath = "Assets/Scenes/Map.unity";
    const string SelectScenePath = "Assets/Scenes/ChampSelect.unity";
    const string NavDataPath = "Assets/Scenes/Map_NavMesh.asset";
    const string MatDir = "Assets/Data/Greybox";
    static readonly float[] LaneZ = { 20f * K, -20f * K };
    static readonly string[] LaneName = { "Top", "Bottom" };
    static readonly string[] TeamName = { "Blue", "Red" }; // index 0 = side -1, index 1 = side +1

    static Material ground, lane, blue, red;
    static Transform root;
    static int towers, nexuses, camps, pits, waypoints;

    [MenuItem("Tools/Build Greybox Map")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        towers = nexuses = camps = pits = waypoints = 0;

        ground = Mat("Ground", new Color(0.25f, 0.27f, 0.25f));
        lane = Mat("Lane", new Color(0.55f, 0.55f, 0.5f));
        blue = Mat("Blue", new Color(0.2f, 0.4f, 0.9f));
        red = Mat("Red", new Color(0.9f, 0.25f, 0.25f));

        root = new GameObject("Map").transform;
        Box("Ground", PrimitiveType.Cube, new Vector3(0, -0.5f, 0), new Vector3(100 * K, 1, 60 * K), ground);

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
        Debug.Log($"Greybox map {(ok ? "OK" : "COUNT MISMATCH")} ({100 * K:0}x{60 * K:0} m): towers={towers} nexuses={nexuses} camps={camps} pits={pits} waypoints={waypoints}");
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

    static void BuildBoundary()
    {
        float hw = 50 * K, hd = 30 * K;
        Box("Wall_N", PrimitiveType.Cube, new Vector3(0, 1.5f, hd + 0.5f), new Vector3(2 * hw + 2, 3, 1), ground);
        Box("Wall_S", PrimitiveType.Cube, new Vector3(0, 1.5f, -hd - 0.5f), new Vector3(2 * hw + 2, 3, 1), ground);
        Box("Wall_W", PrimitiveType.Cube, new Vector3(-hw - 0.5f, 1.5f, 0), new Vector3(1, 3, 2 * hd + 2), ground);
        Box("Wall_E", PrimitiveType.Cube, new Vector3(hw + 0.5f, 1.5f, 0), new Vector3(1, 3, 2 * hd + 2), ground);
    }

    static void BuildLanes()
    {
        for (int i = 0; i < 2; i++)
        {
            Box($"Lane_{LaneName[i]}", PrimitiveType.Cube, new Vector3(0, 0.05f, LaneZ[i]), new Vector3(90 * K, 0.1f, 8), lane);
            // One shared ordered list, blue base -> red base. Red minions walk it in reverse.
            for (int n = 0; n < 9; n++)
                Marker($"Waypoint_{LaneName[i]}_{n:00}", new Vector3((-40 + n * 10) * K, 0, LaneZ[i]));
            waypoints += 9;
        }
    }

    static void BuildJungleWalls()
    {
        // Mirrored wall segments with gaps at x=0 and x=+-27 so the jungle has entrances.
        float[,] segs = { { -38, -30 }, { -24, -3 }, { 3, 24 }, { 30, 38 } };
        foreach (float z in new[] { 12f * K, -12f * K })
            for (int s = 0; s < segs.GetLength(0); s++)
            {
                float a = segs[s, 0] * K, b = segs[s, 1] * K;
                Box($"JungleWall_{(z > 0 ? "N" : "S")}_{s}", PrimitiveType.Cube,
                    new Vector3((a + b) / 2, 1, z), new Vector3(b - a, 2, 1), ground);
            }
    }

    static void BuildJungleMarkers()
    {
        // Empty-ish named markers; M7 fills these with camps and big-buff objectives.
        foreach (int side in new[] { -1, 1 })
            for (int i = 0; i < 2; i++)
            {
                Box($"Camp_{TeamName[side < 0 ? 0 : 1]}_{LaneName[i]}", PrimitiveType.Cylinder,
                    new Vector3(side * 18 * K, 0.1f, (i == 0 ? 5 : -5) * K), new Vector3(2, 0.1f, 2), lane);
                camps++;
            }
        for (int i = 0; i < 2; i++)
        {
            Box($"BigBuff_{LaneName[i]}", PrimitiveType.Cylinder,
                new Vector3(0, 0.1f, (i == 0 ? 6 : -6) * K), new Vector3(4, 0.1f, 4), lane);
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
            Box($"Tower_{team}_{LaneName[i]}_Outer", PrimitiveType.Cylinder, new Vector3(side * 22 * K, 1.5f, LaneZ[i]), new Vector3(1.5f, 1.5f, 1.5f), mat);
            Box($"Tower_{team}_{LaneName[i]}_Inner", PrimitiveType.Cylinder, new Vector3(side * 34 * K, 1.5f, LaneZ[i]), new Vector3(1.5f, 1.5f, 1.5f), mat);
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

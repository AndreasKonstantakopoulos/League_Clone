using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Tools > Build Greybox Map. Rebuilds Assets/Scenes/Map.unity and ChampSelect.unity from scratch (overwrites them).
// Layout follows Twisted Treeline as it was in 2013 (the Shadow Isles version): a wide 160 x 96 m field, left-right mirrored.
//   - Per lane: outer turret, inner turret, inhibitor. One more turret guards each Nexus.
//   - Two altars in the jungle (west and east), a health relic in the centre, Vilemaw's lair at the top centre.
//   - Three monster camps per side (wolves, wraiths, golems) and patches of brush.
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

    static Material floorMat, wallMat, lane, blue, red, lairMat, stoneMat, bushMat, barkMat, leafMat, glowGreen, glowOrange;
    static Transform root;
    static Vector3[][] lanePts = new Vector3[2][];
    static int towers, nexuses, camps, pits, waypoints, wallCount, inhibitors, altars, bushes;

    [MenuItem("Tools/Build Greybox Map")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        towers = nexuses = camps = pits = waypoints = wallCount = inhibitors = altars = bushes = 0;

        floorMat = Mat("Floor", new Color(0.08f, 0.2f, 0.24f));
        wallMat = Mat("Wall", new Color(0.17f, 0.12f, 0.26f));
        lane = Mat("Lane", new Color(0.22f, 0.36f, 0.42f));
        lairMat = Mat("Lair", new Color(0.07f, 0.06f, 0.13f));
        stoneMat = Mat("Stone", new Color(0.35f, 0.4f, 0.45f));
        bushMat = Mat("Bush", new Color(0.1f, 0.38f, 0.2f));
        barkMat = Mat("Bark", new Color(0.09f, 0.07f, 0.1f));
        leafMat = Mat("Leaf", new Color(0.07f, 0.22f, 0.25f));
        glowGreen = Mat("GlowGreen", new Color(0.3f, 1f, 0.6f), true);
        glowOrange = Mat("GlowOrange", new Color(1f, 0.5f, 0.15f), true);
        blue = Mat("Blue", new Color(0.2f, 0.4f, 0.9f));
        red = Mat("Red", new Color(0.9f, 0.25f, 0.25f));

        root = new GameObject("Map").transform;
        // The Ground object keeps its name and size: the fog and the minimap measure the map from it.
        Box("Ground", PrimitiveType.Cube, new Vector3(0, -0.5f, 0), new Vector3(2 * HalfW, 1, 2 * HalfD), floorMat);

        BuildBoundary();
        BuildLanes();
        BuildJungleWalls();
        BuildJungleMarkers();
        BuildBrush();
        for (int t = 0; t < 2; t++) BuildTeam(t, t == 0 ? -1 : 1);
        BuildMood();

        // Top-down-ish camera so the whole map is visible on open.
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0, 112, -64);
            cam.transform.rotation = Quaternion.Euler(60, 0, 0);
        }

        var surface = root.gameObject.AddComponent<NavMeshSurface>();
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders; // decorations have no collider, so they never block paths
        surface.BuildNavMesh();
        // The baked data must be a saved asset or it is lost when the scene reloads.
        AssetDatabase.DeleteAsset(NavDataPath);
        AssetDatabase.CreateAsset(surface.navMeshData, NavDataPath);

        EditorSceneManager.SaveScene(scene, ScenePath);
        BuildChampSelect(); // also registers both scenes in Build Settings, which scene loading needs
        // Symmetry check: 10 turrets (4 per lane pair + 1 per Nexus), 4 inhibitors, 2 nexuses, 6 camps, 2 altars, Vilemaw, 18 waypoints.
        bool ok = towers == 10 && inhibitors == 4 && nexuses == 2 && camps == 6 && altars == 2 && pits == 1 && waypoints == 18;
        Debug.Log($"Greybox map {(ok ? "OK" : "COUNT MISMATCH")} ({2 * HalfW:0}x{2 * HalfD:0} m): turrets={towers} inhibitors={inhibitors} nexuses={nexuses} camps={camps} altars={altars} vilemaw={pits} waypoints={waypoints} walls={wallCount} bushes={bushes}");
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
        // Named markers; the jungle manager spawns the camps, Vilemaw, the altars and the relic on them at runtime.
        foreach (int side in new[] { -1, 1 })
        {
            string team = TeamName[side < 0 ? 0 : 1];
            Box($"Camp_{team}_Wolves", PrimitiveType.Cylinder, new Vector3(side * 43f, 0.1f, 12f), new Vector3(2, 0.1f, 2), lane);
            Box($"Camp_{team}_Wraiths", PrimitiveType.Cylinder, new Vector3(side * 34f, 0.1f, -12f), new Vector3(2, 0.1f, 2), lane);
            Box($"Camp_{team}_Golems", PrimitiveType.Cylinder, new Vector3(side * 24f, 0.1f, -19f), new Vector3(2, 0.1f, 2), lane);
            camps += 3;

            // Altar: a stone ring with a disc inside that the game tints with the owner's colour.
            string name = side < 0 ? "Altar_West" : "Altar_East";
            Box(name + "_Ring", PrimitiveType.Cylinder, new Vector3(side * 27f, 0.06f, 1f), new Vector3(7.5f, 0.06f, 7.5f), stoneMat);
            Box(name, PrimitiveType.Cylinder, new Vector3(side * 27f, 0.1f, 1f), new Vector3(5.5f, 0.1f, 5.5f), lane);
            altars++;
        }
        // Vilemaw's lair fills the alcove at the top centre.
        Box("Lair_Floor", PrimitiveType.Cube, new Vector3(0, 0.03f, 31f), new Vector3(44f, 0.06f, 26f), lairMat);
        Box("Vilemaw", PrimitiveType.Cylinder, new Vector3(0, 0.1f, 29f), new Vector3(5, 0.1f, 5), lane);
        pits++;
        // Health relic in the middle of the jungle.
        Box("Relic", PrimitiveType.Cylinder, new Vector3(0, 0.1f, -14f), new Vector3(2.5f, 0.1f, 2.5f), stoneMat);
    }

    // Brush: flat patches of tall grass. Units standing in one are hidden from enemies outside it (see Vision).
    // The names must start with "Bush".
    static void BuildBrush()
    {
        float[][] spots =
        {
            new[] { 12f, -0.6f, 8f, 2.4f },   // under the top lane, either side of the centre gap
            new[] { 36f, 7f, 4.5f, 4.5f },    // beside each altar
            new[] { 8f, -10f, 4f, 4f },       // beside the relic
            new[] { 44f, -29.5f, 8f, 3f },    // above the bottom lane, near each outer turret
        };
        foreach (var b in spots)
            foreach (int sx in new[] { -1, 1 })
            {
                Box($"Bush_{bushes++}", PrimitiveType.Cube, new Vector3(sx * b[0], 0.1f, b[1]), new Vector3(b[2], 0.2f, b[3]), bushMat);
                // a few blades so it reads as grass from the game camera
                for (int i = 0; i < 5; i++)
                    Decor(PrimitiveType.Cube, new Vector3(sx * b[0] + (i - 2) * b[2] * 0.18f, 0.6f, b[1] + ((i % 2) - 0.5f) * b[3] * 0.5f), new Vector3(0.15f, 1f, 0.15f), bushMat);
            }
        Box($"Bush_{bushes++}", PrimitiveType.Cube, new Vector3(0f, 0.1f, -29.5f), new Vector3(10f, 0.2f, 3f), bushMat); // bottom lane centre
    }

    // Shadow Isles mood: dim blue light, glowing altars and lair, dead trees and glowing mushrooms on the walls and borders.
    static void BuildMood()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.2f, 0.27f, 0.34f);
        var sun = Object.FindFirstObjectByType<Light>();
        if (sun != null)
        {
            sun.color = new Color(0.55f, 0.72f, 0.95f);
            sun.intensity = 0.75f;
            sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        }
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
        }
        Glow(new Vector3(-27f, 3f, 1f), new Color(0.3f, 1f, 0.6f), 12f);
        Glow(new Vector3(27f, 3f, 1f), new Color(0.3f, 1f, 0.6f), 12f);
        Glow(new Vector3(0f, 4f, 30f), new Color(0.5f, 1f, 0.3f), 22f);
        Glow(new Vector3(0f, 3f, -14f), new Color(1f, 0.5f, 0.8f), 9f);
        Glow(new Vector3(-62f, 5f, 0f), new Color(0.3f, 0.5f, 1f), 26f);
        Glow(new Vector3(62f, 5f, 0f), new Color(0.75f, 0.3f, 0.9f), 26f);

        // Trees and mushrooms stand only on top of walls and the borders, so they never block a path.
        var rng = new System.Random(2013);
        for (float x = -76f; x <= 76f; x += 8f)
            foreach (float z in new[] { HalfD - 2f, -HalfD + 2f })
                Tree(new Vector3(x + (float)rng.NextDouble() * 3f, 2.4f, z), 0.8f + (float)rng.NextDouble() * 0.6f);
        foreach (var w in root.GetComponentsInChildren<Transform>())
        {
            if (!w.name.StartsWith("JungleWall")) continue;
            int roll = rng.Next(4);
            Vector3 top = new Vector3(w.position.x, 2.2f, w.position.z);
            if (roll == 0) Tree(top, 0.7f);
            else if (roll == 1) // glowing mushrooms: orange on the east side, green on the west, like the old map
                for (int i = 0; i < 3; i++)
                    Decor(PrimitiveType.Sphere, top + new Vector3(i * 0.7f - 0.7f, 0.15f, (i % 2) * 0.4f), Vector3.one * (0.45f + 0.15f * i), w.position.x > 0 ? glowOrange : glowGreen);
        }
    }

    static void Glow(Vector3 pos, Color c, float range)
    {
        var go = new GameObject("Glow");
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.range = range;
        l.intensity = 3f;
    }

    static void Tree(Vector3 basePos, float size)
    {
        Decor(PrimitiveType.Cylinder, basePos + Vector3.up * 1.5f * size, new Vector3(0.35f, 1.5f, 0.35f) * size, barkMat);
        Decor(PrimitiveType.Sphere, basePos + Vector3.up * 3.4f * size, new Vector3(2.4f, 1.8f, 2.4f) * size, leafMat);
    }

    // Decoration only: no collider.
    static void Decor(PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = Box("Decor", type, pos, scale, mat);
        Object.DestroyImmediate(go.GetComponent<Collider>());
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
        Box($"Tower_{team}_Nexus", PrimitiveType.Cylinder, new Vector3(side * 53f, 1.5f, 0), new Vector3(1.5f, 1.5f, 1.5f), mat); // the last turret, in front of the Nexus
        towers++;

        for (int i = 0; i < 2; i++)
        {
            // Lane points run blue -> red: the outer tower is on the third point from this team's end, the inner one near its base.
            var pts = lanePts[i];
            Vector3 outer = side < 0 ? pts[2] : pts[6];
            Vector3 inner = side < 0 ? (pts[0] + pts[1]) / 2f : (pts[8] + pts[7]) / 2f;
            Box($"Tower_{team}_{LaneName[i]}_Outer", PrimitiveType.Cylinder, new Vector3(outer.x, 1.5f, outer.z), new Vector3(1.5f, 1.5f, 1.5f), mat);
            Box($"Tower_{team}_{LaneName[i]}_Inner", PrimitiveType.Cylinder, new Vector3(inner.x, 1.5f, inner.z), new Vector3(1.5f, 1.5f, 1.5f), mat);
            towers += 2;
            // The inhibitor sits beside the lane's first point (not on it, so minions do not walk into it).
            Box($"Inhibitor_{team}_{LaneName[i]}", PrimitiveType.Sphere, new Vector3(side * 63f, 1.3f, i == 0 ? 21f : -25f), new Vector3(2.6f, 2.6f, 2.6f), mat);
            inhibitors++;
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

    static Material Mat(string name, Color color, bool glow = false)
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Data", "Greybox");
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color; // also refreshes materials left over from an earlier build
        if (glow)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 2f);
        }
        EditorUtility.SetDirty(m);
        return m;
    }
}

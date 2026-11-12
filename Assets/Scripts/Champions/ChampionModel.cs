using UnityEngine;

// Builds the body a champion is drawn with.
//   1. If Assets/Resources/Models/<Champion Name>.prefab exists, that prefab is used (feet at its origin, facing +Z, about 2 m tall).
//   2. Otherwise a placeholder figure is assembled from basic shapes: body in the champion's colour, with gear that shows
//      the role (tank: shield and sword, bruiser: axe, assassin: hood and daggers, marksman: bow and quiver,
//      mage: robe, hat and orb staff, support: robe, halo and ring staff).
public static class ChampionModel
{
    static readonly Color Skin = new Color(0.9f, 0.75f, 0.62f), Metal = new Color(0.75f, 0.78f, 0.84f), Wood = new Color(0.42f, 0.29f, 0.18f);

    // `parent` has its origin 1 m above the ground (the champion's centre), so the model hangs 1 m below it.
    public static GameObject Build(Transform parent, ChampionDef def)
    {
        var model = new GameObject("Model");
        model.transform.SetParent(parent, false);
        model.transform.localPosition = new Vector3(0f, -1f, 0f);
        var anim = model.AddComponent<ModelAnim>();

        var prefab = Resources.Load<GameObject>("Models/" + def.Name);
        if (prefab != null)
        {
            var inst = Object.Instantiate(prefab, model.transform, false);
            foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            anim.Animator = inst.GetComponentInChildren<Animator>();
            return model;
        }

        Color main = def.Color, dark = def.Color * 0.55f, glow = Icons.RoleColor(def.Role);
        dark.a = 1f;
        int hash = 0;
        foreach (char ch in def.Name) hash = (hash * 31 + ch) & 0xFFFF;
        model.transform.localScale = Vector3.one * (0.94f + hash % 5 * 0.035f); // champions differ a little in height
        var root = model.transform;
        var role = def.Role;
        bool robed = role == Role.Mage || role == Role.Support;
        float w = role == Role.Tank ? 0.9f : role == Role.Bruiser ? 0.8f : role == Role.Assassin ? 0.55f : 0.65f;

        // legs and arms hang from pivots at the hip and shoulder so they can swing
        anim.LegL = Pivot(root, new Vector3(-0.17f, 0.85f, 0f));
        anim.LegR = Pivot(root, new Vector3(0.17f, 0.85f, 0f));
        anim.ArmL = Pivot(root, new Vector3(-(w / 2f + 0.11f), 1.6f, 0f));
        anim.ArmR = Pivot(root, new Vector3(w / 2f + 0.11f, 1.6f, 0f));
        Part(anim.LegL, PrimitiveType.Cube, new Vector3(0f, -0.42f, 0f), new Vector3(0.24f, 0.85f, 0.26f), dark);
        Part(anim.LegR, PrimitiveType.Cube, new Vector3(0f, -0.42f, 0f), new Vector3(0.24f, 0.85f, 0.26f), dark);
        Part(anim.ArmL, PrimitiveType.Cube, new Vector3(0f, -0.36f, 0f), new Vector3(0.2f, 0.75f, 0.22f), dark);
        Part(anim.ArmR, PrimitiveType.Cube, new Vector3(0f, -0.36f, 0f), new Vector3(0.2f, 0.75f, 0.22f), dark);
        Part(root, PrimitiveType.Cube, new Vector3(0f, 1.25f, 0f), new Vector3(w, 0.8f, 0.42f), main);
        Part(root, PrimitiveType.Sphere, new Vector3(0f, 1.9f, 0f), Vector3.one * 0.44f, Skin);
        Part(root, PrimitiveType.Cube, new Vector3(0f, 1.92f, 0.2f), new Vector3(0.26f, 0.06f, 0.06f), Color.black); // eyes: shows which way it faces
        if (robed) Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 0f), new Vector3(0.78f, 0.5f, 0.78f), main);

        switch (role)
        {
            case Role.Tank:
                Part(anim.ArmL, PrimitiveType.Cube, new Vector3(0f, -0.45f, 0.24f), new Vector3(0.75f, 0.95f, 0.1f), Metal);          // shield
                Part(anim.ArmL, PrimitiveType.Cube, new Vector3(0f, -0.45f, 0.3f), new Vector3(0.3f, 0.4f, 0.04f), main);             // crest on it
                Part(anim.ArmR, PrimitiveType.Cube, new Vector3(0f, -0.72f, 0.4f), new Vector3(0.1f, 0.06f, 0.95f), Metal);           // sword
                Part(root, PrimitiveType.Sphere, new Vector3(-(w / 2f + 0.1f), 1.68f, 0f), Vector3.one * 0.38f, Metal);               // shoulder plates
                Part(root, PrimitiveType.Sphere, new Vector3(w / 2f + 0.1f, 1.68f, 0f), Vector3.one * 0.38f, Metal);
                Part(root, PrimitiveType.Sphere, new Vector3(0f, 2f, -0.02f), new Vector3(0.5f, 0.36f, 0.5f), Metal);                 // helmet
                break;
            case Role.Bruiser:
                Part(anim.ArmR, PrimitiveType.Cylinder, new Vector3(0f, -0.72f, 0.35f), new Vector3(0.07f, 0.6f, 0.07f), Wood, new Vector3(90f, 0f, 0f)); // axe handle
                Part(anim.ArmR, PrimitiveType.Cube, new Vector3(0f, -0.72f, 0.85f), new Vector3(0.08f, 0.6f, 0.42f), Metal);          // axe blade
                Part(root, PrimitiveType.Sphere, new Vector3(-(w / 2f + 0.1f), 1.68f, 0f), Vector3.one * 0.36f, Metal);               // one shoulder plate
                Part(root, PrimitiveType.Cube, new Vector3(0f, 0.92f, 0f), new Vector3(w + 0.04f, 0.14f, 0.46f), Wood);               // belt
                break;
            case Role.Assassin:
                Part(root, PrimitiveType.Sphere, new Vector3(0f, 1.94f, -0.05f), Vector3.one * 0.53f, dark);                          // hood
                Part(root, PrimitiveType.Cube, new Vector3(0f, 1.3f, -0.24f), new Vector3(w + 0.1f, 0.9f, 0.05f), dark);              // cloak
                Part(anim.ArmL, PrimitiveType.Cube, new Vector3(0f, -0.74f, 0.24f), new Vector3(0.06f, 0.06f, 0.5f), Metal);          // daggers
                Part(anim.ArmR, PrimitiveType.Cube, new Vector3(0f, -0.74f, 0.24f), new Vector3(0.06f, 0.06f, 0.5f), Metal);
                break;
            case Role.Marksman:
                Part(anim.ArmL, PrimitiveType.Cube, new Vector3(0f, -0.7f, 0.32f), new Vector3(0.06f, 1.3f, 0.08f), Wood);            // bow
                Part(anim.ArmL, PrimitiveType.Cube, new Vector3(0f, -0.7f, 0.22f), new Vector3(0.02f, 1.2f, 0.02f), Color.white);     // string
                Part(root, PrimitiveType.Cylinder, new Vector3(0.15f, 1.35f, -0.3f), new Vector3(0.15f, 0.36f, 0.15f), Wood, new Vector3(0f, 0f, -20f)); // quiver
                Part(root, PrimitiveType.Sphere, new Vector3(0f, 2.06f, 0f), new Vector3(0.48f, 0.2f, 0.48f), main);                  // cap
                break;
            case Role.Mage:
                Part(anim.ArmR, PrimitiveType.Cylinder, new Vector3(0f, -0.3f, 0.3f), new Vector3(0.06f, 0.95f, 0.06f), Wood);        // staff
                Part(anim.ArmR, PrimitiveType.Sphere, new Vector3(0f, 0.75f, 0.3f), Vector3.one * 0.3f, glow);                        // orb
                Part(root, PrimitiveType.Cylinder, new Vector3(0f, 2.04f, 0f), new Vector3(0.8f, 0.03f, 0.8f), main);                 // hat brim
                Part(root, PrimitiveType.Cylinder, new Vector3(0f, 2.28f, 0f), new Vector3(0.3f, 0.24f, 0.3f), main);                 // hat
                break;
            default: // Support
                Part(anim.ArmR, PrimitiveType.Cylinder, new Vector3(0f, -0.3f, 0.3f), new Vector3(0.06f, 0.95f, 0.06f), Wood);        // staff
                Part(anim.ArmR, PrimitiveType.Cylinder, new Vector3(0f, 0.8f, 0.3f), new Vector3(0.4f, 0.03f, 0.4f), glow, new Vector3(90f, 0f, 0f)); // ring
                Part(root, PrimitiveType.Cylinder, new Vector3(0f, 2.36f, 0f), new Vector3(0.5f, 0.02f, 0.5f), glow);                 // halo
                break;
        }
        return model;
    }

    static Transform Pivot(Transform parent, Vector3 pos)
    {
        var t = new GameObject("Pivot").transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        return t;
    }

    static void Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color color, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().material.color = color;
    }
}

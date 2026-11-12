using UnityEngine;

// Builds the body a champion is drawn with.
//   1. If Assets/Resources/Models/<Champion Name>.prefab exists, that prefab is used (feet at its origin, facing +Z, about 2 m tall).
//   2. Otherwise a placeholder figure is assembled from basic shapes. Every champion has its own gear, taken from its kit
//      (Kael's ember greatsword, Hrolf's anchor, Ilyra's storm cloud, Oren's lantern...). A champion without an entry
//      in Unique() falls back to plain gear for its role.
public static class ChampionModel
{
    const PrimitiveType Cube = PrimitiveType.Cube, Ball = PrimitiveType.Sphere, Cyl = PrimitiveType.Cylinder;
    static readonly Color Skin = new Color(0.9f, 0.75f, 0.62f), Metal = new Color(0.75f, 0.78f, 0.84f), Wood = new Color(0.42f, 0.29f, 0.18f);
    static readonly Color White = new Color(0.95f, 0.95f, 0.95f), Black = new Color(0.06f, 0.06f, 0.08f);

    // The figure being built (models are built one at a time, on the main thread).
    static Transform root, armL, armR;
    static Color main, dark, glow;
    static float w; // torso width

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

        root = model.transform;
        main = def.Color;
        dark = def.Color * 0.55f;
        dark.a = 1f;
        glow = Icons.RoleColor(def.Role);
        var role = def.Role;
        bool robed = role == Role.Mage || role == Role.Support;
        w = role == Role.Tank ? 0.9f : role == Role.Bruiser ? 0.8f : role == Role.Assassin ? 0.55f : 0.65f;
        float size = 1f;
        switch (def.Name) // build: a few champions are much bigger or smaller than their role
        {
            case "Grum Boulderfist": w = 1.15f; size = 1.1f; break;
            case "Orrin Ironwall": size = 1.06f; break;
            case "Hrolf Wavebreaker": w = 0.95f; size = 1.05f; break;
            case "Zephyra Quickblade": size = 0.95f; break;
            case "Pia Thistledown": size = 0.8f; break;
        }
        root.localScale = Vector3.one * size;

        // legs and arms hang from pivots at the hip and shoulder so they can swing
        anim.LegL = Pivot(new Vector3(-0.17f, 0.85f, 0f));
        anim.LegR = Pivot(new Vector3(0.17f, 0.85f, 0f));
        anim.ArmL = armL = Pivot(new Vector3(-(w / 2f + 0.11f), 1.6f, 0f));
        anim.ArmR = armR = Pivot(new Vector3(w / 2f + 0.11f, 1.6f, 0f));
        Part(anim.LegL, Cube, new Vector3(0f, -0.42f, 0f), new Vector3(0.24f, 0.85f, 0.26f), dark);
        Part(anim.LegR, Cube, new Vector3(0f, -0.42f, 0f), new Vector3(0.24f, 0.85f, 0.26f), dark);
        L(Cube, 0f, -0.36f, 0f, 0.2f, 0.75f, 0.22f, dark);
        A(Cube, 0f, -0.36f, 0f, 0.2f, 0.75f, 0.22f, dark);
        R(Cube, 0f, 1.25f, 0f, w, 0.8f, 0.42f, main);
        R(Ball, 0f, 1.9f, 0f, 0.44f, 0.44f, 0.44f, Skin);
        R(Cube, 0f, 1.92f, 0.2f, 0.26f, 0.06f, 0.06f, Black); // eyes: shows which way it faces
        if (robed) R(Cyl, 0f, 0.5f, 0f, 0.78f, 0.5f, 0.78f, main);

        if (!Unique(def.Name)) RoleGear(role);
        return model;
    }

    // ---- part helpers: (shape, position x y z, size x y z, colour, rotation x y z) ----
    // R = on the body, L = in the left hand/arm, A = in the right (attacking) arm. A hand is at about (0, -0.72, 0) of its arm.
    static void R(PrimitiveType t, float x, float y, float z, float sx, float sy, float sz, Color c, float rx = 0, float ry = 0, float rz = 0) =>
        Part(root, t, new Vector3(x, y, z), new Vector3(sx, sy, sz), c, new Vector3(rx, ry, rz));
    static void L(PrimitiveType t, float x, float y, float z, float sx, float sy, float sz, Color c, float rx = 0, float ry = 0, float rz = 0) =>
        Part(armL, t, new Vector3(x, y, z), new Vector3(sx, sy, sz), c, new Vector3(rx, ry, rz));
    static void A(PrimitiveType t, float x, float y, float z, float sx, float sy, float sz, Color c, float rx = 0, float ry = 0, float rz = 0) =>
        Part(armR, t, new Vector3(x, y, z), new Vector3(sx, sy, sz), c, new Vector3(rx, ry, rz));

    static void Shoulders(Color c, float s = 0.38f)
    {
        R(Ball, -(w / 2f + 0.1f), 1.68f, 0f, s, s, s, c);
        R(Ball, w / 2f + 0.1f, 1.68f, 0f, s, s, s, c);
    }
    static void Helmet(Color c) { R(Ball, 0f, 2f, -0.02f, 0.5f, 0.36f, 0.5f, c); }
    static void Horns(Color c, float len = 0.3f)
    {
        R(Cube, -0.24f, 2.12f, 0f, 0.08f, len, 0.08f, c, 0, 0, 25);
        R(Cube, 0.24f, 2.12f, 0f, 0.08f, len, 0.08f, c, 0, 0, -25);
    }
    static void Hood() { R(Ball, 0f, 1.94f, -0.05f, 0.53f, 0.53f, 0.53f, dark); }
    static void Cloak(Color c) { R(Cube, 0f, 1.25f, -0.25f, w + 0.12f, 1.05f, 0.05f, c); }
    static void Staff(Color c) { A(Cyl, 0f, -0.3f, 0.3f, 0.06f, 0.95f, 0.06f, c); } // its top is at about (0, 0.65, 0.3)
    static void Quiver() { R(Cyl, 0.15f, 1.35f, -0.3f, 0.15f, 0.36f, 0.15f, Wood, 0, 0, -20); }
    static void Bow(float length, Color stringColor)
    {
        L(Cube, 0f, -0.7f, 0.32f, 0.06f, length, 0.08f, Wood);
        L(Cube, 0f, -0.7f, 0.22f, 0.02f, length - 0.1f, 0.02f, stringColor);
    }

    // Gear for one specific champion, drawn from its abilities and passive. Returns false if there is no entry.
    static bool Unique(string name)
    {
        switch (name)
        {
            // ---------------- tanks ----------------
            case "Orrin Ironwall": // Shield Bash, Stone Skin: a wall of a man with a tower shield and a mace
                L(Cube, 0f, -0.45f, 0.26f, 0.9f, 1.25f, 0.12f, Metal);
                L(Ball, 0f, -0.45f, 0.34f, 0.28f, 0.28f, 0.12f, main);
                A(Cyl, 0f, -0.72f, 0.32f, 0.07f, 0.4f, 0.07f, Wood, 90);
                A(Ball, 0f, -0.72f, 0.78f, 0.32f, 0.32f, 0.32f, Metal);
                Shoulders(Metal); Helmet(Metal); Horns(White);
                break;
            case "Grum Boulderfist": // Boulder Toss, Spiked Hide: living rock with boulder fists and spikes on his back
            {
                var rock = new Color(0.5f, 0.45f, 0.4f); var pale = new Color(0.72f, 0.68f, 0.6f);
                L(Ball, 0f, -0.8f, 0.05f, 0.55f, 0.55f, 0.55f, rock);
                A(Ball, 0f, -0.8f, 0.05f, 0.55f, 0.55f, 0.55f, rock);
                Shoulders(rock, 0.5f);
                for (int i = -1; i <= 1; i++)
                {
                    R(Cube, i * 0.3f, 1.6f, -0.3f, 0.12f, 0.42f, 0.12f, pale, -40);
                    R(Cube, i * 0.3f, 1.2f, -0.3f, 0.1f, 0.34f, 0.1f, pale, -55);
                }
                R(Cube, 0f, 2.12f, 0f, 0.3f, 0.12f, 0.3f, rock); // flat rocky brow
                break;
            }
            case "Seren Wardenheart": // Shield Throw, Guardian Light: round shining shield, spear and cape
            {
                var light = new Color(0.65f, 0.9f, 1f);
                L(Cyl, 0f, -0.45f, 0.26f, 0.95f, 0.05f, 0.95f, Metal, 90);
                L(Cyl, 0f, -0.45f, 0.3f, 0.42f, 0.05f, 0.42f, light, 90);
                A(Cyl, 0f, -0.45f, 0.3f, 0.05f, 1.15f, 0.05f, Wood);
                A(Cube, 0f, 0.85f, 0.3f, 0.12f, 0.34f, 0.04f, Metal);
                Cloak(main * 0.8f); Shoulders(Metal, 0.32f);
                R(Cyl, 0f, 2.08f, 0f, 0.47f, 0.03f, 0.47f, light); // circlet
                break;
            }
            case "Brannoch Stormshield": // Thunder Hook, Static Shield: shield marked with a lightning bolt and a hook on a chain
            {
                var bolt = new Color(1f, 0.92f, 0.3f); var spark = new Color(0.5f, 0.75f, 1f);
                L(Cube, 0f, -0.45f, 0.26f, 0.72f, 0.92f, 0.1f, Metal);
                L(Cube, 0.06f, -0.28f, 0.33f, 0.1f, 0.34f, 0.03f, bolt, 0, 0, 30);
                L(Cube, -0.06f, -0.58f, 0.33f, 0.1f, 0.34f, 0.03f, bolt, 0, 0, 30);
                A(Cyl, 0f, -0.72f, 0.5f, 0.04f, 0.4f, 0.04f, Metal, 90);          // chain
                A(Cube, 0f, -0.72f, 0.95f, 0.07f, 0.34f, 0.07f, spark);           // hook
                A(Cube, 0f, -0.58f, 1.06f, 0.07f, 0.07f, 0.26f, spark);
                Helmet(Metal); Shoulders(Metal);
                R(Cube, 0f, 2.22f, -0.02f, 0.05f, 0.2f, 0.42f, spark);            // crest
                break;
            }
            // ---------------- bruisers ----------------
            case "Kael Ashblade": // Cleaving Slash: a glowing ember greatsword and an ash-red scarf
            {
                var ember = new Color(1f, 0.45f, 0.1f);
                A(Cube, 0f, -0.72f, 1f, 0.16f, 0.06f, 1.5f, ember);
                A(Cube, 0f, -0.72f, 1f, 0.05f, 0.08f, 1.5f, new Color(1f, 0.85f, 0.4f)); // hot core
                A(Cube, 0f, -0.72f, 0.22f, 0.42f, 0.09f, 0.09f, Black);
                R(Cube, 0f, 1.68f, 0f, w * 0.85f, 0.13f, 0.5f, new Color(0.6f, 0.12f, 0.08f));
                R(Ball, -(w / 2f + 0.1f), 1.68f, 0f, 0.36f, 0.36f, 0.36f, Metal);
                break;
            }
            case "Dara Bloodmoon": // Crimson Slash, Bloodthirst: twin crescent blades and a blood moon at her back
            {
                var blood = new Color(0.85f, 0.1f, 0.2f);
                L(Cube, 0f, -0.72f, 0.36f, 0.05f, 0.3f, 0.62f, blood, 20);
                A(Cube, 0f, -0.72f, 0.36f, 0.05f, 0.3f, 0.62f, blood, 20);
                R(Cyl, 0f, 1.55f, -0.32f, 0.95f, 0.03f, 0.95f, new Color(1f, 0.35f, 0.4f), 90);
                R(Ball, 0f, 1.84f, -0.14f, 0.5f, 0.62f, 0.42f, new Color(0.35f, 0.05f, 0.1f)); // long dark hair
                break;
            }
            case "Hrolf Wavebreaker": // Anchor Drop, Tidal Cleave: a sea raider swinging a ship's anchor
            {
                A(Cyl, 0f, -0.72f, 0.65f, 0.09f, 0.7f, 0.09f, Metal, 90);
                A(Cube, 0f, -0.72f, 1.35f, 0.75f, 0.11f, 0.11f, Metal);
                A(Cube, -0.37f, -0.72f, 1.2f, 0.11f, 0.11f, 0.32f, Metal);
                A(Cube, 0.37f, -0.72f, 1.2f, 0.11f, 0.11f, 0.32f, Metal);
                R(Cube, 0f, 1.72f, 0.18f, 0.36f, 0.32f, 0.12f, new Color(0.75f, 0.45f, 0.2f)); // beard
                Helmet(Metal); Horns(White, 0.36f);
                R(Cube, 0f, 0.92f, 0f, w + 0.04f, 0.14f, 0.46f, Wood);
                break;
            }
            // ---------------- assassins ----------------
            case "Vex Nightshade": // Shadow Veil: hooded, glowing eyes, one long dark blade, shadows curling at his feet
            {
                var violet = new Color(0.8f, 0.3f, 1f); var shade = new Color(0.12f, 0.05f, 0.18f);
                Hood(); Cloak(dark);
                R(Cube, 0f, 1.92f, 0.22f, 0.28f, 0.05f, 0.04f, violet);
                A(Cube, 0f, -0.72f, 0.65f, 0.05f, 0.09f, 1.15f, shade);
                R(Ball, -0.35f, 0.18f, 0.1f, 0.34f, 0.3f, 0.34f, shade);
                R(Ball, 0.3f, 0.15f, -0.2f, 0.3f, 0.26f, 0.3f, shade);
                R(Ball, 0.05f, 0.12f, 0.35f, 0.24f, 0.2f, 0.24f, shade);
                break;
            }
            case "Riku Silentstep": // Throwing Knives, Smoke Bomb: masked, red headband, a strap of knives, bombs on his belt
            {
                var band = new Color(0.85f, 0.15f, 0.15f); var smoke = new Color(0.5f, 0.5f, 0.55f);
                R(Cyl, 0f, 1.99f, 0f, 0.47f, 0.04f, 0.47f, band);
                R(Cube, -0.08f, 1.9f, -0.32f, 0.06f, 0.3f, 0.04f, band, 20);
                R(Cube, 0.08f, 1.86f, -0.32f, 0.06f, 0.3f, 0.04f, band, 30);
                R(Cube, 0f, 1.81f, 0.19f, 0.3f, 0.14f, 0.06f, Black);                       // mask
                R(Cube, 0f, 1.25f, 0.22f, 0.12f, 0.95f, 0.04f, Wood, 0, 0, 35);             // strap
                for (int i = -1; i <= 1; i++) R(Cube, i * 0.14f, 1.25f + i * 0.2f, 0.26f, 0.05f, 0.2f, 0.03f, Metal, 0, 0, 35);
                R(Ball, -0.2f, 0.9f, 0.22f, 0.17f, 0.17f, 0.17f, smoke);
                R(Ball, 0.2f, 0.9f, 0.22f, 0.17f, 0.17f, 0.17f, smoke);
                L(Cube, 0f, -0.74f, 0.2f, 0.05f, 0.05f, 0.36f, Metal);
                A(Cube, 0f, -0.74f, 0.2f, 0.05f, 0.05f, 0.36f, Metal);
                break;
            }
            case "Zephyra Quickblade": // Gale Slash, Swift Feet: long ponytail, two thin blades, a ring of wind around her
            {
                var wind = new Color(0.75f, 1f, 0.95f);
                R(Cube, 0f, 1.98f, -0.32f, 0.13f, 0.13f, 0.5f, White, -15);
                R(Cube, 0f, 1.82f, -0.66f, 0.1f, 0.1f, 0.42f, White, -35);
                L(Cube, 0f, -0.74f, 0.4f, 0.04f, 0.05f, 0.85f, White);
                A(Cube, 0f, -0.74f, 0.4f, 0.04f, 0.05f, 0.85f, White);
                R(Cyl, 0f, 1.05f, 0f, 1.15f, 0.012f, 1.15f, wind);
                R(Cyl, 0f, 0.45f, 0f, 0.8f, 0.012f, 0.8f, wind);
                R(Cube, 0f, 1.68f, -0.05f, w * 0.9f, 0.12f, 0.5f, wind); // scarf
                break;
            }
            // ---------------- marksmen ----------------
            case "Sera Longshot": // Piercing Arrow, Eagle Strike: a very long bow, a feathered cap and an eagle on her shoulder
            {
                var eagle = new Color(0.45f, 0.32f, 0.2f);
                Bow(1.8f, White); Quiver();
                R(Ball, 0f, 2.06f, 0f, 0.48f, 0.2f, 0.48f, main);
                R(Cube, 0.2f, 2.24f, -0.05f, 0.04f, 0.38f, 0.1f, White, 0, 0, -20);
                R(Ball, w / 2f + 0.12f, 1.86f, 0f, 0.24f, 0.26f, 0.3f, eagle);
                R(Cube, w / 2f + 0.12f, 1.88f, 0.17f, 0.06f, 0.06f, 0.1f, new Color(1f, 0.8f, 0.2f));
                break;
            }
            case "Cassian Redhawk": // Rapid Reload, Net Shot: a crossbow, a red half-cape and hawk feathers
            {
                var hawk = new Color(0.85f, 0.2f, 0.15f);
                A(Cube, 0f, -0.72f, 0.5f, 0.1f, 0.1f, 0.85f, Wood);
                A(Cube, 0f, -0.72f, 0.88f, 0.85f, 0.06f, 0.06f, Metal);
                R(Cube, -0.2f, 1.3f, -0.26f, 0.5f, 0.85f, 0.05f, hawk, 0, 0, 10);
                for (int i = 0; i < 3; i++) R(Cube, -(w / 2f + 0.12f), 1.74f + i * 0.02f, -0.12f + i * 0.12f, 0.05f, 0.3f, 0.1f, hawk, 0, 0, 30 + i * 12);
                R(Ball, -0.3f, 0.95f, 0.2f, 0.22f, 0.22f, 0.22f, Wood); // net pouch
                R(Ball, 0f, 2.06f, 0f, 0.48f, 0.2f, 0.48f, hawk);
                break;
            }
            case "Marek Emberstring": // Ember Shot, Dragon's Breath: a bow strung with fire, a burning arrow and dragon horns
            {
                var ember = new Color(1f, 0.55f, 0.12f);
                Bow(1.35f, ember); Quiver();
                L(Ball, 0f, -0.7f, 0.55f, 0.2f, 0.2f, 0.2f, ember);
                L(Ball, 0f, -0.7f, 0.55f, 0.1f, 0.1f, 0.3f, new Color(1f, 0.9f, 0.4f));
                R(Ball, 0.15f, 1.72f, -0.3f, 0.16f, 0.16f, 0.16f, ember); // glowing arrow tips
                Horns(new Color(0.5f, 0.15f, 0.1f), 0.26f);
                Shoulders(new Color(0.5f, 0.15f, 0.1f), 0.3f);            // scale plates
                break;
            }
            // ---------------- mages ----------------
            case "Ilyra Stormcaller": // Thunderstorm: a storm cloud over her head and a lightning-bolt staff
            {
                var bolt = new Color(1f, 0.95f, 0.35f); var cloud = new Color(0.5f, 0.55f, 0.65f);
                Staff(Metal);
                A(Cube, 0.05f, 0.82f, 0.3f, 0.1f, 0.3f, 0.05f, bolt, 0, 0, 30);
                A(Cube, -0.05f, 1.06f, 0.3f, 0.1f, 0.3f, 0.05f, bolt, 0, 0, 30);
                R(Ball, 0f, 2.75f, 0f, 0.6f, 0.34f, 0.5f, cloud);
                R(Ball, -0.32f, 2.68f, 0.05f, 0.42f, 0.28f, 0.4f, cloud);
                R(Ball, 0.32f, 2.7f, -0.05f, 0.44f, 0.3f, 0.4f, cloud);
                R(Cube, 0.1f, 2.45f, 0f, 0.05f, 0.2f, 0.05f, bolt, 0, 0, 25); // a small bolt under the cloud
                break;
            }
            case "Nyx Emberweaver": // Fireball, Inferno: hair of flame and a staff topped with a fireball
            {
                var fire = new Color(1f, 0.45f, 0.1f); var hot = new Color(1f, 0.85f, 0.3f);
                Staff(Black);
                A(Ball, 0f, 0.85f, 0.3f, 0.42f, 0.42f, 0.42f, fire);
                A(Ball, 0f, 0.98f, 0.3f, 0.24f, 0.34f, 0.24f, hot);
                R(Ball, 0f, 2.14f, -0.04f, 0.46f, 0.36f, 0.46f, fire);
                R(Ball, 0f, 2.36f, -0.06f, 0.32f, 0.36f, 0.32f, fire);
                R(Ball, 0f, 2.56f, -0.08f, 0.18f, 0.3f, 0.18f, hot);
                break;
            }
            case "Quillon Frostweaver": // Ice Lance, Absolute Zero: an ice-crystal staff, a crown of icicles, frosted shoulders
            {
                var ice = new Color(0.7f, 0.92f, 1f);
                Staff(White);
                A(Cube, 0f, 0.9f, 0.3f, 0.24f, 0.24f, 0.24f, ice, 45, 0, 45);
                A(Cube, 0f, 1.2f, 0.3f, 0.1f, 0.3f, 0.1f, ice);
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 72f * Mathf.Deg2Rad;
                    R(Cube, Mathf.Sin(a) * 0.2f, 2.16f, Mathf.Cos(a) * 0.2f, 0.07f, 0.26f, 0.07f, ice);
                }
                R(Cube, -(w / 2f + 0.12f), 1.74f, 0f, 0.1f, 0.34f, 0.1f, ice, 0, 0, 35);
                R(Cube, w / 2f + 0.12f, 1.74f, 0f, 0.1f, 0.34f, 0.1f, ice, 0, 0, -35);
                break;
            }
            case "Vashti Voidcaller": // Singularity, Rift: no staff, a black orb with a ring floats before her; high collar and horns
            {
                var violet = new Color(0.7f, 0.3f, 1f);
                R(Ball, 0f, 1.3f, 0.6f, 0.4f, 0.4f, 0.4f, Black);
                R(Cyl, 0f, 1.3f, 0.6f, 0.66f, 0.01f, 0.66f, violet, 70);
                R(Cube, 0f, 1.98f, -0.24f, 0.75f, 0.62f, 0.05f, dark);
                Horns(violet, 0.34f);
                R(Cube, 0f, 1.92f, 0.22f, 0.28f, 0.05f, 0.04f, violet); // glowing eyes
                break;
            }
            // ---------------- supports ----------------
            case "Mirel Dawnwhisper": // Radiant Dawn, Mending Light: white wings, a halo and a sun-disc staff
            {
                var gold = new Color(1f, 0.85f, 0.35f);
                Staff(White);
                A(Cyl, 0f, 0.85f, 0.3f, 0.5f, 0.03f, 0.5f, gold, 90);
                R(Cyl, 0f, 2.36f, 0f, 0.5f, 0.02f, 0.5f, gold);
                R(Cube, -0.5f, 1.5f, -0.32f, 0.7f, 0.95f, 0.04f, White, 0, 25, 15);
                R(Cube, 0.5f, 1.5f, -0.32f, 0.7f, 0.95f, 0.04f, White, 0, -25, -15);
                break;
            }
            case "Oren Lanternkeeper": // Lantern Toss, Guiding Light: a hooded traveller with a pack and a lantern hanging from his staff
            {
                var lamp = new Color(1f, 0.85f, 0.35f);
                Hood(); Staff(Wood);
                A(Cube, 0f, 0.63f, 0.47f, 0.05f, 0.05f, 0.36f, Wood);       // crook
                A(Cube, 0f, 0.36f, 0.62f, 0.22f, 0.3f, 0.22f, lamp);        // lantern
                A(Cube, 0f, 0.53f, 0.62f, 0.26f, 0.05f, 0.26f, Black);
                R(Cube, 0f, 1.3f, -0.34f, 0.52f, 0.62f, 0.26f, Wood);       // pack
                R(Cyl, 0f, 1.68f, -0.34f, 0.2f, 0.28f, 0.2f, main, 0, 0, 90); // bedroll
                break;
            }
            case "Pia Thistledown": // Regrowth, Entangle: small, crowned with flowers, a leafy vine staff topped with a thistle
            {
                var leaf = new Color(0.3f, 0.75f, 0.3f); var petal = new Color(1f, 0.55f, 0.8f); var thistle = new Color(0.65f, 0.35f, 0.85f);
                Staff(leaf);
                A(Ball, 0f, 0.82f, 0.3f, 0.3f, 0.3f, 0.3f, thistle);
                A(Cube, 0.1f, 0.3f, 0.3f, 0.2f, 0.04f, 0.1f, leaf, 0, 0, 30);
                A(Cube, -0.1f, 0f, 0.3f, 0.2f, 0.04f, 0.1f, leaf, 0, 0, -30);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 60f * Mathf.Deg2Rad;
                    R(Ball, Mathf.Sin(a) * 0.22f, 2.08f, Mathf.Cos(a) * 0.22f, 0.13f, 0.13f, 0.13f, i % 2 == 0 ? petal : White);
                }
                R(Cube, 0f, 1.3f, -0.25f, w + 0.3f, 0.9f, 0.04f, leaf); // leaf cape
                break;
            }
            default: return false;
        }
        return true;
    }

    // Plain gear for a role, used by any champion without its own entry above.
    static void RoleGear(Role role)
    {
        switch (role)
        {
            case Role.Tank:
                L(Cube, 0f, -0.45f, 0.24f, 0.75f, 0.95f, 0.1f, Metal);
                A(Cube, 0f, -0.72f, 0.4f, 0.1f, 0.06f, 0.95f, Metal);
                Shoulders(Metal); Helmet(Metal);
                break;
            case Role.Bruiser:
                A(Cyl, 0f, -0.72f, 0.35f, 0.07f, 0.6f, 0.07f, Wood, 90);
                A(Cube, 0f, -0.72f, 0.85f, 0.08f, 0.6f, 0.42f, Metal);
                break;
            case Role.Assassin:
                Hood(); Cloak(dark);
                L(Cube, 0f, -0.74f, 0.24f, 0.06f, 0.06f, 0.5f, Metal);
                A(Cube, 0f, -0.74f, 0.24f, 0.06f, 0.06f, 0.5f, Metal);
                break;
            case Role.Marksman:
                Bow(1.3f, White); Quiver();
                break;
            case Role.Mage:
                Staff(Wood);
                A(Ball, 0f, 0.75f, 0.3f, 0.3f, 0.3f, 0.3f, glow);
                break;
            default:
                Staff(Wood);
                R(Cyl, 0f, 2.36f, 0f, 0.5f, 0.02f, 0.5f, glow);
                break;
        }
    }

    static Transform Pivot(Vector3 pos)
    {
        var t = new GameObject("Pivot").transform;
        t.SetParent(root, false);
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
        color.a = 1f;
        go.GetComponent<Renderer>().material.color = color;
    }
}

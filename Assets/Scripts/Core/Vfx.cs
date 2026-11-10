using System.Collections.Generic;
using UnityEngine;

// Runtime particle effects, no assets needed. Every ability style is its own recipe built from a few layers:
// glow flashes, stretched sparks, expanding shockwaves, rising motes, projectiles and lightning lines.
// Auto attacks stay small and pale so abilities always read as the bigger event.
public static class Vfx
{
    // ---------- one particle layer ----------
    class Spec
    {
        public Vector3 Pos;
        public Quaternion Rot = Quaternion.identity;
        public Transform Parent;
        public ParticleSystemShapeType Shape = ParticleSystemShapeType.Sphere;
        public float ShapeRadius = 0.1f, Angle = 25f;
        public bool Edge, Flat;                 // Edge = emit from the rim only; Flat = lay a circle on the ground
        public int Count = 10;                  // one burst at the start
        public float RatePerSec, RatePerDistance, Duration = 0.1f; // continuous emission for Duration seconds
        public float SizeMin = 0.15f, SizeMax = 0.3f, EndScale = 0.3f;
        public float SpeedMin, SpeedMax, LifeMin = 0.3f, LifeMax = 0.5f, Gravity, Rise;
        public Color Color = Color.white, Color2 = Color.white;
        public bool Stretch, Additive = true;
    }

    static readonly Quaternion UpRot = Quaternion.Euler(-90f, 0f, 0f); // shape axis pointing up
    static Material addMat, alphaMat;
    static Texture2D soft;

    static Vector3 At(Vector3 p, float y) => new Vector3(p.x, y, p.z);
    static Color Hot(Color c) => Color.Lerp(c, Color.white, 0.55f);

    // Soft round dot so particles glow instead of showing as hard squares.
    static Texture2D Soft()
    {
        if (soft != null) return soft;
        const int n = 32;
        var px = new Color32[n * n];
        float mid = (n - 1) / 2f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(mid, mid)) / mid;
                float a = Mathf.Clamp01(1f - d);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
        soft = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        soft.SetPixels32(px);
        soft.Apply();
        return soft;
    }

    // Additive shader when available (glow), otherwise plain alpha blending. Sprites/Default is always in builds.
    static Material MatFor(bool additive)
    {
        if (additive && addMat != null) return addMat;
        if (!additive && alphaMat != null) return alphaMat;
        var shader = (additive ? Shader.Find("Legacy Shaders/Particles/Additive") : null) ?? Shader.Find("Sprites/Default");
        var m = new Material(shader) { mainTexture = Soft() };
        if (additive) addMat = m; else alphaMat = m;
        return m;
    }

    static void Make(Spec s)
    {
        var go = new GameObject("Vfx");
        if (s.Parent != null)
        {
            go.transform.SetParent(s.Parent, false);
            go.transform.localPosition = s.Pos;
            go.transform.localRotation = s.Rot;
        }
        else
        {
            go.transform.position = s.Pos;
            go.transform.rotation = s.Rot;
        }
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = Mathf.Max(0.05f, s.Duration);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(s.LifeMin, s.LifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(s.SpeedMin, s.SpeedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(s.SizeMin, s.SizeMax);
        main.startColor = new ParticleSystem.MinMaxGradient(s.Color, s.Color2);
        main.gravityModifier = s.Gravity;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = s.RatePerSec;
        em.rateOverDistance = s.RatePerDistance;
        if (s.Count > 0) em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)s.Count) });

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = s.Shape;
        sh.radius = s.ShapeRadius;
        sh.angle = s.Angle;
        if (s.Edge) sh.radiusThickness = 0f;
        if (s.Flat) sh.rotation = new Vector3(90f, 0f, 0f);

        if (s.Rise != 0f)
        {
            var v = ps.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.World;
            v.x = 0f; v.y = s.Rise; v.z = 0f;
        }

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, s.EndScale));

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = MatFor(s.Additive);
        if (s.Stretch)
        {
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 2.5f;
            r.velocityScale = 0.12f;
        }

        ps.Play();
        Object.Destroy(go, s.Duration + s.LifeMax + 0.3f);
    }

    // ---------- reusable pieces ----------
    static void Glow(Vector3 pos, Color c, float size, float life)
    {
        Make(new Spec { Pos = pos, Count = 1, SizeMin = size, SizeMax = size, EndScale = 1.8f, LifeMin = life, LifeMax = life, Color = Hot(c), Color2 = Hot(c) });
    }

    static void Sparks(Vector3 pos, Quaternion rot, ParticleSystemShapeType shape, float angle, int count,
        float speedMin, float speedMax, float life, Color c, float size, float gravity = 0f)
    {
        Make(new Spec
        {
            Pos = pos, Rot = rot, Shape = shape, Angle = angle, Count = count, SpeedMin = speedMin, SpeedMax = speedMax,
            LifeMin = life * 0.7f, LifeMax = life, SizeMin = size * 0.6f, SizeMax = size, Gravity = gravity,
            Stretch = true, Color = Hot(c), Color2 = c, EndScale = 0.2f
        });
    }

    // Ring that expands outward along the ground and fades.
    static void Shockwave(Vector3 ground, float radius, Color c, float life, float size)
    {
        float speed = radius / life;
        Make(new Spec
        {
            Pos = ground, Shape = ParticleSystemShapeType.Circle, ShapeRadius = 0.3f, Edge = true, Flat = true,
            Count = Mathf.Max(24, (int)(radius * 14f)), SpeedMin = speed, SpeedMax = speed, LifeMin = life, LifeMax = life,
            SizeMin = size, SizeMax = size * 1.3f, EndScale = 0.2f, Color = c, Color2 = Hot(c)
        });
    }

    // Motes rising off the ground in a ring: heals, shields, auras.
    static void Rising(Vector3 ground, float radius, Color c, int count, float rise)
    {
        Make(new Spec
        {
            Pos = ground, Shape = ParticleSystemShapeType.Circle, ShapeRadius = radius, Edge = true, Flat = true, Count = count,
            Rise = rise, LifeMin = 0.7f, LifeMax = 1.1f, SizeMin = 0.15f, SizeMax = 0.27f, Color = c, Color2 = Hot(c), EndScale = 0.2f
        });
    }

    static void Line(Vector3 a, Vector3 b, Color c, float width, float seconds)
    {
        var runner = VfxRunner.Spawn("VfxLine", a);
        var lr = runner.gameObject.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.startWidth = lr.endWidth = width;
        lr.numCapVertices = 4;
        lr.sharedMaterial = MatFor(true);
        lr.startColor = lr.endColor = c;
        runner.FadeLine(lr, seconds);
        Object.Destroy(runner.gameObject, seconds + 0.1f);
    }

    static void Lightning(Vector3 from, Vector3 to, Color c, float width)
    {
        var runner = VfxRunner.Spawn("VfxBolt", from);
        var lr = runner.gameObject.AddComponent<LineRenderer>();
        const int n = 12;
        lr.useWorldSpace = true;
        lr.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            Vector3 p = Vector3.Lerp(from, to, i / (float)(n - 1));
            if (i > 0 && i < n - 1) p += new Vector3(Random.Range(-0.9f, 0.9f), 0f, Random.Range(-0.9f, 0.9f));
            lr.SetPosition(i, p);
        }
        lr.startWidth = lr.endWidth = width;
        lr.sharedMaterial = MatFor(true);
        lr.startColor = lr.endColor = Hot(c);
        runner.FadeLine(lr, 0.2f);
        Object.Destroy(runner.gameObject, 0.3f);
    }

    // A glowing head with a trail that stays in the world behind it.
    static void Projectile(Vector3 from, Vector3 dir, float length, float speed, Color c, float headSize, float trailRate)
    {
        float t = length / speed;
        var r = VfxRunner.Spawn("VfxProjectile", from);
        r.Fly(dir * speed, t);
        Object.Destroy(r.gameObject, t + 1f);
        Make(new Spec
        {
            Parent = r.transform, Count = 0, RatePerSec = 60f, Duration = t, ShapeRadius = 0.02f, SizeMin = headSize, SizeMax = headSize,
            LifeMin = 0.06f, LifeMax = 0.06f, EndScale = 1f, Color = Hot(c), Color2 = Hot(c)
        });
        Make(new Spec
        {
            Parent = r.transform, Count = 0, RatePerDistance = trailRate, Duration = t, ShapeRadius = 0.05f, SizeMin = headSize * 0.6f,
            SizeMax = headSize, LifeMin = 0.25f, LifeMax = 0.4f, EndScale = 0.1f, Color = c, Color2 = Hot(c)
        });
    }

    // ---------- public API ----------
    public static Color ColorOf(AbilityDef a)
    {
        foreach (var e in a.Effects)
        {
            switch (e.Type)
            {
                case EffectType.Damage:
                    return e.DType == DamageType.Physical ? new Color(1f, 0.55f, 0.15f)
                         : e.DType == DamageType.Magic ? new Color(0.7f, 0.35f, 1f) : Color.white;
                case EffectType.Heal: return new Color(0.3f, 1f, 0.4f);
                case EffectType.Shield: return new Color(0.4f, 0.9f, 1f);
                case EffectType.Stun:
                case EffectType.Root:
                case EffectType.Silence:
                case EffectType.Slow: return new Color(1f, 0.9f, 0.3f);
                case EffectType.Buff: return new Color(0.5f, 0.7f, 1f);
            }
        }
        return Color.white; // pure movement abilities
    }

    // Used by recall: a flat ring that stays put.
    // Effects are only drawn where your team has vision.
    static bool Visible(Vector3 p)
    {
        var me = Champion.Local;
        return me == null || Vision.SeesPoint(me.U.Team, p);
    }

    public static void Ring(Vector3 center, float radius, Color c)
    {
        if (Net.IsHost) Net.FxRing(center, radius, c);
        if (!Visible(center)) return;
        Make(new Spec
        {
            Pos = At(center, 0.15f), Shape = ParticleSystemShapeType.Circle, ShapeRadius = radius, Edge = true, Flat = true,
            Count = Mathf.Max(16, (int)(radius * 10f)), LifeMin = 0.7f, LifeMax = 0.7f, SizeMin = 0.3f, SizeMax = 0.3f, EndScale = 0.3f, Color = c, Color2 = Hot(c)
        });
    }

    // Big jungle monster stomp: effect for everyone, camera shake only if you are close.
    public static void MonsterSlam(Vector3 center, float radius, Color c, float power)
    {
        if (Net.IsHost) Net.FxSlam(center, radius, c, power);
        if (!Visible(center)) return;
        Slam(center, radius, c, power);
        var me = Champion.Local;
        if (me != null && Unit.FlatDist(me.transform.position, center) < radius * 2.5f) CameraFollow.Shake(0.25f);
    }

    public static Color TeamColor(Team t) =>
        t == Team.Blue ? new Color(0.35f, 0.6f, 1f) : t == Team.Red ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.8f, 0.3f);

    // Tower bolt: a big glowing orb that flies to the target, then damages it and bursts on arrival.
    public static void TowerShot(Unit tower, Unit target, float damage)
    {
        if (Net.IsHost) Net.FxTower(tower, target);
        Color c = TeamColor(tower.Team);
        Vector3 from = At(tower.transform.position, 3.4f);
        bool show = Visible(tower.transform.position) || Visible(target.transform.position); // the damage lands either way
        if (show) Glow(from, c, 2.2f, 0.2f); // muzzle flash
        var r = VfxRunner.Spawn("VfxTowerShot", from);
        if (show) Make(new Spec { Parent = r.transform, Count = 0, RatePerSec = 80f, Duration = 3f, ShapeRadius = 0.02f, SizeMin = 0.8f, SizeMax = 0.8f, LifeMin = 0.07f, LifeMax = 0.07f, EndScale = 1f, Color = c, Color2 = Hot(c) });
        if (show) Make(new Spec { Parent = r.transform, Count = 0, RatePerSec = 80f, Duration = 3f, ShapeRadius = 0.02f, SizeMin = 0.35f, SizeMax = 0.35f, LifeMin = 0.07f, LifeMax = 0.07f, EndScale = 1f, Color = Color.white, Color2 = Color.white });
        if (show) Make(new Spec { Parent = r.transform, Count = 0, RatePerDistance = 20f, Duration = 3f, ShapeRadius = 0.1f, SizeMin = 0.5f, SizeMax = 0.8f, LifeMin = 0.35f, LifeMax = 0.5f, EndScale = 0.1f, Color = c, Color2 = Hot(c) });
        r.Homing(target, 22f, Vector3.up, () =>
        {
            if (!Net.IsClientOnly) target.TakeDamage(damage, DamageType.Physical, tower); // clients only show the bolt
            if (!show) return;
            Vector3 body = At(target.transform.position, 1f);
            Glow(body, c, 2.4f, 0.2f);
            Sparks(body, Quaternion.identity, ParticleSystemShapeType.Sphere, 0f, 16, 5f, 11f, 0.3f, c, 0.14f);
            Shockwave(At(target.transform.position, 0.15f), 2f, c, 0.3f, 0.3f);
            var me = Champion.Local;
            if (me != null && target == me.U) CameraFollow.Shake(0.2f); // only shake for hits on your own champion
        });
    }

    // Auto attack: melee = a short slash, ranged = a small bolt. Deliberately small and pale next to abilities.
    public static void Auto(Unit caster, Color accent, bool melee, Unit target)
    {
        if (Net.IsHost) Net.FxAuto(caster, accent, melee, target);
        if (!Visible(caster.transform.position) && !Visible(target.transform.position)) return;
        Vector3 from = At(caster.transform.position, 1f), to = At(target.transform.position, 1f);
        Vector3 dir = (to - from).normalized;
        var pale = new Color(1f, 0.95f, 0.75f);
        if (melee) Sparks(from + dir * 0.8f, Quaternion.LookRotation(dir), ParticleSystemShapeType.Cone, 35f, 8, 8f, 12f, 0.18f, Color.Lerp(pale, accent, 0.35f), 0.07f);
        else Projectile(from, dir, Vector3.Distance(from, to), 45f, Color.Lerp(pale, accent, 0.5f), 0.22f, 10f);
        Sparks(to, Quaternion.identity, ParticleSystemShapeType.Sphere, 0f, 5, 2f, 5f, 0.2f, pale, 0.08f);
    }

    // The cast itself, then a reaction on everything it hit. `dist` is how far the caster is aiming.
    public static void Cast(AbilityDef a, Unit caster, Color accent, Vector3 pos, Vector3 dir, Vector3 point, float dist, List<Unit> hit, bool shake)
    {
        if (Net.IsHost) Net.FxCast(a, caster, pos, dir, point, dist, hit);
        if (!Visible(pos) && !Visible(point)) return;
        Color c = a.Tint ?? ColorOf(a);
        float p = a.Power;
        Vector3 chest = At(pos, 1f);
        bool atSelf = a.Shape == Shape.Self || a.Shape == Shape.SelfArea;
        Vector3 center = atSelf ? pos : point;
        var style = a.Style;
        if (style == VfxStyle.Default)
            style = a.Shape == Shape.Skillshot ? VfxStyle.Arrow : a.Shape == Shape.Self ? VfxStyle.Aura : VfxStyle.Nova;

        switch (style)
        {
            case VfxStyle.Impact: Shockwave(At(center, 0.15f), 2.5f, c, 0.3f, 0.35f); break;
            case VfxStyle.Slash: Slash(chest, dir, c); break;
            case VfxStyle.Whirl: Whirl(caster, a.Radius, c); break;
            case VfxStyle.Slam: Slam(center, Mathf.Max(a.Radius, 2f), c, p); break;
            case VfxStyle.Dome: Dome(pos, c); break;
            case VfxStyle.Aura: Aura(caster, c, EffectSeconds(a)); break;
            case VfxStyle.Dash:
            case VfxStyle.Strike: Dash(caster, dir, c, accent); break;
            case VfxStyle.Arrow: Projectile(chest, dir, a.Range, 80f, c, 0.2f, 22f); Glow(chest + dir, c, 1f, 0.12f); break;
            case VfxStyle.Bolt: Projectile(chest, dir, a.Range, 32f, c, 0.45f, 16f); Glow(chest + dir, c, 1.5f, 0.15f); break;
            case VfxStyle.Beam: Beam(chest, dir, a.Range, c, p); break;
            case VfxStyle.Trap: Trap(center, a.Radius, c); break;
            case VfxStyle.Nova: Nova(center, a.Radius, c); break;
            case VfxStyle.Field: Nova(center, a.Radius, c); Field(center, a.Radius, c, EffectSeconds(a)); break;
            case VfxStyle.Storm: Storm(center, a.Radius, c, p); break;
            case VfxStyle.Heal: HealBeams(caster, hit, c); break;
            case VfxStyle.Dawn: Dawn(pos, a.Radius, c); break;
        }

        foreach (var t in hit)
            if (t != caster) Impact(caster, t, a, c, style, p);
        if (shake) CameraFollow.Shake(p);
    }

    static float EffectSeconds(AbilityDef a)
    {
        float d = 1f;
        foreach (var e in a.Effects) d = Mathf.Max(d, e.Duration);
        return d;
    }

    // ---------- ability styles ----------
    static void Slash(Vector3 pos, Vector3 dir, Color c)
    {
        var rot = Quaternion.LookRotation(dir);
        Sparks(pos + dir * 0.6f, rot, ParticleSystemShapeType.Cone, 55f, 26, 10f, 18f, 0.25f, c, 0.14f); // wide crescent of embers
        Sparks(pos + dir * 0.6f, rot, ParticleSystemShapeType.Cone, 8f, 8, 20f, 26f, 0.18f, Color.white, 0.1f); // bright core streaks
        Glow(pos + dir * 1.5f, c, 2.2f, 0.12f);
    }

    static void Whirl(Unit caster, float radius, Color c)
    {
        var r = VfxRunner.Spawn("VfxWhirl", caster.transform.position);
        r.Repeat(0.1f, 6, i =>
        {
            if (caster == null) return;
            Vector3 p = caster.transform.position;
            Vector3 d = Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward;
            Sparks(At(p, 1f) + d * 0.5f, Quaternion.LookRotation(d), ParticleSystemShapeType.Cone, 40f, 14, 12f, 18f, 0.22f, c, 0.14f);
            Shockwave(At(p, 0.15f), radius, c, 0.35f, 0.25f);
        });
        Object.Destroy(r.gameObject, 1f);
    }

    public static void Slam(Vector3 center, float radius, Color c, float p)
    {
        Vector3 g = At(center, 0.15f);
        Shockwave(g, radius, c, 0.5f, 0.55f);
        Shockwave(g, radius * 0.7f, Color.white, 0.3f, 0.3f);
        Glow(At(center, 0.5f), c, radius * 0.9f, 0.2f);
        // dust is alpha blended so it reads as smoke, not light
        Make(new Spec
        {
            Pos = g, Rot = UpRot, Shape = ParticleSystemShapeType.Cone, Angle = 70f, ShapeRadius = radius * 0.3f, Count = (int)(14 + 14 * p),
            SpeedMin = 2f, SpeedMax = 5f, LifeMin = 0.5f, LifeMax = 0.9f, SizeMin = 0.5f, SizeMax = 1.1f, Gravity = -0.1f, EndScale = 1.8f,
            Color = new Color(0.55f, 0.5f, 0.45f, 0.6f), Color2 = new Color(0.4f, 0.38f, 0.35f, 0.5f), Additive = false
        });
        Sparks(g, UpRot, ParticleSystemShapeType.Cone, 45f, (int)(10 + 14 * p), 6f, 12f, 0.7f, c, 0.14f, 2.5f); // debris
    }

    static void Dome(Vector3 pos, Color c)
    {
        Rising(At(pos, 0.15f), 0.9f, c, 36, 2.4f);
        Shockwave(At(pos, 0.15f), 1.8f, c, 0.4f, 0.25f);
    }

    static void Aura(Unit caster, Color c, float seconds)
    {
        Make(new Spec
        {
            Parent = caster.transform, Pos = new Vector3(0f, -0.9f, 0f), Shape = ParticleSystemShapeType.Circle, ShapeRadius = 0.75f,
            Edge = true, Flat = true, Count = 0, RatePerSec = 26f, Duration = seconds, Rise = 1.8f, LifeMin = 0.7f, LifeMax = 1f,
            SizeMin = 0.12f, SizeMax = 0.24f, EndScale = 0.2f, Color = c, Color2 = Hot(c)
        });
        Shockwave(At(caster.transform.position, 0.15f), 2f, c, 0.4f, 0.3f);
    }

    static void Dash(Unit caster, Vector3 dir, Color c, Color accent)
    {
        Vector3 start = caster.transform.position;
        Sparks(At(start, 1f), Quaternion.LookRotation(-dir), ParticleSystemShapeType.Cone, 14f, 18, 6f, 10f, 0.3f, c, 0.12f); // takeoff streaks
        Make(new Spec // ghost trail that follows the caster's movement
        {
            Parent = caster.transform, Count = 0, RatePerDistance = 14f, Duration = 0.3f, ShapeRadius = 0.35f, SpeedMax = 0.6f,
            LifeMin = 0.35f, LifeMax = 0.55f, SizeMin = 0.4f, SizeMax = 0.7f, EndScale = 0.1f, Color = Color.Lerp(c, accent, 0.5f), Color2 = c
        });
        Shockwave(At(start, 0.15f), 1.6f, c, 0.3f, 0.3f);
    }

    static void Beam(Vector3 from, Vector3 dir, float length, Color c, float p)
    {
        Vector3 to = from + dir * length;
        Line(from, to, Hot(c), 0.5f + p * 0.8f, 0.4f);
        Line(from, to, Color.white, 0.2f + p * 0.3f, 0.3f);
        Glow(from + dir * 1.2f, c, 3f + 2f * p, 0.25f);
        for (float d = 1f; d < length; d += 2.5f)
            Sparks(At(from + dir * d, 0.2f), UpRot, ParticleSystemShapeType.Cone, 50f, 6, 3f, 7f, 0.5f, c, 0.12f, 2f); // scorched ground
    }

    static void Trap(Vector3 center, float radius, Color c)
    {
        Vector3 g = At(center, 0.15f);
        float snap = -radius / 0.35f; // negative speed = the ring snaps inward
        Make(new Spec
        {
            Pos = g, Shape = ParticleSystemShapeType.Circle, ShapeRadius = radius, Edge = true, Flat = true, Count = 40,
            SpeedMin = snap, SpeedMax = snap, LifeMin = 0.35f, LifeMax = 0.35f, SizeMin = 0.35f, SizeMax = 0.5f, Color = Hot(c), Color2 = c
        });
        Make(new Spec // rune ring that lingers on the ground
        {
            Pos = g, Shape = ParticleSystemShapeType.Circle, ShapeRadius = radius, Edge = true, Flat = true, Count = 48,
            LifeMin = 1.2f, LifeMax = 1.2f, SizeMin = 0.25f, SizeMax = 0.3f, EndScale = 1f, Color = c, Color2 = c
        });
        Sparks(g, UpRot, ParticleSystemShapeType.Cone, 8f, 14, 4f, 8f, 0.6f, Hot(c), 0.12f);
        Glow(At(center, 0.6f), c, radius, 0.25f);
    }

    static void Nova(Vector3 center, float radius, Color c)
    {
        Vector3 g = At(center, 0.15f);
        Shockwave(g, radius, c, 0.45f, 0.45f);
        Shockwave(g, radius * 0.6f, Hot(c), 0.3f, 0.3f);
        Glow(At(center, 0.8f), c, radius, 0.2f);
        Make(new Spec
        {
            Pos = g, Shape = ParticleSystemShapeType.Circle, ShapeRadius = radius * 0.8f, Flat = true, Count = (int)(radius * 8f), Rise = 2f,
            LifeMin = 0.5f, LifeMax = 0.9f, SizeMin = 0.12f, SizeMax = 0.22f, EndScale = 0.2f, Color = Hot(c), Color2 = c
        });
    }

    // Crackling disc that lasts as long as the slow does.
    static void Field(Vector3 center, float radius, Color c, float seconds)
    {
        Make(new Spec
        {
            Pos = At(center, 0.2f), Shape = ParticleSystemShapeType.Circle, ShapeRadius = radius, Flat = true, Count = 0, RatePerSec = 22f,
            Duration = seconds, Rise = 1.2f, LifeMin = 0.4f, LifeMax = 0.8f, SizeMin = 0.1f, SizeMax = 0.2f, EndScale = 0.1f, Color = Hot(c), Color2 = c
        });
        Make(new Spec
        {
            Pos = At(center, 0.2f), Shape = ParticleSystemShapeType.Circle, ShapeRadius = radius, Edge = true, Flat = true, Count = 0,
            RatePerSec = 12f, Duration = seconds, LifeMin = 0.8f, LifeMax = 0.8f, SizeMin = 0.25f, SizeMax = 0.25f, EndScale = 0.5f, Color = c, Color2 = c
        });
    }

    static void Storm(Vector3 center, float radius, Color c, float p)
    {
        Nova(center, radius, c);
        var r = VfxRunner.Spawn("VfxStorm", center);
        r.Repeat(0.06f, 14, i =>
        {
            Vector2 o = Random.insideUnitCircle * radius;
            Vector3 hitPt = new Vector3(center.x + o.x, 0.1f, center.z + o.y);
            Lightning(hitPt + Vector3.up * 18f, hitPt, c, 0.35f);
            Glow(hitPt + Vector3.up * 0.4f, Hot(c), 2f, 0.15f);
            Sparks(hitPt, UpRot, ParticleSystemShapeType.Cone, 60f, 8, 4f, 9f, 0.4f, c, 0.1f, 2f);
            CameraFollow.Shake(p * 0.25f);
        });
        Object.Destroy(r.gameObject, 1.5f);
    }

    static void HealBeams(Unit caster, List<Unit> hit, Color c)
    {
        Vector3 from = At(caster.transform.position, 1.2f);
        foreach (var t in hit)
            if (t != caster) Line(from, At(t.transform.position, 1.2f), Hot(c), 0.35f, 0.3f);
        Rising(At(caster.transform.position, 0.15f), 0.6f, c, 14, 2.2f);
    }

    static void Dawn(Vector3 center, float radius, Color c)
    {
        Vector3 g = At(center, 0.15f);
        Shockwave(g, radius, c, 0.9f, 0.7f);
        Shockwave(g, radius * 0.5f, Color.white, 0.5f, 0.4f);
        Make(new Spec // pillar of light
        {
            Pos = g, Shape = ParticleSystemShapeType.Circle, ShapeRadius = 1.3f, Edge = true, Flat = true, Count = 60, Rise = 9f,
            LifeMin = 0.5f, LifeMax = 0.8f, SizeMin = 0.25f, SizeMax = 0.4f, EndScale = 0.2f, Color = Color.white, Color2 = c
        });
        Make(new Spec // motes drifting up across the whole area
        {
            Pos = g, Shape = ParticleSystemShapeType.Circle, ShapeRadius = radius, Flat = true, Count = (int)(radius * 6f), Rise = 1.5f,
            LifeMin = 1f, LifeMax = 1.6f, SizeMin = 0.15f, SizeMax = 0.3f, EndScale = 0.2f, Color = Hot(c), Color2 = c
        });
        Glow(At(center, 1f), c, radius * 1.2f, 0.4f);
    }

    // ---------- reactions on whatever was hit ----------
    static void Impact(Unit caster, Unit t, AbilityDef a, Color c, VfxStyle style, float p)
    {
        Vector3 body = At(t.transform.position, 1f);
        Vector3 ground = At(t.transform.position, 0.15f);

        if (t.Team == caster.Team) // healed / shielded / buffed ally
        {
            Rising(ground, 0.6f, c, 18, 2.6f);
            Glow(body, c, 1.6f, 0.3f);
            return;
        }

        Sparks(body, Quaternion.identity, ParticleSystemShapeType.Sphere, 0f, (int)(8 + 14 * p), 5f, 11f, 0.3f, c, 0.12f);
        Glow(body, c, 1.2f + p, 0.15f);
        foreach (var e in a.Effects)
        {
            switch (e.Type)
            {
                case EffectType.Stun: // stars circling over the head
                    Make(new Spec
                    {
                        Pos = At(t.transform.position, 2.4f), Shape = ParticleSystemShapeType.Circle, ShapeRadius = 0.5f, Edge = true, Flat = true,
                        Count = 6, LifeMin = 0.9f, LifeMax = 0.9f, SizeMin = 0.22f, SizeMax = 0.26f, EndScale = 1f, Color = new Color(1f, 0.9f, 0.3f), Color2 = Color.white
                    });
                    break;
                case EffectType.Root: // ground ring plus vines
                    Make(new Spec
                    {
                        Pos = ground, Shape = ParticleSystemShapeType.Circle, ShapeRadius = 0.8f, Edge = true, Flat = true, Count = 14,
                        LifeMin = 1f, LifeMax = 1.2f, SizeMin = 0.2f, SizeMax = 0.25f, EndScale = 0.6f, Color = new Color(0.45f, 0.8f, 0.3f), Color2 = new Color(0.3f, 0.6f, 0.2f)
                    });
                    Sparks(ground, UpRot, ParticleSystemShapeType.Cone, 15f, 10, 2f, 3f, 0.6f, new Color(0.45f, 0.8f, 0.3f), 0.12f, 1f);
                    break;
                case EffectType.Slow: // frost drifting down
                    Make(new Spec
                    {
                        Pos = body, Shape = ParticleSystemShapeType.Sphere, ShapeRadius = 0.5f, Count = 10, SpeedMax = 1f, Gravity = 0.6f,
                        LifeMin = 0.6f, LifeMax = 0.9f, SizeMin = 0.1f, SizeMax = 0.18f, Color = new Color(0.7f, 0.9f, 1f), Color2 = Color.white
                    });
                    break;
                case EffectType.Silence:
                    Make(new Spec
                    {
                        Pos = At(t.transform.position, 2f), Shape = ParticleSystemShapeType.Circle, ShapeRadius = 0.5f, Edge = true, Flat = true,
                        Count = 8, LifeMin = 1f, LifeMax = 1f, SizeMin = 0.15f, SizeMax = 0.18f, EndScale = 1f, Color = new Color(0.7f, 0.4f, 0.9f), Color2 = new Color(0.5f, 0.3f, 0.7f)
                    });
                    break;
            }
        }
        if (style == VfxStyle.Strike) // crossing blade marks
        {
            Vector3 d1 = new Vector3(1f, 1f, 0f).normalized * 1.6f, d2 = new Vector3(-1f, 1f, 0f).normalized * 1.6f;
            Line(body - d1, body + d1, Hot(c), 0.22f, 0.25f);
            Line(body - d2, body + d2, Hot(c), 0.22f, 0.25f);
            Glow(body, c, 2.5f, 0.2f);
        }
    }
}

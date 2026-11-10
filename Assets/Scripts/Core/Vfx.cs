using UnityEngine;

// Runtime particle bursts, no assets needed.
// Auto attacks = a few small pale sparks. Abilities = bigger coloured bursts, area rings and skillshot trails.
// Colour shows what the ability does: orange physical, purple magic, green heal, cyan shield, yellow crowd control.
public static class Vfx
{
    static Material mat;

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

    public static void Hit(Vector3 pos) => Burst(pos, new Color(1f, 0.95f, 0.75f), 5, 0.1f, 2.5f, 0.25f, 0f);

    public static void Impact(Vector3 pos, Color c) => Burst(pos, c, 24, 0.25f, 5f, 0.6f, 0f);

    public static void Ring(Vector3 center, float radius, Color c) =>
        Burst(center + Vector3.up * 0.15f, c, Mathf.Max(16, (int)(radius * 10f)), 0.3f, 0f, 0.7f, radius);

    public static void Trail(Vector3 from, Vector3 dir, float length, Color c)
    {
        for (float d = 0.5f; d < length; d += 1.2f)
            Burst(from + dir * d + Vector3.up * 0.8f, c, 4, 0.25f, 0.5f, 0.5f, 0f);
    }

    static void Burst(Vector3 pos, Color c, int count, float size, float speed, float life, float ringRadius)
    {
        var go = new GameObject("Vfx");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = life;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = c;

        var emission = ps.emission;
        emission.enabled = false; // we emit one burst by hand below

        var shape = ps.shape;
        if (ringRadius > 0f)
        {
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = ringRadius;
            shape.radiusThickness = 0f;               // edge only = a ring
            shape.rotation = new Vector3(90f, 0f, 0f); // lay it flat on the ground
        }
        else
        {
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
        }

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;

        // Sprites/Default is always included in builds and works in both Built-in and URP.
        if (mat == null) mat = new Material(Shader.Find("Sprites/Default"));
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;

        ps.Emit(count);
        ps.Play();
        Object.Destroy(go, life + 0.5f);
    }
}

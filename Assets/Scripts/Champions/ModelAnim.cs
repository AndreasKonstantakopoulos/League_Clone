using UnityEngine;

// Moves a champion model: legs and arms swing while walking, the weapon arm swings on an attack or cast.
// For a real model with an Animator it instead sets the float "Speed" and the trigger "Attack", if the controller has them.
public class ModelAnim : MonoBehaviour
{
    public Transform LegL, LegR, ArmL, ArmR;
    public Animator Animator;
    Vector3 last;
    float phase, speed, attackUntil;
    bool hasSpeed, hasAttack;

    void Start()
    {
        last = transform.position;
        if (Animator == null || Animator.runtimeAnimatorController == null) return;
        foreach (var p in Animator.parameters)
        {
            if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) hasSpeed = true;
            if (p.name == "Attack" && p.type == AnimatorControllerParameterType.Trigger) hasAttack = true;
        }
    }

    public void Attack()
    {
        attackUntil = Time.time + 0.3f;
        if (hasAttack) Animator.SetTrigger("Attack");
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        Vector3 p = transform.position;
        speed = Mathf.Lerp(speed, Unit.FlatDist(p, last) / dt, 10f * dt);
        last = p;
        if (Animator != null)
        {
            if (hasSpeed) Animator.SetFloat("Speed", speed);
            return;
        }
        float swing = 0f;
        if (speed > 0.5f) { phase += dt * 10f; swing = Mathf.Sin(phase) * 35f; }
        else phase = 0f;
        Set(LegL, swing);
        Set(LegR, -swing);
        Set(ArmL, -swing * 0.7f);
        float arm = swing * 0.7f;
        if (Time.time < attackUntil) arm = -100f * Mathf.Sin((1f - (attackUntil - Time.time) / 0.3f) * Mathf.PI); // raise and strike
        Set(ArmR, arm);
    }

    static void Set(Transform t, float degrees) { if (t != null) t.localRotation = Quaternion.Euler(degrees, 0f, 0f); }
}

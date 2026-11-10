using System;
using System.Collections;
using UnityEngine;

// Small helper for effects that have to move or play out over time (projectiles, lightning, repeating pulses).
public class VfxRunner : MonoBehaviour
{
    public static VfxRunner Spawn(string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        return go.AddComponent<VfxRunner>();
    }

    public void Fly(Vector3 velocity, float seconds) { StartCoroutine(FlyRoutine(velocity, seconds)); }

    IEnumerator FlyRoutine(Vector3 v, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            transform.position += v * Time.deltaTime;
            yield return null;
        }
    }

    // Chases the target at `speed` and calls onHit when it gets there. Gives up if the target is destroyed or after 3 s.
    public void Homing(Unit target, float speed, Vector3 aimOffset, Action onHit) { StartCoroutine(HomingRoutine(target, speed, aimOffset, onHit)); }

    IEnumerator HomingRoutine(Unit target, float speed, Vector3 aimOffset, Action onHit)
    {
        for (float timeout = 3f; timeout > 0f && target != null; timeout -= Time.deltaTime)
        {
            Vector3 goal = target.transform.position + aimOffset;
            transform.position = Vector3.MoveTowards(transform.position, goal, speed * Time.deltaTime);
            if ((goal - transform.position).sqrMagnitude < 0.25f) { onHit(); break; }
            yield return null;
        }
        // let the trail fade out instead of vanishing with the projectile
        foreach (var ps in GetComponentsInChildren<ParticleSystem>())
        {
            ps.Stop();
            ps.transform.SetParent(null);
        }
        Destroy(gameObject);
    }

    // Fades the line out and thins it over the given time.
    public void FadeLine(LineRenderer lr, float seconds) { StartCoroutine(FadeRoutine(lr, seconds)); }

    IEnumerator FadeRoutine(LineRenderer lr, float seconds)
    {
        Color c0 = lr.startColor;
        float w0 = lr.startWidth;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = 1f - t / seconds;
            lr.startColor = lr.endColor = new Color(c0.r, c0.g, c0.b, c0.a * k);
            lr.startWidth = lr.endWidth = w0 * (0.4f + 0.6f * k);
            yield return null;
        }
    }

    public void Repeat(float interval, int times, Action<int> act) { StartCoroutine(RepeatRoutine(interval, times, act)); }

    IEnumerator RepeatRoutine(float interval, int times, Action<int> act)
    {
        for (int i = 0; i < times; i++)
        {
            act(i);
            yield return new WaitForSeconds(interval);
        }
    }
}

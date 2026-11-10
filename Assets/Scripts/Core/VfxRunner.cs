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

using UnityEngine;

// Smoothly follows the position the host last reported (used by mirrored minions, monsters and champions on clients).
public class NetGhost : MonoBehaviour
{
    Vector3 target;
    float yaw;
    bool has;

    public void SetTarget(Vector3 pos, float yawDegrees)
    {
        if (!has) { transform.position = pos; has = true; }
        target = pos;
        yaw = yawDegrees;
    }

    void Update()
    {
        if (!has) return;
        float k = 1f - Mathf.Exp(-15f * Time.deltaTime);
        if ((transform.position - target).sqrMagnitude > 36f) transform.position = target; // teleport: recall, respawn
        else transform.position = Vector3.Lerp(transform.position, target, k);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, yaw, 0f), k);
    }
}

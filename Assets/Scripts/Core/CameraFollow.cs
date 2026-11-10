using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Instance;
    public Transform Target;
    float shake;

    void Awake() { Instance = this; }

    // power 0..1; the strongest recent shake wins and then fades out
    public static void Shake(float power)
    {
        if (Instance != null) Instance.shake = Mathf.Max(Instance.shake, power);
    }

    void LateUpdate()
    {
        if (Target == null) return;
        transform.position = Target.position + new Vector3(0f, 22f, -14f);
        transform.rotation = Quaternion.Euler(57f, 0f, 0f);
        if (shake <= 0f) return;
        transform.position += Random.insideUnitSphere * shake * 0.4f;
        shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 2.5f);
    }
}

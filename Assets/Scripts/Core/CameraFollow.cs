using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform Target;

    void LateUpdate()
    {
        if (Target == null) return;
        transform.position = Target.position + new Vector3(0f, 22f, -14f);
        transform.rotation = Quaternion.Euler(57f, 0f, 0f);
    }
}

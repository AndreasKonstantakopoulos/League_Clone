using UnityEngine;

// Lives on the persistent Net object and drives the host's send loop.
public class NetDriver : MonoBehaviour
{
    void Update() { Net.HostTick(Time.deltaTime); }
}

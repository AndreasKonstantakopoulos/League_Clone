using UnityEngine;
using UnityEngine.SceneManagement;

// Starts the game automatically in any scene that contains the greybox map (so no scene wiring is needed).
public static class Bootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        Spawn();
        SceneManager.sceneLoaded += (s, m) => Spawn(); // also after Restart reloads the scene
    }

    static void Spawn()
    {
        if (GameManager.I != null) return;
        if (GameObject.Find("Map") == null || GameObject.Find("Nexus_Blue") == null) return;
        new GameObject("Game").AddComponent<GameManager>();
    }
}

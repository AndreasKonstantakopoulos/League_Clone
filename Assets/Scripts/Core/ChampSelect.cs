using UnityEngine;
using UnityEngine.SceneManagement;

// Champion-select scene: roster with icons on the left, a spinning 3D preview in the middle,
// stats and ability descriptions on the right. Starting loads the Map scene.
public class ChampSelect : MonoBehaviour
{
    ChampionDef sel;
    GameObject preview;
    Vector2 listScroll;

    void Start()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.transform.position = new Vector3(0f, -0.05f, 0f);
        floor.transform.localScale = new Vector3(3f, 0.05f, 3f);
        floor.GetComponent<Renderer>().material.color = new Color(0.2f, 0.22f, 0.28f);

        preview = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        preview.transform.position = new Vector3(0f, 1f, 0f);
        var visor = GameObject.CreatePrimitive(PrimitiveType.Cube); // makes the spin visible
        visor.transform.SetParent(preview.transform, false);
        visor.transform.localPosition = new Vector3(0f, 0.45f, 0.4f);
        visor.transform.localScale = new Vector3(0.6f, 0.2f, 0.2f);
        visor.GetComponent<Renderer>().material.color = Color.black;

        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 1.8f, -5.5f);
            cam.transform.LookAt(new Vector3(0f, 1f, 0f));
        }
        Select(Session.Selected ?? Content.Champions[0]);
    }

    void Select(ChampionDef d)
    {
        sel = d;
        preview.GetComponent<Renderer>().material.color = d.Color;
    }

    void Update() { preview.transform.Rotate(0f, 40f * Time.deltaTime, 0f); }

    void OnGUI()
    {
        float h = Screen.height - 40f;

        GUILayout.BeginArea(new Rect(20f, 20f, 320f, h), GUI.skin.box);
        GUILayout.Label("Choose your champion");
        listScroll = GUILayout.BeginScrollView(listScroll);
        foreach (var d in Content.Champions)
        {
            Rect row = GUILayoutUtility.GetRect(280f, 58f);
            var old = GUI.color;
            if (d == sel) GUI.color = new Color(1f, 1f, 0.6f);
            if (GUI.Button(row, GUIContent.none)) Select(d);
            GUI.color = old;
            Icons.Draw(new Rect(row.x + 4f, row.y + 4f, 50f, 50f), d);
            GUI.Label(new Rect(row.x + 62f, row.y + 8f, 210f, 22f), d.Name);
            GUI.Label(new Rect(row.x + 62f, row.y + 30f, 210f, 22f), d.Role.ToString());
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        GUILayout.BeginArea(new Rect(Screen.width - 460f, 20f, 440f, h), GUI.skin.box);
        GUILayout.Label($"{sel.Name} - {sel.Role}");
        var b = sel.Base;
        GUILayout.Label($"HP {b.Health:0}   Mana {b.Mana:0}   AD {b.AD:0}   Armor {b.Armor:0}   MR {b.MR:0}\n" +
                        $"Attack speed {b.AttackSpeed:0.00}   Move speed {b.MoveSpeed:0.0}   Attack range {sel.AttackRange:0.#}");
        for (int i = 0; i < 4; i++)
        {
            var a = sel.Abilities[i];
            GUILayout.Space(6f);
            GUILayout.Label($"{"QWER"[i]} - {a.Name}\n{a.Describe()}");
        }
        GUILayout.EndArea();

        if (GUI.Button(new Rect(Screen.width / 2f - 130f, Screen.height - 70f, 260f, 50f), "Start match as " + sel.Name))
        {
            Session.Selected = sel;
            SceneManager.LoadScene("Map");
        }
    }
}

using UnityEditor;
using UnityEngine;

// Menu: Tools > Set Game Icon. Makes a texture the icon of the built game (the .exe and its window).
// Select one of the PNGs in Assets/Icons first to use that one; with nothing selected it uses Assets/Icons/GameIcon.png.
public static class GameIcon
{
    [MenuItem("Tools/Set Game Icon")]
    public static void Apply()
    {
        var tex = Selection.activeObject as Texture2D ?? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icons/GameIcon.png");
        if (tex == null) { Debug.LogError("No icon found. Expected Assets/Icons/GameIcon.png, or select a texture first."); return; }
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { tex }); // Unknown = the default icon for every platform
        AssetDatabase.SaveAssets();
        Debug.Log($"Game icon set to {AssetDatabase.GetAssetPath(tex)}. It shows on the next build.");
    }
}

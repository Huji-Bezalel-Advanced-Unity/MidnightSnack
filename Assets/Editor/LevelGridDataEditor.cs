using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LevelGridData))]
public class LevelGridDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); // Draw the normal inspector fields

        LevelGridData gridScript = (LevelGridData)target;
        if (GUILayout.Button("Generate Grid Data"))
        {
            gridScript.GenerateGridData();
            EditorUtility.SetDirty(target); // Mark script as dirty to save changes
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gridScript.gameObject.scene); // Mark scene as dirty
        }
    }
}
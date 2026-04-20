using UnityEngine;
using UnityEditor;
using EJR.Game.Gameplay;
using System.Collections.Generic;

public class MapAutoLinker : EditorWindow
{
    [MenuItem("Tools/EJR/Auto Link Map Prefabs")]
    public static void LinkMaps()
    {
        // 1. Find RunStateController in the scene
        RunStateController controller = GameObject.FindObjectOfType<RunStateController>();
        if (controller == null)
        {
            Debug.LogError("Could not find RunStateController in the scene!");
            return;
        }

        // 2. Load Prefabs from the known path
        string folderPath = "Assets/_Project/Prefabs/Maps";
        GameObject map1 = AssetDatabase.LoadAssetAtPath<GameObject>(folderPath + "/Map1.prefab");
        GameObject map2 = AssetDatabase.LoadAssetAtPath<GameObject>(folderPath + "/Map2.prefab");
        GameObject map3 = AssetDatabase.LoadAssetAtPath<GameObject>(folderPath + "/Map3.prefab");

        if (map1 == null || map2 == null || map3 == null)
        {
            Debug.LogError($"Missing some prefabs in {folderPath}! Map1: {map1 != null}, Map2: {map2 != null}, Map3: {map3 != null}");
            return;
        }

        // 3. Assign using SerializedObject (to circumvent private field access if needed)
        SerializedObject so = new SerializedObject(controller);
        SerializedProperty mapListProp = so.FindProperty("mapPrefabs");

        if (mapListProp != null && mapListProp.isArray)
        {
            mapListProp.ClearArray();
            mapListProp.InsertArrayElementAtIndex(0);
            mapListProp.GetArrayElementAtIndex(0).objectReferenceValue = map1;
            
            mapListProp.InsertArrayElementAtIndex(1);
            mapListProp.GetArrayElementAtIndex(1).objectReferenceValue = map2;
            
            mapListProp.InsertArrayElementAtIndex(2);
            mapListProp.GetArrayElementAtIndex(2).objectReferenceValue = map3;

            so.ApplyModifiedProperties();
            Debug.Log("Successfully linked Map1, Map2, and Map3 to RunStateController!");
            
            // Mark scene dirty to save changes
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }
        else
        {
            Debug.LogError("Could not find 'mapPrefabs' property in RunStateController. Did you change the name?");
        }
    }
}

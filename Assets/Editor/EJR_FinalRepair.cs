using UnityEngine;
using UnityEditor;
using System.IO;

public class EJR_FinalRepair : AssetPostprocessor
{
    [MenuItem("Tools/EJR/EMERGENCY - Full Project Repair")]
    public static void FullRepair()
    {
        Debug.Log("Starting Emergency Repair...");

        // 1. Delete Corrupted Meta Files
        string plantMeta = "Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Plant.png.meta";
        string propsMeta = "Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Props.png.meta";
        
        if (File.Exists(plantMeta)) File.Delete(plantMeta);
        if (File.Exists(propsMeta)) File.Delete(propsMeta);

        // 2. Refresh Asset Database to let Unity RECREATE metas naturally
        AssetDatabase.ImportAsset("Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Plant.png", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Props.png", ImportAssetOptions.ForceUpdate);
        
        AssetDatabase.Refresh();

        // 3. Delete Broken Palettes for a clean start
        string plantPalette = "Assets/Cainos/Pixel Art Top Down - Basic/Tile Palette/TP Plant.prefab";
        string propsPalette = "Assets/Cainos/Pixel Art Top Down - Basic/Tile Palette/TP Props.prefab";
        
        if (File.Exists(plantPalette)) AssetDatabase.DeleteAsset(plantPalette);
        if (File.Exists(propsPalette)) AssetDatabase.DeleteAsset(propsPalette);

        Debug.Log("Repair Complete! Meta files recreated by Unity engine. Please re-run the Palette Generator if needed.");
    }
}

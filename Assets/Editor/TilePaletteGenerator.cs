using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class TilePaletteGenerator : EditorWindow
{
    [MenuItem("Tools/EJR/Generate All Palettes")]
    public static void GenerateAll()
    {
        GeneratePalette("Plant", "Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Plant.png", true);
        GeneratePalette("Props", "Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Props.png", false);
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Finished generating all palettes with Tree grouping.");
    }

    private static void GeneratePalette(string paletteName, string texturePath, bool isPlant)
    {
        string paletteRoot = "Assets/Cainos/Pixel Art Top Down - Basic/Tile Palette";
        string tilesFolder = Path.Combine(paletteRoot, "TP " + paletteName);
        string palettePath = Path.Combine(paletteRoot, "TP " + paletteName + ".prefab");

        if (!Directory.Exists(tilesFolder)) Directory.CreateDirectory(tilesFolder);

        Object[] sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath).Where(q => q is Sprite).OrderBy(q => q.name).ToArray();
        
        // 1. Create Tile Assets
        Dictionary<string, Tile> tileDict = new Dictionary<string, Tile>();
        foreach (var s in sprites)
        {
            Sprite sprite = (Sprite)s;
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.name = sprite.name;
            string tilePath = Path.Combine(tilesFolder, sprite.name + ".asset");
            AssetDatabase.CreateAsset(tile, tilePath);
            tileDict.Add(sprite.name, tile);
        }

        // 2. Build Palette Object
        GameObject paletteGO = new GameObject("TP " + paletteName);
        paletteGO.layer = 31;
        paletteGO.AddComponent<Grid>().cellSize = new Vector3(1, 1, 0);

        GameObject layerGO = new GameObject("Layer1");
        layerGO.transform.SetParent(paletteGO.transform);
        layerGO.layer = 31;
        Tilemap tilemap = layerGO.AddComponent<Tilemap>();
        TilemapRenderer tr = layerGO.AddComponent<TilemapRenderer>();
        tr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

        // 3. Arrange Tiles
        if (isPlant)
        {
            // Special sorting for Trees: Upper at y=0, Lower at y=-1
            int treeCol = 0;
            HashSet<string> placed = new HashSet<string>();

            // Find pairs
            for (int t = 1; t <= 3; t++)
            {
                string uName = $"TX Tree T{t} Upper";
                string lName = $"TX Tree T{t} Lower";
                if (tileDict.ContainsKey(uName) && tileDict.ContainsKey(lName))
                {
                    tilemap.SetTile(new Vector3Int(treeCol, 0, 0), tileDict[uName]);
                    tilemap.SetTile(new Vector3Int(treeCol, -1, 0), tileDict[lName]);
                    placed.Add(uName); placed.Add(lName);
                    treeCol++;
                }
            }

            // Place others starting from y=-2
            int otherIdx = 0;
            int width = 8;
            foreach (var kv in tileDict)
            {
                if (placed.Contains(kv.Key)) continue;
                int x = otherIdx % width;
                int y = -2 - (otherIdx / width);
                tilemap.SetTile(new Vector3Int(x, y, 0), kv.Value);
                otherIdx++;
            }
        }
        else
        {
            // Standard 8-wide for others
            int i = 0;
            foreach (var kv in tileDict)
            {
                tilemap.SetTile(new Vector3Int(i % 8, -(i / 8), 0), kv.Value);
                i++;
            }
        }

        // 4. Save
        if (File.Exists(palettePath)) AssetDatabase.DeleteAsset(palettePath);
        PrefabUtility.SaveAsPrefabAsset(paletteGO, palettePath);
        DestroyImmediate(paletteGO);
    }
}

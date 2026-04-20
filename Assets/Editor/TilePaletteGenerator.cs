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
        GeneratePalette("Plant", "Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Plant.png");
        GeneratePalette("Props", "Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Props.png");
        AssetDatabase.Refresh();
        Debug.Log("Finished generating all palettes.");
    }

    private static void GeneratePalette(string paletteName, string texturePath)
    {
        string paletteRoot = "Assets/Cainos/Pixel Art Top Down - Basic/Tile Palette";
        string tilesFolder = Path.Combine(paletteRoot, "TP " + paletteName);
        string palettePath = Path.Combine(paletteRoot, "TP " + paletteName + ".prefab");

        if (!Directory.Exists(tilesFolder)) Directory.CreateDirectory(tilesFolder);

        Object[] sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath).Where(q => q is Sprite).OrderBy(q => q.name).ToArray();
        if (sprites.Length == 0)
        {
            Debug.LogError("No sprites found at: " + texturePath + ". Make sure Texture Type is Sprite (2D and UI)!");
            return;
        }

        // 1. Create Tile Assets
        List<Tile> tiles = new List<Tile>();
        foreach (var s in sprites)
        {
            Sprite sprite = (Sprite)s;
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.name = sprite.name;

            string tilePath = Path.Combine(tilesFolder, sprite.name + ".asset");
            AssetDatabase.CreateAsset(tile, tilePath);
            tiles.Add(tile);
        }

        // 2. Build Palette Object
        GameObject paletteGO = new GameObject("TP " + paletteName);
        paletteGO.layer = 31; // Tile Palette Layer
        
        Grid grid = paletteGO.AddComponent<Grid>();
        grid.cellSize = new Vector3(1, 1, 0);

        GameObject layerGO = new GameObject("Layer1");
        layerGO.transform.SetParent(paletteGO.transform);
        layerGO.layer = 31;
        
        Tilemap tilemap = layerGO.AddComponent<Tilemap>();
        TilemapRenderer tr = layerGO.AddComponent<TilemapRenderer>();
        tr.material = new Material(Shader.Find("Sprites/Default"));

        // 3. Arrange on Grid (8 per row)
        int width = 8;
        for (int i = 0; i < tiles.Count; i++)
        {
            int x = i % width;
            int y = -(i / width);
            tilemap.SetTile(new Vector3Int(x, y, 0), tiles[i]);
        }

        // 4. Save
        PrefabUtility.SaveAsPrefabAsset(paletteGO, palettePath);
        DestroyImmediate(paletteGO);

        Debug.Log("Generated Palette: " + paletteName + " with " + tiles.Count + " tiles.");
    }
}

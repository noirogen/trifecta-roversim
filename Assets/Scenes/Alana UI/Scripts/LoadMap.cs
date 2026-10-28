using System.Collections.Generic;
using UnityEngine;

public class LoadMap : MonoBehaviour
{
    [Header("Map Terrain Arrays")]
    public GameObject[] JezeroTerrains;
    public GameObject[] TestTerrain;
    public GameObject[] TestTerrain2;

    public TileGrid gridSelection;

    Dictionary<string, GameObject[]> maps; // Map name, dictionary of tiles
    readonly Dictionary<string, GameObject> currentTiles = new Dictionary<string, GameObject>();  // "C6" -> tile
    string currentMap = "";

    void Awake()
    {
        maps = new Dictionary<string, GameObject[]>
        {
            { "Jezero", JezeroTerrains },
            { "TestTerrain", TestTerrain },
            { "TestTerrain2", TestTerrain2 }
        };
        HideAll();
    }
    public void OpenMap(string mapName)
    {
        if (!maps.ContainsKey(mapName)){
            Debug.Log("This map set doesn't exist");
            return;
        }

        if(currentMap != mapName) // if they're the same, we do not have to remake the currentTilesList 
        {
            Debug.Log($"This map set \"{mapName}\" exists, setting current map and clearing current tiles");
            currentMap = mapName;
            currentTiles.Clear();

            foreach (var tile in maps[mapName])
            {
                if (tile == null) continue;
                // tile names must end in the grid label, e.g. "Jezero_C6", C6 also works
                string label = tile.name.Substring(tile.name.LastIndexOf('_') + 1);
                currentTiles[label] = tile;
            }

            gridSelection.SetAvailable(currentTiles.Keys);   // grid turns green where tiles exist
        }


    }

    public void LoadSelectedTile()
    {
        string label = gridSelection.selectedTile;
        if (label == "" || !currentTiles.ContainsKey(label)) return;
        ShowOnly(label);
    }

    //a "Random tile" button
    public void LoadRandomTile()
    {
        if (currentTiles.Count == 0) return;
        var keys = new List<string>(currentTiles.Keys);
        string label = keys[Random.Range(0, keys.Count)];
        ShowOnly(label);
        gridSelection.selectFromLabel(label);

    }

    void ShowOnly(string label)
    {
        HideAll();                          // hides tiles from every map, not just this one
        currentTiles[label].SetActive(true);
        gridSelection.selectedTile = label;
    }

    void HideAll()
    {
        foreach (var arr in maps.Values)
            foreach (var t in arr)
                if (t != null) t.SetActive(false);
    }
}
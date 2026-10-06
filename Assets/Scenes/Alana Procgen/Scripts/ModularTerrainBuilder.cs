using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Globalization;

public class ModularTerrainBuilder : MonoBehaviour
{
    public string rawFolderPath = "Assets/MarsHeightmaps";
    public string csvFileName = "_tile_summary.csv"; // expected inside rawFolderPath
    public int heightmapResolution = 2049;
    public float tileWorldSize = 2049f; // meters (1m/pixel data -> matches heightmapResolution)

    // filename -> unity_height_y value, loaded from the CSV
    private Dictionary<string, float> tileHeights = new Dictionary<string, float>();

    void Start()
    {
        BuildGrid(2, 3);
    }
        
    public void BuildGrid(int gridWidth, int gridHeight)
    {
        LoadHeightCSV();

        string[] files = Directory.GetFiles(rawFolderPath, "*.raw");
        System.Array.Sort(files); // ensure order matches your intended grid layout

        Terrain[,] terrainGrid = new Terrain[gridWidth, gridHeight];

        int index = 0;
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                if (index >= files.Length) break;

                string filePath = files[index];
                string fileName = Path.GetFileName(filePath);

                if (!tileHeights.TryGetValue(fileName, out float heightScale))
                {
                    Debug.LogWarning($"No CSV entry found for {fileName} - defaulting to 500. " +
                                      $"Check that the CSV's 'file' column matches this raw filename exactly.");
                    heightScale = 500f;
                }

                TerrainData data = new TerrainData();
                data.heightmapResolution = heightmapResolution;
                data.size = new Vector3(tileWorldSize, heightScale, tileWorldSize);

                float[,] heights = ReadRaw16(filePath, heightmapResolution);
                data.SetHeights(0, 0, heights);

                GameObject terrainGO = Terrain.CreateTerrainGameObject(data);
                terrainGO.transform.position = new Vector3(x * tileWorldSize, 0, y * tileWorldSize);
                terrainGO.name = $"Tile_{x}_{y}_{fileName}";

                terrainGrid[x, y] = terrainGO.GetComponent<Terrain>();
                index++;
            }
        }

        // Stitch edges together so seams blend
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                Terrain t = terrainGrid[x, y];
                if (t == null) continue;

                Terrain left   = (x > 0) ? terrainGrid[x - 1, y] : null;
                Terrain right  = (x < gridWidth - 1) ? terrainGrid[x + 1, y] : null;
                Terrain bottom = (y > 0) ? terrainGrid[x, y - 1] : null;
                Terrain top    = (y < gridHeight - 1) ? terrainGrid[x, y + 1] : null;

                t.SetNeighbors(left, top, right, bottom);
            }
        }
    }

    // Reads the CSV written by batch_tiles_to_raw.py:
    // file,min_elev_m,max_elev_m,unity_height_y
    void LoadHeightCSV()
    {
        tileHeights.Clear();
        string csvPath = Path.Combine(rawFolderPath, csvFileName);

        if (!File.Exists(csvPath))
        {
            Debug.LogError($"CSV not found at {csvPath} - all tiles will fall back to a default height of 500.");
            return;
        }

        string[] lines = File.ReadAllLines(csvPath);
        // lines[0] is the header: file,min_elev_m,max_elev_m,unity_height_y
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] parts = lines[i].Split(',');
            if (parts.Length < 4) continue;

            string fileName = parts[0].Trim();
            if (float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float heightY))
            {
                tileHeights[fileName] = heightY;
            }
        }

        Debug.Log($"Loaded {tileHeights.Count} tile height entries from {csvPath}");
    }

    float[,] ReadRaw16(string path, int resolution)
    {
        byte[] bytes = File.ReadAllBytes(path);
        float[,] heights = new float[resolution, resolution];

        int i = 0;
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                ushort val = (ushort)(bytes[i] | (bytes[i + 1] << 8)); // little-endian
                heights[y, x] = val / 65535f; // Unity wants normalized 0-1
                i += 2;
            }
        }
        return heights;
    }
}
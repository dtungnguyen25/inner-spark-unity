using System;
using System.IO;
using Pcb;
using UnityEditor;
using UnityEngine;

/// <summary>Generates the materials, sprites and theme the PCB board needs, under Assets/PCB.</summary>
[InitializeOnLoad]
public static class PcbAssetSetup
{
    const string Root = "Assets/PCB";
    const string SpriteFolder = Root + "/Sprites";
    const string MaterialFolder = Root + "/Materials";
    const string ThemePath = Root + "/PcbTheme.asset";
    const string MaterialPath = Root + "/PcbSprite.mat";
    const string LevelListPath = Root + "/LevelList.asset";
    public const string LevelFolder = Root + "/Levels";

    // Upgrade an existing theme (e.g. from the 2D version) as soon as the editor loads.
    static PcbAssetSetup()
    {
        EditorApplication.delayCall += () =>
        {
            var theme = AssetDatabase.LoadAssetAtPath<PcbTheme>(ThemePath);
            if (theme && !theme.IsComplete) GetOrCreateTheme();
        };
    }

    [MenuItem("Tools/PCB/Create Theme Assets")]
    static void CreateFromMenu() => Selection.activeObject = GetOrCreateTheme();

    public static PcbTheme GetOrCreateTheme()
    {
        EnsureFolder(Root);
        EnsureFolder(SpriteFolder);
        EnsureFolder(MaterialFolder);

        var theme = AssetDatabase.LoadAssetAtPath<PcbTheme>(ThemePath);
        if (!theme)
        {
            theme = ScriptableObject.CreateInstance<PcbTheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }

        if (!theme.spriteMaterial)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!mat)
            {
                mat = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            theme.spriteMaterial = mat;
        }

        if (!theme.triangle) theme.triangle = MakeSprite("Triangle", 64, Triangle);

        if (!theme.boardMaterial) theme.boardMaterial = Lit("Board", new Color32(7, 138, 75, 255), 0f, 0.55f);
        if (!theme.copperMaterial) theme.copperMaterial = Lit("Copper", new Color32(252, 210, 120, 255), 0.7f, 0.65f);
        if (!theme.capacitorMaterial) theme.capacitorMaterial = Lit("Capacitor", new Color32(247, 178, 51, 255), 0.1f, 0.5f);
        if (!theme.metalMaterial) theme.metalMaterial = Lit("Metal", new Color32(200, 200, 205, 255), 1f, 0.75f);
        if (!theme.holeMaterial) theme.holeMaterial = Lit("Hole", new Color32(15, 15, 15, 255), 0f, 0.2f);
        if (!theme.chipMaterial) theme.chipMaterial = Lit("Chip", new Color32(45, 45, 48, 255), 0f, 0.35f);
        if (!theme.plugMaterial) theme.plugMaterial = Lit("Plug", new Color32(235, 235, 235, 255), 0f, 0.4f);
        if (!theme.sparkMaterial)
            theme.sparkMaterial = Lit("Spark", theme.spark, 0f, 0.9f, theme.spark * theme.sparkGlow);

        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        return theme;
    }

    public static LevelList GetOrCreateLevelList()
    {
        EnsureFolder(Root);
        EnsureFolder(LevelFolder);
        var list = AssetDatabase.LoadAssetAtPath<LevelList>(LevelListPath);
        if (list) return list;
        list = ScriptableObject.CreateInstance<LevelList>();
        AssetDatabase.CreateAsset(list, LevelListPath);
        AssetDatabase.SaveAssets();
        return list;
    }

    static Material Lit(string name, Color color, float metallic, float smoothness, Color? emission = null)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission.Value);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // Triangle pointing to +X.
    static float Triangle(float x, float y)
    {
        Vector2 p = new Vector2(x, y);
        Vector2 a = new Vector2(0.9f, 0f), b = new Vector2(-0.7f, 0.8f), c = new Vector2(-0.7f, -0.8f);
        return Mathf.Max(Edge(p, a, b), Mathf.Max(Edge(p, b, c), Edge(p, c, a)));
    }

    static float Edge(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 n = new Vector2(b.y - a.y, a.x - b.x).normalized; // outward for counter-clockwise a->b->c
        return Vector2.Dot(p - a, n);
    }

    static Sprite MakeSprite(string name, int size, Func<float, float, float> sdf)
    {
        string path = $"{SpriteFolder}/{name}.png";
        if (!File.Exists(path))
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float pixelsPerUnit = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float alpha = Mathf.Clamp01(0.5f - sdf(u, v) * pixelsPerUnit);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
            tex.SetPixels32(pixels);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size; // every sprite is 1x1 world unit
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}

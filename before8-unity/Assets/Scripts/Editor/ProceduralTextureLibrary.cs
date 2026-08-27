using System.IO;
using UnityEditor;
using UnityEngine;

namespace Before8AM.EditorTools
{
    /// <summary>
    /// 小尺寸、可重复的低模环境贴图。只在场景构建时生成一次 PNG，运行时不创建纹理。
    /// 贴图承担材质节奏，不模拟写实表面，避免破坏午夜卡通低多边形方向。
    /// </summary>
    public static class ProceduralTextureLibrary
    {
        const string TextureFolder = "Assets/Art/Environment/Textures";
        const int TextureSize = 128;

        public enum Pattern
        {
            Grass,
            Paving,
            WallPanels,
            RoofTiles,
            Concrete
        }

        public static Texture2D GetOrCreate(string fileName, Pattern pattern, Color baseColor, Color detailColor)
        {
            EnsureTextureFolder();
            string assetPath = TextureFolder + "/" + fileName + ".png";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (existing != null) return existing;

            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, true)
            {
                name = fileName,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
                for (int x = 0; x < TextureSize; x++)
                    pixels[y * TextureSize + x] = Sample(pattern, x, y, baseColor, detailColor);

            texture.SetPixels(pixels);
            texture.Apply(true, false);
            string diskPath = Path.Combine(Application.dataPath, "Art", "Environment", "Textures", fileName + ".png");
            File.WriteAllBytes(diskPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = TextureSize;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        static void EnsureTextureFolder()
        {
            if (AssetDatabase.IsValidFolder(TextureFolder)) return;
            if (!AssetDatabase.IsValidFolder("Assets/Art/Environment"))
                AssetDatabase.CreateFolder("Assets/Art", "Environment");
            AssetDatabase.CreateFolder("Assets/Art/Environment", "Textures");
        }

        static Color Sample(Pattern pattern, int x, int y, Color baseColor, Color detailColor)
        {
            switch (pattern)
            {
                case Pattern.Grass:
                    float grassNoise = Hash(x / 3, y / 3) * 0.12f - 0.06f;
                    Color grass = AdjustValue(baseColor, grassNoise);
                    return Hash(x, y) > 0.985f ? Color.Lerp(grass, detailColor, 0.55f) : grass;

                case Pattern.Paving:
                    const int paverWidth = 32;
                    const int paverHeight = 24;
                    int pavingRow = y / paverHeight;
                    int pavingOffset = (pavingRow & 1) == 0 ? 0 : paverWidth / 2;
                    bool grout = y % paverHeight < 3 || PositiveMod(x + pavingOffset, paverWidth) < 3;
                    float paverNoise = Hash((x + pavingOffset) / paverWidth, pavingRow) * 0.10f - 0.05f;
                    return grout ? detailColor : AdjustValue(baseColor, paverNoise);

                case Pattern.WallPanels:
                    bool verticalSeam = x % 48 < 3;
                    bool horizontalSeam = y % 40 < 3;
                    float wallNoise = Hash(x / 10, y / 10) * 0.045f - 0.0225f;
                    return verticalSeam || horizontalSeam ? detailColor : AdjustValue(baseColor, wallNoise);

                case Pattern.RoofTiles:
                    const int tileHeight = 18;
                    int roofRow = y / tileHeight;
                    int roofOffset = (roofRow & 1) == 0 ? 0 : 20;
                    bool roofSeam = y % tileHeight < 3 || PositiveMod(x + roofOffset, 40) < 3;
                    return roofSeam ? detailColor : AdjustValue(baseColor, Hash(x / 12, y / 9) * 0.07f - 0.035f);

                default:
                    float concreteNoise = Hash(x / 5, y / 5) * 0.08f - 0.04f;
                    return Hash(x, y) > 0.992f ? detailColor : AdjustValue(baseColor, concreteNoise);
            }
        }

        static int PositiveMod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        static float Hash(int x, int y)
        {
            unchecked
            {
                int hash = x * 374761393 + y * 668265263;
                hash = (hash ^ (hash >> 13)) * 1274126177;
                return (hash & 0x00FFFFFF) / 16777215f;
            }
        }

        static Color AdjustValue(Color color, float amount)
        {
            return new Color(
                Mathf.Clamp01(color.r + amount),
                Mathf.Clamp01(color.g + amount),
                Mathf.Clamp01(color.b + amount),
                color.a);
        }
    }
}

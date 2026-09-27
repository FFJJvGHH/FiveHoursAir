using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeepPressure.Editor
{
    public class DeepPressureArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/DeepPressure/Art/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            if (assetPath.EndsWith("_Normal.png"))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                importer.convertToNormalmap = false;
            }
            else if (assetPath.EndsWith("_AO.png"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
            }
            else
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = assetPath.Contains("/Terrain/") ? 64 : 128;
                importer.alphaIsTransparency = true;
                importer.spritePivot = new Vector2(.5f, .5f);
                if(assetPath.EndsWith("_Color.png") && (assetPath.Contains("/Industrial/")||assetPath.Contains("/Props/")))
                {
                    importer.spritePixelsPerUnit=512f/5.5f;
                    var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                    settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.5f,.111839f);importer.SetTextureSettings(settings);
                }
                if(assetPath.Contains("/Workers/")&&assetPath.EndsWith("_Color.png"))
                {
                    importer.spritePixelsPerUnit=256f/2.2f;
                    var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.5f,.0697f);importer.SetTextureSettings(settings);
                }
            }
        }

        [MenuItem("深压/素材/绑定 Blender 法线与 AO")]
        public static void BindSecondaryTextures()
        {
            const string folder = "Assets/DeepPressure/Art";
            if (!Directory.Exists(folder)) return;
            foreach (var path in Directory.GetFiles(folder, "*_Color.png",SearchOption.AllDirectories))
            {
                string asset = path.Replace('\\', '/');
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(asset.Replace("_Color.png", "_Normal.png"));
                var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(asset.Replace("_Color.png", "_AO.png"));
                if (normal == null || ao == null) continue;
                var importer = (TextureImporter)AssetImporter.GetAtPath(asset);
                importer.secondarySpriteTextures = new[] {
                    new SecondarySpriteTexture { name = "_NormalMap", texture = normal },
                    new SecondarySpriteTexture { name = "_AOMap", texture = ao }
                };
                importer.SaveAndReimport();
            }
        }
    }
}

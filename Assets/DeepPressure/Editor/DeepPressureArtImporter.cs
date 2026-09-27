using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeepPressure.Editor
{
    public class DeepPressureArtImporter : AssetPostprocessor
    {
        [Serializable] sealed class SpriteLayout
        {
            public float pixelsPerBlenderUnit;
            public float[] unitySpritePivotNormalized;
        }
        static readonly HashSet<string> pendingBindings=new HashSet<string>();
        static readonly HashSet<string> pendingLayouts=new HashSet<string>();
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
                importer.alphaIsTransparency = false;
            }
            else if (assetPath.EndsWith("_AO.png"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaIsTransparency = false;
            }
            else if(assetPath.EndsWith("_Preview.png",StringComparison.Ordinal))
            {
                // Preview contains baked light and is deliberately not a runtime Sprite.
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
            }
            else
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.sRGBTexture = true;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = assetPath.Contains("/Terrain/") ? 64 : 128;
                importer.alphaIsTransparency = true;
                importer.spritePivot = new Vector2(.5f, .5f);
                if(assetPath.EndsWith("_Color.png") && (assetPath.Contains("/Industrial/")||assetPath.Contains("/Props/")))
                {
                    importer.spritePixelsPerUnit=512f/5.5f;
                    var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                    settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.5f,.111839f);importer.SetTextureSettings(settings);
                    string layoutPath=assetPath.Replace("_Color.png","_Layout.json");
                    if(File.Exists(layoutPath))
                    {
                        var layout=JsonUtility.FromJson<SpriteLayout>(File.ReadAllText(layoutPath));
                        if(layout!=null&&layout.pixelsPerBlenderUnit>0){importer.spritePixelsPerUnit=layout.pixelsPerBlenderUnit;settings.spritePixelsPerUnit=layout.pixelsPerBlenderUnit;}
                        if(layout?.unitySpritePivotNormalized?.Length==2)
                        {
                            settings.spritePivot=new Vector2(layout.unitySpritePivotNormalized[0],layout.unitySpritePivotNormalized[1]);
                            importer.SetTextureSettings(settings);
                        }
                    }
                }
                if(assetPath.Contains("/Workers/")&&assetPath.EndsWith("_Color.png"))
                {
                    importer.spritePixelsPerUnit=256f/2.2f;
                    var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.5f,.0697f);importer.SetTextureSettings(settings);
                }
            }
        }

        static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom)
        {
            foreach(string path in imported.Concat(moved))
            {
                if(!path.StartsWith("Assets/DeepPressure/Art/",StringComparison.Ordinal)||
                    !(path.Contains("/Industrial/")||path.Contains("/Props/")||path.Contains("/Workers/")))continue;
                string color=path.EndsWith("_Normal.png",StringComparison.Ordinal)?path.Replace("_Normal.png","_Color.png"):
                    path.EndsWith("_AO.png",StringComparison.Ordinal)?path.Replace("_AO.png","_Color.png"):
                    path.EndsWith("_Layout.json",StringComparison.Ordinal)?path.Replace("_Layout.json","_Color.png"):path;
                if(!color.EndsWith("_Color.png",StringComparison.Ordinal)||!File.Exists(color))continue;
                pendingBindings.Add(color);
                if(path.EndsWith("_Layout.json",StringComparison.Ordinal))pendingLayouts.Add(color);
            }
            if(pendingBindings.Count>0){EditorApplication.delayCall-=BindPending;EditorApplication.delayCall+=BindPending;}
        }
        static void BindPending()
        {
            var paths=pendingBindings.ToArray();pendingBindings.Clear();
            foreach(string path in paths)
            {
                if(pendingLayouts.Remove(path))AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                BindTextureSet(path);
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
                BindTextureSet(asset);
            }
        }
        static void BindTextureSet(string colorPath)
        {
            string normalPath=colorPath.Replace("_Color.png","_Normal.png"),aoPath=colorPath.Replace("_Color.png","_AO.png");
            var color=AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath);
            var normal=AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);var ao=AssetDatabase.LoadAssetAtPath<Texture2D>(aoPath);
            if(color==null||normal==null||ao==null)return;
            if(color.width!=normal.width||color.height!=normal.height||color.width!=ao.width||color.height!=ao.height)
            {Debug.LogError("Color / Normal / AO canvas mismatch; secondary binding skipped: "+colorPath);return;}
            EnsureDataImport(normalPath,TextureImporterType.NormalMap);EnsureDataImport(aoPath,TextureImporterType.Default);
            var importer=AssetImporter.GetAtPath(colorPath) as TextureImporter;if(importer==null)return;
            var current=importer.secondarySpriteTextures??Array.Empty<SecondarySpriteTexture>();
            if(importer.sRGBTexture&&current.Any(x=>x.name=="_NormalMap"&&x.texture==normal)&&current.Any(x=>x.name=="_AOMap"&&x.texture==ao))return;
            // Preserve any independently authored light masks; AO is never written to _MaskTex.
            var bindings=current.Where(x=>x.name!="_NormalMap"&&x.name!="_AOMap").ToList();
            bindings.Add(new SecondarySpriteTexture{name="_NormalMap",texture=normal});bindings.Add(new SecondarySpriteTexture{name="_AOMap",texture=ao});
            importer.sRGBTexture=true;importer.secondarySpriteTextures=bindings.ToArray();importer.SaveAndReimport();
        }
        static void EnsureDataImport(string path,TextureImporterType type)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)return;
            if(importer.textureType==type&&!importer.sRGBTexture&&!importer.convertToNormalmap&&!importer.alphaIsTransparency)return;
            importer.textureType=type;importer.sRGBTexture=false;importer.convertToNormalmap=false;importer.alphaIsTransparency=false;importer.SaveAndReimport();
        }
    }
}

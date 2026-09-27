using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace DeepPressure.Editor
{
    public static class DeepEnvironmentBaker
    {
        public static void Bake(DeepPressureWorld world,Camera camera,Material lit,Material unlit,Sprite pixel)
        {
            string path="Assets/DeepPressure/Art/Utility/DustGlow.png";
            if(!File.Exists(path))
            {
                var t=new Texture2D(64,64,TextureFormat.RGBA32,false);
                for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=Vector2.Distance(new Vector2(x,y),Vector2.one*31.5f)/31.5f;t.SetPixel(x,y,new Color(1,1,1,Mathf.Exp(-d*d*10)*Mathf.Clamp01(1-d)));}
                t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            }
            var glow=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var ambience=new GameObject("08 • Authored atmosphere details");ambience.transform.SetParent(world.transform,false);
            for(int i=0;i<66;i++)
            {
                float x=5+(i*7.137f)%29,y=14+(i*3.813f)%16;
                if(world.levelDefinition!=null){x+=world.levelDefinition.starterBaseOffset.x;y+=world.levelDefinition.starterBaseOffset.y;}
                var cell=new Vector2Int((int)x,(int)y);if(world.GetTerrain(cell.x,cell.y)!=TerrainKind.Empty)continue;
                var go=new GameObject("Drifting dust");go.transform.SetParent(ambience.transform,false);go.transform.localPosition=new Vector3(x,y,0);
                var sr=go.AddComponent<SpriteRenderer>();sr.sprite=glow;sr.sharedMaterial=unlit;sr.sortingOrder=16;sr.color=new Color(.69f,.88f,.79f,.12f+(i%5)*.035f);
                go.transform.localScale=Vector3.one*(.05f+(i%4)*.02f)/glow.bounds.size.x;
                var motion=go.AddComponent<DeepDustMotion>();motion.phase=i*1.731f;motion.range=.3f+(i%3)*.18f;motion.speed=.12f;
            }
            for(int y=1;y<world.height-1;y++)
            {
                int start=-1;
                for(int x=0;x<=world.width;x++)
                {
                    bool edge=x<world.width&&world.GetTerrain(x,y)!=TerrainKind.Empty&&(world.GetTerrain(x,y+1)==TerrainKind.Empty||world.GetTerrain(x,y-1)==TerrainKind.Empty);
                    if(edge&&start<0)start=x;
                    if(!edge&&start>=0){AddShadow(ambience.transform,start,y,x-start);start=-1;}
                }
            }
        }
        static void AddShadow(Transform parent,int x,int y,int length)
        {
            var go=new GameObject("Terrain light occluder");go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(x,y,0);
            var shadow=go.AddComponent<ShadowCaster2D>();shadow.useRendererSilhouette=false;shadow.selfShadows=false;shadow.castsShadows=true;
            var so=new SerializedObject(shadow);var path=so.FindProperty("m_ShapePath");path.arraySize=4;
            Vector3[] v={new Vector3(.03f,.03f),new Vector3(.03f,.97f),new Vector3(length-.03f,.97f),new Vector3(length-.03f,.03f)};
            for(int i=0;i<4;i++)path.GetArrayElementAtIndex(i).vector3Value=v[i];
            so.FindProperty("m_ShapePathHash").intValue=(x+1)*92821+y*193+length;so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

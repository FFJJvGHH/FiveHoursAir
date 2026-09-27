using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepPressure.Editor
{
    /// <summary>Bakes persistent meshes and named visual attachments in edit mode. Call after routes
    /// and device sprites have been authored. The LineRenderer is retained as route authority.
    /// Re-baking is idempotent; each scene link owns a unique mesh asset.</summary>
    public static class DeepPipeVisualBaker
    {
        const string Folder="Assets/DeepPressure/Art/Pipes";
        static Sprite indicator,needle,rotor,dial;
        static Material pipeMaterial,spriteMaterial;

        [MenuItem("深压/工业视觉/烘焙实体管网与设备动效")]
        public static void BakeCurrent()
        {
            var world=UnityEngine.Object.FindObjectOfType<DeepPressureWorld>();
            if(world==null){Debug.LogWarning("请先生成或打开深压关卡。");return;}
            BakeAll(world);
        }

        public static void BakeAll(DeepPressureWorld world)
        {
            if(world==null)throw new ArgumentNullException(nameof(world));
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Pipe authoring runs in Edit mode only.");
            PrepareAssets();
            int count=0;
            foreach(var link in world.GetComponentsInChildren<GasLink>(true))
            {
                var line=link.GetComponent<LineRenderer>();
                if(line==null||line.positionCount<2)continue;
                var visual=link.GetComponent<DeepPipeVisual>();
                if(visual==null)visual=Undo.AddComponent<DeepPipeVisual>(link.gameObject);
                var geometry=Child(link.transform,"Baked • metallic pipe body");
                var filter=geometry.GetComponent<MeshFilter>();if(filter==null)filter=Undo.AddComponent<MeshFilter>(geometry.gameObject);
                var renderer=geometry.GetComponent<MeshRenderer>();if(renderer==null)renderer=Undo.AddComponent<MeshRenderer>(geometry.gameObject);
                renderer.sharedMaterial=pipeMaterial;renderer.sortingLayerID=line.sortingLayerID;renderer.sortingOrder=line.sortingOrder;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                if(filter.sharedMesh==null||string.IsNullOrEmpty(AssetDatabase.GetAssetPath(filter.sharedMesh)))
                {
                    var mesh=new Mesh{name="Industrial pipe • "+link.name};
                    string path=Folder+"/Pipe_"+GUID.Generate()+".asset";
                    AssetDatabase.CreateAsset(mesh,path);filter.sharedMesh=mesh;
                }
                visual.pipeMesh=filter;visual.pipeRenderer=renderer;
                visual.diameter=.24f;visual.elbowRadius=.36f;
                visual.gasColor=link.fromPort==GasOutputPort.OxygenProduct?new Color(.25f,.91f,.83f):link.fromPort==GasOutputPort.TailGas?new Color(.66f,.60f,.94f):new Color(.98f,.62f,.27f);
                visual.valveIndicator=SpriteChild(link.transform,"Baked • valve status lamp",indicator,Vector3.zero,.13f,line.sortingOrder+2);
                visual.RebuildGeometry();
                // Old flat square couplings are preserved but replaced visually by mesh flanges.
                foreach(Transform child in link.transform)
                    if(child.name.StartsWith("Pipe coupling",StringComparison.Ordinal))
                    {var old=child.GetComponent<Renderer>();if(old!=null)old.enabled=false;}
                line.enabled=false;
                EditorUtility.SetDirty(visual);EditorUtility.SetDirty(filter);EditorUtility.SetDirty(renderer);EditorUtility.SetDirty(line);
                count++;
            }
            foreach(var node in world.GetComponentsInChildren<GasNode>(true))BakeMachine(node);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            Debug.Log($"Baked {count} cylindrical industrial pipes. Routes remain editable; flow pulses follow actual simulated transfers.");
        }

        static void BakeMachine(GasNode node)
        {
            var housing=node.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(s=>s.sprite!=null&&s.sprite.name.EndsWith("_Color",StringComparison.Ordinal));
            if(housing==null)return;
            var motion=node.GetComponent<DeepMachineMotion>();if(motion==null)motion=Undo.AddComponent<DeepMachineMotion>(node.gameObject);
            motion.node=node;motion.housing=housing.transform;motion.housingRestPosition=housing.transform.localPosition;
            string name=housing.sprite.name;
            float scale=(512f/5.5f)/housing.sprite.pixelsPerUnit;
            Vector3 gauge,light;
            if(name.StartsWith("compressor",StringComparison.Ordinal))
            {
                gauge=Project(.7f,-.655f,1.53f)*scale;light=Project(.49f,-.67f,1.03f)*scale;
                var fan=SpriteChild(housing.transform,"Baked • operating cooling rotor",rotor,Project(-.83f,-.755f,1.2f)*scale,.53f*scale,housing.sortingOrder+2);
                motion.fan=fan.transform;motion.fanRestPosition=fan.transform.localPosition;
            }
            else if(name.StartsWith("separator",StringComparison.Ordinal))
            {gauge=Project(0,-.66f,2.41f)*scale;light=Project(0,-.65f,1.58f)*scale;}
            else
            {gauge=Project(-.42f,-.914f,2.64f)*scale;light=Project(-.15f,-.84f,2.30f)*scale;}
            // Cover the original baked needle with a clean dial before adding a live one.
            SpriteChild(housing.transform,"Baked • live pressure dial",dial,gauge,.33f*scale,housing.sortingOrder+2);
            var needleSprite=SpriteChild(housing.transform,"Baked • live pressure needle",needle,gauge,.28f*scale,housing.sortingOrder+3);
            motion.pressureNeedle=needleSprite.transform;motion.needleRestPosition=needleSprite.transform.localPosition;
            motion.statusLight=SpriteChild(housing.transform,"Baked • operating status lamp",indicator,light,.10f*scale,housing.sortingOrder+4);
            EditorUtility.SetDirty(motion);
        }
        static Vector3 Project(float x,float y,float z)
        {
            // Same fixed orthographic basis as Tools/Blender/build_industrial_assets.py.
            float length=Mathf.Sqrt(15*15+3.1f*3.1f);
            return new Vector3(x,(15*z+3.1f*y)/length,-.04f);
        }
        static Transform Child(Transform parent,string name)
        {
            var existing=parent.Find(name);if(existing!=null)return existing;
            var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Bake industrial visual");go.transform.SetParent(parent,false);return go.transform;
        }
        static SpriteRenderer SpriteChild(Transform parent,string name,Sprite sprite,Vector3 position,float size,int order)
        {
            var child=Child(parent,name);child.localPosition=position;child.localRotation=Quaternion.identity;
            var renderer=child.GetComponent<SpriteRenderer>();if(renderer==null)renderer=Undo.AddComponent<SpriteRenderer>(child.gameObject);
            renderer.sprite=sprite;renderer.sharedMaterial=spriteMaterial;renderer.sortingOrder=order;renderer.color=Color.white;
            child.localScale=Vector3.one*(size/Mathf.Max(.001f,sprite.bounds.size.x));return renderer;
        }
        static void PrepareAssets()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            pipeMaterial=LoadMaterial(Folder+"/IndustrialPipe.mat","DeepPressure/IndustrialPipe");
            spriteMaterial=LoadMaterial(Folder+"/InstrumentOverlay.mat","Universal Render Pipeline/2D/Sprite-Unlit-Default");
            indicator=Icon("Indicator",(x,y)=>
            {
                float radius=Mathf.Sqrt(x*x+y*y);
                if(radius>.96f)return Color.clear;
                float alpha=1-Mathf.SmoothStep(.35f,.98f,radius);
                return new Color(1,1,1,alpha);
            });
            dial=Icon("GaugeFace",(x,y)=>
            {
                float radius=Mathf.Sqrt(x*x+y*y);if(radius>.98f)return Color.clear;
                if(radius>.91f)return new Color(.20f,.34f,.33f,1);
                float angle=Mathf.Atan2(y,x);
                if(radius>.69f&&Mathf.Cos(angle*12)>.83f)return new Color(.18f,.28f,.27f,1);
                return new Color(.89f,.94f,.81f,1);
            });
            needle=Icon("Needle",(x,y)=>
            {
                float hub=Mathf.Sqrt(x*x+y*y);
                if(hub<.15f)return new Color(.10f,.18f,.19f,1);
                if(y>0&&y<.88f&&Mathf.Abs(x)<Mathf.Lerp(.08f,.018f,y/.88f))return new Color(.95f,.32f,.16f,1);
                return Color.clear;
            });
            rotor=Icon("Rotor",(x,y)=>
            {
                float radius=Mathf.Sqrt(x*x+y*y);if(radius>.95f)return Color.clear;
                if(radius>.84f)return new Color(.42f,.62f,.62f,.95f);
                if(radius<.16f)return new Color(.82f,.86f,.73f,1);
                float angle=Mathf.Atan2(y,x)+radius*.85f;
                if(Mathf.Cos(angle*5)>.32f&&radius<.77f)return new Color(.63f,.80f,.74f,.9f);
                return Color.clear;
            });
        }
        static Material LoadMaterial(string path,string shaderName)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader=Shader.Find(shaderName);if(shader==null)throw new InvalidOperationException("Missing shader: "+shaderName);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            else if(material.shader!=shader){material.shader=shader;EditorUtility.SetDirty(material);}
            return material;
        }
        static Sprite Icon(string name,Func<float,float,Color> pixel)
        {
            string path=Folder+"/"+name+".png";
            if(!File.Exists(path))
            {
                const int size=64;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)texture.SetPixel(x,y,pixel((x+.5f)/size*2-1,(y+.5f)/size*2-1));
                texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace DeepPressure.Editor
{
    public static class DeepPressureBuilder
    {
        public const string Root = "Assets/DeepPressure";
        public const string ScenePath = Root + "/Scenes/DeepPressureColony.unity";
        static DeepTerrainTile[] tiles;
        static Material lit, unlit;
        static Sprite pixel;

        [MenuItem("深压/一键生成可编辑示范关卡 %#F7", priority = 0)]
        public static void Generate()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode before authoring."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(false);
        }

        public static void Build(bool overwrite)
        {
            EnsureAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("DEEP PRESSURE • Authored Level");
            var world = root.AddComponent<DeepPressureWorld>();
            world.width = 64; world.height = 36; world.cellSize = 1;
            var grid = Child(root.transform, "01 • Terrain Grid"); grid.AddComponent<Grid>();
            world.background = MakeTilemap(grid.transform, "Background • visual only", -20);
            world.terrain = MakeTilemap(grid.transform, "Terrain • solid occupancy", 0);
            world.terrain.gameObject.AddComponent<TilemapCollider2D>();
            world.terrainKinds = new TerrainKind[64 * 36];
            for (int y = 0; y < 36; y++) for (int x = 0; x < 64; x++)
            {
                float strata=y+2.1f*Mathf.Sin(x*.135f)+1.25f*Mathf.Sin(x*.38f)+(Mathf.PerlinNoise(x*.11f,y*.09f)-.5f)*3.4f;
                if(x>39)strata+=2.8f;
                TerrainKind k=strata>29?TerrainKind.Soil:strata>21?TerrainKind.Sandstone:strata>10?TerrainKind.Shale:TerrainKind.Basalt;
                float lens=Mathf.Pow((x-47f)/12,2)+Mathf.Pow((y-17f)/3.5f,2);
                if(lens<.9f+Mathf.PerlinNoise(x*.22f,y*.25f)*.25f)k=TerrainKind.Sandstone;
                if(Mathf.Abs(y-(.37f*x+3.8f+Mathf.Sin(x*.4f)*.55f))<.6f&&y<23)k=TerrainKind.Basalt;
                Set(world, x, y, k);
                world.background.SetTile(new Vector3Int(x,y,0),tiles[(int)TerrainKind.Basalt]);
            }
            world.background.color=new Color(.27f,.35f,.43f,1);
            world.background.gameObject.AddComponent<DeepParallax>().cameraFollow=.025f;
            Carve(world, new RectInt(4, 24, 26, 8));
            Carve(world, new RectInt(5, 13, 31, 8));
            Carve(world, new RectInt(7, 3, 23, 7));
            Carve(world, new RectInt(31, 4, 3, 27));
            Carve(world, new RectInt(28, 26, 6, 3));
            Carve(world, new RectInt(28, 5, 5, 3));
            Carve(world, new RectInt(43, 11, 15, 9));
            Carve(world, new RectInt(45, 9, 11, 13));
            for (int x = 4; x < 30; x++) Set(world, x, 23, TerrainKind.Metal);
            for (int x = 5; x < 36; x++) if(x<31||x>33) Set(world, x, 12, TerrainKind.Metal);
            for (int x = 7; x < 30; x++) Set(world, x, 2, TerrainKind.Metal);
            for (int y=3;y<10;y++) Set(world,30,y,TerrainKind.Metal);

            var regions = Child(root.transform, "02 • Semantic Regions");
            Region(regions.transform, "habitat", "01  HABITAT", new RectInt(4, 24, 26, 8), 102, new Vector4(.21f,.78f,.01f,0));
            Region(regions.transform, "processing", "02  PROCESSING", new RectInt(5,13,31,8), 98, new Vector4(.2f,.78f,.02f,0));
            Region(regions.transform, "reservoir", "03  WET RESERVOIR", new RectInt(43,9,15,13), 600, new Vector4(.3f,.55f,.1f,.05f));
            Region(regions.transform, "archive", "04  ABANDONED STATION", new RectInt(7,3,23,7), 145, new Vector4(.08f,.62f,.3f,0));

            var decor = Child(root.transform, "03 • Modular Structure (visual only)");
            WallPanels(decor.transform, new RectInt(4,24,26,8));
            WallPanels(decor.transform, new RectInt(5,13,31,8));
            WallPanels(decor.transform, new RectInt(7,3,23,7));
            for (int y = 4; y < 31; y++)
            {
                Bar(decor.transform, "Ladder rung", new Vector2(32.5f,y+.5f), new Vector2(1.4f,.09f), new Color(.43f,.54f,.55f), 2);
            }
            for (int i = 0; i < 2; i++) Bar(decor.transform, "Ladder rail", new Vector2(32+i,17.5f), new Vector2(.09f,27), new Color(.38f,.46f,.47f), 2);
            Prop(decor.transform,"bunk",new Vector2(7.5f,24),.92f);
            Prop(decor.transform,"bunk",new Vector2(12,24),.92f);
            Prop(decor.transform,"planter",new Vector2(18,24),1);
            Prop(decor.transform,"console",new Vector2(25,24),1);
            Prop(decor.transform,"crate",new Vector2(6.4f,13),.75f);
            Prop(decor.transform,"crate",new Vector2(15.5f,13),.85f);
            Prop(decor.transform,"crate",new Vector2(17,13),.65f);
            Prop(decor.transform,"console",new Vector2(23,3),.9f);
            Prop(decor.transform,"crate",new Vector2(17,3),.9f);
            for(int x=9;x<29;x+=7){Prop(decor.transform,"vent",new Vector2(x,28.6f),.52f);Prop(decor.transform,"vent",new Vector2(x,18),.5f);}

            var plant = Child(root.transform, "04 • Explicit Gas Network");
            var source = Node(plant.transform,"WET FEED", "tank",new Vector2(50,14),GasNodeKind.Reservoir,600,30,new Vector4(.3f,.55f,.1f,.05f));
            var regulator = Node(plant.transform,"PRESSURE REGULATOR", "compressor",new Vector2(31,16),GasNodeKind.Regulator,30,1, new Vector4(.3f,.55f,.1f,.05f));
            regulator.targetPressureKPa=180;
            var separator=Node(plant.transform,"OXYGEN SEPARATOR", "separator",new Vector2(23,16),GasNodeKind.Separator,20,2,new Vector4(.3f,.55f,.1f,.05f));
            var product=Node(plant.transform,"OXYGEN BUFFER", "tank",new Vector2(10,17),GasNodeKind.Storage,10,8,new Vector4(1,0,0,0));
            var tail=Node(plant.transform,"TAIL GAS BUFFER", "tank",new Vector2(11,7),GasNodeKind.Storage,10,15,new Vector4(0,.8f,.15f,.05f));
            product.maxPressureKPa=160; tail.maxPressureKPa=100;
            Link(plant.transform,source,regulator,GasOutputPort.Mixed,new Color(.93f,.58f,.25f),new[]{new Vector3(50,15),new Vector3(50,18.5f),new Vector3(31,18.5f),new Vector3(31,17)});
            Link(plant.transform,regulator,separator,GasOutputPort.Mixed,new Color(.93f,.58f,.25f),new[]{new Vector3(31,16),new Vector3(23,16)});
            Link(plant.transform,separator,product,GasOutputPort.OxygenProduct,new Color(.32f,.84f,.79f),new[]{new Vector3(23,17),new Vector3(23,18.5f),new Vector3(10,18.5f),new Vector3(10,18)});
            Link(plant.transform,separator,tail,GasOutputPort.TailGas,new Color(.64f,.59f,.76f),new[]{new Vector3(23,15),new Vector3(23,11),new Vector3(29.8f,11),new Vector3(29.8f,6),new Vector3(11,6),new Vector3(11,7)});
            var simulator=root.AddComponent<GasNetworkSimulator>();
            var cameraObject=Child(root.transform,"05 • Camera");
            var camera=cameraObject.AddComponent<Camera>(); camera.tag="MainCamera";
            camera.orthographic=true; camera.orthographicSize=18.8f; camera.transform.position=new Vector3(29,18.4f,-20);
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.023f,.037f,.052f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=true;
            var lightRoot=Child(root.transform,"06 • HD2D Lighting");
            var ambient=Child(lightRoot.transform,"Ambient").AddComponent<Light2D>(); ambient.lightType=Light2D.LightType.Global;
            ambient.intensity=.42f; ambient.color=new Color(.53f,.64f,.76f);
            Lamp(lightRoot.transform,new Vector2(7.5f,29.9f),new Color(.92f,.78f,.53f),1.65f,6);
            Lamp(lightRoot.transform,new Vector2(18,29.9f),new Color(.55f,.91f,.84f),1.65f,7);
            Lamp(lightRoot.transform,new Vector2(27,29.9f),new Color(.86f,.78f,.6f),1.6f,5);
            Lamp(lightRoot.transform,new Vector2(9,19.7f),new Color(.48f,.84f,.94f),1.8f,6);
            Lamp(lightRoot.transform,new Vector2(23,19.7f),new Color(.96f,.70f,.39f),2.0f,7);
            Lamp(lightRoot.transform,new Vector2(33,19.7f),new Color(.69f,.90f,.78f),1.65f,5);
            Lamp(lightRoot.transform,new Vector2(48,18.8f),new Color(.49f,.72f,.94f),1.5f,8);
            Lamp(lightRoot.transform,new Vector2(17,8.6f),new Color(.58f,.68f,.94f),1.1f,7);
            for(int y=6;y<29;y+=7)Lamp(lightRoot.transform,new Vector2(33.6f,y),new Color(.47f,.75f,.79f),.8f,4);
            AddPostProcessing(root.transform);
            var hud=root.AddComponent<DeepPressureHUD>(); hud.world=world; hud.network=simulator; hud.viewCamera=camera;
            var atmosphere=Child(root.transform,"07 • Exploration & Atmosphere");
            var exploration=root.AddComponent<DeepExploration>();exploration.world=world;
            exploration.initialExploredAreas=new[]{new RectInt(1,11,38,24),new RectInt(30,4,6,9)};
            var fog=SpriteObject(atmosphere.transform,"Unknown-space fog",pixel,new Vector3(32,18,0),Color.white,32760);
            fog.transform.localScale=new Vector3(64/pixel.bounds.size.x,36/pixel.bounds.size.y,1);
            fog.sharedMaterial=Material("FogOfWar","DeepPressure/FogOfWar");exploration.fogRenderer=fog;
            var gas=SpriteObject(atmosphere.transform,"Gas atmosphere · sampled visualization",pixel,new Vector3(32,18,0),Color.white,15);
            gas.transform.localScale=fog.transform.localScale;gas.sharedMaterial=Material("GasAtmosphere","DeepPressure/GasAtmosphere");exploration.gasRenderer=gas;
            hud.gasRenderer=gas;
            world.SyncTerrainFromTilemap(); world.RebuildRooms();
            DeepColonyLevelBuilder.Setup(world,camera,lit);
            DeepPipeVisualBaker.BakeAll(world);
            DeepEnvironmentBaker.Bake(world,camera,lit,unlit,pixel);
            string savePath=overwrite?ScenePath:AssetDatabase.GenerateUniqueAssetPath(ScenePath);
            EditorSceneManager.SaveScene(scene,savePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(savePath,true)};
            Selection.activeGameObject=root;
            if (SceneView.lastActiveSceneView!=null)
            { SceneView.lastActiveSceneView.in2DMode=true; SceneView.lastActiveSceneView.LookAt(new Vector3(32,18,0),Quaternion.identity,38); }
            Debug.Log("Deep Pressure authored scene saved: "+savePath+". No map generation occurs in Play mode.");
        }

        public static void EnsureAssets()
        {
            foreach (string dir in new[]{"Scenes","Data/Terrain","Art/Terrain","Art/Utility","Materials"}) Directory.CreateDirectory(Root+"/"+dir);
            tiles = DeepTerrainArtBaker.Prepare();
            string whitePath=Root+"/Art/Utility/White.png";
            if(!File.Exists(whitePath))
            { var t=new Texture2D(4,4);t.SetPixels(Enumerable.Repeat(Color.white,16).ToArray());t.Apply();File.WriteAllBytes(whitePath,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);AssetDatabase.ImportAsset(whitePath,ImportAssetOptions.ForceSynchronousImport); }
            pixel=AssetDatabase.LoadAssetAtPath<Sprite>(whitePath);
            lit=Material("IndustrialLit", "DeepPressure/Sprite-Lit-AO");
            unlit=Material("Unlit", "Universal Render Pipeline/2D/Sprite-Unlit-Default");
            DeepPressureArtImporter.BindSecondaryTextures(); AssetDatabase.SaveAssets();
        }
        static Material Material(string name,string shader)
        {
            string p=Root+"/Materials/"+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(p);
            if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,p);}return m;
        }
        static GameObject Child(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go;}
        static Tilemap MakeTilemap(Transform parent,string name,int order)
        {var go=Child(parent,name);var map=go.AddComponent<Tilemap>();var r=go.AddComponent<TilemapRenderer>();r.sortingOrder=order;r.sharedMaterial=lit;return map;}
        static void Set(DeepPressureWorld w,int x,int y,TerrainKind kind){w.terrainKinds[y*w.width+x]=kind;w.terrain.SetTile(new Vector3Int(x,y,0),kind==TerrainKind.Empty?null:tiles[(int)kind]);}
        static void Carve(DeepPressureWorld w,RectInt r){foreach(var p in r.allPositionsWithin)Set(w,p.x,p.y,TerrainKind.Empty);}
        static void Region(Transform parent,string id,string name,RectInt bounds,float pressure,Vector4 composition)
        {var region=Child(parent,name).AddComponent<DeepPressureRegion>();region.stableId=id;region.displayName=name;region.bounds=bounds;region.initialPressureKPa=pressure;region.composition=composition;}
        static SpriteRenderer SpriteObject(Transform parent,string name,Sprite sprite,Vector3 position,Color color,int order)
        {var go=Child(parent,name);go.transform.position=position;var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.color=color;sr.sortingOrder=order;sr.sharedMaterial=lit;return sr;}
        static void Bar(Transform parent,string name,Vector2 at,Vector2 size,Color color,int order)
        {var sr=SpriteObject(parent,name,pixel,at,color,order);sr.sharedMaterial=unlit;sr.transform.localScale=new Vector3(size.x/pixel.bounds.size.x,size.y/pixel.bounds.size.y,1);}
        static void WallPanels(Transform parent,RectInt rect)
        {
            for(int x=rect.xMin;x<rect.xMax;x+=2)
            {
                Bar(parent,"Inset wall panel",new Vector2(x+1,rect.center.y),new Vector2(1.94f,rect.height),new Color(.045f,.085f,.105f,.84f),-10);
                Bar(parent,"Recessed wall rib",new Vector2(x,rect.center.y),new Vector2(.045f,rect.height),new Color(.12f,.18f,.21f),-9);
                for(float y=rect.yMin+.65f;y<rect.yMax;y+=1.5f)
                {
                    Bar(parent,"Wall seam",new Vector2(x+1,y),new Vector2(1.92f,.018f),new Color(.14f,.20f,.21f,.55f),-9);
                    for(int j=0;j<2;j++)Bar(parent,"Wall fixing",new Vector2(x+.16f+j*1.65f,y+.08f),new Vector2(.055f,.045f),new Color(.34f,.39f,.35f),-8);
                }
            }
            Bar(parent,"Ceiling beam shadow",new Vector2(rect.center.x,rect.yMax-.25f),new Vector2(rect.width,.32f),new Color(.025f,.055f,.066f),1);
            Bar(parent,"Ceiling beam lip",new Vector2(rect.center.x,rect.yMax-.1f),new Vector2(rect.width,.065f),new Color(.35f,.43f,.43f),2);
            for(int x=rect.xMin;x<rect.xMax;x++)
                Bar(parent,"Cable tray notch",new Vector2(x+.5f,rect.yMax-.32f),new Vector2(.08f,.19f),new Color(.22f,.29f,.3f),2);
        }
        static void Prop(Transform parent,string name,Vector2 floor,float scale)
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/Props/"+name+"_Color.png");
            if(sprite==null)return;var r=SpriteObject(parent,"Furnishing • "+name,sprite,floor,Color.white,4);r.transform.localScale=Vector3.one*scale;
        }
        static void Lamp(Transform parent,Vector2 p,Color tint,float intensity,float radius)
        {
            var fixture=Child(parent,"Wall lamp");fixture.transform.position=p;
            Bar(fixture.transform,"Lamp bracket",p+Vector2.up*.16f,new Vector2(1.15f,.26f),new Color(.08f,.13f,.16f),10);
            Bar(fixture.transform,"Lamp reflector",p+Vector2.up*.03f,new Vector2(.94f,.14f),new Color(.45f,.51f,.51f),11);
            Bar(fixture.transform,"Luminous lamp",p-Vector2.up*.035f,new Vector2(.75f,.065f),tint*2.3f,12);
            PointLight(fixture.transform,p+Vector2.down*.18f,tint,intensity,radius);
        }
        static void AddPostProcessing(Transform parent)
        {
            string path=Root+"/Data/LightingRefined.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(profile==null)
            {
                profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path);
                var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.22f);bloom.threshold.Override(.9f);bloom.scatter.Override(.55f);
                var vignette=profile.Add<Vignette>(true);vignette.intensity.Override(.18f);vignette.smoothness.Override(.65f);
                var color=profile.Add<ColorAdjustments>(true);color.postExposure.Override(.2f);color.contrast.Override(9);color.saturation.Override(-6);
                foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);
                AssetDatabase.SaveAssets();
            }
            var volume=Child(parent,"Depth grading").AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;
        }
        static void Label(Transform parent,string text,Vector2 at,float size,Color color)
        {var go=Child(parent,text);go.transform.position=at;var t=go.AddComponent<TextMesh>();t.text=text;t.fontSize=40;t.characterSize=size;t.anchor=TextAnchor.MiddleLeft;t.color=color;go.GetComponent<MeshRenderer>().sortingOrder=12;}
        static GasNode Node(Transform parent,string name,string asset,Vector2 position,GasNodeKind kind,float pressure,float volume,Vector4 composition)
        {
            position.y=kind==GasNodeKind.Reservoir?11:name=="TAIL GAS BUFFER"?5:15;
            var go=Child(parent,name);go.transform.position=position;
            var placement=go.AddComponent<DeepDevicePlacement>();
            var n=go.AddComponent<GasNode>();n.kind=kind;n.displayName=name;n.initialPressureKPa=pressure;n.volumeM3=volume;n.initialComposition=composition;n.maxPressureKPa=800;n.throughputMolPerSecond=2;
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/Industrial/"+asset+"_Color.png");
            if(sprite!=null){var r=SpriteObject(go.transform,"Visual • "+asset,sprite,position+Vector2.down*2,Color.white,7);r.transform.localScale=Vector3.one*.82f;r.flipX=true;}
            else {Bar(go.transform,"Placeholder • awaiting Blender",position,new Vector2(2.2f,2.8f),new Color(.43f,.66f,.62f),7);}
            string layoutPath=Root+"/Art/Industrial/"+asset+"_Layout.json";
            if(File.Exists(layoutPath))
            {
                var layout=JsonUtility.FromJson<VisualLayout>(File.ReadAllText(layoutPath));
                foreach(var anchor in layout.ports)
                {
                    var uv=anchor.spritePositionNormalized;
                    Vector2 offset=new Vector2(-(uv[0]-.5f)*5.5f*.82f,(uv[1]-.111839f)*5.5f*.82f-2);
                    if(anchor.name.Contains("INLET"))placement.inletOffset=offset;
                    else if(anchor.name.Contains("WASTE"))placement.tailOffset=offset;
                    else placement.productOffset=offset;
                }
            }
            return n;
        }
        static void Link(Transform parent,GasNode from,GasNode to,GasOutputPort port,Color color,Vector3[] points)
        {
            var fromPlacement=from.GetComponent<DeepDevicePlacement>();var toPlacement=to.GetComponent<DeepDevicePlacement>();
            Vector3 start=from.transform.TransformPoint(port==GasOutputPort.TailGas?fromPlacement.tailOffset:fromPlacement.productOffset);
            Vector3 end=to.transform.TransformPoint(toPlacement.inletOffset);
            if(points.Length==2)points=new[]{start,new Vector3(start.x,17.4f),new Vector3(end.x,17.4f),end};
            else {points[0]=start;points[1].x=start.x;points[points.Length-1]=end;points[points.Length-2].x=end.x;}
            var go=Child(parent,from.displayName+" → "+to.displayName);var link=go.AddComponent<GasLink>();link.from=from;link.to=to;link.fromPort=port;link.conductanceMolPerSecondPerKPa=.06f;link.maxFlowMolPerSecond=2;
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=unlit;line.useWorldSpace=true;line.positionCount=points.Length;line.SetPositions(points);line.widthMultiplier=.17f;line.startColor=line.endColor=color;line.sortingOrder=5;line.numCornerVertices=3;line.numCapVertices=3;
            go.AddComponent<DeepPipeRoute>().CaptureEndpoints();
        }
        static void PointLight(Transform parent,Vector2 p,Color color,float intensity,float radius)
        {var go=Child(parent,"Local work light");go.transform.position=new Vector3(p.x,p.y,-1);var l=go.AddComponent<Light2D>();l.lightType=Light2D.LightType.Point;l.color=color;l.intensity=intensity;l.pointLightOuterRadius=radius;l.pointLightInnerRadius=.6f;l.shadowIntensity=.65f;var so=new SerializedObject(l);so.FindProperty("m_NormalMapQuality").intValue=(int)Light2D.NormalMapQuality.Accurate;so.FindProperty("m_NormalMapDistance").floatValue=1.8f;so.ApplyModifiedPropertiesWithoutUndo();go.AddComponent<DeepLampPulse>();}
        [Serializable] class VisualLayout {public VisualAnchor[] ports;}
        [Serializable] class VisualAnchor {public string name;public float[] spritePositionNormalized;}
    }
}



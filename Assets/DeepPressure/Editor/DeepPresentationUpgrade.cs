using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepPressure.Editor
{
    /// <summary>Explicit, idempotent asset migration. Opaque feet define the baseline;
    /// PNG canvases and secondary normal/AO texture registration are preserved.</summary>
    public static class DeepPresentationUpgrade
    {
        const string Root="Assets/DeepPressure";
        static readonly Dictionary<Sprite,Rect> alphaBounds=new Dictionary<Sprite,Rect>();

        [MenuItem("深压/素材/修复底脚与人物细节")]
        public static void UpgradeCurrent()
        {
            ApplyToCatalogAndLoadedScenes(DeepCatalogBuilder.CreateOrUpdateCatalog());
            Debug.Log(ValidateCatalogGrounding(AssetDatabase.LoadAssetAtPath<DeepGameplayCatalog>(DeepCatalogBuilder.CatalogPath)));
        }

        public static void ApplyToCatalogAndLoadedScenes(DeepGameplayCatalog catalog)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before presentation migration.");
            alphaBounds.Clear();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BindDetailTextures();
            if(catalog!=null)foreach(var definition in catalog.buildings)GroundPrefab(definition);
            string workerPath=DeepCatalogBuilder.PrefabRoot+"/Worker.prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(workerPath)!=null)
            {
                var prefab=PrefabUtility.LoadPrefabContents(workerPath);
                try{foreach(var worker in prefab.GetComponentsInChildren<DeepWorkerPresentation>(true))UpgradeWorker(worker);PrefabUtility.SaveAsPrefabAsset(prefab,workerPath);}
                finally{PrefabUtility.UnloadPrefabContents(prefab);}
            }
            foreach(var world in UnityEngine.Object.FindObjectsOfType<DeepPressureWorld>())
            {
                foreach(var building in world.GetComponentsInChildren<DeepBuildingInstance>(true))GroundObject(building);
                foreach(var worker in world.GetComponentsInChildren<DeepWorkerPresentation>(true))UpgradeWorker(worker);
                foreach(var node in world.GetComponentsInChildren<GasNode>(true))
                    if(node.GetComponentInParent<DeepBuildingInstance>()==null)GroundLegacyNode(node,world);
                EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            }
            AssetDatabase.SaveAssets();
        }

        public static void GroundPrefab(DeepBuildingDefinition definition)
        {
            if(definition==null||definition.prefab==null)return;
            string path=AssetDatabase.GetAssetPath(definition.prefab);if(string.IsNullOrEmpty(path))return;
            var root=PrefabUtility.LoadPrefabContents(path);
            try{var instance=root.GetComponent<DeepBuildingInstance>();if(instance!=null){instance.definition=definition;GroundObject(instance);}PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }

        public static void GroundObject(DeepBuildingInstance building)
        {
            if(building==null||building.definition==null)return;
            building.RefreshOwnedComponents();var definition=building.definition;
            Transform visual=building.visualRoot;
            if(definition.role==DeepBuildingRole.Light){MountLamp(building);return;}
            if(!definition.requiresFloor||definition.role==DeepBuildingRole.Floor||visual==null||visual==building.transform)return;
            var art=visual.GetComponentsInChildren<SpriteRenderer>(true).Where(IsIllustration).ToArray();
            if(art.Length==0)return;
            Bounds bounds=Combined(art,building.transform);
            // Fit the visible family, including intentional stacked bins/control panels.
            float factor=Mathf.Min(definition.footprint.x*.91f/Mathf.Max(.01f,bounds.size.x),definition.footprint.y*.94f/Mathf.Max(.01f,bounds.size.y));
            if(Mathf.Abs(factor-1)>.0001f)visual.localScale*=factor;
            bounds=Combined(art,building.transform);
            Vector3 desired=new Vector3(definition.footprint.x*.5f-bounds.center.x,-bounds.min.y+.003f,0);
            visual.position+=building.transform.TransformVector(desired);
            // Feet are immobile; a dedicated small contact shadow belongs to the same object.
            ContactShadow(building.transform,definition.footprint.x*.83f,art.Min(x=>x.sortingOrder)-1);
            DifferentiateRole(building,art[0]);
            InstrumentMachine(building,art[0]);
            foreach(var motion in building.GetComponentsInChildren<DeepMachineMotion>(true))
                if(motion.housing!=null){motion.housingRestPosition=motion.housing.localPosition;EditorUtility.SetDirty(motion);}
            EditorUtility.SetDirty(visual);EditorUtility.SetDirty(building);
            if(PrefabUtility.IsPartOfPrefabInstance(building))PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
        }

        static void GroundLegacyNode(GasNode node,DeepPressureWorld world)
        {
            var placement=node.GetComponent<DeepDevicePlacement>();if(placement==null)return;
            var art=node.GetComponentsInChildren<SpriteRenderer>(true).Where(IsIllustration).ToArray();if(art.Length==0)return;
            var main=art[0];if(main.transform==node.transform)return;
            Rect local=OpaqueLocalBounds(main.sprite);
            float bottom=world.transform.InverseTransformPoint(main.transform.TransformPoint(new Vector3(local.center.x,local.yMin,0))).y;
            float target=placement.Bounds(world).yMin*world.cellSize+.003f*world.cellSize;
            Vector3 deltaWorld=world.transform.up*(target-bottom);
            main.transform.position+=deltaWorld;
            Vector3 delta=node.transform.InverseTransformVector(deltaWorld);
            placement.inletOffset+=(Vector2)delta;placement.productOffset+=(Vector2)delta;placement.tailOffset+=(Vector2)delta;
            foreach(var link in world.GetComponentsInChildren<GasLink>(true))
            {
                var line=link.GetComponent<LineRenderer>();if(line==null||line.positionCount<2)continue;
                if(link.from==node){Vector3 point=line.GetPosition(0)+(line.useWorldSpace?deltaWorld:line.transform.InverseTransformVector(deltaWorld));line.SetPosition(0,point);}
                if(link.to==node){int end=line.positionCount-1;Vector3 point=line.GetPosition(end)+(line.useWorldSpace?deltaWorld:line.transform.InverseTransformVector(deltaWorld));line.SetPosition(end,point);}
                var pipe=link.GetComponent<DeepPipeVisual>();if(pipe!=null)pipe.RebuildGeometry();
                EditorUtility.SetDirty(line);
            }
            var motion=node.GetComponent<DeepMachineMotion>();if(motion!=null&&motion.housing!=null){motion.housingRestPosition=motion.housing.localPosition;EditorUtility.SetDirty(motion);}
            EditorUtility.SetDirty(main.transform);EditorUtility.SetDirty(placement);
        }

        static void UpgradeWorker(DeepWorkerPresentation look)
        {
            var worker=look.GetComponent<DeepWorker>();if(worker==null||worker.visualRenderer==null)return;
            look.idleDetails=Frames("idle_detail",4);look.carrying=Frames("carry",4);look.climbing=Frames("climb",4);
            var frames=new List<Sprite>{look.idle};frames.AddRange(look.walking??Array.Empty<Sprite>());frames.AddRange(look.working??Array.Empty<Sprite>());
            frames.AddRange(look.idleDetails);frames.AddRange(look.carrying);frames.AddRange(look.climbing);
            look.frameAnchors=frames.Where(x=>x!=null).Distinct().Select(x=>new DeepWorkerPresentation.FrameAnchor{sprite=x,visibleFootY=OpaqueLocalBounds(x).yMin}).ToArray();
            look.authoredScale=worker.visualRenderer.transform.localScale;
            look.groundedFootLocalPosition=new Vector3(0,.003f,worker.visualRenderer.transform.localPosition.z);look.anchorsBaked=true;
            worker.visualRenderer.transform.localPosition=look.groundedFootLocalPosition-Vector3.up*look.Foot(worker.visualRenderer.sprite)*look.authoredScale.y;
            if(look.carriedCrate==null)
            {
                var go=new GameObject("Carried reserved materials");go.transform.SetParent(worker.transform,false);
                var sr=go.AddComponent<SpriteRenderer>();sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/Props/crate_Color.png");sr.sharedMaterial=worker.visualRenderer.sharedMaterial;sr.sortingOrder=worker.visualRenderer.sortingOrder+1;
                go.transform.localScale=Vector3.one*.19f;go.SetActive(false);look.carriedCrate=go.transform;
            }
            ContactShadow(worker.transform,.49f,worker.visualRenderer.sortingOrder-1);
            EditorUtility.SetDirty(look);EditorUtility.SetDirty(worker.visualRenderer.transform);
        }
        static Sprite[] Frames(string name,int count)=>Enumerable.Range(0,count).Select(i=>AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/Workers/engineer_"+name+i+"_Color.png")).ToArray();
        static void BindDetailTextures()
        {
            foreach(string kind in new[]{"idle_detail","carry","climb"})foreach(var sprite in Frames(kind,4))
            {
                if(sprite==null)continue;string path=AssetDatabase.GetAssetPath(sprite);
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;
                var normal=AssetDatabase.LoadAssetAtPath<Texture2D>(path.Replace("_Color.png","_Normal.png"));var ao=AssetDatabase.LoadAssetAtPath<Texture2D>(path.Replace("_Color.png","_AO.png"));
                if(normal==null||ao==null)throw new InvalidOperationException("Missing aligned worker maps: "+path);
                if(importer.secondarySpriteTextures.Any(x=>x.name=="_NormalMap"&&x.texture==normal)&&importer.secondarySpriteTextures.Any(x=>x.name=="_AOMap"&&x.texture==ao))continue;
                importer.secondarySpriteTextures=new[]{new SecondarySpriteTexture{name="_NormalMap",texture=normal},new SecondarySpriteTexture{name="_AOMap",texture=ao}};importer.SaveAndReimport();
            }
        }

        static void MountLamp(DeepBuildingInstance building)
        {
            Transform visual=building.visualRoot??building.transform;
            var fixture=visual.Find("Fixture");
            // Catalog lights are explicitly ceiling mounted. Older wall lamps retain their
            // authored placement, and receive a wall plate instead of being pulled to a floor.
            bool ceiling=fixture!=null;
            Vector2 center=ceiling?new Vector2(.5f,.90f):new Vector2(.5f,.72f);
            Strip(visual,"Mount • structural bracket",center,ceiling?new Vector2(.12f,.20f):new Vector2(.22f,.30f),new Color(.19f,.28f,.31f),9);
            if(ceiling)Strip(visual,"Mount • ceiling plate",new Vector2(.5f,.985f),new Vector2(.32f,.03f),new Color(.40f,.48f,.49f),9);
        }
        static void DifferentiateRole(DeepBuildingInstance building,SpriteRenderer main)
        {
            string id=building.definition.id;Color color;
            if(id=="oxygen_tank"||id=="supply_vent")color=new Color(.35f,.90f,.81f);
            else if(id=="waste_tank"||id=="exhaust_vent")color=new Color(.96f,.64f,.30f);
            else if(id=="battery")color=new Color(.93f,.83f,.39f);
            else if(id=="intake_pump")color=new Color(.47f,.77f,.94f);
            else if(id=="co2_scrubber")color=new Color(.67f,.85f,.47f);
            else return;
            float width=building.definition.footprint.x,height=building.definition.footprint.y;
            Strip(building.visualRoot,"Role • service band",new Vector2(width*.5f,height*.58f),new Vector2(width*.34f,.055f),color,main.sortingOrder+5,building.transform);
            if(id=="battery")for(int i=0;i<3;i++)Strip(building.visualRoot,"Charge marking "+i,new Vector2(width*(.40f+i*.10f),height*.44f),new Vector2(width*.065f,height*.15f),color,main.sortingOrder+5,building.transform);
        }
        static void InstrumentMachine(DeepBuildingInstance building,SpriteRenderer main)
        {
            var definition=building.definition;
            if(definition.powerRequired<=0&&definition.powerGenerated<=0&&building.GetComponent<GasNode>()==null&&definition.id!="battery")return;
            var motion=building.GetComponent<DeepMachineMotion>();if(motion==null)motion=building.gameObject.AddComponent<DeepMachineMotion>();
            motion.node=building.GetComponent<GasNode>();motion.housing=main.transform;motion.housingRestPosition=main.transform.localPosition;
            float unit=(512f/5.5f)/main.sprite.pixelsPerUnit;
            if(main.sprite.name.StartsWith("compressor",StringComparison.Ordinal))
            {
                var rotor=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/Pipes/Rotor.png");
                if(rotor!=null)
                {
                    var fan=Instrument(main.transform,"Baked • operating cooling rotor",rotor,Project(-.83f,-.755f,1.2f)*unit,.53f*unit,main.sortingOrder+2);
                    motion.fan=fan.transform;motion.fanRestPosition=fan.transform.localPosition;
                }
                var lamp=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/Pipes/Indicator.png");
                if(lamp!=null)motion.statusLight=Instrument(main.transform,"Baked • operating status lamp",lamp,Project(.49f,-.67f,1.03f)*unit,.10f*unit,main.sortingOrder+4);
            }
            else if(motion.statusLight==null)
            {
                var lamp=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/Pipes/Indicator.png");
                if(lamp!=null){Rect b=OpaqueLocalBounds(main.sprite);motion.statusLight=Instrument(main.transform,"Baked • operating status lamp",lamp,new Vector3(b.center.x+b.width*.25f,b.yMin+b.height*.52f,-.04f),.10f*unit,main.sortingOrder+4);}
            }
            if(definition.id=="battery")motion.chargeSegments=Enumerable.Range(0,3).Select(i=>building.visualRoot.Find("Charge marking "+i)?.GetComponent<SpriteRenderer>()).Where(x=>x!=null).ToArray();
            EditorUtility.SetDirty(motion);
        }
        static Vector3 Project(float x,float y,float z)=>new Vector3(x,(15*z+3.1f*y)/Mathf.Sqrt(15*15+3.1f*3.1f),-.04f);
        static SpriteRenderer Instrument(Transform parent,string name,Sprite sprite,Vector3 position,float size,int order)
        {
            var child=parent.Find(name);if(child==null){child=new GameObject(name).transform;child.SetParent(parent,false);}
            child.localPosition=position;child.localScale=Vector3.one*(size/Mathf.Max(.001f,sprite.bounds.size.x));
            var renderer=child.GetComponent<SpriteRenderer>();if(renderer==null)renderer=child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite=sprite;renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Art/Pipes/InstrumentOverlay.mat");renderer.sortingOrder=order;renderer.color=Color.white;return renderer;
        }
        static void ContactShadow(Transform root,float width,int order)
        {Strip(root,"Contact • grounded shadow",new Vector2(root.GetComponent<DeepWorker>()!=null?0:width/.83f*.5f,.013f),new Vector2(width,.035f),new Color(.035f,.07f,.08f,.34f),order);}
        static void Strip(Transform parent,string name,Vector2 center,Vector2 size,Color color,int order,Transform basis=null)
        {
            var child=parent.Find(name);if(child==null){child=new GameObject(name).transform;child.SetParent(parent,false);}
            var renderer=child.GetComponent<SpriteRenderer>();if(renderer==null)renderer=child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/Utility/White.png");if(renderer.sprite==null)return;
            renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Unlit.mat");renderer.color=color;renderer.sortingOrder=order;
            Vector3 desired=new Vector3(center.x,center.y,-.01f);child.position=basis==null?parent.TransformPoint(desired):basis.TransformPoint(desired);
            Vector3 scale=new Vector3(size.x/renderer.sprite.bounds.size.x,size.y/renderer.sprite.bounds.size.y,1);
            if(basis!=null){Vector3 ratio=basis.lossyScale;Vector3 p=parent.lossyScale;scale.x*=ratio.x/p.x;scale.y*=ratio.y/p.y;}
            child.localScale=scale;EditorUtility.SetDirty(child);EditorUtility.SetDirty(renderer);
        }

        static bool IsIllustration(SpriteRenderer renderer)=>renderer.sprite!=null&&renderer.sprite.name.EndsWith("_Color",StringComparison.Ordinal);
        static Bounds Combined(SpriteRenderer[] renderers,Transform basis)
        {
            bool first=true;Bounds bounds=default;
            foreach(var renderer in renderers)
            {
                Rect rect=OpaqueLocalBounds(renderer.sprite);
                foreach(Vector2 p in new[]{new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMin),new Vector2(rect.xMin,rect.yMax),new Vector2(rect.xMax,rect.yMax)})
                {
                    Vector3 local=new Vector3(renderer.flipX?-p.x:p.x,renderer.flipY?-p.y:p.y,0);
                    Vector3 point=basis.InverseTransformPoint(renderer.transform.TransformPoint(local));
                    if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }
        public static Rect OpaqueLocalBounds(Sprite sprite)
        {
            if(alphaBounds.TryGetValue(sprite,out Rect result))return result;
            string path=AssetDatabase.GetAssetPath(sprite);var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!texture.LoadImage(File.ReadAllBytes(path),false))throw new InvalidOperationException("Cannot inspect sprite alpha: "+path);
                var pixels=texture.GetPixels32();Rect rect=sprite.rect;int x0=Mathf.RoundToInt(rect.x),y0=Mathf.RoundToInt(rect.y),x1=Mathf.RoundToInt(rect.xMax),y1=Mathf.RoundToInt(rect.yMax);
                int minX=x1,minY=y1,maxX=x0,maxY=y0;
                for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)if(pixels[y*texture.width+x].a>=26){minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x+1);maxY=Mathf.Max(maxY,y+1);}
                if(maxX<=minX||maxY<=minY)throw new InvalidOperationException("Empty sprite alpha: "+path);
                float ppu=sprite.pixelsPerUnit;result=Rect.MinMaxRect((minX-x0-sprite.pivot.x)/ppu,(minY-y0-sprite.pivot.y)/ppu,(maxX-x0-sprite.pivot.x)/ppu,(maxY-y0-sprite.pivot.y)/ppu);alphaBounds[sprite]=result;return result;
            }
            finally{UnityEngine.Object.DestroyImmediate(texture);}
        }

        public static string ValidateCatalogGrounding(DeepGameplayCatalog catalog)
        {
            var report=new StringBuilder("Presentation validation\n");int checkedBuildings=0;
            foreach(var definition in catalog.buildings)
            {
                if(definition==null||definition.prefab==null||!definition.requiresFloor||definition.role==DeepBuildingRole.Floor)continue;
                var art=definition.prefab.GetComponentsInChildren<SpriteRenderer>(true).Where(IsIllustration).ToArray();if(art.Length==0)continue;
                var bounds=Combined(art,definition.prefab.transform);float delta=bounds.min.y;
                if(Mathf.Abs(delta)>.02f)throw new InvalidOperationException(definition.id+" visible feet offset "+delta.ToString("F4")+" cells");
                checkedBuildings++;report.AppendLine("PASS "+definition.id+": visible feet "+delta.ToString("F4")+" cells");
            }
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(DeepCatalogBuilder.PrefabRoot+"/Worker.prefab");
            if(prefab!=null)
            {
                var look=prefab.GetComponent<DeepWorkerPresentation>();
                if(look==null||!look.anchorsBaked||look.idleDetails.Any(x=>x==null)||look.carrying.Any(x=>x==null)||look.climbing.Any(x=>x==null))throw new InvalidOperationException("Worker detail frames/anchors missing");
                foreach(var frame in look.frameAnchors)
                {
                    float min=OpaqueLocalBounds(frame.sprite).yMin;
                    foreach(float breath in new[]{.9965f,1f,1.0035f})
                    {
                        float y=look.groundedFootLocalPosition.y-frame.visibleFootY*look.authoredScale.y*breath+min*look.authoredScale.y*breath;
                        if(Mathf.Abs(y)>.02f)throw new InvalidOperationException("Unstable boot baseline "+frame.sprite.name);
                    }
                }
                report.AppendLine("PASS worker: "+look.frameAnchors.Length+" opaque frame anchors; idle/breathing baseline stable within .02 cells.");
            }
            report.AppendLine("Checked "+checkedBuildings+" grounded prefabs; source image and normal/AO UVs unchanged.");return report.ToString();
        }
    }
}

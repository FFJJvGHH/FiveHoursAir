using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
            BindDetailTextures();DeepPressureArtImporter.BindSecondaryTextures();
            var sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/IndustrialLit.mat");
            if(sharedMaterial!=null)
            {
                sharedMaterial.SetFloat("_AOIntensity",.28f);sharedMaterial.SetFloat("_PaletteSaturation",.82f);
                sharedMaterial.SetFloat("_MinimumLight",.12f);sharedMaterial.SetColor("_PaletteTint",DeepArtPalette.MachineTint);EditorUtility.SetDirty(sharedMaterial);
                string workerMaterialPath=Root+"/Materials/WorkerLit.mat";
                var workerMaterial=AssetDatabase.LoadAssetAtPath<Material>(workerMaterialPath);
                if(workerMaterial==null){workerMaterial=new Material(sharedMaterial){name="WorkerLit"};AssetDatabase.CreateAsset(workerMaterial,workerMaterialPath);}
                workerMaterial.SetFloat("_AOIntensity",.20f);workerMaterial.SetFloat("_PaletteSaturation",.91f);
                workerMaterial.SetFloat("_MinimumLight",.38f);workerMaterial.SetColor("_PaletteTint",new Color(1,.985f,.94f));EditorUtility.SetDirty(workerMaterial);
            }
            var terrainMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Art/TerrainV2/TerrainHD2D.mat");
            if(terrainMaterial!=null){terrainMaterial.SetFloat("_AOIntensity",.32f);terrainMaterial.SetFloat("_PaletteSaturation",.94f);EditorUtility.SetDirty(terrainMaterial);}
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
                HarmonizeWorldLighting(world);
                foreach(var building in world.GetComponentsInChildren<DeepBuildingInstance>(true))GroundObject(building);
                foreach(var worker in world.GetComponentsInChildren<DeepWorkerPresentation>(true))UpgradeWorker(worker);
                foreach(var node in world.GetComponentsInChildren<GasNode>(true))
                    if(node.GetComponentInParent<DeepBuildingInstance>()==null)GroundLegacyNode(node,world);
                GroundFurnishings(world);
                EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            }
            AssetDatabase.SaveAssets();
        }

        static void HarmonizeWorldLighting(DeepPressureWorld world)
        {
            // Neutral fill separates foreground actors from a quieter blue-grey cavity.
            if(world.background!=null){world.background.color=new Color(.32f,.38f,.39f);EditorUtility.SetDirty(world.background);}
            foreach(var light in world.GetComponentsInChildren<Light2D>(true))
            {
                if(light.lightType==Light2D.LightType.Global){light.color=DeepArtPalette.AmbientLight;light.intensity=.56f;}
                else if(light.GetComponentInParent<DeepBuildingInstance>()==null){light.color=DeepArtPalette.WorkLight;light.intensity=Mathf.Min(1,light.intensity);}
                EditorUtility.SetDirty(light);
            }
            foreach(var volume in world.GetComponentsInChildren<Volume>(true))
            {
                var profile=volume.sharedProfile;if(profile==null)continue;
                if(profile.TryGet<ColorAdjustments>(out var grade)){grade.postExposure.Override(.10f);grade.contrast.Override(3);grade.saturation.Override(-3);EditorUtility.SetDirty(grade);}
                if(profile.TryGet<Bloom>(out var bloom)){bloom.intensity.Override(.10f);bloom.threshold.Override(1.1f);EditorUtility.SetDirty(bloom);}
                if(profile.TryGet<Vignette>(out var vignette)){vignette.intensity.Override(.12f);EditorUtility.SetDirty(vignette);}
                EditorUtility.SetDirty(profile);
            }
            var palette=world.GetComponent<DeepTerrainMaterialField>();if(palette!=null){palette.RefreshField();EditorUtility.SetDirty(palette);}
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
            foreach(var light in building.lights)
            {
                light.color=DeepArtPalette.WorkLight;light.intensity=definition.id=="printing_pod"?.30f:.90f;
                if(definition.id=="printing_pod")light.pointLightOuterRadius=5;
                EditorUtility.SetDirty(light);
            }
            if(definition.id=="printing_pod"&&visual!=null)
            {
                var chamber=visual.Find("Bioprint chamber")?.GetComponent<SpriteRenderer>();
                if(chamber!=null){chamber.color=new Color(.57f,.73f,.65f,.16f);EditorUtility.SetDirty(chamber);}
            }
            if(definition.role==DeepBuildingRole.Storage&&definition.id!="printing_pod"&&visual!=null)
            {
                // This was an accidentally generated second warehouse, not part of the crate image.
                var duplicate=visual.Find("Stacked bin");
                if(duplicate!=null)
                {
                    if(PrefabUtility.IsPartOfPrefabInstance(duplicate))duplicate.gameObject.SetActive(false);
                    else UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
                }
            }
            if(definition.role==DeepBuildingRole.Light){MountLamp(building);return;}
            if(definition.role==DeepBuildingRole.Vent){MountWallVent(building);return;}
            if(!definition.requiresFloor||definition.role==DeepBuildingRole.Floor||visual==null||visual==building.transform)return;
            var art=visual.GetComponentsInChildren<SpriteRenderer>(true).Where(IsIllustration).ToArray();
            if(art.Length==0)return;
            Bounds bounds=Combined(art,building.transform);
            // Fit the visible machine and its attached controls as one grounded object.
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
            Vector3 deltaWorld=world.transform.TransformVector(Vector3.up*(target-bottom));
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

        /// <summary>Only known floor furniture is eligible. Wall vents, lamps, machinery
        /// attachments, carried parcels and shared decorative containers are never shifted.</summary>
        static void GroundFurnishings(DeepPressureWorld world)
        {
            foreach(var renderer in world.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if(!IsFloorFurnishing(renderer))continue;
                Bounds bounds=Combined(new[]{renderer},world.transform);
                if(!FindNearbySupport(world,bounds,out Vector2Int support))
                {
                    Debug.LogWarning("No nearby solid floor for authored furnishing: "+renderer.name,renderer);continue;
                }
                float target=(support.y+1)*world.cellSize+.003f*world.cellSize;
                renderer.transform.position+=world.transform.TransformVector(Vector3.up*(target-bounds.min.y));
                var mount=renderer.GetComponent<DeepFurnishingMount>();if(mount==null)mount=renderer.gameObject.AddComponent<DeepFurnishingMount>();
                mount.artwork=renderer;mount.supportCell=support;
                Rect local=OpaqueLocalBounds(renderer.sprite);mount.visibleFootLocalY=local.yMin;
                Strip(renderer.transform,"Contact • furnishing shadow",new Vector2(local.center.x,local.yMin+.01f),new Vector2(local.width*.82f,.035f),new Color(.035f,.07f,.08f,.34f),renderer.sortingOrder-1);
                EditorUtility.SetDirty(mount);EditorUtility.SetDirty(renderer.transform);
                if(PrefabUtility.IsPartOfPrefabInstance(renderer))PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.transform);
            }
        }
        static bool IsFloorFurnishing(SpriteRenderer renderer)
        {
            if(renderer==null||renderer.sprite==null||renderer.GetComponentInParent<DeepBuildingInstance>()!=null||renderer.GetComponentInParent<DeepWorker>()!=null||renderer.GetComponentInParent<GasNode>()!=null)return false;
            string path=AssetDatabase.GetAssetPath(renderer.sprite);
            if(!path.StartsWith(Root+"/Art/Props/",StringComparison.Ordinal))return false;
            string name=Path.GetFileNameWithoutExtension(path);
            return name=="bunk_Color"||name=="planter_Color"||name=="console_Color"||name=="crate_Color";
        }
        static bool FindNearbySupport(DeepPressureWorld world,Bounds bounds,out Vector2Int support)
        {
            support=default;float best=float.PositiveInfinity,cell=world.cellSize;
            int x=Mathf.FloorToInt(bounds.center.x/cell),nearY=Mathf.FloorToInt(bounds.min.y/cell);
            // A bounded local repair, never teleport an intentionally placed object across floors.
            for(int y=nearY;y>=nearY-2;y--)
            {
                var at=new Vector2Int(x,y);
                if(!world.IsInside(at)||world.GetTerrain(x,y)==TerrainKind.Empty||world.GetTerrain(x,y+1)!=TerrainKind.Empty)continue;
                float distance=(y+1)*cell-bounds.min.y;
                if(distance>cell*.25f||distance< -cell*1.25f||Mathf.Abs(distance)>=best)continue;
                support=at;best=Mathf.Abs(distance);
            }
            return !float.IsPositiveInfinity(best);
        }

        static void UpgradeWorker(DeepWorkerPresentation look)
        {
            var worker=look.GetComponent<DeepWorker>();if(worker==null||worker.visualRenderer==null)return;
            var workerMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/WorkerLit.mat");
            if(workerMaterial!=null){worker.visualRenderer.sharedMaterial=workerMaterial;EditorUtility.SetDirty(worker.visualRenderer);}
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
        static void MountWallVent(DeepBuildingInstance building)
        {
            var visual=building.visualRoot;if(visual==null||visual==building.transform)return;
            var art=visual.GetComponentsInChildren<SpriteRenderer>(true).Where(IsIllustration).ToArray();if(art.Length==0)return;
            Bounds bounds=Combined(art,building.transform);
            float factor=Mathf.Min(.82f/Mathf.Max(.01f,bounds.size.x),.78f/Mathf.Max(.01f,bounds.size.y));visual.localScale*=factor;
            bounds=Combined(art,building.transform);visual.position+=building.transform.TransformVector(new Vector3(.5f-bounds.center.x,.53f-bounds.center.y,0));
            for(int i=0;i<4;i++)Strip(visual,"Mount • vent bolt "+i,new Vector2(i%2==0?.14f:.86f,i<2?.22f:.84f),new Vector2(.045f,.045f),new Color(.71f,.78f,.75f),art[0].sortingOrder+3,building.transform);
            DifferentiateRole(building,art[0]);InstrumentMachine(building,art[0]);EditorUtility.SetDirty(visual);
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
            bool workstation=definition.role==DeepBuildingRole.Fabricator||definition.role==DeepBuildingRole.Research;
            if(!workstation&&definition.role!=DeepBuildingRole.Storage&&definition.id!="oxygen_diffuser"&&definition.powerRequired<=0&&definition.powerGenerated<=0&&building.GetComponent<GasNode>()==null&&definition.id!="battery")return;
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
            if(motion.node!=null&&(main.sprite.name.StartsWith("compressor",StringComparison.Ordinal)||main.sprite.name.StartsWith("separator",StringComparison.Ordinal)||main.sprite.name.StartsWith("tank",StringComparison.Ordinal)))
            {
                Vector3 gauge=main.sprite.name.StartsWith("compressor",StringComparison.Ordinal)?Project(.7f,-.655f,1.53f):
                    main.sprite.name.StartsWith("separator",StringComparison.Ordinal)?Project(0,-.66f,2.41f):Project(-.42f,-.914f,2.64f);
                var dial=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/Pipes/GaugeFace.png");
                var needle=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/Pipes/Needle.png");
                if(dial!=null&&needle!=null)
                {
                    Instrument(main.transform,"Baked • live pressure dial",dial,gauge*unit,.33f*unit,main.sortingOrder+2);
                    var pointer=Instrument(main.transform,"Baked • live pressure needle",needle,gauge*unit,.28f*unit,main.sortingOrder+3);
                    motion.pressureNeedle=pointer.transform;motion.needleRestPosition=pointer.transform.localPosition;
                }
            }
            if(definition.id=="battery")motion.chargeSegments=Enumerable.Range(0,3).Select(i=>building.visualRoot.Find("Charge marking "+i)?.GetComponent<SpriteRenderer>()).Where(x=>x!=null).ToArray();
            var scans=new List<DeepMachineMotion.MovingDetail>();
            foreach(var console in building.visualRoot.GetComponentsInChildren<SpriteRenderer>(true).Where(x=>x.sprite!=null&&x.sprite.name=="console_Color"))
            {
                // Positions reference the existing 512 px Color canvas; all maps keep the same UVs.
                for(int i=0;i<2;i++)
                {
                    float ppu=console.sprite.pixelsPerUnit;
                    Vector2 center=(new Vector2(i==0?176:290,251)-console.sprite.pivot)/ppu;
                    string name="Motion • screen scan "+i;
                    Strip(console.transform,name,center,new Vector2(74/ppu,2.5f/ppu),new Color(.48f,.88f,.77f,.5f),console.sortingOrder+3);
                    var scan=console.transform.Find(name).GetComponent<SpriteRenderer>();scan.enabled=false;
                    scans.Add(new DeepMachineMotion.MovingDetail{renderer=scan,restPosition=scan.transform.localPosition,travel=36/ppu,phase=i*.35f});
                }
            }
            if(definition.id=="printing_pod")for(int i=0;i<3;i++)
            {
                var scan=building.visualRoot.Find("Chamber scan "+i)?.GetComponent<SpriteRenderer>();
                if(scan!=null)scans.Add(new DeepMachineMotion.MovingDetail{renderer=scan,restPosition=scan.transform.localPosition,travel=.28f,phase=i*.3f,opacity=.35f});
            }
            motion.screenScans=scans.ToArray();
            if(definition.id=="oxygen_diffuser")
            {
                var bubble=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/Pipes/Indicator.png");
                var details=new List<DeepMachineMotion.MovingDetail>();Rect bounds=OpaqueLocalBounds(main.sprite);
                if(bubble!=null)for(int i=0;i<4;i++)
                {
                    var sprite=Instrument(main.transform,"Motion • oxygen bubble "+i,bubble,new Vector3(bounds.xMin+bounds.width*(.25f+i*.17f),bounds.yMin+bounds.height*.53f,-.05f),.055f*unit,main.sortingOrder+3);
                    sprite.enabled=false;details.Add(new DeepMachineMotion.MovingDetail{renderer=sprite,restPosition=sprite.transform.localPosition,travel=bounds.height*.58f,phase=i*.25f});
                }
                motion.oxygenBubbles=details.ToArray();
            }
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

        static bool IsIllustration(SpriteRenderer renderer)=>renderer.gameObject.activeSelf&&renderer.sprite!=null&&renderer.sprite.name.EndsWith("_Color",StringComparison.Ordinal);
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
            int furnished=0;
            foreach(var world in UnityEngine.Object.FindObjectsOfType<DeepPressureWorld>())
                foreach(var renderer in world.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if(!IsFloorFurnishing(renderer))continue;
                    var mount=renderer.GetComponent<DeepFurnishingMount>();
                    if(mount==null||mount.artwork!=renderer)throw new InvalidOperationException("Authored furnishing lacks whole-object mounting: "+renderer.name);
                    Bounds bounds=Combined(new[]{renderer},world.transform);
                    float delta=(bounds.min.y-(mount.supportCell.y+1)*world.cellSize)/world.cellSize;
                    if(world.GetTerrain(mount.supportCell.x,mount.supportCell.y)==TerrainKind.Empty||Mathf.Abs(delta)>.02f)
                        throw new InvalidOperationException("Authored furnishing feet have no contact: "+renderer.name+" / "+delta.ToString("F4")+" cells");
                    report.AppendLine("PASS "+renderer.name+" at "+mount.supportCell+": visible feet "+delta.ToString("F4")+" cells");furnished++;
                }
            report.AppendLine("Checked "+checkedBuildings+" grounded prefabs and "+furnished+" authored floor furnishings; source image and normal/AO UVs unchanged.");return report.ToString();
        }

        public static string ValidateMachinePresentation(DeepGameplayCatalog catalog)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Run machine validation outside Play.");
            int checkedCount=0;
            foreach(var definition in catalog.buildings)
            {
                if(definition==null||definition.prefab==null)continue;
                var art=definition.prefab.GetComponentsInChildren<SpriteRenderer>(true).Where(IsIllustration).ToArray();
                if(definition.role==DeepBuildingRole.Storage&&definition.id!="printing_pod"&&art.Count(x=>x.sprite.name=="crate_Color")!=1)
                    throw new InvalidOperationException(definition.id+" must render exactly one warehouse crate.");
                bool bench=definition.role==DeepBuildingRole.Fabricator||definition.role==DeepBuildingRole.Research;
                if(!bench&&definition.id!="oxygen_diffuser")continue;
                var motion=definition.prefab.GetComponent<DeepMachineMotion>();
                if(motion==null||motion.statusLight==null)throw new InvalidOperationException(definition.id+" lacks operating feedback.");
                if(bench&&(motion.screenScans.Length<2||motion.screenScans.Any(x=>x.renderer==null)))throw new InvalidOperationException(definition.id+" has no live workstation screens.");
                if(definition.id=="oxygen_diffuser"&&(motion.oxygenBubbles.Length!=4||motion.oxygenBubbles.Any(x=>x.renderer==null)))throw new InvalidOperationException("Diffuser lacks flow-driven oxygen bubbles.");
                checkedCount++;
            }
            var preview=EditorSceneManager.NewPreviewScene();var root=new GameObject("Machine motion validation");root.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,preview);
            var definitionFixture=ScriptableObject.CreateInstance<DeepBuildingDefinition>();
            var stockItem=ScriptableObject.CreateInstance<DeepItemDefinition>();stockItem.id="art_validation_stock";
            try
            {
                var session=root.AddComponent<DeepGameSession>();
                var station=new GameObject("Unpowered hand workbench");station.transform.SetParent(root.transform,false);
                var building=station.AddComponent<DeepBuildingInstance>();definitionFixture.id="test_bench";definitionFixture.role=DeepBuildingRole.Fabricator;definitionFixture.powerRequired=0;
                building.definition=definitionFixture;building.session=session;
                var visual=new GameObject("Grounded chassis");visual.transform.SetParent(station.transform,false);visual.transform.localPosition=new Vector3(.3f,.7f,0);
                var fan=new GameObject("Rotor");fan.transform.SetParent(visual.transform,false);
                var scan=new GameObject("Live screen").AddComponent<SpriteRenderer>();scan.transform.SetParent(visual.transform,false);
                var worker=station.AddComponent<DeepWorker>();var motion=station.AddComponent<DeepMachineMotion>();
                motion.housing=visual.transform;motion.housingRestPosition=visual.transform.localPosition;motion.fan=fan.transform;
                motion.screenScans=new[]{new DeepMachineMotion.MovingDetail{renderer=scan,restPosition=new Vector3(.1f,.4f,0),travel=.2f}};
                var order=new DeepWorkOrder{targetBuilding=building,worker=worker,state=DeepWorkState.Queued};session.Orders.Add(order);
                motion.AdvanceVisuals(.2f);AssertMotion(!motion.IsAnimatingWork&&motion.RotorSpeed==0&&!scan.enabled,"Queued work must not animate production.");
                order.state=DeepWorkState.Working;motion.AdvanceVisuals(.2f);
                AssertMotion(motion.IsAnimatingWork&&motion.RotorSpeed>0&&scan.enabled,"A zero-power hand workbench must animate real work. active="+motion.IsAnimatingWork+", rotor="+motion.RotorSpeed+", screen="+scan.enabled+", operational="+building.IsOperational+", worker="+worker.IsAlive+", paused="+session.IsSimulationPaused);
                AssertMotion(visual.transform.localPosition==motion.housingRestPosition,"Working machine feet must remain grounded.");
                Vector3 before=scan.transform.localPosition;session.paused=true;motion.AdvanceVisuals(.25f);
                AssertMotion(before==scan.transform.localPosition,"Simulation pause must freeze machine details.");session.paused=false;
                order.state=DeepWorkState.Blocked;motion.AdvanceVisuals(1);
                AssertMotion(!motion.IsAnimatingWork&&motion.RotorSpeed==0&&!scan.enabled,"Blocked work must stop production motion.");
                order.state=DeepWorkState.Working;motion.AdvanceVisuals(.1f);order.state=DeepWorkState.Completed;motion.AdvanceVisuals(.1f);
                AssertMotion(!motion.IsAnimatingWork&&scan.enabled,"Completion should give one short confirmation.");motion.AdvanceVisuals(1);
                AssertMotion(!scan.enabled,"Completion feedback must settle back to idle.");
                definitionFixture.id="oxygen_diffuser";definitionFixture.role=DeepBuildingRole.Structure;session.Orders.Clear();motion.screenScans=Array.Empty<DeepMachineMotion.MovingDetail>();
                motion.oxygenBubbles=new[]{new DeepMachineMotion.MovingDetail{renderer=scan,travel=.5f}};
                building.lastRoomGasTransferMolPerSecond=.1f;motion.AdvanceVisuals(.1f);AssertMotion(motion.IsAnimatingWork&&scan.enabled,"Actual oxygen delivery must animate bubbles.");
                building.lastRoomGasTransferMolPerSecond=0;motion.AdvanceVisuals(.1f);AssertMotion(!motion.IsAnimatingWork&&!scan.enabled,"Empty or pressure-limited diffusers must stop bubbles.");
                definitionFixture.id="storage";definitionFixture.role=DeepBuildingRole.Storage;motion.oxygenBubbles=Array.Empty<DeepMachineMotion.MovingDetail>();motion.statusLight=scan;
                session.inventory.TryAdd(stockItem,2);motion.AdvanceVisuals(.1f);
                session.inventory.TryReserve(new[]{new DeepItemAmount(stockItem,1)},out var reservation,out _);motion.AdvanceVisuals(.1f);
                AssertMotion(!motion.HasStorageFeedback,"A reservation without actual inventory transfer must not animate a warehouse.");
                int used=session.inventory.UsedCapacity;
                session.inventory.Complete(reservation,new[]{new DeepItemAmount(stockItem,1)},1,out _);motion.AdvanceVisuals(.1f);
                AssertMotion(session.inventory.UsedCapacity==used&&motion.HasStorageFeedback,"Equal incoming/outgoing transactions must remain visible even with zero net capacity change.");
                motion.AdvanceVisuals(1);AssertMotion(!motion.HasStorageFeedback,"Warehouse feedback must settle without fake idle work.");
                Vector3 badgeRest=scan.transform.localScale;session.paused=true;motion.NotifySwitchFeedback();motion.NotifySwitchFeedback();
                AssertMotion(motion.HasInteractionFeedback,"A direct switch must acknowledge input while paused.");motion.AdvanceInteraction(1);
                AssertMotion(!motion.HasInteractionFeedback&&Vector3.Distance(scan.transform.localScale,badgeRest)<.0001f,"Repeated switch feedback must settle to its original scale in real time.");session.paused=false;
            }
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(definitionFixture);UnityEngine.Object.DestroyImmediate(stockItem);EditorSceneManager.ClosePreviewScene(preview);}
            return "PASS machine presentation: single warehouse sprite; "+checkedCount+" instrumented workstations/diffuser prefabs; real work, blocked, pause, completion, oxygen delivery, stationary chassis, actual warehouse transactions and paused switch confirmation.\n"+ValidateLegacySceneArtCompatibility(catalog);
        }
        static string ValidateLegacySceneArtCompatibility(DeepGameplayCatalog catalog)
        {
            var preview=EditorSceneManager.NewPreviewScene();var root=new GameObject("Legacy world art compatibility fixture");root.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,preview);
            try
            {
                var world=root.AddComponent<DeepPressureWorld>();
                var storage=catalog.FindBuilding("storage");AssertMotion(storage?.prefab!=null,"Legacy art fixture requires catalog storage.");
                var go=UnityEngine.Object.Instantiate(storage.prefab,root.transform);var building=go.GetComponent<DeepBuildingInstance>();building.definition=storage;building.RefreshOwnedComponents();
                var duplicate=new GameObject("Stacked bin");duplicate.transform.SetParent(building.visualRoot,false);
                var ambient=new GameObject("Ambient").AddComponent<Light2D>();ambient.transform.SetParent(root.transform,false);ambient.lightType=Light2D.LightType.Global;ambient.intensity=.1f;
                var custom=new GameObject("My custom work light").AddComponent<Light2D>();custom.transform.SetParent(root.transform,false);custom.color=Color.magenta;custom.intensity=.73f;
                var workerObject=new GameObject("New printed legacy worker");workerObject.transform.SetParent(root.transform,false);
                var worker=workerObject.AddComponent<DeepWorker>();worker.visualRenderer=workerObject.AddComponent<SpriteRenderer>();
                worker.visualRenderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/IndustrialLit.mat");
                var material=worker.visualRenderer.sharedMaterial;Vector3 origin=building.transform.position;
                var upgrade=root.AddComponent<DeepWorldArtUpgrade>();upgrade.ApplyNow();
                AssertMotion(!duplicate.activeSelf&&building.transform.position==origin,"Legacy warehouse repair must remove its known duplicate without moving the logical building.");
                var block=new MaterialPropertyBlock();worker.visualRenderer.GetPropertyBlock(block);
                AssertMotion(Mathf.Approximately(block.GetFloat("_MinimumLight"),.38f)&&worker.visualRenderer.sharedMaterial==material,"Legacy/new workers need readable fill without cloned material assets.");
                AssertMotion(Mathf.Approximately(ambient.intensity,.56f)&&custom.color==Color.magenta&&Mathf.Approximately(custom.intensity,.73f),"Only named project ambient and owned building fixtures may be calibrated.");
                ambient.intensity=.81f;upgrade.ApplyNow();AssertMotion(Mathf.Approximately(ambient.intensity,.81f),"Compatibility must not repeatedly overwrite live authored adjustments.");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);}
            return "PASS legacy scene Play compatibility: duplicate warehouse removal preserves building coordinates; worker property-block fill preserves shared material; project ambient is calibrated once; custom lights and later manual changes remain intact.";
        }
        static void AssertMotion(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}

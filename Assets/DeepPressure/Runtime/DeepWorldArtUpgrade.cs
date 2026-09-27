using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DeepPressure
{
    /// <summary>Play-only compatibility for authored scenes made before the current visual revision.
    /// Applies each owned object once; never modifies shared material assets or terrain contents.</summary>
    [DisallowMultipleComponent,DefaultExecutionOrder(-110)]
    public sealed class DeepWorldArtUpgrade:MonoBehaviour
    {
        DeepPressureWorld world;
        readonly HashSet<int> prepared=new HashSet<int>();
        MaterialPropertyBlock block;
        float nextScan;
        Sprite detailSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded-=SceneLoaded;SceneManager.sceneLoaded+=SceneLoaded;
            AttachToWorlds();
        }
        static void SceneLoaded(Scene scene,LoadSceneMode mode){AttachToWorlds();}
        static void AttachToWorlds()
        {
            if(!Application.isPlaying)return;
            foreach(var owner in FindObjectsOfType<DeepPressureWorld>())
                if(owner.GetComponent<DeepWorldArtUpgrade>()==null)owner.gameObject.AddComponent<DeepWorldArtUpgrade>();
        }
        void Start(){ApplyNow();}
        void Update()
        {
            // Includes newly printed workers and buildings created from old catalog prefabs.
            if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+.35f;ApplyNow();
        }
        void OnDestroy(){if(detailSprite!=null){if(Application.isPlaying)Destroy(detailSprite);else DestroyImmediate(detailSprite);}}

        public void ApplyNow()
        {
            if(world==null)world=GetComponent<DeepPressureWorld>();if(world==null)return;
            if(!prepared.Contains(world.GetInstanceID()))
            {
                if(world.background!=null)world.background.color=new Color(.32f,.38f,.39f);
                var field=world.GetComponent<DeepTerrainMaterialField>();
                if(field==null)field=world.gameObject.AddComponent<DeepTerrainMaterialField>();field.world=world;field.RefreshField();
                if(world.terrain!=null)SetTreatment(world.terrain.GetComponent<Renderer>(),.32f,.94f,Color.white,0);
                prepared.Add(world.GetInstanceID());
            }
            foreach(var light in world.GetComponentsInChildren<Light2D>(true))
            {
                if(light.lightType!=Light2D.LightType.Global||light.name!="Ambient"||!prepared.Add(light.GetInstanceID()))continue;
                light.color=DeepArtPalette.AmbientLight;light.intensity=.56f;
            }
            foreach(var building in world.GetComponentsInChildren<DeepBuildingInstance>(true))
            {
                if(building.definition==null||prepared.Contains(building.GetInstanceID()))continue;
                PrepareBuilding(building);
                prepared.Add(building.GetInstanceID());
            }
            foreach(var worker in world.GetComponentsInChildren<DeepWorker>(true))
            {
                if(worker.visualRenderer==null||prepared.Contains(worker.GetInstanceID()))continue;
                SetTreatment(worker.visualRenderer,.20f,.91f,new Color(1,.985f,.94f),.38f);
                prepared.Add(worker.GetInstanceID());
            }
        }
        void SetTreatment(Renderer renderer,float ao,float saturation,Color tint,float fill)
        {
            if(renderer==null||renderer.sharedMaterial==null||!renderer.sharedMaterial.HasProperty("_PaletteTint"))return;
            // Native rendering resources must be allocated on this main-thread call path,
            // not in a MonoBehaviour field initializer or only in Awake (inactive fixtures/loaders).
            if(block==null)block=new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);block.SetFloat("_AOIntensity",ao);block.SetFloat("_PaletteSaturation",saturation);
            block.SetColor("_PaletteTint",tint);block.SetFloat("_MinimumLight",fill);renderer.SetPropertyBlock(block);block.Clear();
        }
        void PrepareBuilding(DeepBuildingInstance building)
        {
            building.RefreshOwnedComponents();var definition=building.definition;
            RepairWarehouse(building);
            foreach(var renderer in building.GetComponentsInChildren<SpriteRenderer>(true))
                if(renderer.sprite!=null&&renderer.sprite.name.EndsWith("_Color",StringComparison.Ordinal))
                    SetTreatment(renderer,.28f,.82f,DeepArtPalette.MachineTint,.12f);
            foreach(var light in building.lights)
            {
                // Only the project's named fixtures; preserve custom scene lighting.
                if(light.name!="Printing chamber light"&&light.name!="Work light")continue;
                light.color=DeepArtPalette.WorkLight;light.intensity=definition.id=="printing_pod"?.30f:.90f;
                if(definition.id=="printing_pod")light.pointLightOuterRadius=5;
            }
            var visual=building.visualRoot;if(visual==null)return;
            if(definition.id=="printing_pod")
            {
                var glass=visual.Find("Bioprint chamber")?.GetComponent<SpriteRenderer>();
                if(glass!=null)glass.color=new Color(.57f,.73f,.65f,.16f);
            }
            bool bench=definition.role==DeepBuildingRole.Fabricator||definition.role==DeepBuildingRole.Research;
            var motion=building.GetComponent<DeepMachineMotion>();
            if(motion==null&&(bench||definition.role==DeepBuildingRole.Storage))motion=building.gameObject.AddComponent<DeepMachineMotion>();
            if(motion==null)return;
            SpriteRenderer main=null;
            foreach(var renderer in visual.GetComponentsInChildren<SpriteRenderer>(true))
                if(renderer.gameObject.activeSelf&&renderer.sprite!=null&&renderer.sprite.name.EndsWith("_Color",StringComparison.Ordinal)){main=renderer;break;}
            if(main==null)return;
            if(motion.housing==null){motion.housing=main.transform;motion.housingRestPosition=main.transform.localPosition;}
            if(motion.statusLight==null)
            {
                // A small status panel for legacy hand-workbenches. Uses one owned white sprite,
                // the machine's existing material and a property block, never a material clone.
                Vector2 center=main.sprite.bounds.center;
                if(main.sprite.name=="crate_Color"||main.sprite.name=="console_Color")
                    center=((main.sprite.name=="crate_Color"?new Vector2(256,111):new Vector2(374,139))-main.sprite.pivot)/main.sprite.pixelsPerUnit;
                else if(main.sprite.name=="compressor_Color")
                    center=new Vector2(.49f,(15*1.03f-3.1f*.67f)/Mathf.Sqrt(15*15+3.1f*3.1f))*((512f/5.5f)/main.sprite.pixelsPerUnit);
                motion.statusLight=Detail(main,"Runtime • activity indicator",center,new Vector2(.10f,.06f));
                SetTreatment(motion.statusLight,0,1,Color.white,.75f);
            }
            if(bench&&(motion.screenScans==null||motion.screenScans.Length==0))
            {
                var scans=new List<DeepMachineMotion.MovingDetail>();
                foreach(var console in visual.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if(console.sprite==null||console.sprite.name!="console_Color")continue;
                    float ppu=console.sprite.pixelsPerUnit;
                    for(int i=0;i<2;i++)
                    {
                        Vector2 position=(new Vector2(i==0?176:290,251)-console.sprite.pivot)/ppu;
                        var scan=Detail(console,"Runtime • screen scan "+i,position,new Vector2(74/ppu,2.5f/ppu));scan.enabled=false;
                        SetTreatment(scan,0,1,Color.white,.7f);
                        scans.Add(new DeepMachineMotion.MovingDetail{renderer=scan,restPosition=scan.transform.localPosition,travel=36/ppu,phase=i*.35f});
                    }
                }
                motion.screenScans=scans.ToArray();
            }
            if(definition.id=="printing_pod"&&motion.screenScans!=null)
                foreach(var scan in motion.screenScans)if(scan?.renderer!=null&&scan.renderer.name.StartsWith("Chamber scan",StringComparison.Ordinal))scan.opacity=.35f;
        }
        SpriteRenderer Detail(SpriteRenderer parent,string name,Vector2 position,Vector2 size)
        {
            if(detailSprite==null)
            {
                var texture=Texture2D.whiteTexture;
                detailSprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),texture.width);
                detailSprite.name="Runtime instrument geometry";detailSprite.hideFlags=HideFlags.HideAndDontSave;
            }
            var go=new GameObject(name);go.transform.SetParent(parent.transform,false);
            go.transform.localPosition=new Vector3(position.x,position.y,-.05f);go.transform.localScale=new Vector3(size.x,size.y,1);
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=detailSprite;renderer.sharedMaterial=parent.sharedMaterial;
            renderer.sortingLayerID=parent.sortingLayerID;renderer.sortingOrder=parent.sortingOrder+4;return renderer;
        }
        static void RepairWarehouse(DeepBuildingInstance building)
        {
            if(building.definition.role!=DeepBuildingRole.Storage||building.definition.id=="printing_pod"||building.visualRoot==null)return;
            var duplicate=building.visualRoot.Find("Stacked bin");if(duplicate==null||!duplicate.gameObject.activeSelf)return;
            duplicate.gameObject.SetActive(false);
            if(building.visualRoot==building.transform)return;
            foreach(var main in building.visualRoot.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if(!main.gameObject.activeSelf||main.sprite==null||main.sprite.name!="crate_Color"||main.sprite.rect.width!=512)continue;
                // Verified opaque bounds of the existing crate PNG, not its 512px transparent canvas.
                Vector2 min=(new Vector2(179,54)-main.sprite.pivot)/main.sprite.pixelsPerUnit;
                Vector2 max=(new Vector2(333,196)-main.sprite.pivot)/main.sprite.pixelsPerUnit;
                Vector3 a=building.transform.InverseTransformPoint(main.transform.TransformPoint(min));
                Vector3 b=building.transform.InverseTransformPoint(main.transform.TransformPoint(max));
                float factor=Mathf.Min(building.definition.footprint.x*.91f/Mathf.Max(.01f,b.x-a.x),building.definition.footprint.y*.94f/Mathf.Max(.01f,b.y-a.y));
                building.visualRoot.localScale*=factor;
                a=building.transform.InverseTransformPoint(main.transform.TransformPoint(min));b=building.transform.InverseTransformPoint(main.transform.TransformPoint(max));
                building.visualRoot.position+=building.transform.TransformVector(new Vector3(building.definition.footprint.x*.5f-(a.x+b.x)*.5f,.003f-a.y,0));
                break;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
namespace DeepPressure
{
    public sealed class DeepAirDemo : MonoBehaviour
    {
        static DeepAirDemo active;
        static int serial;
        public static bool IsActive=>active!=null&&!active.returning;
        public static void Exit(){if(active!=null)active.Return();}
        readonly List<GameObject> suspended=new List<GameObject>();
        readonly List<SpriteRenderer> colors=new List<SpriteRenderer>();
        readonly List<Vector2Int> coordinates=new List<Vector2Int>();
        readonly List<DeepWorker> crew=new List<DeepWorker>();
        Scene previous;
        DeepPressureHUD source;
        DeepPressureWorld world;
        DeepGameSession session;
        DeepAtmosphereField field;
        DeepBuildingInstance oxygen;
        Texture2D texture;
        Sprite pixel;
        float elapsed,oldTimeScale;
        int stage=-1;
        bool returning;
        GUIStyle label,heading;
        static readonly string[] names={"密闭：气压与含氧量不同","打开隔墙：气体沿浓度差扩散","1 人呼吸：消耗氧气，排出 CO₂","3 人呼吸：氧气需求增加到 3 倍","有限供氧：补充 1 份藻类","供氧结束后，呼吸仍在继续"};
        public static void Enter(DeepPressureHUD hud)
        {
            if(active!=null||hud==null||hud.world==null)return;
            var scene=SceneManager.CreateScene("Air mechanism demonstration "+(++serial));
            var host=new GameObject("Air demonstration");SceneManager.MoveGameObjectToScene(host,scene);
            active=host.AddComponent<DeepAirDemo>();active.previous=SceneManager.GetActiveScene();active.source=hud;active.oldTimeScale=Time.timeScale;
            try
            {
                foreach(var root in active.previous.GetRootGameObjects())if(root.activeSelf){active.suspended.Add(root);root.SetActive(false);}
                active.Build(hud,scene);
                SceneManager.SetActiveScene(scene);Time.timeScale=1;
            }
            catch(Exception error){Debug.LogException(error);active.Return();}
        }
        void Build(DeepPressureHUD hud,Scene scene)
        {
            var original=hud.world.GetComponent<DeepGameSession>();
            DeepTerrainTile rock=null;
            for(int y=0;y<hud.world.height&&rock==null;y++)for(int x=0;x<hud.world.width&&rock==null;x++)rock=hud.world.MaterialAt(new Vector2Int(x,y));
            var root=new GameObject("Live gas chambers");root.SetActive(false);SceneManager.MoveGameObjectToScene(root,scene);
            world=root.AddComponent<DeepPressureWorld>();world.width=24;world.height=13;world.crossSectionDepthM=.08f;
            var grid=new GameObject("Grid");grid.transform.SetParent(root.transform,false);grid.AddComponent<Grid>();
            var map=new GameObject("Rock");map.transform.SetParent(grid.transform,false);world.terrain=map.AddComponent<Tilemap>();
            map.AddComponent<TilemapRenderer>().sharedMaterial=hud.world.terrain.GetComponent<TilemapRenderer>().sharedMaterial;
            world.terrainKinds=new TerrainKind[world.width*world.height];
            for(int y=0;y<13;y++)for(int x=0;x<24;x++)world.terrain.SetTile(new Vector3Int(x,y,0),rock);
            for(int y=3;y<9;y++)for(int x=2;x<22;x++)if(x!=11)world.terrain.SetTile(new Vector3Int(x,y,0),null);
            Region(root.transform,"高氧室",new RectInt(2,3,9,6),110,new Vector4(.28f,.72f,0,0));
            Region(root.transform,"低氧室",new RectInt(12,3,10,6),65,new Vector4(.06f,.94f,0,0));
            session=root.AddComponent<DeepGameSession>();session.enabled=false;session.world=world;session.catalog=original.catalog;
            session.lifeSupportEnabled=true;session.hazardsEnabled=false;session.baseStorageCapacity=10;session.breathingMolPerSecond=.25f;session.startingInventory=Array.Empty<DeepItemAmount>();
            var template=hud.world.GetComponentInChildren<DeepWorker>(true);
            for(int i=0;i<3;i++)
            {
                var worker=Instantiate(template,root.transform);worker.session=session;worker.automationPaused=true;worker.health=100;worker.airReserveSeconds=10000;worker.currentOrder=null;worker.SetPath(null);
                worker.transform.localPosition=new Vector3(6.5f+i*2,3,0);worker.gameObject.SetActive(false);crew.Add(worker);
            }
            var definition=original.catalog.FindBuilding("oxygen_diffuser");var supply=Instantiate(definition.prefab,root.transform);
            oxygen=supply.GetComponent<DeepBuildingInstance>();oxygen.definition=definition;oxygen.origin=new Vector2Int(15,3);oxygen.session=session;oxygen.isOn=false;supply.transform.localPosition=new Vector3(15,3,0);
            var cameraObject=Instantiate(hud.viewCamera.gameObject);SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.GetComponent<Camera>();camera.transform.position=new Vector3(12,6,-20);camera.orthographicSize=8.4f;camera.targetTexture=null;camera.cullingMask=~0;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.05f,.065f);
            texture=new Texture2D(1,1);texture.SetPixel(0,0,Color.white);texture.Apply();pixel=Sprite.Create(texture,new Rect(0,0,1,1),Vector2.one*.5f,1);
            for(int y=3;y<9;y++)for(int x=2;x<22;x++)
            {
                var cell=new GameObject("Atmosphere");cell.transform.SetParent(root.transform,false);cell.transform.localPosition=new Vector3(x+.5f,y+.5f,0);cell.transform.localScale=Vector3.one*.98f;
                var visual=cell.AddComponent<SpriteRenderer>();visual.sprite=pixel;visual.sortingOrder=2;colors.Add(visual);coordinates.Add(new Vector2Int(x,y));
            }
            root.SetActive(true);
            var lighting=new GameObject("Ambient");lighting.transform.SetParent(root.transform,false);var light=lighting.AddComponent<UnityEngine.Rendering.Universal.Light2D>();light.lightType=UnityEngine.Rendering.Universal.Light2D.LightType.Global;light.intensity=.9f;
            session.InitializeSession();session.Workers.Clear();field=session.Atmosphere;field.diffusionPerSecond=2.5f;
        }
        static void Region(Transform parent,string name,RectInt bounds,float pressure,Vector4 mixture)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);var region=go.AddComponent<DeepPressureRegion>();region.stableId=name;region.displayName=name;region.bounds=bounds;region.initialPressureKPa=pressure;region.composition=mixture;region.initialTemperatureC=22;}
        void Update()
        {
            if(returning||session==null)return;
            if(Input.GetKeyDown(KeyCode.Escape)){Return();return;}
            float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);elapsed+=dt;
            int next=elapsed<6?0:elapsed<17?1:elapsed<28?2:elapsed<39?3:elapsed<40||oxygen.fuelSecondsRemaining>.001f?4:5;
            if(next!=stage)
            {
                stage=next;
                if(stage==1){for(int y=3;y<9;y++)world.terrain.SetTile(new Vector3Int(11,y,0),null);world.SyncTerrainFromTilemap();world.RebuildRooms();field.RebuildAfterTerrainChange();}
                if(stage==2){crew[0].gameObject.SetActive(true);session.Workers.Add(crew[0]);}
                if(stage==3){crew[1].gameObject.SetActive(true);crew[2].gameObject.SetActive(true);session.Workers.Add(crew[1]);session.Workers.Add(crew[2]);}
                if(stage==4){session.inventory.TryAdd(session.catalog.FindItem("algae"),1);oxygen.isOn=true;}
            }
            session.Tick(dt);
            for(int i=0;i<colors.Count;i++)
            {
                var gas=field.Sample(coordinates[i]);float o=gas.Total>0?(float)(gas.oxygen/gas.Total):0,c=gas.Total>0?(float)(gas.carbonDioxide/gas.Total):0;
                colors[i].color=Color.Lerp(Color.Lerp(new Color(.15f,.28f,.37f),new Color(.22f,.84f,.74f),Mathf.Clamp01(o/.28f)),new Color(.92f,.46f,.21f),Mathf.Clamp01(c*5));
                colors[i].enabled=world.GetTerrain(coordinates[i].x,coordinates[i].y)==TerrainKind.Empty;
            }
        }
        string Read(Vector2Int at)
        {var gas=field.Sample(at);double total=Math.Max(1e-9,gas.Total);return field.PressureKPa(at).ToString("0.0")+" kPa   O₂ "+(gas.oxygen/total*100).ToString("0.0")+"%   CO₂ "+(gas.carbonDioxide/total*100).ToString("0.0")+"%";}
        void OnGUI()
        {
            if(field==null||returning)return;
            if(label==null){var font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Microsoft YaHei","Arial"},18);label=new GUIStyle(GUI.skin.label){font=font,fontSize=18};label.normal.textColor=Color.white;heading=new GUIStyle(label){fontSize=24,fontStyle=FontStyle.Bold};}
            GUI.Box(new Rect(18,18,Screen.width-36,146),GUIContent.none);GUI.Label(new Rect(36,28,700,34),"空气机制演示",heading);
            GUI.Label(new Rect(36,65,900,30),names[Mathf.Max(0,stage)],label);
            GUI.Label(new Rect(36,100,Screen.width-80,30),"左室  "+Read(new Vector2Int(7,5))+"       右室  "+Read(new Vector2Int(16,5)),label);
            GUI.Label(new Rect(36,Screen.height-72,Screen.width-280,35),"需氧 "+session.OxygenDemandRate.ToString("0.00")+" mol/s   供氧 "+session.OxygenSupplyRate.ToString("0.00")+" mol/s   剩余氧料 "+(oxygen.fuelSecondsRemaining*1.5f).ToString("0.0")+" mol",label);
            if(GUI.Button(new Rect(Screen.width-232,Screen.height-74,96,38),"重播")){var hud=source;Return();Enter(hud);}
            if(GUI.Button(new Rect(Screen.width-126,Screen.height-74,106,38),"返回 · Esc"))Return();
        }
        public void Return()
        {
            if(returning)return;returning=true;
            foreach(var root in gameObject.scene.GetRootGameObjects())if(root.activeSelf)root.SetActive(false);
            foreach(var root in suspended)if(root!=null)root.SetActive(true);
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            Time.timeScale=oldTimeScale;active=null;
            var scene=gameObject.scene;if(scene.IsValid()&&scene.isLoaded)SceneManager.UnloadSceneAsync(scene);
        }
        void OnDestroy(){if(pixel!=null)Destroy(pixel);if(texture!=null)Destroy(texture);if(active==this){active=null;foreach(var root in suspended)if(root!=null)root.SetActive(true);Time.timeScale=oldTimeScale;}}
    }
}

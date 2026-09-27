using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace DeepPressure.Editor
{
    public static class DeepColonyLevelBuilder
    {
        const string Root="Assets/DeepPressure";
        public static DeepLevelDefinition DefaultLevel()
        {
            const string path=Root+"/Data/DeepStation.asset";
            var level=AssetDatabase.LoadAssetAtPath<DeepLevelDefinition>(path);
            if(level!=null)return level;
            level=ScriptableObject.CreateInstance<DeepLevelDefinition>();
            level.caves=new[]{
                Cave("western_mine","旧矿井",new RectInt(7,23,24,12),92,new Vector4(.19f,.77f,.03f,.01f)),
                Cave("wet_basin","湿气盆地",new RectInt(43,11,27,13),320,new Vector4(.16f,.55f,.14f,.15f)),
                Cave("buried_workshop","埋藏工坊",new RectInt(70,36,22,11),85,new Vector4(.17f,.77f,.05f,.01f)),
                Cave("deep_pressure","深层气藏",new RectInt(93,13,27,17),850,new Vector4(.06f,.68f,.22f,.04f)),
                Cave("upper_fault","上部裂隙",new RectInt(84,59,24,13),120,new Vector4(.23f,.74f,.02f,.01f))};
            AssetDatabase.CreateAsset(level,path);return level;
        }
        static DeepCaveDefinition Cave(string id,string name,RectInt bounds,float pressure,Vector4 composition)
        {return new DeepCaveDefinition{id=id,displayName=name,bounds=bounds,pressureKPa=pressure,temperatureC=22,composition=composition};}
        public static void Setup(DeepPressureWorld world,Camera camera,Material lit)
        {
            var level=DefaultLevel();var catalog=DeepCatalogBuilder.CreateOrUpdateCatalog();level.catalog=catalog;world.levelDefinition=level;
            var tiles=DeepTerrainArtBaker.Prepare();
            Vector2Int offset=level.starterBaseOffset;Vector3 shift=new Vector3(offset.x,offset.y,0);
            int oldWidth=world.width,oldHeight=world.height;var old=(TerrainKind[])world.terrainKinds.Clone();
            foreach(Transform child in world.transform)
            {
                if(child.GetComponent<Grid>()!=null||child.name.StartsWith("02")||child.name.StartsWith("07"))continue;
                child.position+=shift;
            }
            foreach(var region in world.GetComponentsInChildren<DeepPressureRegion>())region.bounds=new RectInt(region.bounds.position+offset,region.bounds.size);
            foreach(var line in world.GetComponentsInChildren<LineRenderer>())
            {if(!line.useWorldSpace)continue;for(int i=0;i<line.positionCount;i++)line.SetPosition(i,line.GetPosition(i)+shift);}
            foreach(var route in world.GetComponentsInChildren<DeepPipeRoute>())route.CaptureEndpoints();
            world.width=Mathf.Max(96,level.width);world.height=Mathf.Max(60,level.height);
            world.terrain.ClearAllTiles();world.background.ClearAllTiles();world.terrainKinds=new TerrainKind[world.width*world.height];
            var cells=new Vector3Int[world.width*world.height];var foreground=new UnityEngine.Tilemaps.TileBase[cells.Length];var back=new UnityEngine.Tilemaps.TileBase[cells.Length];
            for(int y=0;y<world.height;y++)for(int x=0;x<world.width;x++)
            {
                int i=y*world.width+x;cells[i]=new Vector3Int(x,y,0);
                float strata=y+3.1f*Mathf.Sin(x*.09f)+1.6f*Mathf.Sin(x*.29f)+(Mathf.PerlinNoise(x*.085f,y*.085f)-.5f)*4;
                if(x>78)strata+=4;
                var kind=strata>68?TerrainKind.Soil:strata>50?TerrainKind.Sandstone:strata>28?TerrainKind.Shale:TerrainKind.Basalt;
                if(Mathf.Pow((x-68f)/21,2)+Mathf.Pow((y-35f)/5,2)<1)kind=TerrainKind.Sandstone;
                if(Mathf.Abs(y-(.41f*x+12+Mathf.Sin(x*.24f)))<.65f&&y<59)kind=TerrainKind.Basalt;
                foreground[i]=tiles[(int)kind];back[i]=tiles[(int)TerrainKind.Basalt];
            }
            world.terrain.SetTiles(cells,foreground);world.background.SetTiles(cells,back);
            for(int y=0;y<oldHeight;y++)for(int x=0;x<oldWidth;x++)
            {var kind=old[y*oldWidth+x];if(kind==TerrainKind.Empty||kind==TerrainKind.Metal)world.terrain.SetTile(new Vector3Int(x+offset.x,y+offset.y,0),kind==TerrainKind.Empty?null:tiles[(int)kind]);}
            if(level.caves!=null)foreach(var cave in level.caves)
            {
                RectInt rect=cave.bounds;
                foreach(var p in rect.allPositionsWithin)
                {
                    float nx=(p.x+.5f-rect.center.x)/(rect.width*.5f),ny=(p.y+.5f-rect.center.y)/(rect.height*.5f);
                    if(nx*nx+ny*ny<.83f+Mathf.PerlinNoise(p.x*.31f,p.y*.27f)*.2f&&world.IsInside(p))world.terrain.SetTile((Vector3Int)p,null);
                }
                var go=new GameObject(cave.displayName);go.transform.SetParent(world.transform,false);var region=go.AddComponent<DeepPressureRegion>();
                region.stableId=cave.id;region.displayName=cave.displayName;region.bounds=cave.bounds;region.composition=cave.composition;region.initialPressureKPa=cave.pressureKPa;region.initialTemperatureC=cave.temperatureC;
            }
            // An empty construction hall has proper flooring and a two-cell-high route to the factory.
            for(int y=53;y<60;y++)for(int x=5;x<18;x++)world.terrain.SetTile(new Vector3Int(x,y,0),null);
            for(int x=5;x<18;x++)world.terrain.SetTile(new Vector3Int(x,52,0),tiles[(int)TerrainKind.Metal]);
            for(int y=64;y<68;y++)for(int x=40;x<47;x++)world.terrain.SetTile(new Vector3Int(x,y,0),null);
            for(int x=40;x<44;x++)world.terrain.SetTile(new Vector3Int(x,63,0),tiles[(int)TerrainKind.Metal]);
            world.terrain.SetTile(new Vector3Int(43,52,0),tiles[(int)TerrainKind.Metal]);
            var hallObject=new GameObject("建造大厅");hallObject.transform.SetParent(world.transform,false);var hall=hallObject.AddComponent<DeepPressureRegion>();hall.stableId="construction_hall";hall.displayName="建造大厅";hall.bounds=new RectInt(5,53,13,7);hall.initialPressureKPa=100;hall.composition=new Vector4(.21f,.78f,.01f,0);
            world.SyncTerrainFromTilemap();world.RebuildRooms();
            var explore=world.GetComponent<DeepExploration>();explore.initialExploredAreas=new[]{new RectInt(3,50,49,28),new RectInt(42,43,7,10)};
            foreach(var sr in new[]{explore.fogRenderer,explore.gasRenderer})
            {sr.transform.position=world.transform.TransformPoint(new Vector3(world.width*.5f,world.height*.5f,0));sr.transform.localScale=new Vector3(world.width/sr.sprite.bounds.size.x,world.height/sr.sprite.bounds.size.y,1);}
            camera.transform.position=world.transform.TransformPoint(new Vector3(31,60,-20));camera.orthographicSize=15.8f;
            var session=world.gameObject.AddComponent<DeepGameSession>();session.world=world;session.network=world.GetComponent<GasNetworkSimulator>();session.catalog=catalog;
            session.baseStorageCapacity=40;session.startingInventory=new[]{new DeepItemAmount(catalog.FindItem("ore"),90),new DeepItemAmount(catalog.FindItem("alloy"),65),new DeepItemAmount(catalog.FindItem("electronics"),12),new DeepItemAmount(catalog.FindItem("research_data"),4)};
            session.excavationItem=catalog.FindItem("ore");session.constructedFloorTile=tiles[(int)TerrainKind.Metal];
            var buildings=new GameObject("09 • Buildings from content catalog");buildings.transform.SetParent(world.transform,false);
            Place(catalog,"storage",new Vector2Int(18,53),buildings.transform);
            Place(catalog,"fabricator",new Vector2Int(27,53),buildings.transform);
            Place(catalog,"generator",new Vector2Int(39,53),buildings.transform);
            Place(catalog,"generator",new Vector2Int(7,53),buildings.transform);
            foreach(var sr in world.GetComponentsInChildren<SpriteRenderer>().ToArray())
                if(sr.name=="Ladder rung"||sr.name=="Ladder rail"||(sr.name=="Furnishing • crate"&&sr.transform.position.y>50))UnityEngine.Object.DestroyImmediate(sr.gameObject);
            for(int y=44;y<71;y++)Place(catalog,"ladder",new Vector2Int(44,y),buildings.transform);
            foreach(var node in world.GetComponentsInChildren<GasNode>().ToArray())
            {
                if(node.GetComponentInParent<DeepBuildingInstance>()!=null||node.kind==GasNodeKind.Reservoir)continue;
                string id=node.kind==GasNodeKind.Separator?"gas_separator":node.kind==GasNodeKind.Regulator?"gas_regulator":"gas_tank";
                var definition=catalog.FindBuilding(id);var cell=world.WorldToCell(node.transform.position)+new Vector2Int(-definition.footprint.x/2,-2);
                var wrapper=new GameObject(definition.displayName);wrapper.transform.SetParent(buildings.transform,false);wrapper.transform.position=new Vector3(cell.x,cell.y,0);
                node.transform.SetParent(wrapper.transform,true);var instance=wrapper.AddComponent<DeepBuildingInstance>();instance.definition=definition;instance.origin=cell;instance.visualRoot=node.transform;
            }
            DeepWorldObjectAuthoring.RepairWorld(world,catalog,buildings.transform);
            Worker(world,session,lit,new Vector2Int(24,53),"阿砾",Color.white);
            Worker(world,session,lit,new Vector2Int(31,53),"桐",new Color(.86f,.96f,1));
            Worker(world,session,lit,new Vector2Int(38,53),"洛",new Color(1,.9f,.76f));
            DeepParticleBaker.Attach(world);DeepPressureArtImporter.BindSecondaryTextures();
            EditorUtility.SetDirty(level);AssetDatabase.SaveAssets();
        }
        static void Place(DeepGameplayCatalog catalog,string id,Vector2Int cell,Transform parent)
        {
            var definition=catalog.FindBuilding(id);if(definition==null||definition.prefab==null)throw new InvalidOperationException("Missing authored prefab: "+id);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(definition.prefab);go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(cell.x,cell.y,0);
            var instance=go.GetComponent<DeepBuildingInstance>();instance.definition=definition;instance.origin=cell;instance.isConstructed=true;instance.isOn=true;
        }
        static void Worker(DeepPressureWorld world,DeepGameSession session,Material material,Vector2Int cell,string name,Color tint)
        {
            var go=new GameObject("工程员 · "+name);go.transform.SetParent(world.transform,false);go.transform.localPosition=new Vector3(cell.x+.5f,cell.y,0);
            var worker=go.AddComponent<DeepWorker>();worker.displayName=name;worker.session=session;
            var visual=new GameObject("Sprite body • normal mapped");visual.transform.SetParent(go.transform,false);var sr=visual.AddComponent<SpriteRenderer>();sr.sharedMaterial=material;sr.sortingOrder=25;
            var appearance=go.AddComponent<DeepWorkerPresentation>();appearance.idle=WorkerSprite("idle0");appearance.walking=Enumerable.Range(0,6).Select(i=>WorkerSprite("walk"+i)).ToArray();appearance.working=Enumerable.Range(0,4).Select(i=>WorkerSprite("work"+i)).ToArray();appearance.suitTint=tint;
            sr.sprite=appearance.idle;worker.visualRenderer=sr;
            var parcel=new GameObject("Carried reserved materials");parcel.transform.SetParent(go.transform,false);var parcelSprite=parcel.AddComponent<SpriteRenderer>();parcelSprite.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/Props/crate_Color.png");parcelSprite.sharedMaterial=material;parcelSprite.sortingOrder=26;parcel.transform.localScale=Vector3.one*.19f;parcel.SetActive(false);appearance.carriedCrate=parcel.transform;
            string path=Root+"/Data/Gameplay/Prefabs/Worker.prefab";if(!File.Exists(path))PrefabUtility.SaveAsPrefabAsset(go,path);
        }
        static Sprite WorkerSprite(string frame)=>AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/Workers/engineer_"+frame+"_Color.png");
    }
}

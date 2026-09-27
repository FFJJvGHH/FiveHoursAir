using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace DeepPressure.Editor
{
    public static class DeepIndustrialAuthoring
    {
        public static void Upgrade(DeepGameSession session)
        {
            if(session==null||session.world==null)throw new InvalidOperationException("打开殖民地场景后再升级。");
            DeepWorldObjectAuthoring.UpgradeOpenScene();
            DeepPowerAuthoring.Upgrade(session);
            var world=session.world;session.lifeSupportEnabled=true;session.hazardsEnabled=true;
            foreach(var region in world.GetComponentsInChildren<DeepPressureRegion>(true))
            {
                if(region.stableId=="deep_pressure")region.reactiveFractions=new Vector2(.11f,.18f);
                if(region.stableId=="wet_basin")region.reactiveFractions=new Vector2(0,.08f);
                EditorUtility.SetDirty(region);
            }
            // Give the player a finite emergency buffer. New supply infrastructure remains a player decision.
            if(!world.GetComponentsInChildren<DeepBuildingInstance>(true).Any(b=>b.name=="应急供气站"))
            {
                var workers=world.GetComponentsInChildren<DeepWorker>(true);
                var focus=workers.Length>0?world.WorldToCell(workers[0].transform.position+Vector3.up*.1f):new Vector2Int(18,53);
                var def=session.catalog.FindBuilding("supply_vent");
                if(def!=null&&FindFloor(world,focus,def.footprint,out var cell))
                {
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(def.prefab,world.gameObject.scene);go.transform.SetParent(world.transform,false);
                    go.transform.position=session.BuildingPosition(cell);go.name="应急供气站";
                    var b=go.GetComponent<DeepBuildingInstance>();b.definition=def;b.origin=cell;b.session=session;
                    var node=go.GetComponentInChildren<GasNode>();
                    if(node!=null){node.volumeM3=3;node.maxPressureKPa=900;node.initialPressureKPa=700;node.initialComposition=new Vector4(1,0,0,0);node.ResetInventory();EditorUtility.SetDirty(node);}
                    EditorUtility.SetDirty(b);
                }
            }
            DeepPresentationUpgrade.ApplyToCatalogAndLoadedScenes(session.catalog);
            EditorUtility.SetDirty(session);EditorSceneManager.MarkSceneDirty(world.gameObject.scene);AssetDatabase.SaveAssets();
        }
        static bool FindFloor(DeepPressureWorld world,Vector2Int origin,Vector2Int size,out Vector2Int result)
        {
            var buildings=world.GetComponentsInChildren<DeepBuildingInstance>(true);
            for(int radius=1;radius<40;radius++)for(int dir=-1;dir<=1;dir+=2)
            {
                var cell=origin+new Vector2Int(radius*dir,0);bool valid=true;
                foreach(var p in new RectInt(cell,size).allPositionsWithin)
                {
                    if(!world.IsInside(p)||world.GetTerrain(p.x,p.y)!=TerrainKind.Empty||buildings.Any(b=>b.Bounds.Contains(p))){valid=false;break;}
                }
                for(int x=0;x<size.x&&valid;x++)if(world.GetTerrain(cell.x+x,cell.y-1)==TerrainKind.Empty)valid=false;
                if(valid){result=cell;return true;}
            }
            result=default;return false;
        }
    }
}

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
            // Upgrades only repair existing content. Life support is constructed by the player.
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

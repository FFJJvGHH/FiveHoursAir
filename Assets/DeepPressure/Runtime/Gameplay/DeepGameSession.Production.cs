using System;
using System.Collections.Generic;
using UnityEngine;
namespace DeepPressure
{
    [Serializable] public sealed class DeepProductionTarget
    {
        public string recipeId,stationId;
        public int targetAmount=10;
        public bool enabled=true;
        [NonSerialized] public string status;
    }
    public sealed partial class DeepGameSession
    {
        public List<DeepProductionTarget> productionTargets=new List<DeepProductionTarget>();
        float nextProductionCheck;
        public DeepProductionTarget ProductionTargetFor(string recipeId)=>productionTargets.Find(t=>t.recipeId==recipeId);
        public DeepProductionTarget ProductionTargetForAt(DeepBuildingInstance station,string recipeId)
        {
            if(station==null)return null;string id=ObjectId(station,"b");
            return productionTargets.Find(t=>t.recipeId==recipeId&&t.stationId==id);
        }
        public bool SetProductionTargetAt(DeepBuildingInstance station,DeepRecipeDefinition recipe,int target,bool enabled,out string reason)
        {
            InitializeSession();
            if(!StationCanMake(station,recipe)){reason="请选择支持此配方的已建成实体设备";return false;}
            var rule=ProductionTargetForAt(station,recipe.id);
            if(rule==null){rule=new DeepProductionTarget{recipeId=recipe.id,stationId=ObjectId(station,"b")};productionTargets.Add(rule);}
            rule.targetAmount=Mathf.Clamp(target,1,999);rule.enabled=enabled;nextProductionCheck=0;
            reason=enabled?"本设备已设置库存补充目标":"本设备库存补充已暂停";return true;
        }
        public void SetProductionTarget(DeepRecipeDefinition recipe,int target,bool enabled)
        {
            if(recipe==null)return;
            if(FindReachableStation(DeepBuildingRole.Fabricator,recipe.requiredBuildingId,false,out var station))
            {SetProductionTargetAt(station,recipe,target,enabled,out _);return;}
            // Compatibility for old saves/API callers: an unbound rule is retained
            // for review but cannot silently attach itself to a future machine.
            var rule=productionTargets.Find(t=>t.recipeId==recipe.id&&string.IsNullOrEmpty(t.stationId));
            if(rule==null){rule=new DeepProductionTarget{recipeId=recipe.id};productionTargets.Add(rule);}
            rule.targetAmount=Mathf.Clamp(target,1,999);rule.enabled=enabled;nextProductionCheck=0;
        }
        void TickProductionTargets()
        {
            foreach(var order in Orders)
            {
                if(order.IsTerminal||order.kind!=DeepWorkKind.Craft)continue;
                if(order.targetBuilding!=null&&Buildings.Contains(order.targetBuilding)&&order.targetBuilding.definition!=null)continue;
                CancelOrder(order);order.statusReason="指定生产设备已移除，工单取消并退回原料";
            }
            if(SimulationTime<nextProductionCheck||catalog==null)return;nextProductionCheck=SimulationTime+1;
            foreach(var rule in productionTargets)
            {
                if(!rule.enabled){rule.status="已暂停";continue;}
                var recipe=catalog.FindRecipe(rule.recipeId);
                if(recipe==null||recipe.outputs.Length==0){rule.status="配方不可用";continue;}
                if(string.IsNullOrEmpty(rule.stationId)){rule.status="请在实体生产设备中重新设置此目标";continue;}
                DeepBuildingInstance station=null;
                foreach(var candidate in Buildings)if(candidate!=null&&ObjectId(candidate,"b")==rule.stationId){station=candidate;break;}
                if(station==null){rule.enabled=false;rule.status="指定生产设备已移除";continue;}
                var product=recipe.outputs[0];bool pending=false;
                foreach(var order in Orders)if(!order.IsTerminal&&order.recipe==recipe){pending=true;break;}
                if(pending){rule.status="等待本批完成";continue;}
                if(inventory.GetAmount(product.item)>=rule.targetAmount){rule.status="已达目标，暂停投料";continue;}
                if(inventory.AvailableCapacity<product.amount){rule.status="仓满，保留原料";continue;}
                bool ok=RequestCraftAt(station,recipe,1,out string reason);rule.status=ok?"本设备补充库存中":reason;
            }
        }
    }
}

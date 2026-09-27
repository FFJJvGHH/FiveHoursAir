using System;
using System.Collections.Generic;
using UnityEngine;
namespace DeepPressure
{
    [Serializable] public sealed class DeepProductionTarget
    {
        public string recipeId;
        public int targetAmount=10;
        public bool enabled=true;
        [NonSerialized] public string status;
    }
    public sealed partial class DeepGameSession
    {
        public List<DeepProductionTarget> productionTargets=new List<DeepProductionTarget>();
        float nextProductionCheck;
        public DeepProductionTarget ProductionTargetFor(string recipeId)=>productionTargets.Find(t=>t.recipeId==recipeId);
        public void SetProductionTarget(DeepRecipeDefinition recipe,int target,bool enabled)
        {
            if(recipe==null)return;var rule=ProductionTargetFor(recipe.id);
            if(rule==null){rule=new DeepProductionTarget{recipeId=recipe.id};productionTargets.Add(rule);}
            rule.targetAmount=Mathf.Clamp(target,1,999);rule.enabled=enabled;nextProductionCheck=0;
        }
        void TickProductionTargets()
        {
            if(SimulationTime<nextProductionCheck||catalog==null)return;nextProductionCheck=SimulationTime+1;
            foreach(var rule in productionTargets)
            {
                if(!rule.enabled){rule.status="已暂停";continue;}
                var recipe=catalog.FindRecipe(rule.recipeId);
                if(recipe==null||recipe.outputs.Length==0){rule.status="配方不可用";continue;}
                var product=recipe.outputs[0];bool pending=false;
                foreach(var order in Orders)if(!order.IsTerminal&&order.recipe==recipe){pending=true;break;}
                if(pending){rule.status="等待本批完成";continue;}
                if(inventory.GetAmount(product.item)>=rule.targetAmount){rule.status="已达目标，暂停投料";continue;}
                if(inventory.AvailableCapacity<product.amount){rule.status="仓满，保留原料";continue;}
                bool ok=RequestCraft(recipe,1,out string reason);rule.status=ok?"补充库存中":reason;
            }
        }
    }
}

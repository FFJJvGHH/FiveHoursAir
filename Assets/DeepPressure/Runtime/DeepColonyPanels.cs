using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Icon=DeepPressure.DeepUIIcons.Icon;

namespace DeepPressure
{
    /// <summary>Player-facing colony orders. All inventory, validation and completion belong to DeepGameSession.</summary>
    public sealed partial class DeepPressureHUD
    {
        enum ColonyPanel{None,Build,Workers,Research,Craft,Resources}
        enum ColonyTool{None,Build,Dig,Pipe,Wire}
        DeepGameSession session;
        ColonyPanel colonyPanel;
        ColonyTool colonyTool;
        DeepBuildingDefinition buildChoice;
        DeepWorker selectedWorker,hoveredWorker;
        DeepBuildingInstance selectedBuilding,hoveredBuilding;
        DeepWorkOrder selectedOrder,hoveredOrder;
        GasNode pipeSource,hoveredPipeNode;
        GasOutputPort pipePort;
        DeepRecipeDefinition recipeChoice;
        int buildingPage,ordersPage,recipePage,craftBatches=1,productionAmount=10;
        Rect colonyRect,inventoryRect;
        bool pauseMenu,settingsMenu,drawingPauseMenu,pauseBeforeMenu,particlesEnabled=true,cameraCaptured;
        Vector3 startingCameraPosition;
        float startingCameraSize;
        Vector2Int lastDigCell=new Vector2Int(int.MinValue,int.MinValue);
        readonly Dictionary<SpriteRenderer,bool> particleVisibility=new Dictionary<SpriteRenderer,bool>();
        readonly Dictionary<DeepDustMotion,bool> dustActivity=new Dictionary<DeepDustMotion,bool>();
        readonly Dictionary<ParticleSystem,bool> particleActivity=new Dictionary<ParticleSystem,bool>();
        readonly Dictionary<DeepParticleFeedback,bool> feedbackActivity=new Dictionary<DeepParticleFeedback,bool>();
        bool ColonyHasSelection=>selectedWorker!=null||selectedBuilding!=null||selectedOrder!=null&&!selectedOrder.IsTerminal;
        bool ColonyPaused=>session!=null?session.paused:network!=null&&network.paused;
        bool ColonyBlocksPointer=>pauseMenu||(colonyPanel!=ColonyPanel.None&&colonyRect.Contains(pointer))||inventoryRect.Contains(pointer);

        void InitializeColony()
        {
            if(session==null)session=GetComponent<DeepGameSession>();
            if(!cameraCaptured&&viewCamera!=null){startingCameraPosition=viewCamera.transform.position;startingCameraSize=viewCamera.orthographicSize;cameraCaptured=true;}
        }
        void UpdateColonyInterface()
        {
            InitializeColony();
            float width=colonyPanel==ColonyPanel.Research?uiWidth-38:colonyPanel==ColonyPanel.Workers?590:416;
            colonyRect=new Rect(19,74,width,uiHeight-190);
            inventoryRect=new Rect(142,17,Mathf.Max(0,uiWidth-475),39);
        }
        void SetColonyPause(bool value){if(session!=null)session.paused=value;if(network!=null)network.paused=value;}
        void SetColonySpeed(float value){if(session!=null)session.speed=value;if(network!=null)network.simulationSpeed=value;}
        void ClearColonySelection(){selectedWorker=null;selectedBuilding=null;selectedOrder=null;}
        void ActivateColonyTool(int index)
        {
            tool=ToolMode.None;colonyTool=ColonyTool.None;
            switch(index)
            {
                case 0:colonyPanel=colonyPanel==ColonyPanel.Build?ColonyPanel.None:ColonyPanel.Build;break;
                case 1:colonyPanel=ColonyPanel.None;colonyTool=ColonyTool.Dig;ClearSelection();break;
                case 2:colonyPanel=colonyPanel==ColonyPanel.Workers?ColonyPanel.None:ColonyPanel.Workers;break;
                case 3:colonyPanel=colonyPanel==ColonyPanel.Research?ColonyPanel.None:ColonyPanel.Research;break;
            }
        }
        void DrawInventory()
        {
            if(session==null||session.catalog==null||session.inventory==null)return;
            float x=inventoryRect.x;
            int count=Mathf.Min(session.catalog.items.Length,Mathf.FloorToInt(inventoryRect.width/87));
            for(int i=0;i<count;i++)
            {
                var item=session.catalog.items[i];if(item==null)continue;
                Rect slot=new Rect(x,inventoryRect.y,82,35);
                Rounded(slot,new Color(.035f,.065f,.078f,.68f),9);
                DrawAssetIcon(item.icon,new Rect(x+8,slot.y+8,19,19),item.tint,Icon.Material);
                Label(new Rect(x+35,slot.y,43,35),session.inventory.GetAmount(item).ToString(),body,White);
                RegisterHover("stock"+item.id,slot,item.displayName,item.description);x+=87;
                if(Click(slot))colonyPanel=ColonyPanel.Resources;
            }
            if(x+119<TimeRect.x-10)
            {
                Rect power=new Rect(x,inventoryRect.y,119,35);Rounded(power,new Color(.035f,.065f,.078f,.68f),9);
                DrawIcon(Icon.Power,new Rect(x+8,power.y+8,19,19),session.HasPower?Mint:Amber);
                Label(new Rect(x+33,power.y,81,35),session.PowerDemand.ToString("0")+" / "+session.PowerProduction.ToString("0"),small,session.HasPower?White:Amber);
                RegisterHover("powerstock",power,"基地供电","耗电 "+session.PowerDemand.ToString("0")+" W · 发电 "+session.PowerProduction.ToString("0")+" W");
            }
            string goal=null;
            if(!string.IsNullOrEmpty(goal)&&colonyPanel==ColonyPanel.None&&!HasSelection)
            {
                Rect hint=new Rect(22,66,Mathf.Min(260,uiWidth-40),28);
                DrawIcon(Icon.Research,new Rect(hint.x,hint.y+5,17,17),Mint);
                Label(new Rect(hint.x+27,hint.y,hint.width-27,hint.height),goal,small,WithAlpha(White,.83f));
            }
        }
        string FirstGoal()
        {
            if(session.Buildings==null)return "建造研究台";
            if(!session.Buildings.Any(b=>b!=null&&b.isConstructed&&b.definition!=null&&b.definition.role==DeepBuildingRole.Research))return "建造研究台";
            if(session.catalog.technologies.Any(t=>t!=null&&!session.IsTechUnlocked(t.id)))return "研究第一项基地技术";
            return null;
        }
        void DrawColonyPanels()
        {
            if(pauseMenu||colonyPanel==ColonyPanel.None)return;
            PanelBackground(colonyRect);
            string heading=colonyPanel==ColonyPanel.Build?"建造":colonyPanel==ColonyPanel.Workers?"人员与工单":colonyPanel==ColonyPanel.Research?"研究与工程":colonyPanel==ColonyPanel.Resources?"库存与生产":"制造";
            Icon icon=colonyPanel==ColonyPanel.Build?Icon.Build:colonyPanel==ColonyPanel.Workers?Icon.People:colonyPanel==ColonyPanel.Research?Icon.Research:Icon.Craft;
            DrawIcon(icon,new Rect(colonyRect.x+17,colonyRect.y+16,22,22),Mint);
            Label(new Rect(colonyRect.x+52,colonyRect.y+12,colonyRect.width-100,29),heading,title,White);
            Rect close=new Rect(colonyRect.xMax-38,colonyRect.y+12,27,27);DrawIcon(Icon.Close,Inset(close,5),Muted);
            if(Click(close)){colonyPanel=ColonyPanel.None;return;}
            if(session==null||session.catalog==null){Label(new Rect(colonyRect.x+18,colonyRect.y+70,colonyRect.width-36,30),"正在准备基地",body,Muted);return;}
            switch(colonyPanel){case ColonyPanel.Build:DrawBuildPanel();break;case ColonyPanel.Workers:DrawWorkersPanel();break;case ColonyPanel.Research:DrawResearchPanel();break;case ColonyPanel.Craft:DrawCraftPanel();break;case ColonyPanel.Resources:DrawResourcePanel();break;}
        }
        void DrawBuildPanelLegacy()
        {
            var definitions=session.catalog.buildings.Where(b=>b!=null).ToArray();
            int rows=Mathf.Max(1,Mathf.FloorToInt((colonyRect.height-101)/83)),pageSize=rows*2;
            int pages=Mathf.Max(1,Mathf.CeilToInt(definitions.Length/(float)pageSize));buildingPage=Mathf.Clamp(buildingPage,0,pages-1);
            for(int index=0;index<pageSize&&buildingPage*pageSize+index<definitions.Length;index++)
            {
                var definition=definitions[buildingPage*pageSize+index];
                bool unlocked=string.IsNullOrEmpty(definition.requiredTechId)||session.IsTechUnlocked(definition.requiredTechId);
                bool affordable=CanAfford(definition.cost);
                Rect rect=new Rect(colonyRect.x+14+(index%2)*176,colonyRect.y+57+(index/2)*83,166,74);
                bool active=colonyTool==ColonyTool.Build&&buildChoice==definition;
                Rounded(rect,active?new Color(.15f,.29f,.25f):new Color(.075f,.12f,.135f),9);
                DrawAssetIcon(definition.icon,new Rect(rect.x+9,rect.y+9,38,43),unlocked?White:Muted,Icon.Build);
                Label(new Rect(rect.x+55,rect.y+11,104,21),definition.displayName,body,unlocked?White:Muted);
                Label(new Rect(rect.x+55,rect.y+34,104,20),CostText(definition.cost),tiny,affordable?Muted:Amber);
                if(!unlocked)DrawIcon(Icon.Lock,new Rect(rect.xMax-23,rect.y+5,16,16),Amber);
                string detail=definition.description+" · "+definition.footprint.x+"×"+definition.footprint.y+" · "+definition.workSeconds.ToString("0")+"秒";
                if(!unlocked)detail="需要科技："+TechName(definition.requiredTechId);
                else if(!affordable)detail="材料不足 · "+CostText(definition.cost);
                RegisterHover("build"+definition.id,rect,definition.displayName,detail);
                if(Click(rect))
                {
                    if(!unlocked){ShowToast("尚未解锁："+TechName(definition.requiredTechId),false);continue;}
                    if(!affordable){ShowToast("材料不足",false);continue;}
                    buildChoice=definition;colonyTool=ColonyTool.Build;tool=ToolMode.None;ClearSelection();
                }
            }
            if(pages>1)
            {
                float y=colonyRect.yMax-36;
                SmallButton(new Rect(colonyRect.x+18,y,73,24),"上一页",()=>buildingPage=Mathf.Max(0,buildingPage-1));
                Label(new Rect(colonyRect.center.x-30,y,60,24),(buildingPage+1)+" / "+pages,tiny,Muted);
                SmallButton(new Rect(colonyRect.xMax-91,y,73,24),"下一页",()=>buildingPage=Mathf.Min(pages-1,buildingPage+1));
            }
        }
        void DrawWorkersPanelLegacy()
        {
            float x=colonyRect.x+16,y=colonyRect.y+57,width=colonyRect.width-32;
            foreach(var worker in session.Workers)
            {
                if(worker==null)continue;
                Rect rect=new Rect(x,y,width,51);Rounded(rect,selectedWorker==worker?new Color(.13f,.26f,.23f):new Color(.065f,.105f,.12f),8);
                DrawIcon(Icon.Person,new Rect(x+10,y+12,25,25),Mint);
                Label(new Rect(x+47,y+5,width-55,21),worker.displayName,body,White);
                Label(new Rect(x+47,y+26,width-55,19),worker.Status,small,Muted);
                if(Click(rect)){SelectWorker(worker);FocusAt(worker.transform.position);}
                y+=59;
            }
            y+=9;Label(new Rect(x,y,width,25),"待办工单",body,Muted);y+=32;
            var activeOrders=session.Orders.Where(o=>o!=null&&!o.IsTerminal).ToArray();
            int rows=Mathf.Max(1,(int)((colonyRect.yMax-y-43)/57));
            int pages=Mathf.Max(1,Mathf.CeilToInt(activeOrders.Length/(float)rows));ordersPage=Mathf.Clamp(ordersPage,0,pages-1);
            foreach(var order in activeOrders.Skip(ordersPage*rows).Take(rows))
            {DrawOrderRow(order,new Rect(x,y,width,50));y+=57;}
            if(!session.Orders.Any(o=>o!=null&&!o.IsTerminal))Label(new Rect(x,y,width,28),"暂无待办任务",small,Muted);
            if(pages>1)
            {float bottom=colonyRect.yMax-32;SmallButton(new Rect(x,bottom,68,23),"上一页",()=>ordersPage=Mathf.Max(0,ordersPage-1));Label(new Rect(x+112,bottom,110,23),(ordersPage+1)+" / "+pages,tiny,Muted);SmallButton(new Rect(x+width-68,bottom,68,23),"下一页",()=>ordersPage=Mathf.Min(pages-1,ordersPage+1));}
        }
        void DrawOrderRow(DeepWorkOrder order,Rect rect)
        {
            Rounded(rect,new Color(.066f,.105f,.118f),7);
            Label(new Rect(rect.x+10,rect.y+3,rect.width-43,21),order.label,body,White);
            string state=order.worker!=null?order.worker.displayName+" · ":"";
            state+=string.IsNullOrEmpty(order.statusReason)?OrderState(order):order.statusReason;
            Label(new Rect(rect.x+10,rect.y+24,rect.width-45,20),state,small,order.state==DeepWorkState.Blocked?Amber:Muted);
            Rect close=new Rect(rect.xMax-31,rect.y+8,23,25);DrawIcon(Icon.Close,Inset(close,5),Muted);
            RegisterHover("cancelorder"+order.id,close,"取消工单","退回尚未消耗的预留材料");
            if(Click(close)){bool ok=session.CancelOrder(order,out string reason);ShowToast(reason,ok);}
            Rounded(new Rect(rect.x+10,rect.yMax-3,(rect.width-20)*Mathf.Clamp01(order.Progress),2),Mint,1);
        }
        void DrawResearchPanelLegacy()
        {
            var technologies=session.catalog.technologies.Where(t=>t!=null).ToArray();
            var depth=new Dictionary<string,int>();foreach(var tech in technologies)depth[tech.id]=0;
            for(int pass=0;pass<technologies.Length;pass++)foreach(var tech in technologies)
                foreach(string prerequisite in tech.prerequisiteIds??Array.Empty<string>())
                    if(depth.TryGetValue(prerequisite,out int d))depth[tech.id]=Mathf.Min(technologies.Length,Mathf.Max(depth[tech.id],d+1));
            int columns=Mathf.Max(1,depth.Count==0?1:depth.Values.Max()+1);float cellWidth=(colonyRect.width-32)/columns;
            var rowByDepth=new Dictionary<int,int>();var cards=new Dictionary<string,Rect>();
            foreach(var tech in technologies)
            {
                int d=depth[tech.id];if(!rowByDepth.ContainsKey(d))rowByDepth[d]=0;
                int row=rowByDepth[d]++;cards[tech.id]=new Rect(colonyRect.x+16+d*cellWidth,colonyRect.y+66+row*110,cellWidth-17,85);
            }
            foreach(var tech in technologies)foreach(string prerequisite in tech.prerequisiteIds??Array.Empty<string>())
                if(cards.TryGetValue(prerequisite,out Rect parent))
                {Rect child=cards[tech.id];Color color=session.IsTechUnlocked(prerequisite)?WithAlpha(Mint,.6f):WithAlpha(Muted,.3f);float mid=(parent.xMax+child.x)*.5f;Fill(new Rect(parent.xMax,parent.center.y,mid-parent.xMax,1),color);Fill(new Rect(mid,Mathf.Min(parent.center.y,child.center.y),1,Mathf.Abs(parent.center.y-child.center.y)),color);Fill(new Rect(mid,child.center.y,child.x-mid,1),color);}
            foreach(var tech in technologies)
            {
                Rect rect=cards[tech.id];bool complete=session.IsTechUnlocked(tech.id);
                var order=session.Orders.FirstOrDefault(o=>o!=null&&!o.IsTerminal&&o.technology==tech);
                bool can=session.CanResearch(tech,out string reason);
                Rounded(rect,complete?new Color(.13f,.26f,.21f):can?new Color(.1f,.19f,.19f):new Color(.066f,.105f,.12f),9);
                DrawAssetIcon(tech.icon,new Rect(rect.x+9,rect.y+10,25,25),complete||can?Mint:Muted,Icon.Research);
                Label(new Rect(rect.x+42,rect.y+8,rect.width-50,23),tech.displayName,body,complete||can?White:Muted);
                Label(new Rect(rect.x+10,rect.y+37,rect.width-20,19),complete?"已掌握":order!=null?"研究中 "+(order.Progress*100).ToString("0")+"%":CostText(tech.cost),small,complete?Mint:Muted);
                if(order!=null)Rounded(new Rect(rect.x+10,rect.yMax-12,(rect.width-20)*Mathf.Clamp01(order.Progress),3),Mint,1.5f);
                else if(!complete&&!can)DrawIcon(Icon.Lock,new Rect(rect.xMax-24,rect.yMax-24,14,14),Muted);
                RegisterHover("research"+tech.id,rect,tech.displayName,complete?tech.description:order!=null?order.statusReason:can?tech.description+" · "+tech.workSeconds.ToString("0")+"秒":reason);
                if(Click(rect))
                {
                    if(complete){ShowToast("已掌握此项技术",true);continue;}
                    bool ok=session.RequestResearch(tech,out string result);ShowToast(result,ok);
                }
            }
            var current=session.Orders.FirstOrDefault(o=>o!=null&&!o.IsTerminal&&o.technology!=null);
            if(current!=null)DrawOrderRow(current,new Rect(colonyRect.x+17,colonyRect.yMax-69,colonyRect.width-34,50));
        }
        void DrawCraftPanel()
        {
            float x=colonyRect.x+16,y=colonyRect.y+57,width=colonyRect.width-32;
            var recipes=session.catalog.recipes.Where(r=>r!=null&&(selectedBuilding==null||selectedBuilding.definition==null||r.requiredBuildingId==selectedBuilding.definition.id)).ToArray();
            int recipeRows=Mathf.Max(1,Mathf.Min(3,(int)((colonyRect.height-230)/67))),recipePages=Mathf.Max(1,Mathf.CeilToInt(recipes.Length/(float)recipeRows));
            recipePage=Mathf.Clamp(recipePage,0,recipePages-1);
            foreach(var recipe in recipes.Skip(recipePage*recipeRows).Take(recipeRows))
            {
                Rect rect=new Rect(x,y,width,59);Rounded(rect,recipeChoice==recipe?new Color(.13f,.25f,.22f):new Color(.065f,.105f,.12f),8);
                DrawAssetIcon(recipe.icon,new Rect(x+9,y+10,34,34),White,Icon.Craft);
                Label(new Rect(x+53,y+5,width-61,22),recipe.displayName,body,White);
                Label(new Rect(x+53,y+30,width-61,19),CostText(recipe.inputs)+"  →  "+CostText(recipe.outputs),small,Muted);
                RegisterHover("recipe"+recipe.id,rect,recipe.displayName,recipe.description);
                if(Click(rect))recipeChoice=recipe;y+=67;
            }
            if(recipePages>1)
            {
                SmallButton(new Rect(x,y,72,24),"上一页",()=>recipePage=Mathf.Max(0,recipePage-1));
                Label(new Rect(x+95,y,168,24),(recipePage+1)+" / "+recipePages+"  ·  科技解锁更多配方",tiny,Muted);
                SmallButton(new Rect(x+width-72,y,72,24),"下一页",()=>recipePage=Mathf.Min(recipePages-1,recipePage+1));y+=33;
            }
            if(recipeChoice!=null)
            {
                y+=8;Label(new Rect(x,y,65,28),"数量",body,Muted);
                SmallButton(new Rect(x+75,y,30,28),"−",()=>craftBatches=Mathf.Max(1,craftBatches-1));
                Label(new Rect(x+113,y,39,28),craftBatches.ToString(),body,White);
                SmallButton(new Rect(x+150,y,30,28),"+",()=>craftBatches=Mathf.Min(99,craftBatches+1));
                bool can=session.CanCraft(recipeChoice,craftBatches,out string reason);
                Rect submit=new Rect(x+197,y,width-197,28);ActionButton(submit,Icon.Craft,"安排制造",can);
                RegisterHover("craftsubmit",submit,"安排制造",can?CostText(recipeChoice.inputs,craftBatches):reason);
                if(Click(submit)){bool ok=session.RequestCraft(recipeChoice,craftBatches,out string result);ShowToast(result,ok);}y+=43;
                var target=session.ProductionTargetFor(recipeChoice.id);
                Label(new Rect(x,y,100,28),"目标库存",small,Muted);
                SmallButton(new Rect(x+83,y,27,27),"−",()=>productionAmount=Mathf.Max(1,productionAmount-5));
                Label(new Rect(x+119,y,39,27),productionAmount.ToString(),body,White);
                SmallButton(new Rect(x+153,y,27,27),"+",()=>productionAmount=Mathf.Min(999,productionAmount+5));
                SmallButton(new Rect(x+193,y,width-193,27),target!=null&&target.enabled?"暂停补货":"维持库存",()=>session.SetProductionTarget(recipeChoice,productionAmount,!(target!=null&&target.enabled)));y+=36;
                Label(new Rect(x,y,width,26),target==null?"开启后低于目标自动投料，达到目标停止。":target.status??"等待检查库存",small,Muted);y+=32;
            }
            foreach(var order in session.Orders.Where(o=>o!=null&&!o.IsTerminal&&o.recipe!=null).Take(Mathf.Max(0,(int)((colonyRect.yMax-y-10)/57))))
            {DrawOrderRow(order,new Rect(x,y,width,50));y+=57;}
        }
        void FindColonyHover(bool blocked)
        {
            hoveredWorker=null;hoveredBuilding=null;hoveredOrder=null;hoveredPipeNode=null;
            if(blocked||session==null||tool!=ToolMode.None)return;
            if(colonyTool==ColonyTool.Pipe){hoveredPipeNode=PipeTargetAtPointer();return;}
            if(colonyTool!=ColonyTool.None)return;
            foreach(var worker in session.Workers)
            {
                if(worker==null||!Visible(worker.Cell))continue;
                if(WorkerBounds(worker).Contains(pointer)){hoveredWorker=worker;break;}
            }
            if(hoveredWorker==null)
                foreach(var order in session.Orders)
                {
                    if(order==null||order.IsTerminal||!Visible(order.targetCell))continue;
                    if(order.kind==DeepWorkKind.Pipe&&order.fromNode!=null&&order.toNode!=null)
                    {Vector2 a=WorldPoint(PipePortPosition(order.fromNode,false,order.fromPort)),b=WorldPoint(PipePortPosition(order.toNode,true,order.fromPort)),corner=new Vector2(b.x,a.y);if(SegmentDistance(pointer,a,corner)<6||SegmentDistance(pointer,corner,b)<6){hoveredOrder=order;break;}continue;}
                    if(order.kind!=DeepWorkKind.Build&&order.kind!=DeepWorkKind.Dig)continue;
                    RectInt bounds=new RectInt(order.targetCell,order.buildingDefinition!=null?order.buildingDefinition.footprint:Vector2Int.one);if(WorldBounds(bounds).Contains(pointer)){hoveredOrder=order;break;}
                }
            if(hoveredWorker==null&&hoveredOrder==null)
                foreach(var building in session.Buildings.AsEnumerable().Reverse())
                {if(building==null||building.definition==null||!Visible(building.origin))continue;if(WorldBounds(building.Bounds).Contains(pointer)){hoveredBuilding=building;break;}}
            if(hoveredWorker!=null||hoveredBuilding!=null||hoveredOrder!=null){hoveredNode=null;hoveredLink=null;}
        }
        void DrawColonyWorldAccents(bool overInterface)
        {
            if(session==null||pauseMenu)return;
            foreach(var order in session.Orders)
            {
                if(order==null||order.IsTerminal||!Visible(order.targetCell))continue;
                if(order.kind==DeepWorkKind.Pipe&&order.fromNode!=null&&order.toNode!=null)
                {
                    Vector2 from=WorldPoint(PipePortPosition(order.fromNode,false,order.fromPort)),to=WorldPoint(PipePortPosition(order.toNode,true,order.fromPort));
                    Color pipeTint=order.state==DeepWorkState.Blocked?Amber:Mint;DrawPipePath(from,to,WithAlpha(pipeTint,.48f),1,true);
                    if(hoveredOrder==order)RegisterHover("pipeorder"+order.id,new Rect(pointer.x-8,pointer.y-8,16,16),order.label,order.statusReason);
                    continue;
                }
                if(order.kind!=DeepWorkKind.Build&&order.kind!=DeepWorkKind.Dig)continue;
                RectInt bounds=new RectInt(order.targetCell,order.buildingDefinition!=null?order.buildingDefinition.footprint:Vector2Int.one);
                Rect rect=WorldBounds(bounds);Color tint=order.state==DeepWorkState.Blocked?Amber:Mint;
                Fill(rect,WithAlpha(tint,.055f));Brackets(rect,WithAlpha(tint,.63f),7);
                if(order.buildingDefinition!=null&&order.buildingDefinition.icon!=null)DrawAssetIcon(order.buildingDefinition.icon,Inset(rect,2),WithAlpha(tint,.25f),Icon.Build);
                DrawIcon(order.kind==DeepWorkKind.Dig?Icon.Dig:Icon.Build,new Rect(rect.center.x-9,rect.y-21,18,18),tint);
                if(order.Progress>0)Rounded(new Rect(rect.x,rect.yMax+3,rect.width*order.Progress,3),tint,1);
                if(hoveredOrder==order)RegisterHover("planned"+order.id,rect,order.label,order.statusReason);
            }
            if(colonyTool==ColonyTool.Pipe&&!overInterface)DrawPipePreview();
            if((colonyTool==ColonyTool.Build||colonyTool==ColonyTool.Dig)&&!overInterface)
            {
                Vector2Int cell=world.WorldToCell(PointerWorld(pointer));
                string reason="";bool allowed;
                RectInt bounds;
                if(colonyTool==ColonyTool.Build&&buildChoice!=null)
                {bounds=new RectInt(cell,buildChoice.footprint);allowed=session.CanBuild(buildChoice,cell,out reason);}
                else{bounds=new RectInt(cell,Vector2Int.one);allowed=session.CanDig(cell,out reason);if(allowed)reason=session.ExcavationRisk(cell);}
                Rect rect=WorldBounds(bounds);Color color=allowed?Mint:new Color(.94f,.34f,.27f);
                Fill(rect,WithAlpha(color,.13f));Brackets(rect,WithAlpha(color,.92f),Mathf.Min(12,rect.width*.25f));
                if(colonyTool==ColonyTool.Build&&buildChoice!=null&&buildChoice.icon!=null)DrawAssetIcon(buildChoice.icon,Inset(rect,2),WithAlpha(color,.38f),Icon.Build);
                DrawIcon(colonyTool==ColonyTool.Build?Icon.Build:Icon.Dig,new Rect(pointer.x+13,pointer.y+12,21,21),color);
                if(!string.IsNullOrEmpty(reason))
                {Rect hint=new Rect(Mathf.Min(pointer.x+38,uiWidth-244),Mathf.Min(pointer.y+13,uiHeight-120),227,28);Rounded(hint,Panel,7);Label(Inset(hint,7),reason,small,Amber);}
            }
            DeepWorker worker=hoveredWorker!=null?hoveredWorker:selectedWorker;
            if(worker!=null&&Visible(worker.Cell))
            {
                Rect rect=WorkerBounds(worker);Brackets(rect,Mint,7);
                if(hoveredWorker!=null)RegisterHover("worker"+worker.GetInstanceID(),rect,worker.displayName,worker.Status);
            }
            DeepBuildingInstance building=hoveredBuilding!=null?hoveredBuilding:selectedBuilding;
            if(building!=null)
            {
                Rect rect=WorldBounds(building.Bounds);Brackets(rect,WithAlpha(Mint,.8f),8);
                if(hoveredBuilding!=null)RegisterHover("building"+building.GetInstanceID(),rect,building.definition.displayName,BuildingStatus(building)+(building.definition.powerRequired>0?" · "+building.definition.powerRequired.ToString("0")+" W":""));
            }
        }
        bool HandleColonyInput(Event e,bool blocked)
        {
            if(HandleCommandInput(e,blocked))return true;
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){TogglePauseMenu();e.Use();return true;}
            if(pauseMenu)return true;
            if(session==null)return false;
            if(e.type==EventType.MouseDown&&e.button==1)
            {
                if(colonyTool!=ColonyTool.None||tool!=ToolMode.None){colonyTool=ColonyTool.None;tool=ToolMode.None;buildChoice=null;pipeSource=null;e.Use();return true;}
                if(!blocked&&selectedWorker!=null)
                {bool ok=session.RequestMove(selectedWorker,world.WorldToCell(PointerWorld(pointer)),out string reason);ShowToast(reason,ok);e.Use();return true;}
            }
            if(blocked)return false;
            if((e.type==EventType.MouseDown||e.type==EventType.MouseDrag)&&e.button==0)
            {
                Vector2Int cell=world.WorldToCell(PointerWorld(pointer));
                if(colonyTool==ColonyTool.Pipe)
                {
                    if(e.type==EventType.MouseDown)
                    {
                        if(pipeSource!=null&&hoveredPipeNode!=null&&hoveredPipeNode!=pipeSource)
                        {bool ok=session.RequestPipe(pipeSource,hoveredPipeNode,pipePort,out string reason);ShowToast(reason,ok);colonyTool=ColonyTool.None;pipeSource=null;}
                        else ShowToast("请选择另一台气体设备",false);
                    }
                    e.Use();return true;
                }
                if(colonyTool==ColonyTool.Build&&buildChoice!=null)
                {if(e.type==EventType.MouseDown){bool ok=session.RequestBuild(buildChoice,cell,out string reason);ShowToast(reason,ok);}e.Use();return true;}
                if(colonyTool==ColonyTool.Dig)
                {if(e.type==EventType.MouseDown||cell!=lastDigCell){lastDigCell=cell;bool ok=session.RequestDig(cell,out string reason);ShowToast(reason,ok);}e.Use();return true;}
                if(e.type==EventType.MouseDown&&hoveredWorker!=null){SelectWorker(hoveredWorker);e.Use();return true;}
                if(e.type==EventType.MouseDown&&hoveredOrder!=null){var order=hoveredOrder;ClearSelection();selectedOrder=order;e.Use();return true;}
                if(e.type==EventType.MouseDown&&hoveredBuilding!=null){var building=hoveredBuilding;ClearSelection();selectedBuilding=building;e.Use();return true;}
                ClearColonySelection();
            }
            return false;
        }
        void SelectWorker(DeepWorker worker){ClearSelection();selectedWorker=worker;colonyTool=ColonyTool.None;}
        Rect ColonySelectionRect()
        {
            Vector3 position=selectedWorker!=null?selectedWorker.transform.position:selectedOrder!=null?world.CellToWorld(selectedOrder.targetCell):selectedBuilding.transform.position;
            Vector2 p=WorldPoint(position);float height=selectedWorker!=null?320:selectedOrder!=null?262:selectedBuilding.GetComponentInChildren<GasNode>()!=null?500:selectedBuilding.definition!=null&&selectedBuilding.definition.role==DeepBuildingRole.Storage?365:330;
            float x=p.x+38;if(x+254>uiWidth-16)x=p.x-292;
            if(colonyPanel!=ColonyPanel.None&&x<colonyRect.xMax+12)x=colonyRect.xMax+14;
            return new Rect(Mathf.Clamp(x,16,uiWidth-270),Mathf.Clamp(p.y-height*.5f,74,uiHeight-height-97),254,height);
        }
        void DrawColonySelectionCard(float x,ref float y)
        {
            if(selectedOrder!=null&&!selectedOrder.IsTerminal)
            {
                CardTitle(x,ref y,selectedOrder.kind==DeepWorkKind.Pipe?Icon.Flow:selectedOrder.kind==DeepWorkKind.Dig?Icon.Dig:Icon.Build,selectedOrder.label);
                DrawOrderRow(selectedOrder,new Rect(x,y,220,58));y+=72;
                Label(new Rect(x,y,220,25),selectedOrder.Progress>0?"完成 "+(selectedOrder.Progress*100).ToString("0")+"%":"等待人员领取",small,Muted);y+=33;
                DrawPriorityPicker(new Rect(x,y,220,31),selectedOrder);return;
            }
            if(selectedWorker!=null)
            {
                CardTitle(x,ref y,Icon.Person,selectedWorker.displayName);
                Label(new Rect(x,y,220,26),selectedWorker.Status,body,White);y+=33;
                if(selectedWorker.currentOrder!=null&&!selectedWorker.currentOrder.IsTerminal){DrawOrderRow(selectedWorker.currentOrder,new Rect(x,y,220,55));y+=65;}
                else{Label(new Rect(x,y,220,26),"等待新任务",small,Muted);y+=45;}
                Label(new Rect(x,y,220,24),"右键地点 · 派遣移动",small,Mint);y+=32;
                Label(new Rect(x,y,220,22),selectedWorker.environmentUnsafe?"气氛不适 · 呼吸缓冲 "+selectedWorker.airReserveSeconds.ToString("0")+"秒":"当前气氛适宜作业",small,selectedWorker.environmentUnsafe?Amber:Muted);y+=27;
                var picked=selectedWorker;
                SmallButton(new Rect(x,y,104,29),picked.automationPaused?"恢复调度":"停止待命",()=>{if(picked.automationPaused)session.ResumeWorker(picked);else{bool ok=session.RequestStop(picked,out string reason);ShowToast(reason,ok);}});
                SmallButton(new Rect(x+113,y,104,29),"人员分工",()=>colonyPanel=ColonyPanel.Workers);return;
            }
            var building=selectedBuilding;if(building==null||building.definition==null)return;
            var definition=building.definition;CardTitle(x,ref y,BuildingIcon(definition.role),definition.displayName);
            Label(new Rect(x,y,220,23),BuildingStatus(building),body,building.IsOperational?Mint:Amber);y+=30;
            if(definition.role==DeepBuildingRole.Storage)
            {
                Metric(new Rect(x,y,220,24),Icon.Storage,session.inventory.UsedCapacity+" / "+session.inventory.Capacity);y+=29;
                Label(new Rect(x,y,220,23),"此仓库容量 +"+definition.storageCapacity,small,Muted);y+=28;
                foreach(var item in session.catalog.items.Take(6)){if(item==null)continue;Label(new Rect(x,y,220,21),item.displayName+"  "+session.inventory.GetAmount(item),small,White);y+=22;}return;
            }
            if(definition.powerRequired>0||definition.powerGenerated>0)
            {Metric(new Rect(x,y,220,24),Icon.Power,definition.powerGenerated>0?"供电 "+(building.isOn&&building.powered?definition.powerGenerated:0).ToString("0")+" W":"功耗 "+(building.isOn?definition.powerRequired:0).ToString("0")+" W");y+=28;Label(new Rect(x,y,220,25),session.PowerStatus(building),small,building.powered?Muted:Amber);y+=30;}
            if(definition.role==DeepBuildingRole.Battery){Metric(new Rect(x,y,220,26),Icon.Battery,building.batteryEnergy.ToString("0")+" / "+definition.batteryCapacity.ToString("0")+" J");y+=32;}
            var gasNode=building.GetComponentInChildren<GasNode>();
            if(gasNode!=null)
            {
                Label(new Rect(x,y,220,34),DeepGasFacility.Function(definition),new GUIStyle(small){wordWrap=true},Mint);y+=37;
                PressureValue(x,ref y,gasNode.PressureKPa,NodeStatus(gasNode));DrawComposition(x,ref y,gasNode.gas);
                Metric(new Rect(x,y,135,24),Icon.Flow,Math.Max(gasNode.lastInflowMolPerSecond,gasNode.lastOutflowMolPerSecond).ToString("0.0")+" mol/s");
                int output=0;foreach(var link in network.links)
                {if(link==null||link.from!=gasNode||output>=2)continue;Rect valve=new Rect(x+153+output*31,y,25,25);DrawIcon(Icon.Valve,Inset(valve,4),link.isOpen?Mint:Muted);RegisterHover("buildingvalve"+link.GetInstanceID(),valve,LinkName(link),link.isOpen?"点击关闭阀门":"点击开启阀门");if(Click(valve))link.isOpen=!link.isOpen;output++;}y+=33;
                DrawPipeButtons(x,y,gasNode);y+=38;
            }
            if(building.isConstructed)
            {
                Rect toggle=new Rect(x,y,220,31);ActionButton(toggle,building.isOn?Icon.Power:Icon.Pause,building.isOn?"关闭设施":"开启设施",building.isOn);
                if(Click(toggle))session.ToggleBuilding(building);y+=42;
            }
            else
            {
                var order=session.Orders.FirstOrDefault(o=>o!=null&&!o.IsTerminal&&o.targetBuilding==building);
                if(order!=null){DrawOrderRow(order,new Rect(x,y,220,51));y+=60;}
            }
            if(definition.role==DeepBuildingRole.Fabricator)
            {Rect craft=new Rect(x,y,220,31);ActionButton(craft,Icon.Craft,"制造配方",true);if(Click(craft)){colonyPanel=ColonyPanel.Craft;recipeChoice=null;}}
            if(definition.role==DeepBuildingRole.Research)
            {Rect research=new Rect(x,y,220,31);ActionButton(research,Icon.Research,"打开科技树",true);if(Click(research))colonyPanel=ColonyPanel.Research;}
        }
        void TogglePauseMenu()
        {
            if(!pauseMenu){pauseBeforeMenu=ColonyPaused;pauseMenu=true;settingsMenu=false;if(session!=null)session.menuOpen=true;if(network!=null)network.paused=true;}
            else ClosePauseMenu();
        }
        void DrawPipeButtons(float x,float y,GasNode source)
        {
            if(session==null||source==null)return;
            if(source.kind==GasNodeKind.Separator)
            {
                Rect oxygen=new Rect(x,y,104,28),tail=new Rect(x+113,y,104,28);
                ActionButton(oxygen,Icon.Flow,"接氧气",false);ActionButton(tail,Icon.Flow,"接尾气",false);
                RegisterHover("pipeoxygen"+source.GetInstanceID(),oxygen,"氧气出口接管","选择另一台设备，安排管道施工");
                RegisterHover("pipetail"+source.GetInstanceID(),tail,"尾气出口接管","选择另一台设备，安排管道施工");
                if(Click(oxygen))BeginPipe(source,GasOutputPort.OxygenProduct);
                else if(Click(tail))BeginPipe(source,GasOutputPort.TailGas);
            }
            else
            {
                Rect connect=new Rect(x,y,217,28);ActionButton(connect,Icon.Flow,"接管",false);
                RegisterHover("pipeconnect"+source.GetInstanceID(),connect,"连接供气管道","选择另一台设备，安排管道施工");
                if(Click(connect))BeginPipe(source,GasOutputPort.Mixed);
            }
        }
        void BeginPipe(GasNode source,GasOutputPort port)
        {pipeSource=source;pipePort=port;colonyTool=ColonyTool.Pipe;tool=ToolMode.None;colonyPanel=ColonyPanel.None;ClearSelection();ShowToast("选择要接入的气体设备",true);}
        GasNode PipeTargetAtPointer()
        {
            foreach(var building in session.Buildings.AsEnumerable().Reverse())
            {
                if(building==null||!building.isConstructed||!Visible(building.origin)||!WorldBounds(building.Bounds).Contains(pointer))continue;
                var node=building.GetComponentInChildren<GasNode>();if(node!=null)return node;
            }
            foreach(var node in network.nodes)
            {
                if(node==null||!Visible(world.WorldToCell(node.transform.position)))continue;
                foreach(var renderer in node.GetComponentsInChildren<SpriteRenderer>())
                {
                    if(renderer.sprite==null||!renderer.sprite.name.EndsWith("_Color",StringComparison.Ordinal))continue;
                    Bounds bounds=renderer.bounds;Vector2 a=WorldPoint(bounds.min),b=WorldPoint(bounds.max);
                    if(Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y)).Contains(pointer))return node;
                }
                if(Vector2.Distance(WorldPoint(node.transform.position),pointer)<28)return node;
            }
            return null;
        }
        void DrawPipePreview()
        {
            if(pipeSource==null){colonyTool=ColonyTool.None;return;}
            Vector2 from=WorldPoint(PipePortPosition(pipeSource,false,pipePort));
            Vector2 to=hoveredPipeNode!=null?WorldPoint(PipePortPosition(hoveredPipeNode,true,pipePort)):pointer;
            Color color=pipePort==GasOutputPort.TailGas?new Color(.7f,.66f,.93f):pipePort==GasOutputPort.OxygenProduct?Mint:Amber;
            string reason="";bool valid=hoveredPipeNode!=null&&hoveredPipeNode!=pipeSource&&session.CanPipe(pipeSource,hoveredPipeNode,pipePort,out reason);
            if(hoveredPipeNode!=null&&!valid)color=new Color(.95f,.31f,.22f);
            DrawPipePath(from,to,WithAlpha(color,.76f),2,false);
            Rounded(new Rect(from.x-4,from.y-4,8,8),color,4);Rounded(new Rect(to.x-4,to.y-4,8,8),color,4);
            if(hoveredPipeNode!=null)
            {
                var alloy=session.catalog.FindItem("alloy");string detail=valid?(alloy==null?"合金":alloy.displayName)+" "+session.PipeCost(pipeSource,hoveredPipeNode)+" · 点击安排施工":string.IsNullOrEmpty(reason)?"请选择另一台设备":reason;
                RegisterHover("pipetarget"+hoveredPipeNode.GetInstanceID(),new Rect(pointer.x-8,pointer.y-8,16,16),NodeName(hoveredPipeNode),detail);
            }
            DrawIcon(Icon.Flow,new Rect(pointer.x+14,pointer.y+14,22,22),color);
        }
        static void DrawPipePath(Vector2 from,Vector2 to,Color color,float thickness,bool dashed)
        {
            float left=Mathf.Min(from.x,to.x),top=Mathf.Min(from.y,to.y),width=Mathf.Abs(to.x-from.x),height=Mathf.Abs(to.y-from.y);
            if(!dashed){Fill(new Rect(left,from.y-thickness*.5f,width,thickness),color);Fill(new Rect(to.x-thickness*.5f,top,thickness,height),color);return;}
            for(float at=0;at<width;at+=13)Fill(new Rect(left+at,from.y-thickness*.5f,Mathf.Min(7,width-at),thickness),color);
            for(float at=0;at<height;at+=13)Fill(new Rect(to.x-thickness*.5f,top+at,thickness,Mathf.Min(7,height-at)),color);
        }
        static Vector3 PipePortPosition(GasNode node,bool inlet,GasOutputPort output)
        {
            var placement=node.GetComponentInParent<DeepDevicePlacement>();
            if(placement!=null)return placement.transform.TransformPoint(inlet?placement.inletOffset:output==GasOutputPort.TailGas?placement.tailOffset:placement.productOffset);
            return node.transform.position;
        }
        void ClosePauseMenu(){pauseMenu=false;settingsMenu=false;if(session!=null)session.menuOpen=false;SetColonyPause(pauseBeforeMenu);}
        void DrawPauseMenu()
        {
            if(!pauseMenu)return;drawingPauseMenu=true;
            Fill(new Rect(0,0,uiWidth,uiHeight),new Color(.005f,.017f,.026f,.80f));
            Rect rect=new Rect((uiWidth-380)*.5f,(uiHeight-470)*.5f,380,470);PanelBackground(rect);
            Label(new Rect(rect.x+30,rect.y+23,282,33),settingsMenu?"设置":"基地已暂停",title,White);
            if(!settingsMenu)
            {
                PauseButton(new Rect(rect.x+28,rect.y+77,324,43),Icon.Play,"继续工程",ClosePauseMenu);
                PauseButton(new Rect(rect.x+28,rect.y+131,324,43),Icon.Storage,"保存档案",()=>OpenSaveBrowser(true));
                PauseButton(new Rect(rect.x+28,rect.y+185,324,43),Icon.Home,"读取档案",()=>OpenSaveBrowser(false));
                PauseButton(new Rect(rect.x+28,rect.y+239,324,43),Icon.Settings,"声音与效果",()=>settingsMenu=true);
                PauseButton(new Rect(rect.x+28,rect.y+293,324,43),Icon.Home,"保存并返回主菜单",ReturnMainMenu);
                PauseButton(new Rect(rect.x+28,rect.y+347,324,43),Icon.Explore,"回到基地镜头",()=>{viewCamera.transform.position=startingCameraPosition;viewCamera.orthographicSize=startingCameraSize;ClearSelection();ClosePauseMenu();});
                Label(new Rect(rect.x+29,rect.y+420,324,23),"Esc 继续  ·  F5 快存  ·  F9 档案",small,Muted);
            }
            else
            {
                Label(new Rect(rect.x+29,rect.y+81,286,24),"音量",body,White);
                AudioListener.volume=GUI.HorizontalSlider(new Rect(rect.x+29,rect.y+119,243,19),AudioListener.volume,0,1);
                Label(new Rect(rect.x+277,rect.y+106,42,27),(AudioListener.volume*100).ToString("0"),small,Muted);
                Rect particles=new Rect(rect.x+29,rect.y+162,284,33);ActionButton(particles,Icon.Air,particlesEnabled?"粒子效果：开启":"粒子效果：关闭",particlesEnabled);
                if(Click(particles))SetParticles(!particlesEnabled);
                PauseButton(new Rect(rect.x+28,rect.y+242,286,43),Icon.Close,"返回",()=>settingsMenu=false);
            }
            drawingPauseMenu=false;
        }
        void SetParticles(bool value)
        {
            particlesEnabled=value;
            if(!value)
            {
                particleVisibility.Clear();dustActivity.Clear();particleActivity.Clear();feedbackActivity.Clear();
                foreach(var feedback in world.GetComponentsInChildren<DeepParticleFeedback>(true)){feedbackActivity[feedback]=feedback.effectsEnabled;feedback.effectsEnabled=false;}
                foreach(var dust in world.GetComponentsInChildren<DeepDustMotion>(true))
                {dustActivity[dust]=dust.enabled;dust.enabled=false;var sprite=dust.GetComponent<SpriteRenderer>();if(sprite!=null){particleVisibility[sprite]=sprite.enabled;sprite.enabled=false;}}
                foreach(var particles in world.GetComponentsInChildren<ParticleSystem>(true)){particleActivity[particles]=particles.isPlaying;particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
            }
            else
            {
                foreach(var pair in dustActivity)if(pair.Key!=null)pair.Key.enabled=pair.Value;
                foreach(var pair in particleVisibility)if(pair.Key!=null)pair.Key.enabled=pair.Value;
                foreach(var pair in particleActivity)if(pair.Key!=null&&pair.Value)pair.Key.Play(true);
                foreach(var pair in feedbackActivity)if(pair.Key!=null)pair.Key.effectsEnabled=pair.Value;
            }
        }
        void PauseButton(Rect rect,Icon icon,string text,Action action){ActionButton(rect,icon,text,true);if(Click(rect))action();}
        void SmallButton(Rect rect,string text,Action action){Rounded(rect,new Color(.12f,.20f,.20f),6);Label(Inset(rect,6),text,small,White);if(Click(rect))action();}
        void PanelBackground(Rect rect){Rounded(new Rect(rect.x+2,rect.y+5,rect.width,rect.height),new Color(0,0,0,.2f),12);Rounded(rect,Border,12);Rounded(Inset(rect,1),Panel,11);}
        bool CanAfford(DeepItemAmount[] costs){return session.inventory!=null&&session.inventory.CanAfford(costs,out _);}
        string CostText(DeepItemAmount[] amounts,int batches=1){if(amounts==null||amounts.Length==0)return "无材料消耗";return string.Join(" · ",amounts.Where(c=>c.item!=null).Select(c=>c.item.displayName+" "+(c.amount*batches)));}
        string TechName(string id){var tech=session.catalog.FindTech(id);return tech==null?id:tech.displayName;}
        static string OrderState(DeepWorkOrder order){switch(order.state){case DeepWorkState.Moving:return "前往作业点";case DeepWorkState.Working:return "作业中";case DeepWorkState.Blocked:return "等待条件";case DeepWorkState.Completed:return "完成";case DeepWorkState.Cancelled:return "已取消";default:return "等待人员";}}
        static string BuildingStatus(DeepBuildingInstance b){if(!b.isConstructed)return "施工中";if(!b.isOn)return "已关闭";if(b.definition.role==DeepBuildingRole.Generator&&!b.powered)return "缺少燃料";if(b.definition.powerRequired>0&&!b.powered)return "等待供电";return b.IsOperational?"运行正常":"等待工作条件";}
        static Icon BuildingIcon(DeepBuildingRole role){switch(role){case DeepBuildingRole.Light:return Icon.Lamp;case DeepBuildingRole.Generator:return Icon.Power;case DeepBuildingRole.Research:return Icon.Research;case DeepBuildingRole.Fabricator:return Icon.Craft;case DeepBuildingRole.Storage:return Icon.Storage;default:return Icon.Build;}}
        Rect WorldBounds(RectInt bounds){Vector2 a=WorldPoint(world.transform.TransformPoint(new Vector3(bounds.xMin*world.cellSize,bounds.yMax*world.cellSize,0))),b=WorldPoint(world.transform.TransformPoint(new Vector3(bounds.xMax*world.cellSize,bounds.yMin*world.cellSize,0)));return Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));}
        Rect WorkerBounds(DeepWorker worker)
        {
            var renderer=worker.visualRenderer;
            if(renderer!=null)
            {Bounds bounds=renderer.bounds;Vector2 a=WorldPoint(new Vector3(bounds.min.x,bounds.max.y,bounds.center.z)),b=WorldPoint(new Vector3(bounds.max.x,bounds.min.y,bounds.center.z));return Inset(Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y)),-3);}
            Vector2 bottom=WorldPoint(worker.transform.position);float height=Mathf.Max(28,1.8f*Screen.height/(viewCamera.orthographicSize*2*scale));return new Rect(bottom.x-height*.32f,bottom.y-height,height*.64f,height);
        }
        void FocusAt(Vector3 position){viewCamera.transform.position=new Vector3(position.x,position.y+1.5f,viewCamera.transform.position.z);viewCamera.orthographicSize=Mathf.Min(viewCamera.orthographicSize,9);}
        static void DrawAssetIcon(Sprite sprite,Rect rect,Color color,Icon fallback)
        {
            if(sprite==null){DrawIcon(fallback,rect,color);return;}
            var previous=GUI.color;GUI.color=color;Rect texture=sprite.textureRect;
            float aspect=texture.width/Mathf.Max(1,texture.height),height=Mathf.Min(rect.height,rect.width/aspect),width=height*aspect;
            Rect draw=new Rect(rect.center.x-width*.5f,rect.center.y-height*.5f,width,height);
            GUI.DrawTextureWithTexCoords(draw,sprite.texture,new Rect(texture.x/sprite.texture.width,texture.y/sprite.texture.height,texture.width/sprite.texture.width,texture.height/sprite.texture.height));GUI.color=previous;
        }
    }
}

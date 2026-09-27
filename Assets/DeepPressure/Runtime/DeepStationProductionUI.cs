using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Icon=DeepPressure.DeepUIIcons.Icon;

namespace DeepPressure
{
    public sealed partial class DeepPressureHUD
    {
        DeepBuildingInstance craftStation;
        Vector2 stationRecipeScroll,stationQueueScroll,stationDirectoryScroll;
        readonly Dictionary<int,float> shownProductionProgress=new Dictionary<int,float>();
        readonly Dictionary<int,int> lastStationCompletion=new Dictionary<int,int>();
        float recipeChangedAt,completionShownAt=-10;

        bool IsProductionStation(DeepBuildingInstance station)=>station!=null&&station.definition!=null&&station.isConstructed&&session.Buildings.Contains(station)&&session.catalog.recipes.Any(r=>r!=null&&session.StationCanMake(station,r));
        void ToggleProductionPanel()
        {
            if(colonyPanel==ColonyPanel.Craft){colonyPanel=ColonyPanel.None;return;}
            if(IsProductionStation(selectedBuilding))OpenStationProduction(selectedBuilding);else OpenProductionDirectory();
        }
        void OpenProductionDirectory()
        {
            ClearSelection();craftStation=null;recipeChoice=null;colonyPanel=ColonyPanel.Craft;colonyTool=ColonyTool.None;tool=ToolMode.None;
        }
        void OpenStationProduction(DeepBuildingInstance station)
        {
            if(!IsProductionStation(station)){ShowToast("这台设备没有可用的生产配方",false);return;}
            ClearSelection();craftStation=station;colonyPanel=ColonyPanel.Craft;colonyTool=ColonyTool.None;tool=ToolMode.None;
            recipeChoice=session.catalog.recipes.FirstOrDefault(r=>r!=null&&session.StationCanMake(station,r)&&session.IsTechUnlocked(r.requiredTechId));
            if(recipeChoice==null)recipeChoice=session.catalog.recipes.FirstOrDefault(r=>r!=null&&session.StationCanMake(station,r));
            craftBatches=1;stationRecipeScroll=stationQueueScroll=Vector2.zero;recipeChangedAt=Time.unscaledTime;
        }
        void DrawStationProductionPanel()
        {
            if(craftStation!=null&&!IsProductionStation(craftStation)){craftStation=null;recipeChoice=null;}
            if(craftStation==null){DrawProductionDirectory();return;}
            var station=craftStation;
            var queue=session.CraftQueueFor(station);
            float x=colonyRect.x+20,y=colonyRect.y+56,w=colonyRect.width-40;
            Rounded(new Rect(x,y,w,61),new Color(.09f,.145f,.15f),6);
            SemanticIcon(DeepSemanticIcons.ForBuilding(station.definition),new Rect(x+10,y+10,40,40),White);
            Label(new Rect(x+64,y+7,w-355,22),station.definition.displayName+"  ·  ("+station.origin.x+", "+station.origin.y+")",body,White);
            Label(new Rect(x+64,y+32,w-355,18),BuildingStatus(station)+"  ·  本台队列 "+queue.Count+" 项",small,station.IsOperational?Mint:Amber);
            SmallButton(new Rect(x+w-272,y+16,79,29),"定位设备",()=>FocusAt(station.transform.position));
            SmallButton(new Rect(x+w-183,y+16,76,29),station.isOn?"关闭设备":"开启设备",()=>{session.ToggleBuilding(station);ShowToast(station.isOn?"设备已开启":"设备已关闭，队列保留",true);});
            SmallButton(new Rect(x+w-97,y+16,87,29),"设备列表",OpenProductionDirectory);
            y+=78;
            float listWidth=Mathf.Clamp(w*.29f,224,260);
            Rect left=new Rect(x,y,listWidth,colonyRect.yMax-y-20);
            Rect right=new Rect(left.xMax+22,y,w-listWidth-22,left.height);
            Fill(new Rect(left.xMax+10,y,1,left.height),Border);
            DrawStationRecipes(station,left);
            if(recipeChoice==null){Label(new Rect(right.x,right.y,right.width,32),"选择本设备的生产配方",body,Muted);return;}
            DrawProductionDetail(station,recipeChoice,right,queue);
        }
        void DrawProductionDirectory()
        {
            float x=colonyRect.x+22,y=colonyRect.y+62,w=colonyRect.width-44;
            Label(new Rect(x,y,w,24),"选择一台已建成设备，管理它的配方和生产队列",body,White);y+=40;
            var stations=session.Buildings.Where(IsProductionStation).OrderBy(b=>b.origin.x).ThenBy(b=>b.origin.y).ToArray();
            if(stations.Length==0)
            {
                DrawIcon(Icon.Craft,new Rect(x+22,y+27,56,56),Muted);
                Label(new Rect(x+106,y+20,w-120,28),"还没有生产设备",title,White);
                Label(new Rect(x+106,y+60,w-120,27),"先建造合成台；工人需要到台前加工原料。",body,Muted);
                SmallButton(new Rect(x+106,y+110,180,37),"打开合成台建造",()=>{colonyPanel=ColonyPanel.Build;buildCategory="制造";buildingPage=0;});
                return;
            }
            var viewport=new Rect(x,y,w,colonyRect.yMax-y-24);float cellWidth=(w-16)*.5f;
            Vector2 previousPointer=pointer;
            stationDirectoryScroll=GUI.BeginScrollView(viewport,stationDirectoryScroll,new Rect(0,0,w-18,Mathf.Max(viewport.height,Mathf.Ceil(stations.Length/2f)*108)));
            pointer=previousPointer-viewport.position+stationDirectoryScroll;
            for(int i=0;i<stations.Length;i++)
            {
                var station=stations[i];Rect hit=new Rect((i%2)*cellWidth,(i/2)*108,cellWidth-12,96);Rect r=ButtonVisual(hit);
                Rounded(r,new Color(.085f,.14f,.15f),6);
                SemanticIcon(DeepSemanticIcons.ForBuilding(station.definition),new Rect(r.x+14,r.y+19,48,48),White);
                Label(new Rect(r.x+78,r.y+13,r.width-90,26),station.definition.displayName,body,White);
                Label(new Rect(r.x+78,r.y+43,r.width-90,20),BuildingStatus(station)+" · ("+station.origin.x+", "+station.origin.y+")",small,station.IsOperational?Mint:Amber);
                Label(new Rect(r.x+78,r.y+69,r.width-90,18),session.CraftQueueFor(station).Count+" 项排队  ·  查看配方 →",small,Muted);
                if(Click(hit))OpenStationProduction(station);
            }
            GUI.EndScrollView();pointer=previousPointer;
        }
        void DrawStationRecipes(DeepBuildingInstance station,Rect rect)
        {
            Label(new Rect(rect.x,rect.y,rect.width,22),"本台配方",small,Muted);
            var recipes=session.catalog.recipes.Where(r=>r!=null&&session.StationCanMake(station,r)).ToArray();
            Rect viewport=new Rect(rect.x,rect.y+33,rect.width,rect.height-33);
            Vector2 previousPointer=pointer;
            stationRecipeScroll=GUI.BeginScrollView(viewport,stationRecipeScroll,new Rect(0,0,rect.width-18,Mathf.Max(viewport.height,recipes.Length*76)));
            pointer=previousPointer-viewport.position+stationRecipeScroll;
            for(int i=0;i<recipes.Length;i++)
            {
                var recipe=recipes[i];bool selected=recipeChoice==recipe,unlocked=session.IsTechUnlocked(recipe.requiredTechId);
                Rect hit=new Rect(0,i*76,rect.width-22,68);Rect r=ButtonVisual(hit);
                Rounded(r,selected?new Color(.15f,.265f,.24f):new Color(.07f,.115f,.125f),5);
                if(selected)Fill(new Rect(r.x,r.y+8,3,r.height-16),Mint);
                DrawAssetIcon(recipe.icon,new Rect(r.x+12,r.y+13,35,35),unlocked?White:Muted,Icon.Craft);
                Label(new Rect(r.x+59,r.y+10,r.width-67,23),recipe.displayName,body,unlocked?White:Muted);
                Label(new Rect(r.x+59,r.y+35,r.width-67,20),unlocked?recipe.workSeconds.ToString("0.#")+" 秒 / 批次":"需研究 · "+TechName(recipe.requiredTechId),small,unlocked?Muted:Amber);
                if(Click(hit)){recipeChoice=recipe;craftBatches=1;recipeChangedAt=Time.unscaledTime;var target=session.ProductionTargetForAt(station,recipe.id);productionAmount=target?.targetAmount??10;}
            }
            GUI.EndScrollView();pointer=previousPointer;
        }
        void DrawProductionDetail(DeepBuildingInstance station,DeepRecipeDefinition recipe,Rect rect,List<DeepWorkOrder> queue)
        {
            float x=rect.x,y=rect.y,w=rect.width;
            float arrival=EaseOut((Time.unscaledTime-recipeChangedAt)/.16f);
            Label(new Rect(x+6*(1-arrival),y,w,28),recipe.displayName,title,White);
            Label(new Rect(x,y+32,w,31),recipe.description,new GUIStyle(small){wordWrap=true},Muted);
            float materialWidth=(w-44)*.5f;
            DrawRecipeMaterials(new Rect(x,y+73,materialWidth,98),recipe.inputs,craftBatches,true);
            DrawIcon(Icon.Flow,new Rect(x+materialWidth+10,y+113,24,24),Mint);
            DrawRecipeMaterials(new Rect(x+materialWidth+44,y+73,materialWidth,98),recipe.outputs,craftBatches,false);
            Label(new Rect(x,y+181,w,22),"加工设备："+station.definition.displayName+"  ·  基础工时 "+(recipe.workSeconds*craftBatches).ToString("0.#")+" 秒",small,Muted);
            Label(new Rect(x,y+218,55,29),"批次数",small,Muted);
            SmallButton(new Rect(x+57,y+216,29,30),"−",()=>craftBatches=Mathf.Max(1,craftBatches-1));
            Label(new Rect(x+91,y+217,39,29),craftBatches.ToString(),tiny,White);
            SmallButton(new Rect(x+137,y+216,29,30),"+",()=>craftBatches=Mathf.Min(99,craftBatches+1));
            bool can=session.CanCraftAt(station,recipe,craftBatches,out string reason);
            Rect submit=new Rect(x+184,y+215,w-184,33);
            bool enabled=GUI.enabled;GUI.enabled&=can;
            ActionButton(submit,Icon.Craft,"加入本台队列",true);
            if(Click(submit)){bool ok=session.RequestCraftAt(station,recipe,craftBatches,out string result);ShowToast(ok?"已加入 "+station.definition.displayName+" 的生产队列":result,ok);}
            GUI.enabled=enabled;
            Label(new Rect(x,y+255,w,26),can?"工人到本台加工，完成后产物进入基地库存。":reason,new GUIStyle(small){wordWrap=true},can?Muted:Amber);
            var target=session.ProductionTargetForAt(station,recipe.id);
            Label(new Rect(x,y+292,80,27),"库存目标",small,Muted);
            SmallButton(new Rect(x+81,y+290,27,27),"−",()=>productionAmount=Mathf.Max(1,productionAmount-5));
            Label(new Rect(x+112,y+290,40,27),productionAmount.ToString(),tiny,White);
            SmallButton(new Rect(x+157,y+290,27,27),"+",()=>productionAmount=Mathf.Min(999,productionAmount+5));
            SmallButton(new Rect(x+201,y+290,w-201,27),target!=null&&target.enabled?"停止本台补货":"本台持续补货",()=>{bool ok=session.SetProductionTargetAt(station,recipe,productionAmount,!(target!=null&&target.enabled),out string result);ShowToast(result,ok);});
            Fill(new Rect(x,y+331,w,1),Border);
            Label(new Rect(x,y+339,w,22),"本台生产队列  ·  "+queue.Count,small,White);
            DrawStationQueue(station,new Rect(x,y+370,w,Mathf.Max(30,rect.yMax-y-396)),queue);
        }
        void DrawRecipeMaterials(Rect rect,DeepItemAmount[] amounts,int batches,bool input)
        {
            Label(new Rect(rect.x,rect.y,rect.width,18),input?"投入原料":"产出物品",small,Muted);
            var entries=(amounts??Array.Empty<DeepItemAmount>()).Where(a=>a.item!=null).ToArray();
            if(entries.Length==0){Label(new Rect(rect.x,rect.y+35,rect.width,23),input?"无需材料":"无产物",body,Muted);return;}
            float width=(rect.width-5*(entries.Length-1))/entries.Length;
            for(int i=0;i<entries.Length;i++)
            {
                var amount=entries[i];int required=amount.amount*batches,stock=session.inventory.GetAmount(amount.item);
                Rect r=new Rect(rect.x+i*(width+5),rect.y+26,width,72);
                Rounded(r,input?new Color(.085f,.13f,.14f):new Color(.10f,.18f,.16f),5);
                DrawAssetIcon(amount.item.icon,new Rect(r.x+8,r.y+9,26,26),White,Icon.Material);
                Label(new Rect(r.x+40,r.y+8,Mathf.Max(20,r.width-44),24),"×"+required,body,White);
                Label(new Rect(r.x+5,r.y+40,r.width-10,22),input?stock+" / "+required:amount.item.displayName,tiny,input&&stock<required?Amber:Muted);
                RegisterHover("material"+amount.item.id+(input?"in":"out"),r,amount.item.displayName,input?"可用 "+stock+" · 本批次需求 "+required:"完成后获得 "+required);
            }
        }
        void DrawStationQueue(DeepBuildingInstance station,Rect viewport,List<DeepWorkOrder> queue)
        {
            var completed=session.Orders.LastOrDefault(o=>o.kind==DeepWorkKind.Craft&&o.targetBuilding==station&&o.state==DeepWorkState.Completed);
            int key=station.GetInstanceID(),id=completed?.id??-1;
            if(lastStationCompletion.TryGetValue(key,out int previous)&&previous!=id&&id>=0)completionShownAt=Time.unscaledTime;
            lastStationCompletion[key]=id;
            Vector2 previousPointer=pointer;
            stationQueueScroll=GUI.BeginScrollView(viewport,stationQueueScroll,new Rect(0,0,viewport.width-18,Mathf.Max(viewport.height,queue.Count*61)));
            pointer=previousPointer-viewport.position+stationQueueScroll;
            for(int i=0;i<queue.Count;i++)
            {
                var order=queue[i];Rect r=new Rect(0,i*61,viewport.width-22,54);
                DrawOrderRow(order,r);
                if(!shownProductionProgress.TryGetValue(order.id,out float shown))shown=order.Progress;
                if(Event.current.type==EventType.Repaint)shownProductionProgress[order.id]=shown=Mathf.MoveTowards(shown,order.Progress,Time.unscaledDeltaTime*1.8f);
                Fill(new Rect(r.x+10,r.yMax-4,r.width-20,2),Border);
                Fill(new Rect(r.x+10,r.yMax-4,(r.width-20)*shown,2),order.state==DeepWorkState.Blocked?Amber:Mint);
            }
            if(queue.Count==0)Label(new Rect(0,9,viewport.width-18,25),"队列为空",small,Muted);
            GUI.EndScrollView();pointer=previousPointer;
            string completion=completed==null?"": "已完成："+completed.label;
            float flash=1-EaseOut((Time.unscaledTime-completionShownAt)/.7f);
            Label(new Rect(viewport.x,viewport.yMax+5,viewport.width,22),completion,small,Color.Lerp(Muted,Mint,flash));
        }
    }
}

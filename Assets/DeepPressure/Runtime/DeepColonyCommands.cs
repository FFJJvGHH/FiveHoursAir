using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Icon=DeepPressure.DeepUIIcons.Icon;
namespace DeepPressure
{
    public sealed partial class DeepPressureHUD
    {
        string buildCategory="全部";
        DeepTechDefinition inspectedTech;
        Vector2 researchScroll;
        bool draggingDig,missionCollapsed;
        Vector2Int digStart,digEnd;
        Rect MissionRect=>new Rect(uiWidth-266,75,247,missionCollapsed?35:104);
        Rect CommandRect=>new Rect(dockRect.x,dockRect.y-42,dockRect.width,32);
        Rect TimeRect=>new Rect(uiWidth-316,17,297,39);
        bool CommandBlocksPointer=>TimeRect.Contains(pointer)||(colonyPanel==ColonyPanel.None&&MissionRect.Contains(pointer))||(colonyTool!=ColonyTool.None&&CommandRect.Contains(pointer));
        void DrawCommandInterface()
        {
            if(session==null||pauseMenu)return;
            Rounded(TimeRect,Panel,9);
            Label(new Rect(TimeRect.x+13,TimeRect.y,130,39),"第 "+(1+(int)(session.SimulationTime/600))+" 周期  "+TimeLabel(session.SimulationTime),body,White);
            string rate=ColonyPaused?"已暂停":session.speed.ToString("0.#")+"×";
            SmallButton(new Rect(TimeRect.x+147,TimeRect.y+7,55,25),rate,()=>SetColonyPause(!ColonyPaused));
            SmallButton(new Rect(TimeRect.x+210,TimeRect.y+7,76,25),"菜单 Esc",TogglePauseMenu);
            if(colonyPanel==ColonyPanel.None)DrawColonyPulse();
            DrawSystemWorldFeedback();
            if(colonyTool!=ColonyTool.None)
            {
                Rounded(CommandRect,Panel,7);
                string action=colonyTool==ColonyTool.Dig?"挖掘：拖出矩形，松开提交":colonyTool==ColonyTool.Build?"建造："+(buildChoice!=null?buildChoice.displayName:""):colonyTool==ColonyTool.Wire?(removeWireMode?"拆除电线 · 点击或拖动":"铺设电线 · 拖动规划路径") :"接管：点击目标设施";
                Label(new Rect(CommandRect.x+12,CommandRect.y,355,32),action,body,Mint);
                Label(new Rect(CommandRect.x+390,CommandRect.y,90,32),"任务优先级",small,Muted);
                for(int i=1;i<=9;i++){int priority=i;Rect r=new Rect(CommandRect.x+480+(i-1)*28,CommandRect.y+4,24,24);Rounded(r,session.defaultOrderPriority==i?new Color(.24f,.4f,.31f):new Color(.10f,.16f,.18f),4);Label(r,i.ToString(),tiny,session.defaultOrderPriority==i?Mint:Muted);if(Click(r))session.defaultOrderPriority=priority;}
                Label(new Rect(CommandRect.xMax-117,CommandRect.y,112,32),"右键 / Esc 取消",small,Muted);
            }
            if(draggingDig)
            {
                var area=DigArea();Rect rect=WorldBounds(area);Fill(rect,WithAlpha(Mint,.12f));Brackets(rect,Mint,12);
                Label(new Rect(rect.x,rect.y-27,230,23),area.width+" × "+area.height+"  ·  松开安排挖掘",body,Mint);
            }
        }
        void DrawMission()
        {
            Rect rect=MissionRect;PanelBackground(rect);
            bool research=session.Buildings.Any(b=>b!=null&&b.definition!=null&&b.definition.role==DeepBuildingRole.Research&&b.isConstructed);
            int stage=!research?0:!session.IsTechUnlocked("survey_basics")?1:!session.IsTechUnlocked("pressure_engineering")?2:!session.IsTechUnlocked("selective_separation")?3:4;
            string[] headings={"建立研究能力","恢复地下测量","控制来气压力","分离产品与尾气","扩展你的地下工业"};
            string[] instructions={"建造研究台，工人会从仓库取料后施工。建议在左侧建造大厅的空地放置。","打开科技树研究「地下测量」。数据不足时，在制造面板解析电子元件。","研究「压力工程」解锁调压器；通过制造补充合金与研究数据。","研究「选择分离」。建设分离设备，并为产品和尾气分别连接管道。","勘探未知气藏，安排开挖与梯道；根据需要发展仓储、电力与制造分支。"};
            Label(new Rect(rect.x+14,rect.y+5,238,28),"工程目标  "+(stage+1).ToString("00")+" / 05",small,Mint);
            Rect fold=new Rect(rect.xMax-37,rect.y+7,25,24);DrawIcon(missionCollapsed?Icon.Explore:Icon.Close,Inset(fold,5),Muted);if(Click(fold))missionCollapsed=!missionCollapsed;
            if(missionCollapsed)return;
            Label(new Rect(rect.x+14,rect.y+39,270,25),headings[stage],title,White);
            Label(new Rect(rect.x+14,rect.y+71,270,65),instructions[stage],new GUIStyle(small){wordWrap=true,alignment=TextAnchor.UpperLeft},Muted);
            SmallButton(new Rect(rect.x+14,rect.yMax-35,130,25),stage==0?"打开建造 B":"打开科技 R",()=>{colonyPanel=stage==0?ColonyPanel.Build:ColonyPanel.Research;});
            Label(new Rect(rect.x+152,rect.yMax-35,130,25),"制造 C  ·  人员 J",small,Muted);
        }
        bool HandleCommandInput(Event e,bool blocked)
        {
            if(session==null)return false;
            if(HandleWireInput(e,blocked))return true;
            if(GUI.GetNameOfFocusedControl()=="ResearchSearch"&&e.type==EventType.KeyDown&&e.keyCode!=KeyCode.Escape)return true;
            if(e.type==EventType.KeyDown)
            {
                if(e.keyCode==KeyCode.Escape&&!pauseMenu)
                {
                    if(colonyTool!=ColonyTool.None||tool!=ToolMode.None||draggingDig){colonyTool=ColonyTool.None;tool=ToolMode.None;draggingDig=false;}
                    else if(colonyPanel!=ColonyPanel.None)colonyPanel=ColonyPanel.None;
                    else if(HasSelection)ClearSelection();else return false;
                    e.Use();return true;
                }
                if(pauseMenu)return false;
                switch(e.keyCode)
                {
                    case KeyCode.B:ActivateColonyTool(0);break;
                    case KeyCode.G:ActivateColonyTool(1);break;
                    case KeyCode.J:ActivateColonyTool(2);break;
                    case KeyCode.R:ActivateColonyTool(3);break;
                    case KeyCode.C:colonyPanel=colonyPanel==ColonyPanel.Craft?ColonyPanel.None:ColonyPanel.Craft;colonyTool=ColonyTool.None;selectedBuilding=null;break;
                    case KeyCode.E:ToggleWireTool(e.shift);break;
                    case KeyCode.I:colonyPanel=colonyPanel==ColonyPanel.Resources?ColonyPanel.None:ColonyPanel.Resources;break;
                    case KeyCode.F5:SaveQuick();break;
                    case KeyCode.F9:pauseBeforeMenu=ColonyPaused;pauseMenu=true;OpenSaveBrowser(false);break;
                    case KeyCode.Alpha1:SetColonySpeed(1);break;
                    case KeyCode.Alpha2:SetColonySpeed(2);break;
                    case KeyCode.Alpha3:SetColonySpeed(4);break;
                    case KeyCode.Home:viewCamera.transform.position=startingCameraPosition;viewCamera.orthographicSize=startingCameraSize;break;
                    default:return false;
                }
                e.Use();return true;
            }
            if(pauseMenu)return false;
            if(draggingDig&&e.type==EventType.MouseDown&&e.button==1){draggingDig=false;colonyTool=ColonyTool.None;e.Use();return true;}
            if(colonyTool==ColonyTool.Dig&&e.button==0)
            {
                if(e.type==EventType.MouseDown&&!blocked){digStart=digEnd=world.WorldToCell(PointerWorld(pointer));draggingDig=true;e.Use();return true;}
                if(draggingDig&&e.type==EventType.MouseDrag){digEnd=world.WorldToCell(PointerWorld(pointer));e.Use();return true;}
                if(draggingDig&&e.type==EventType.MouseUp)
                {
                    int accepted=0,rejected=0;string last="";
                    foreach(var cell in DigArea().allPositionsWithin){if(session.RequestDig(cell,out string reason))accepted++;else{rejected++;last=reason;}}
                    draggingDig=false;ShowToast(accepted>0?"已安排 "+accepted+" 格挖掘"+(rejected>0?" · 跳过 "+rejected+" 格":""):last,accepted>0);e.Use();return true;
                }
            }
            return false;
        }
        RectInt DigArea()
        {
            int x=Mathf.Clamp(Mathf.Min(digStart.x,digEnd.x),0,world.width-1),y=Mathf.Clamp(Mathf.Min(digStart.y,digEnd.y),0,world.height-1);
            return new RectInt(x,y,Mathf.Min(32,Mathf.Clamp(Mathf.Max(digStart.x,digEnd.x),0,world.width-1)-x+1),Mathf.Min(32,Mathf.Clamp(Mathf.Max(digStart.y,digEnd.y),0,world.height-1)-y+1));
        }
        void DrawBuildPanel()
        {
            var all=session.catalog.buildings.Where(b=>b!=null).ToArray();
            string[] categories=new[]{"全部"}.Concat(all.Select(b=>b.category).Distinct()).ToArray();
            int categoryRows=Mathf.CeilToInt(categories.Length/4f);
            for(int i=0;i<categories.Length;i++)
            {
                string category=categories[i];Rect r=new Rect(colonyRect.x+14+(i%4)*97,colonyRect.y+54+(i/4)*32,91,27);
                Rounded(r,buildCategory==category?new Color(.20f,.34f,.29f):new Color(.07f,.12f,.14f),5);Label(r,category,tiny,buildCategory==category?Mint:Muted);
                if(Click(r)){buildCategory=category;buildingPage=0;}
            }
            float top=colonyRect.y+63+categoryRows*32;
            var definitions=all.Where(b=>buildCategory=="全部"||b.category==buildCategory).ToArray();
            int rows=Mathf.Max(1,(int)((colonyRect.yMax-top-41)/84)),pageSize=rows*2,pages=Mathf.Max(1,Mathf.CeilToInt(definitions.Length/(float)pageSize));
            buildingPage=Mathf.Clamp(buildingPage,0,pages-1);
            for(int i=0;i<pageSize&&buildingPage*pageSize+i<definitions.Length;i++)
            {
                var b=definitions[buildingPage*pageSize+i];bool unlocked=session.IsTechUnlocked(b.requiredTechId),affordable=CanAfford(b.cost);
                Rect r=new Rect(colonyRect.x+14+(i%2)*196,top+(i/2)*84,186,75);
                Rounded(r,buildChoice==b&&colonyTool==ColonyTool.Build?new Color(.16f,.30f,.24f):new Color(.067f,.113f,.129f),8);
                DrawAssetIcon(b.icon,new Rect(r.x+8,r.y+12,42,43),unlocked?White:Muted,BuildingIcon(b.role));
                Label(new Rect(r.x+57,r.y+8,123,23),b.displayName,body,unlocked?White:Muted);
                Label(new Rect(r.x+57,r.y+31,123,32),unlocked?CostText(b.cost):"需要 "+TechName(b.requiredTechId),new GUIStyle(tiny){alignment=TextAnchor.MiddleLeft,wordWrap=true},unlocked&&affordable?Muted:Amber);
                if(!unlocked)DrawIcon(Icon.Lock,new Rect(r.xMax-19,r.y+2,14,14),Amber);
                RegisterHover("build"+b.id,r,b.displayName,b.description+"  ·  "+b.footprint.x+"×"+b.footprint.y+"  ·  "+b.workSeconds+"秒");
                if(Click(r))
                {
                    if(!unlocked){inspectedTech=session.catalog.FindTech(b.requiredTechId);colonyPanel=ColonyPanel.Research;continue;}
                    if(!affordable){ShowToast("材料不足："+CostText(b.cost),false);continue;}
                    buildChoice=b;colonyTool=ColonyTool.Build;tool=ToolMode.None;ClearSelection();DeepInterfaceFeedback.Play(true);
                }
            }
            float bottom=colonyRect.yMax-33;
            SmallButton(new Rect(colonyRect.x+14,bottom,75,25),"上一页",()=>buildingPage=Mathf.Max(0,buildingPage-1));
            Label(new Rect(colonyRect.x+100,bottom,197,25),(buildingPage+1)+" / "+pages+"  ·  点击锁定设施查看科技",tiny,Muted);
            SmallButton(new Rect(colonyRect.xMax-89,bottom,75,25),"下一页",()=>buildingPage=Mathf.Min(pages-1,buildingPage+1));
        }
        void DrawWorkersPanel()
        {
            float x=colonyRect.x+15,y=colonyRect.y+56;
            Label(new Rect(x,y,550,24),"分工先决定谁来做，再按工单优先级与路程分配工作。",small,Muted);y+=31;
            DeepWorkKind[] kinds={DeepWorkKind.Dig,DeepWorkKind.Build,DeepWorkKind.Research,DeepWorkKind.Craft,DeepWorkKind.Pipe};
            string[] names={"挖掘","建造","研究","制造","管线"};
            for(int i=0;i<5;i++)Label(new Rect(x+210+i*65,y,61,22),names[i],tiny,Muted);y+=24;
            foreach(var worker in session.Workers)
            {
                if(worker==null)continue;Rect r=new Rect(x,y,560,66);Rounded(r,new Color(.07f,.115f,.13f),7);
                Rect select=new Rect(x+8,y+5,192,53);DrawIcon(Icon.Person,new Rect(x+10,y+10,20,20),Mint);
                Label(new Rect(x+37,y+6,167,22),worker.displayName,body,White);Label(new Rect(x+10,y+33,190,23),worker.Status,small,Muted);
                if(Click(select)){SelectWorker(worker);FocusAt(worker.transform.position);}
                for(int i=0;i<5;i++)
                {
                    var kind=kinds[i];int preference=worker.GetPreference(kind);Rect p=new Rect(x+210+i*65,y+17,59,31);
                    string[] options={"禁用","低","正常","高"};Rounded(p,preference==3?new Color(.21f,.36f,.28f):new Color(.11f,.17f,.19f),5);
                    Label(p,options[Mathf.Clamp(preference,0,3)],tiny,preference==0?Amber:preference==3?Mint:White);
                    RegisterHover("pref"+worker.GetInstanceID()+i,p,names[i]+"偏好","点击轮换：禁用 → 低 → 正常 → 高");
                    if(Click(p))session.SetWorkerPreference(worker,kind,(preference+1)%4);
                }
                y+=73;
            }
            Label(new Rect(x,y,560,26),"工单队列  ·  点击优先级数字循环调整  ·  × 取消并返还预留材料",small,Muted);y+=34;
            var pending=session.Orders.Where(o=>o!=null&&!o.IsTerminal).OrderByDescending(o=>o.priority).ThenBy(o=>o.id).ToArray();
            int rows=Mathf.Max(1,(int)((colonyRect.yMax-y-36)/57)),pages=Mathf.Max(1,Mathf.CeilToInt(pending.Length/(float)rows));ordersPage=Mathf.Clamp(ordersPage,0,pages-1);
            foreach(var order in pending.Skip(ordersPage*rows).Take(rows))
            {
                Rect priority=new Rect(x,y+6,35,35);Rounded(priority,new Color(.16f,.27f,.25f),6);Label(priority,order.priority.ToString(),new GUIStyle(title){alignment=TextAnchor.MiddleCenter},Mint);
                if(Click(priority))session.SetOrderPriority(order,order.priority%9+1);
                DrawOrderRow(order,new Rect(x+43,y,516,50));y+=57;
            }
            if(pending.Length==0)Label(new Rect(x,y,550,28),"没有待办工单。按 B 建造，或按 G 框选挖掘。",small,Muted);
            if(pages>1){float bottom=colonyRect.yMax-31;SmallButton(new Rect(x,bottom,80,24),"上一页",()=>ordersPage=Mathf.Max(0,ordersPage-1));Label(new Rect(x+237,bottom,85,24),(ordersPage+1)+" / "+pages,tiny,Muted);SmallButton(new Rect(x+479,bottom,80,24),"下一页",()=>ordersPage=Mathf.Min(pages-1,ordersPage+1));}
        }
        void DrawPriorityPicker(Rect rect,DeepWorkOrder order)
        {
            for(int i=1;i<=9;i++){Rect r=new Rect(rect.x+(i-1)*24,rect.y,22,26);Rounded(r,order.priority==i?new Color(.22f,.38f,.29f):new Color(.08f,.14f,.16f),4);Label(r,i.ToString(),tiny,order.priority==i?Mint:Muted);if(Click(r))session.SetOrderPriority(order,i);}
        }
        void DrawResearchPanelPrevious()
        {
            var techs=session.catalog.technologies.Where(t=>t!=null).ToArray();
            if(inspectedTech==null&&techs.Length>0)inspectedTech=techs.FirstOrDefault(t=>!session.IsTechUnlocked(t.id))??techs[0];
            var depths=new Dictionary<string,int>();foreach(var tech in techs)depths[tech.id]=0;
            for(int pass=0;pass<techs.Length;pass++)foreach(var tech in techs)foreach(string id in tech.prerequisiteIds??Array.Empty<string>())if(depths.TryGetValue(id,out int depth))depths[tech.id]=Mathf.Min(techs.Length,Mathf.Max(depths[tech.id],depth+1));
            Rect viewport=new Rect(colonyRect.x+15,colonyRect.y+57,colonyRect.width-30,colonyRect.height-206);
            string[] branches=techs.Select(t=>string.IsNullOrEmpty(t.branch)?"工程基础":t.branch).Distinct().ToArray();
            var cards=new Dictionary<string,Rect>();var rows=new Dictionary<string,int>();
            foreach(var tech in techs)
            {
                string branch=string.IsNullOrEmpty(tech.branch)?"工程基础":tech.branch;int row=Array.IndexOf(branches,branch),column=depths[tech.id];
                string key=row+"/"+column;int extra=rows.TryGetValue(key,out int occupied)?occupied:0;rows[key]=extra+1;
                cards[tech.id]=new Rect(126+column*234,12+row*116+extra*99,210,91);
            }
            float fullWidth=Mathf.Max(viewport.width,techs.Length==0?1:cards.Values.Max(r=>r.xMax)+16),fullHeight=Mathf.Max(viewport.height,techs.Length==0?1:cards.Values.Max(r=>r.yMax)+20);
            Vector2 oldPointer=pointer;researchScroll=GUI.BeginScrollView(viewport,researchScroll,new Rect(0,0,fullWidth,fullHeight));pointer=oldPointer-viewport.position+researchScroll;
            for(int i=0;i<branches.Length;i++){Label(new Rect(8,20+i*116,108,26),branches[i],body,Mint);Fill(new Rect(8,54+i*116,90,1),Border);}
            foreach(var tech in techs)foreach(string id in tech.prerequisiteIds??Array.Empty<string>())if(cards.TryGetValue(id,out Rect parent))
            {Rect child=cards[tech.id];float mid=(parent.xMax+child.x)*.5f;Color color=session.IsTechUnlocked(id)?WithAlpha(Mint,.75f):WithAlpha(Muted,.35f);Fill(new Rect(parent.xMax,parent.center.y,mid-parent.xMax,2),color);Fill(new Rect(mid,Mathf.Min(parent.center.y,child.center.y),2,Mathf.Abs(parent.center.y-child.center.y)),color);Fill(new Rect(mid,child.center.y,child.x-mid,2),color);}
            foreach(var tech in techs)
            {
                Rect r=cards[tech.id];bool complete=session.IsTechUnlocked(tech.id),available=session.CanResearch(tech,out _);var order=session.Orders.FirstOrDefault(o=>!o.IsTerminal&&o.technology==tech);
                Rounded(r,inspectedTech==tech?Mint:complete?new Color(.21f,.39f,.29f):Border,8);Rounded(Inset(r,inspectedTech==tech?2:1),new Color(.045f,.085f,.10f),7);
                DrawAssetIcon(tech.icon,new Rect(r.x+11,r.y+11,28,28),complete||available?Mint:Muted,Icon.Research);
                Label(new Rect(r.x+47,r.y+9,157,25),tech.displayName,body,complete||available?White:Muted);
                Label(new Rect(r.x+12,r.y+42,185,20),complete?"已掌握":order!=null?"研究中 "+(order.Progress*100).ToString("0")+"%":available?"可研究  ·  "+tech.workSeconds+"秒":"等待条件",small,complete||available?Mint:Muted);
                string unlocks=string.Join(" / ",(tech.unlockBuildingIds??Array.Empty<string>()).Select(id=>session.catalog.FindBuilding(id)?.displayName??id));
                Label(new Rect(r.x+12,r.y+65,187,20),unlocks.Length>0?unlocks:"工业工艺与制造配方",tiny,Muted);
                if(order!=null)Fill(new Rect(r.x+3,r.yMax-4,(r.width-6)*order.Progress,2),Mint);
                if(Click(r))inspectedTech=tech;
            }
            GUI.EndScrollView();pointer=oldPointer;
            if(inspectedTech==null)return;
            var selected=inspectedTech;float x=colonyRect.x+20,y=colonyRect.yMax-136;Fill(new Rect(x,y-9,colonyRect.width-40,1),Border);
            Label(new Rect(x,y,colonyRect.width-270,27),selected.displayName+"  ·  "+selected.workSeconds.ToString("0")+" 秒",title,White);
            Label(new Rect(x,y+31,colonyRect.width-290,37),selected.description,new GUIStyle(small){wordWrap=true},Muted);
            string prerequisites=string.Join("、",(selected.prerequisiteIds??Array.Empty<string>()).Select(TechName));
            Label(new Rect(x,y+73,colonyRect.width-290,23),"前置："+(prerequisites.Length==0?"无":prerequisites)+"  |  材料："+CostText(selected.cost),small,Muted);
            bool can=session.CanResearch(selected,out string reason);var current=session.Orders.FirstOrDefault(o=>!o.IsTerminal&&o.technology==selected);bool unlocked=session.IsTechUnlocked(selected.id);
            Rect action=new Rect(colonyRect.xMax-255,y+9,232,40);ActionButton(action,unlocked?Icon.Check:Icon.Research,unlocked?"已掌握":current!=null?"研究中 "+(current.Progress*100).ToString("0")+"%":"安排研究",can);
            if(Click(action)&&can){bool ok=session.RequestResearch(selected,out string result);ShowToast(result,ok);}
            Label(new Rect(action.x,y+56,232,49),unlocked?"相关设施已加入建造目录":current!=null?current.statusReason:can?"人员将自动前往供电中的研究台":reason,new GUIStyle(small){wordWrap=true},can?Mint:Amber);
            if(current!=null){Rect cancel=new Rect(action.x,y+105,232,24);SmallButton(cancel,"取消此项研究",()=>{bool ok=session.CancelOrder(current,out string result);ShowToast(result,ok);});}
        }
    }
}

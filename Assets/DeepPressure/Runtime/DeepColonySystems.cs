using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Icon=DeepPressure.DeepUIIcons.Icon;
namespace DeepPressure
{
    public sealed partial class DeepPressureHUD
    {
        bool draggingWire,removeWireMode;
        Vector2Int wireStart,wireEnd,lastWireRemoval;
        Vector2 resourcesScroll;
        void ToggleWireTool(bool remove)
        {
            overlay=OverlayMode.Power;tool=ToolMode.None;colonyPanel=ColonyPanel.None;
            colonyTool=colonyTool==ColonyTool.Wire&&removeWireMode==remove?ColonyTool.None:ColonyTool.Wire;
            removeWireMode=remove;draggingWire=false;ClearSelection();
        }
        IEnumerable<Vector2Int> WirePath()
        {
            int x=Mathf.Clamp(wireStart.x,0,world.width-1),y=Mathf.Clamp(wireStart.y,0,world.height-1);
            int targetX=Mathf.Clamp(wireEnd.x,0,world.width-1),targetY=Mathf.Clamp(wireEnd.y,0,world.height-1);
            yield return new Vector2Int(x,y);
            while(x!=targetX){x+=Math.Sign(targetX-x);yield return new Vector2Int(x,y);}
            while(y!=targetY){y+=Math.Sign(targetY-y);yield return new Vector2Int(x,y);}
        }
        bool HandleWireInput(Event e,bool blocked)
        {
            if(pauseMenu||colonyTool!=ColonyTool.Wire)return false;
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape||e.type==EventType.MouseDown&&e.button==1)
            {draggingWire=false;colonyTool=ColonyTool.None;e.Use();return true;}
            if(e.button!=0)return false;
            var cell=world.WorldToCell(PointerWorld(pointer));
            if(removeWireMode)
            {
                if(!blocked&&(e.type==EventType.MouseDown||e.type==EventType.MouseDrag&&cell!=lastWireRemoval))
                {lastWireRemoval=cell;bool ok=session.RemoveWire(cell,out string reason);ShowToast(reason,ok);e.Use();return true;}
                return false;
            }
            if(e.type==EventType.MouseDown&&!blocked){wireStart=wireEnd=cell;draggingWire=true;e.Use();return true;}
            if(e.type==EventType.MouseDrag&&draggingWire){wireEnd=cell;e.Use();return true;}
            if(e.type==EventType.MouseUp&&draggingWire)
            {
                int count=0;string reason="路径已有电线";
                foreach(var p in WirePath())if(session.RequestWire(p,out string detail))count++;else reason=detail;
                draggingWire=false;ShowToast(count>0?"安排 "+count+" 格电线 · 工人完工后通电":reason,count>0);e.Use();return true;
            }
            return false;
        }
        void DrawColonyPulse()
        {
            Rect r=MissionRect;PanelBackground(r);
            Label(new Rect(r.x+12,r.y+5,190,26),"基地运行",small,Muted);
            Rect fold=new Rect(r.xMax-31,r.y+5,24,24);DrawIcon(missionCollapsed?Icon.Layers:Icon.Close,Inset(fold,6),Muted);if(Click(fold))missionCollapsed=!missionCollapsed;
            if(missionCollapsed)return;
            Icon[] icons={Icon.Air,Icon.Power,Icon.Material};
            var fuel=session.catalog.FindItem("fuel");
            string[] values={session.UnsafeWorkerCount>0?session.UnsafeWorkerCount+" 人":session.OxygenDemandRate.ToString("0.00"),session.PowerProduction.ToString("0")+" W",fuel==null?"—":session.inventory.GetAmount(fuel).ToString()};
            string[] labels={session.UnsafeWorkerCount>0?"气氛不适":"耗氧 mol/s","供电","燃料"};
            for(int i=0;i<3;i++)
            {
                Rect hit=new Rect(r.x+10+i*77,r.y+37,70,58);bool warning=i==0&&session.UnsafeWorkerCount>0||i==2&&fuel!=null&&session.inventory.GetAmount(fuel)<4;
                DrawIcon(icons[i],new Rect(hit.x+2,hit.y+1,21,21),warning?Amber:Mint);
                Label(new Rect(hit.x+28,hit.y,43,24),values[i],small,White);Label(new Rect(hit.x,hit.y+29,70,20),labels[i],tiny,Muted);
                RegisterHover("pulse"+i,hit,labels[i],i==0?session.AirStatus+" · 供氧 "+session.OxygenSupplyRate.ToString("0.00")+" mol/s":i==1?"需求 "+session.PowerDemand+" W · 电池 "+session.StoredEnergy.ToString("0")+" J":"页岩可开采燃料；点击管理库存");
                if(Click(hit)){if(i==0){gasRenderer.enabled=true;overlay=OverlayMode.Gas;}else if(i==1)ToggleWireTool(false);else colonyPanel=ColonyPanel.Resources;}
            }
        }
        void DrawSystemWorldFeedback()
        {
            session.showPowerOverlay=overlay==OverlayMode.Power;
            if(draggingWire)
            {
                foreach(var cell in WirePath())
                {
                    Vector2 p=WorldPoint(world.CellToWorld(cell));bool valid=session.HasWire(cell)||session.CanWire(cell,out _);
                    Rounded(new Rect(p.x-3,p.y-3,6,6),valid?Amber:new Color(.96f,.35f,.3f),3);
                }
            }
            foreach(var order in session.Orders)
            {
                if(order.IsTerminal||order.kind!=DeepWorkKind.Wire||!Visible(order.targetCell))continue;
                var p=WorldPoint(world.CellToWorld(order.targetCell));DrawIcon(Icon.Wire,new Rect(p.x-8,p.y-8,16,16),WithAlpha(Amber,.6f));
            }
            foreach(var building in session.Buildings)
            {
                if(building==null||building.definition==null||!Visible(building.origin))continue;
                var def=building.definition;Rect bounds=WorldBounds(building.Bounds);
                if(overlay==OverlayMode.Power&&(def.powerRequired>0||def.powerGenerated>0||def.role==DeepBuildingRole.Battery))
                {
                    Vector2 p=WorldPoint(world.CellToWorld(session.PowerTerminal(building)));Rect hit=new Rect(p.x-11,p.y-11,22,22);
                    Rounded(hit,Panel,5);DrawIcon(Icon.Power,Inset(hit,3),building.powered?Mint:Amber);
                    RegisterHover("terminal"+building.GetInstanceID(),hit,def.displayName,session.PowerStatus(building));
                }
                if(!building.IsOperational&&building.isOn&&def.powerRequired>0)
                {
                    Rect status=new Rect(bounds.center.x-11,bounds.y-25,22,22);Rounded(status,Panel,6);DrawIcon(Icon.Wire,Inset(status,3),Amber);
                    RegisterHover("powerfault"+building.GetInstanceID(),status,def.displayName,session.PowerStatus(building));
                }
                var gas=building.GetComponentInChildren<GasNode>();
                if(gas!=null&&(building==selectedBuilding||building==hoveredBuilding||overlay==OverlayMode.Pressure))
                {
                    Rect meter=new Rect(bounds.xMax+3,bounds.y+3,4,Mathf.Max(12,bounds.height-6));Rounded(meter,Panel,2);
                    float fill=Mathf.Clamp01((float)(gas.PressureKPa/Math.Max(1,gas.maxPressureKPa)));Rounded(new Rect(meter.x,meter.yMax-meter.height*fill,meter.width,meter.height*fill),def.gasAcceptance==DeepGasAcceptance.Waste?Amber:Mint,2);
                }
                if(gas!=null&&(building==selectedBuilding||colonyTool==ColonyTool.Pipe))
                {
                    foreach(var port in def.ports)
                    {
                        if(port.kind!=DeepPortKind.GasIn&&port.kind!=DeepPortKind.GasOut)continue;
                        Vector2 p=WorldPoint(building.transform.TransformPoint(port.localPosition));Rect hit=new Rect(p.x-9,p.y-9,18,18);
                        Rounded(hit,Panel,9);DrawIcon(port.kind==DeepPortKind.GasIn?Icon.Drop:Icon.Flow,Inset(hit,2),port.id.Contains("tail")?Amber:Mint);
                        RegisterHover("gasport"+building.GetInstanceID()+port.id,hit,port.label,DeepGasFacility.Function(def));
                    }
                }
            }
            foreach(var worker in session.Workers)
            {
                if(worker==null||!Visible(worker.Cell)||!worker.environmentUnsafe)continue;
                var r=WorkerBounds(worker);DrawIcon(Icon.Air,new Rect(r.center.x-9,r.y-23,18,18),Amber);
                Fill(new Rect(r.x,r.yMax+2,r.width*Mathf.Clamp01(worker.airReserveSeconds/90),3),Amber);
            }
        }
        void DrawResourcePanel()
        {
            float x=colonyRect.x+16,y=colonyRect.y+58,w=colonyRect.width-32;
            Label(new Rect(x,y,w,24),"仓储 "+session.inventory.UsedCapacity+" / "+session.inventory.Capacity+"   ·   I 打开库存",body,Mint);y+=34;
            foreach(var item in session.catalog.items)
            {
                int reserved=0;foreach(var order in session.Orders)if(!order.IsTerminal&&order.reservation!=null&&!order.reservation.IsSettled&&order.reservation.items.TryGetValue(item.id,out int amount))reserved+=amount;
                Rect r=new Rect(x,y,w,42);Rounded(r,new Color(.064f,.104f,.12f),5);
                DrawAssetIcon(item.icon,new Rect(x+8,y+8,25,25),item.tint,Icon.Material);Label(new Rect(x+44,y+3,150,34),item.displayName,body,White);
                Label(new Rect(x+w-152,y+3,145,34),session.inventory.GetAmount(item)+" 可用 / "+reserved+" 预留",small,Muted);y+=48;
            }
            y+=10;SmallButton(new Rect(x,y,w,31),"打开制造与目标库存 C",()=>colonyPanel=ColonyPanel.Craft);y+=41;
            foreach(var target in session.productionTargets.Take(Mathf.Max(0,(int)((colonyRect.yMax-y-8)/46))))
            {
                var recipe=session.catalog.FindRecipe(target.recipeId);if(recipe==null)continue;
                Label(new Rect(x,y,w-75,20),recipe.displayName+" · 目标 "+target.targetAmount,body,White);
                Label(new Rect(x,y+22,w-80,18),target.status??(target.enabled?"等待检查库存":"已暂停"),small,Muted);
                SmallButton(new Rect(x+w-70,y+5,70,28),target.enabled?"暂停":"继续",()=>target.enabled=!target.enabled);y+=46;
            }
        }
        static void SemanticIcon(Texture2D icon,Rect rect,Color color)
        {Color old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,icon,ScaleMode.ScaleToFit,true);GUI.color=old;}
    }
}

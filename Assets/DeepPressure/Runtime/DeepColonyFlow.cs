using System;
using System.Linq;
using UnityEngine;
using Icon=DeepPressure.DeepUIIcons.Icon;
namespace DeepPressure
{
    public sealed partial class DeepPressureHUD
    {
        bool mainMenu=true, saveBrowser, saveWriting, hasEnteredColony, helpOpen;
        DeepSaveSlotInfo[] saveSlots;
        string menuMessage;
        void InitializeGameFlow()
        {
            if(session==null)return;
            session.InitializeSession();session.CaptureInitialState();
            session.menuOpen=true;network.paused=true;Time.timeScale=0;
            saveSlots=session.GetSaveSlots();
        }
        void EnterColony()
        {
            mainMenu=false;saveBrowser=false;pauseMenu=false;settingsMenu=false;hasEnteredColony=true;
            session.menuOpen=false;network.paused=session.paused;Time.timeScale=session.paused?0:session.speed;
            ClearSelection();colonyPanel=ColonyPanel.None;colonyTool=ColonyTool.None;tool=ToolMode.None;
            menuMessage=null;draggingDig=false;
        }
        void OpenSaveBrowser(bool writing)
        {
            saveSlots=session.GetSaveSlots();saveWriting=writing;saveBrowser=true;session.menuOpen=true;menuMessage=null;
        }
        bool DrawGameFlow()
        {
            if(session==null || (!mainMenu&&!saveBrowser))return false;
            drawingPauseMenu=true;
            Fill(new Rect(0,0,uiWidth,uiHeight),new Color(.018f,.039f,.047f,.91f));
            float left=Mathf.Max(60,(uiWidth-940)*.5f),top=Mathf.Max(50,(uiHeight-550)*.5f);
            DrawIcon(Icon.Mark,new Rect(left,top,55,55),Mint);
            Label(new Rect(left+72,top-5,440,65),saveBrowser?"基地档案":"深  压",new GUIStyle(title){fontSize=40},White);
            Label(new Rect(left+74,top+61,630,25),saveBrowser?"保存当前工程，或回到一段已记录的旅程。":"DEEP PRESSURE  /  地下工程与气体工业",small,Muted);
            if(saveBrowser)DrawSaveBrowser(left,top+110);
            else
            {
                float y=top+133;
                bool exists=saveSlots!=null&&saveSlots.Any(s=>s.exists&&s.isValid);
                FlowButton(new Rect(left,y,300,47),Icon.Play,hasEnteredColony?"返回当前基地":"继续最近存档",hasEnteredColony||exists,()=>{
                    if(hasEnteredColony)EnterColony();
                    else{bool ok=session.ContinueGame(out menuMessage);if(ok)EnterColony();}
                });y+=60;
                FlowButton(new Rect(left,y,300,47),Icon.Build,"开始新工程",true,()=>{
                    // Preserve the current run before resetting it.
                    if(hasEnteredColony&&!session.SaveGame("autosave",out menuMessage))return;
                    bool ok=session.NewGame(out menuMessage);if(ok){session.paused=false;EnterColony();}
                });y+=60;
                FlowButton(new Rect(left,y,300,47),Icon.Storage,"读取存档",true,()=>OpenSaveBrowser(false));y+=60;
                FlowButton(new Rect(left,y,300,47),Icon.Research,helpOpen?"收起操作说明":"操作与工程流程",true,()=>helpOpen=!helpOpen);
                Rect brief=new Rect(left+350,top+126,550,330);PanelBackground(brief);
                Label(new Rect(brief.x+28,brief.y+21,480,33),helpOpen?"下达指令，让工程员完成工作":"重启一座沉寂的地下站",title,White);
                string[] lines=helpOpen?new[]{"左键选择人物 / 设施；右键目的地下达移动。","B 建造 · G 框选挖掘 · J 人员 · R 科技 · C 制造","1 / 2 / 3 切换速度，空格暂停；Esc 逐级返回。","F5 快速保存 · F9 打开读档；中键拖动 / 滚轮缩放。","材料先预留，工人取料、移动、施工后才产生结果。","任务 1–9 级；人员分工可禁用、降低或提高偏好。"}:new[]{"01  建设研究台，安排人员分工与第一批施工。","02  矿石 → 合金 → 元件 → 数据，形成制造循环。","03  按科技分支解锁储气、调压与分离设备。","04  先取样再勘探，为开挖和管网延伸准备条件。","05  持续经营；手动档案与自动存档保存进度。"};
                for(int i=0;i<lines.Length;i++)Label(new Rect(brief.x+28,brief.y+72+i*37,490,32),lines[i],body,i==0?Mint:Muted);
                Label(new Rect(left,top+474,880,28),"工程演示版本  ·  存档保存在本机  ·  自动存档每 120 秒",small,Muted);
            }
            if(!string.IsNullOrEmpty(menuMessage))Label(new Rect(left,uiHeight-75,920,35),menuMessage,new GUIStyle(body){wordWrap=true},Amber);
            drawingPauseMenu=false;
            if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape)
            {
                if(saveBrowser){saveBrowser=false;session.menuOpen=true;}
                else if(hasEnteredColony)EnterColony();
                Event.current.Use();
            }
            return true;
        }
        void DrawSaveBrowser(float x,float y)
        {
            Label(new Rect(x,y-12,860,28),saveWriting?"选择手动档位保存；已有档位会保留上一版备份。":"选择档案恢复世界、人员、工单、科技与气体状态。",body,Muted);
            if(saveSlots!=null)foreach(var slot in saveSlots)
            {
                if(saveWriting&&!slot.slotId.StartsWith("manual",StringComparison.Ordinal))continue;
                Rect row=new Rect(x,y+29,890,61);Rounded(row,new Color(.068f,.113f,.125f),8);
                DrawIcon(Icon.Storage,new Rect(row.x+15,row.y+16,27,27),slot.exists?Mint:Muted);
                Label(new Rect(row.x+57,row.y+7,220,23),slot.title,body,White);
                string date=slot.exists?slot.savedUtc:"空档位";
                if(DateTime.TryParse(slot.savedUtc,out var saved))date=saved.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                Label(new Rect(row.x+57,row.y+31,570,21),slot.exists?(slot.isValid?date+"  ·  工程时间 "+TimeLabel(slot.simulationTime):"档案无法读取，请选择其他档位"):"尚未保存",small,Muted);
                bool usable=saveWriting||slot.exists&&slot.isValid;
                FlowButton(new Rect(row.xMax-137,row.y+12,118,36),saveWriting?Icon.Storage:Icon.Play,saveWriting?"保存到此处":"读取",usable,()=>{
                    bool ok=saveWriting?session.SaveGame(slot.slotId,out menuMessage):session.LoadGame(slot.slotId,out menuMessage);
                    if(ok&&!saveWriting){EnterColony();ShowToast("档案已恢复",true);}
                    else if(ok){saveSlots=session.GetSaveSlots();DeepInterfaceFeedback.Play(true);}
                });y+=73;
            }
            FlowButton(new Rect(x,y+43,150,40),Icon.Close,"返回",true,()=>saveBrowser=false);
        }
        void FlowButton(Rect rect,Icon icon,string label,bool enabled,Action action)
        {
            ActionButton(rect,icon,label,enabled);
            if(Click(rect)&&enabled){DeepInterfaceFeedback.Play(true);action();}
        }
        static string TimeLabel(float seconds)=>((int)seconds/60).ToString("00")+":"+((int)seconds%60).ToString("00");
        void SaveQuick(){bool ok=session.SaveGame("quick",out string message);ShowToast(message,ok);}
        void ReturnMainMenu()
        {
            if(!session.SaveGame("autosave",out string reason)){ShowToast(reason,false);return;}
            pauseMenu=false;mainMenu=true;session.menuOpen=true;saveSlots=session.GetSaveSlots();
        }
    }
}

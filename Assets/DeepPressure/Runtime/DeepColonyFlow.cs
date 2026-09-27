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
                if(helpOpen)
                {
                    string[] lines={"左键选中人物或设施，右键派遣人物。","B 建造 · G 挖掘 · E 电线 · Shift+E 拆线","J 人员 · R 科技 · C 制造 · I 库存","空格暂停，1 / 2 / 3 调速；Esc 返回。","F5 快存 · F9 档案；中键拖动 / 滚轮缩放。","人员视野会自动揭雾，取样需要到场作业。"};
                    for(int i=0;i<lines.Length;i++)Label(new Rect(brief.x+28,brief.y+72+i*37,490,32),lines[i],body,i==0?Mint:Muted);
                }
                else
                {
                    Icon[] symbols={Icon.Explore,Icon.Pump,Icon.Filter,Icon.OxygenTank,Icon.Home};
                    string[] roles={"气源","采集","处理","缓冲","供气"};
                    for(int i=0;i<5;i++)
                    {
                        float bx=brief.x+35+i*98;DrawIcon(symbols[i],new Rect(bx,brief.y+94,40,40),i==2?Amber:Mint);
                        Label(new Rect(bx-6,brief.y+150,54,24),roles[i],tiny,White);
                        if(i<4){Fill(new Rect(bx+50,brief.y+114,36,2),Border);DrawIcon(Icon.Play,new Rect(bx+75,brief.y+108,13,13),Muted);}
                    }
                    DrawIcon(Icon.Wire,new Rect(brief.x+35,brief.y+217,27,27),Amber);
                    Label(new Rect(brief.x+80,brief.y+214,423,32),"接通电力，观察流量，决定怎样开发下一片洞层。",body,Muted);
                    Label(new Rect(brief.x+35,brief.y+276,480,25),"所有提示都可略过。设备与环境会告诉你正在发生什么。",small,Muted);
                }
                Label(new Rect(left,top+474,880,28),"深井工程 v0.6  ·  本机存档  ·  每 120 秒自动保存",small,Muted);
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
            bool primary=label.StartsWith("开始",StringComparison.Ordinal)||label.StartsWith("返回当前",StringComparison.Ordinal);
            Rounded(rect,primary?new Color(.16f,.34f,.28f):rect.Contains(pointer)&&enabled?new Color(.11f,.19f,.20f):new Color(.06f,.11f,.13f),7);
            DrawIcon(icon,new Rect(rect.x+15,rect.center.y-10,20,20),enabled?Mint:Muted);
            Label(new Rect(rect.x+48,rect.y,rect.width-59,rect.height),label,new GUIStyle(body){fontSize=14},enabled?White:Muted);
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

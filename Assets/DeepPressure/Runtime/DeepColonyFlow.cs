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
            float left=Mathf.Max(60,(uiWidth-(saveBrowser||helpOpen?900:300))*.5f),top=Mathf.Max(50,(uiHeight-440)*.5f);
            DrawIcon(Icon.Mark,new Rect(left,top,55,55),Mint);
            Label(new Rect(left+72,top-5,440,65),saveBrowser?"基地档案":"深  压",new GUIStyle(title){fontSize=40},White);
            if(saveBrowser)DrawSaveBrowser(left,top+110);
            else
            {
                float y=top+112;
                bool exists=saveSlots!=null&&saveSlots.Any(s=>s.exists&&s.isValid);
                FlowButton(new Rect(left,y,300,47),Icon.Build,"开始新基地",true,()=>{
                    // Preserve the current run before resetting it.
                    if(hasEnteredColony&&!session.SaveGame("autosave",out menuMessage))return;
                    bool ok=session.NewGame(out menuMessage);if(ok){session.paused=false;SetColonySpeed(1);EnterColony();}
                });y+=60;
                if(hasEnteredColony||exists)
                {
                    FlowButton(new Rect(left,y,300,47),Icon.Play,hasEnteredColony?"返回当前基地":"继续最近存档",true,()=>{
                        if(hasEnteredColony)EnterColony();
                        else{bool ok=session.ContinueGame(out menuMessage);if(ok)EnterColony();}
                    });y+=60;
                }
                FlowButton(new Rect(left,y,300,47),Icon.Storage,"读取存档",true,()=>OpenSaveBrowser(false));y+=60;
                FlowButton(new Rect(left,y,300,47),Icon.Settings,helpOpen?"收起操作说明":"操作说明",true,()=>helpOpen=!helpOpen);
                if(helpOpen)
                {
                    Rect brief=new Rect(left+350,top+103,550,330);PanelBackground(brief);
                    Label(new Rect(brief.x+28,brief.y+21,480,33),"操作说明",title,White);
                    string[] lines={"左键选中人物或设施，右键派遣人物。","底部独立建造格 / B：选择设施并放置。","C 选择生产设备；在具体合成台安排配方。","空格暂停；倍速 1→2→4→6；Esc 返回。","F5 快存 · F9 档案；中键拖动 / 滚轮缩放。","人员接近后自动揭雾，点击空腔查看当地空气。"};
                    for(int i=0;i<lines.Length;i++)Label(new Rect(brief.x+28,brief.y+72+i*37,490,32),lines[i],body,i==0?Mint:Muted);
                }
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
            bool wasEnabled=GUI.enabled;GUI.enabled&=enabled;
            bool primary=label.StartsWith("开始",StringComparison.Ordinal)||label.StartsWith("返回当前",StringComparison.Ordinal);
            var visual=ButtonVisual(rect);
            Rounded(visual,primary?new Color(.16f,.34f,.28f):rect.Contains(pointer)&&enabled?new Color(.11f,.19f,.20f):new Color(.06f,.11f,.13f),7);
            DrawIcon(icon,new Rect(visual.x+15,visual.center.y-10,20,20),enabled?Mint:Muted);
            Label(new Rect(visual.x+48,visual.y,visual.width-59,visual.height),label,new GUIStyle(body){fontSize=14},enabled?White:Muted);
            if(Click(rect)&&enabled){DeepInterfaceFeedback.Play(true);action();}
            GUI.enabled=wasEnabled;
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

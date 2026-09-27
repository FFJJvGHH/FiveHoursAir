using System;
using UnityEngine;
using Icon = DeepPressure.DeepUIIcons.Icon;

namespace DeepPressure
{
    [DefaultExecutionOrder(100)]
    public sealed partial class DeepPressureHUD : MonoBehaviour
    {
        public DeepPressureWorld world;
        public GasNetworkSimulator network;
        public Camera viewCamera;
        public SpriteRenderer gasRenderer;
        public enum OverlayMode { Surface, Gas, Pressure, Regions, Power }
        public OverlayMode overlay;
        enum ToolMode { None, Sample, Explore }
        DeepExploration exploration;
        ToolMode tool;
        GasNode selectedNode, hoveredNode;
        GasLink selectedLink, hoveredLink;
        Vector2Int selectedCell = new Vector2Int(-1,-1);
        Rect dockRect, cardRect;
        Vector2 pointer;
        float scale = 1, uiWidth, uiHeight;
        readonly float[] hoverAmounts = new float[12];
        GUIStyle body, small, title, number, tiny;
        Font interfaceFont;
        bool initialized, draggingValve;
        string toast;
        bool toastSuccess;
        float toastTime;
        string hoverCandidate, hoverTitle, hoverDetail, activeHover;
        float hoverStarted;
        Rect hoverAnchor;
        static readonly Color Panel = new Color(.035f,.065f,.078f,.98f);
        static readonly Color Border = new Color(.20f,.28f,.30f,.85f);
        static readonly Color White = new Color(.91f,.92f,.86f,1);
        static readonly Color Muted = new Color(.48f,.59f,.60f,1);
        static readonly Color Mint = new Color(.55f,.84f,.72f,1);
        static readonly Color Amber = new Color(.88f,.66f,.36f,1);
        static readonly Color[] SpeciesColors = { new Color(.54f,.84f,.75f), new Color(.44f,.62f,.68f), new Color(.78f,.60f,.39f), new Color(.64f,.61f,.74f) };
        static readonly string[] SpeciesLabels = { "O₂", "N₂", "CO₂", "H₂O" };

        void Start()
        {
            if (world == null) world = GetComponent<DeepPressureWorld>();
            if (network == null) network = GetComponent<GasNetworkSimulator>();
            if (viewCamera == null) viewCamera = Camera.main;
            exploration = GetComponent<DeepExploration>();
            if (gasRenderer == null && exploration != null) gasRenderer = exploration.gasRenderer;
            InitializeColony();
            ClearSelection();
            InitializeGameFlow();
        }
        void InitializeStyles()
        {
            if (initialized) return;
            string[] available = Font.GetOSInstalledFontNames();
            foreach (string candidate in new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "Arial" })
                if (Array.IndexOf(available,candidate) >= 0) { interfaceFont = Font.CreateDynamicFontFromOSFont(candidate,16); break; }
            body = new GUIStyle(GUI.skin.label) { font = interfaceFont, fontSize = 12, padding = new RectOffset(), clipping = TextClipping.Clip, wordWrap = false, alignment = TextAnchor.MiddleLeft };
            body.normal.textColor = Color.white;
            small = new GUIStyle(body) { fontSize = 11 };
            title = new GUIStyle(body) { fontSize = 15, fontStyle = FontStyle.Bold };
            number = new GUIStyle(body) { fontSize = 28 };
            tiny = new GUIStyle(body) { fontSize = 9, alignment = TextAnchor.MiddleCenter };
            initialized = true;
        }
        void OnGUI()
        {
            if (world == null || network == null || viewCamera == null) return;
            InitializeStyles();
            scale = Mathf.Clamp(Mathf.Min(Screen.height / 820f,Screen.width / 1120f),.55f,1.25f);
            uiWidth = Screen.width / scale; uiHeight = Screen.height / scale;
            pointer = Event.current.mousePosition / scale;
            Matrix4x4 originalMatrix = GUI.matrix; Color originalColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale,scale,1));
            dockRect = new Rect((uiWidth-864)*.5f,uiHeight-88,864,72);
            hoverCandidate = null;
            if (DrawGameFlow()) { GUI.matrix = originalMatrix; GUI.color = originalColor; return; }
            UpdateColonyInterface();
            SanitizeSelection(); cardRect = SelectionRect();
            bool overInterface = dockRect.Contains(pointer) || (HasSelection && cardRect.Contains(pointer)) || ColonyBlocksPointer || CommandBlocksPointer;
            FindHoveredWorldObject(overInterface);
            FindColonyHover(overInterface);
            DrawWorldAccents(); DrawColonyWorldAccents(overInterface); DrawBrand(); DrawDock(); DrawInventory(); DrawCommandInterface();
            if (HasSelection && !pauseMenu) DrawSelectionCard();
            DrawColonyPanels(); DrawToast(); DrawTooltip();
            if(!HandleColonyInput(Event.current,overInterface))HandleWorldInput(Event.current,overInterface);
            DrawPauseMenu();
            GUI.matrix = originalMatrix; GUI.color = originalColor;
        }
        void DrawBrand()
        {
            DrawIcon(Icon.Mark,new Rect(22,19,27,27),WithAlpha(Mint,.9f));
            Label(new Rect(58,18,72,30),"深压",title,WithAlpha(White,.92f));
        }
        void DrawDock()
        {
            Rounded(new Rect(dockRect.x,dockRect.y+6,dockRect.width,dockRect.height),new Color(0,0,0,.2f),14);
            Rounded(dockRect,Border,14); Rounded(Inset(dockRect,1),Panel,13);
            Icon[] icons = { Icon.Build,Icon.Dig,Icon.People,Icon.Research,Icon.Air,Icon.Pressure,Icon.Layers,Icon.Sample,Icon.Explore,Icon.Wire,ColonyPaused ? Icon.Play : Icon.Pause,Icon.Speed };
            string[] names = { "建造","挖掘","人员","科技","气氛","压力","地层","取样","勘探","电网",ColonyPaused ? "继续" : "暂停","时间流速" };
            string[] details = { "选择建筑并安排施工","标记需要开挖的地形","查看人员与当前任务","研究技术并解锁设施","查看空气的流动与组成","查看已探明设备的压力","查看已探明区域的边界","从未知区域取得气体样本","确认气氛后揭开区域","允许进入已取样的危险气氛",ColonyPaused ? "继续基地运转" : "暂时停下基地运转",network.simulationSpeed > 1 ? "当前 4 倍，点击恢复正常" : "当前正常，点击加快至 4 倍" };
            bool[] active = { colonyPanel==ColonyPanel.Build,colonyTool==ColonyTool.Dig,colonyPanel==ColonyPanel.Workers,colonyPanel==ColonyPanel.Research,gasRenderer != null && gasRenderer.enabled,overlay == OverlayMode.Pressure,overlay == OverlayMode.Regions,tool == ToolMode.Sample,tool == ToolMode.Explore,overlay==OverlayMode.Power,ColonyPaused,network.simulationSpeed > 1 };
            details[7]="工程员到探测位置作业，记录成分并获得数据";details[8]="工程员前往区域边界，现场调查并揭开视野";details[9]="E 布置电线，Shift+E 拆线；已完工线路才导电";
            for (int i = 0; i < 12; i++)
            {
                Rect hit = new Rect(dockRect.x+12+i*70,dockRect.y+7,68,58);
                bool hover = hit.Contains(pointer);
                if (Event.current.type == EventType.Repaint) hoverAmounts[i] = Mathf.MoveTowards(hoverAmounts[i],hover ? 1 : 0,Time.unscaledDeltaTime*8);
                float amount = hoverAmounts[i];
                Rect visual = new Rect(hit.x,hit.y-amount*2,68,58);
                Color tint = active[i] ? Mint : Color.Lerp(Muted,White,amount);
                if (active[i] || amount > .005f) Rounded(visual,active[i] ? new Color(.13f,.23f,.22f,.8f) : new Color(.14f,.21f,.22f,amount*.8f),10);
                DrawIcon(icons[i],new Rect(visual.x+21-amount*.4f,visual.y+5-amount*.4f,26+amount*.8f,26+amount*.8f),tint);
                Label(new Rect(visual.x,visual.y+33,visual.width,20),names[i],new GUIStyle(small){alignment=TextAnchor.MiddleCenter},tint);
                if (active[i]) Rounded(new Rect(visual.center.x-2,visual.yMax-5,4,2),WithAlpha(Mint,.85f),1);
                if (i == 11 && active[i]) Label(new Rect(visual.xMax-12,visual.y+2,10,13),"4",tiny,Mint);
                RegisterHover("dock"+i,hit,names[i],details[i]);
                if (!pauseMenu && Click(hit)) {if(i<4)ActivateColonyTool(i);else ActivateTool(i-4);}
            }
        }
        void ActivateTool(int index)
        {
            if(index==3||index==4){colonyTool=ColonyTool.None;colonyPanel=ColonyPanel.None;}
            switch (index)
            {
                case 0: if (gasRenderer != null) gasRenderer.enabled = !gasRenderer.enabled; break;
                case 1: overlay = overlay == OverlayMode.Pressure ? OverlayMode.Surface : OverlayMode.Pressure; break;
                case 2: overlay = overlay == OverlayMode.Regions ? OverlayMode.Surface : OverlayMode.Regions; break;
                case 3:
                    tool = tool == ToolMode.Sample ? ToolMode.None : ToolMode.Sample;
                    if (tool == ToolMode.Sample && HasSelection) ExecuteExploration(false,selectedCell); break;
                case 4:
                    tool = tool == ToolMode.Explore ? ToolMode.None : ToolMode.Explore;
                    if (tool == ToolMode.Explore && HasSelection) ExecuteExploration(true,selectedCell); break;
                case 5:
                    ToggleWireTool(false); break;
                case 6: SetColonyPause(!ColonyPaused); break;
                case 7: SetColonySpeed(network.simulationSpeed > 1 ? 1 : 4); break;
            }
        }
        bool HasSelection => ColonyHasSelection || selectedNode != null || selectedLink != null || world.IsInside(selectedCell);
        void ClearSelection() { selectedNode = null; selectedLink = null; selectedCell = new Vector2Int(-1,-1); draggingValve = false; ClearColonySelection(); }
        void SanitizeSelection()
        {
            if (selectedNode != null && !Visible(world.WorldToCell(selectedNode.transform.position))) selectedNode = null;
            if (selectedLink != null && !LinkVisible(selectedLink)) selectedLink = null;
        }
        Rect SelectionRect()
        {
            if (!HasSelection) return default;
            if(ColonyHasSelection)return ColonySelectionRect();
            float height = 166;
            if (selectedNode != null) height = 315;
            else if (selectedLink != null) height = 208;
            else if (!Visible(selectedCell)) height = HasRecordedSample(selectedCell) ? 265 : 172;
            else if (world.GetTerrain(selectedCell.x,selectedCell.y) != TerrainKind.Empty) height = 174;
            else { DeepPressureRoom room = world.RoomAt(selectedCell); height = room != null && RoomVisible(room) ? 250 : 154; }
            Vector2 anchor = WorldPoint(selectedNode != null ? selectedNode.transform.position : selectedLink != null && selectedLink.from != null && selectedLink.to != null ? (selectedLink.from.transform.position+selectedLink.to.transform.position)*.5f : world.CellToWorld(selectedCell));
            float x = anchor.x+32; if (x+244 > uiWidth-18) x = anchor.x-276;
            return new Rect(Mathf.Clamp(x,18,uiWidth-262),Mathf.Clamp(anchor.y-height*.5f,63,uiHeight-height-99),244,height);
        }
        void DrawSelectionCard()
        {
            Rounded(new Rect(cardRect.x+2,cardRect.y+6,cardRect.width,cardRect.height),new Color(0,0,0,.2f),12);
            Rounded(cardRect,Border,12); Rounded(Inset(cardRect,1),Panel,11);
            Rect close = new Rect(cardRect.xMax-36,cardRect.y+9,26,26);
            DrawIcon(Icon.Close,Inset(close,3),close.Contains(pointer) ? White : Muted);
            RegisterHover("close",close,"收起","关闭此处的信息");
            if (Click(close)) { ClearSelection(); return; }
            float x = cardRect.x+17, y = cardRect.y+13;
            if(ColonyHasSelection)DrawColonySelectionCard(x,ref y);
            else if (selectedNode != null) DrawNodeCard(x,ref y);
            else if (selectedLink != null) DrawLinkCard(x,ref y);
            else if (!Visible(selectedCell)) DrawUnknownCard(x,ref y);
            else DrawCellCard(x,ref y);
        }
        void CardTitle(float x,ref float y,Icon icon,string text)
        {
            DrawIcon(icon,new Rect(x,y+1,19,19),Mint);
            Label(new Rect(x+29,y-1,161,24),text,title,White); y += 38;
        }
        void DrawNodeCard(float x,ref float y)
        {
            GasNode node = selectedNode;
            CardTitle(x,ref y,node.kind == GasNodeKind.Separator ? Icon.Air : node.kind == GasNodeKind.Regulator ? Icon.Pressure : Icon.Volume,NodeName(node));
            PressureValue(x,ref y,node.PressureKPa,NodeStatus(node));
            Metric(new Rect(x,y,103,23),Icon.Volume,node.volumeM3.ToString("0.#")+" m³");
            Metric(new Rect(x+111,y,99,23),Icon.Temperature,node.temperatureC.ToString("0")+" °C"); y += 29;
            DrawComposition(x,ref y,node.gas);
            float flow = (float)Math.Max(node.lastInflowMolPerSecond,node.lastOutflowMolPerSecond);
            Metric(new Rect(x,y,125,24),Icon.Flow,flow.ToString("0.0")+" mol/s");
            int port = 0;
            foreach (GasLink link in network.links)
            {
                if (!LinkVisible(link) || link.from != node || port >= 2) continue;
                Rect button = new Rect(x+148+port*33,y-2,28,28);
                if (button.Contains(pointer)) Rounded(button,new Color(.14f,.22f,.22f,.8f),7);
                DrawIcon(Icon.Valve,Inset(button,5),link.isOpen ? Mint : Muted);
                RegisterHover("port"+link.GetInstanceID(),button,LinkName(link),link.isOpen ? "阀门开启，点击关闭" : "阀门关闭，点击开启");
                if (Click(button)) { link.isOpen = !link.isOpen; ShowToast(link.isOpen ? "阀门已开启" : "阀门已关闭",true); }
                port++;
            }
            y+=34;DrawPipeButtons(x,y,node);
        }
        void DrawLinkCard(float x,ref float y)
        {
            GasLink link = selectedLink;
            CardTitle(x,ref y,Icon.Flow,LinkName(link));
            Label(new Rect(x,y-3,210,21),NodeName(link.from)+"  ›  "+NodeName(link.to),small,Muted); y += 29;
            Label(new Rect(x,y,140,32),Math.Abs(link.lastFlowMolPerSecond).ToString("0.00"),number,White);
            Label(new Rect(x+137,y+8,73,20),"mol/s",small,Muted); y += 43;
            Rect toggle = new Rect(x,y,210,27);
            DrawIcon(Icon.Valve,new Rect(x,y+4,18,18),link.isOpen ? Mint : Muted);
            Label(new Rect(x+29,y,145,27),link.isOpen ? "阀门开启" : "阀门关闭",body,link.isOpen ? White : Muted);
            Rounded(new Rect(x+181,y+7,28,14),link.isOpen ? new Color(.24f,.43f,.36f) : new Color(.17f,.23f,.24f),7);
            Rounded(new Rect(x+(link.isOpen ? 196 : 184),y+10,8,8),link.isOpen ? Mint : Muted,4);
            if (Click(toggle)) link.isOpen = !link.isOpen;
            y += 35;
            Rect slider = new Rect(x,y,168,18);
            Rounded(new Rect(x,y+7,168,3),new Color(.16f,.24f,.25f),1.5f);
            Rounded(new Rect(x,y+7,168*link.valve,3),WithAlpha(Mint,.7f),1.5f);
            Rounded(new Rect(x+168*link.valve-5,y+3,10,10),link.isOpen ? Mint : Muted,5);
            Label(new Rect(x+180,y-3,38,23),link.valve.ToString("P0"),small,White);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && Inset(slider,-6).Contains(pointer)) { draggingValve = true; e.Use(); }
            if (draggingValve && (e.type == EventType.MouseDrag || e.type == EventType.Used)) link.valve = Mathf.Clamp01((pointer.x-x)/168);
            if (draggingValve && e.type == EventType.MouseUp) { draggingValve = false; e.Use(); }
        }
        void DrawUnknownCard(float x,ref float y)
        {
            CardTitle(x,ref y,Icon.Explore,"未探明区域");
            DeepPressureRegion region = world.RegionAt(selectedCell);
            if (exploration != null && region != null && exploration.TryGetSample(region,out var sample))
            {
                PressureValue(x,ref y,sample.pressureKPa,"气体样本");
                Metric(new Rect(x,y,210,22),Icon.Temperature,sample.temperatureC.ToString("0")+" °C"); y += 29;
                DrawComposition(x,ref y,GasMixture.FromPressure(100,1,22,sample.composition));
            }
            else
            {
                DrawIcon(Icon.Air,new Rect(x+3,y+2,32,32),WithAlpha(Muted,.5f));
                Label(new Rect(x+48,y,153,24),"气氛未知",body,White);
                Label(new Rect(x+48,y+23,153,21),"先取得一份样本",small,Muted); y += 67;
            }
            DrawExplorationButtons(x,y);
        }
        void DrawCellCard(float x,ref float y)
        {
            TerrainKind kind = world.GetTerrain(selectedCell.x,selectedCell.y);
            var region = world.RegionAt(selectedCell);
            if (kind != TerrainKind.Empty)
            {
                CardTitle(x,ref y,Icon.Layers,TerrainName(kind));
                DeepTerrainTile material = world.MaterialAt(selectedCell);
                float density = material == null ? DeepPressureWorld.DensityKgPerM3(kind) : material.densityKgM3;
                Metric(new Rect(x,y,210,25),Icon.Volume,density.ToString("0")+" kg/m³"); y += 32;
                if (material != null)
                {
                    Label(new Rect(x,y,102,23),"孔隙  "+material.porosity.ToString("P0"),small,Muted);
                    Label(new Rect(x+112,y,101,23),"硬度  "+material.excavationResistance.ToString("0.#"),small,Muted); y += 31;
                }
                if (region != null) Label(new Rect(x,y,210,22),RegionName(region),small,WithAlpha(Muted,.8f));
                return;
            }
            DeepPressureRoom room = world.RoomAt(selectedCell);
            CardTitle(x,ref y,Icon.Air,region == null ? "地下空腔" : RegionName(region));
            if (room == null || !RoomVisible(room))
            {
                Label(new Rect(x,y,210,27),"边界尚未探明",body,Muted); y += 41;
                DrawExplorationButtons(x,y); return;
            }
            PressureValue(x,ref y,room.PressureKPa,room.isOpen ? "开放空间" : "封闭空间");
            Metric(new Rect(x,y,107,23),Icon.Volume,room.volumeM3.ToString("0")+" m³");
            Metric(new Rect(x+111,y,100,23),Icon.Temperature,room.temperatureC.ToString("0")+" °C"); y += 29;
            DrawComposition(x,ref y,room.gas);
        }
        void PressureValue(float x,ref float y,double value,string status)
        {
            Label(new Rect(x,y-3,135,37),value.ToString("0.0"),number,White);
            Label(new Rect(x+137,y+9,39,18),"kPa",small,Muted);
            Label(new Rect(x,y+32,207,20),status,small,status == "出口受阻" ? Amber : Muted); y += 59;
        }
        void DrawComposition(float x,ref float y,GasMixture mixture)
        {
            for (int i = 0; i < 4; i++)
            {
                float fraction = mixture.Total <= 1e-9 ? 0 : (float)(mixture[i]/mixture.Total);
                Label(new Rect(x,y-3,30,19),SpeciesLabels[i],small,SpeciesColors[i]);
                Rounded(new Rect(x+39,y+5,124,3),new Color(.13f,.20f,.22f),1.5f);
                if (fraction > 0) Rounded(new Rect(x+39,y+5,Mathf.Max(2,124*fraction),3),WithAlpha(SpeciesColors[i],.85f),1.5f);
                Label(new Rect(x+177,y-3,39,19),(fraction*100).ToString("0.#")+"%",small,Muted); y += 18;
            }
            y += 9;
        }
        void DrawExplorationButtons(float x,float y)
        {
            if (exploration == null) return;
            Rect sample = new Rect(x,y,99,29), explore = new Rect(x+111,y,99,29);
            bool hasSample = HasRecordedSample(selectedCell);
            ActionButton(sample,Icon.Sample,"取样",false); ActionButton(explore,Icon.Explore,"勘探",hasSample);
            RegisterHover("sample",sample,"取样","记录气氛，保留迷雾");
            RegisterHover("explore",explore,"现场调查",hasSample ? "工程员到达可达的区域边界，视野随人员展开" : "先安排工程员取得气体样本");
            if (Click(sample)) ExecuteExploration(false,selectedCell);
            if (Click(explore)) ExecuteExploration(true,selectedCell);
        }
        void ActionButton(Rect rect,Icon icon,string text,bool emphasized)
        {
            bool hover = rect.Contains(pointer);
            Rounded(rect,emphasized ? new Color(.13f,.28f,.23f,.9f) : new Color(.12f,.19f,.21f,hover ? 1 : .75f),7);
            DrawIcon(icon,new Rect(rect.x+12,rect.y+6,17,17),emphasized || hover ? Mint : Muted);
            Label(new Rect(rect.x+39,rect.y,rect.width-45,rect.height),text,body,emphasized || hover ? White : Muted);
        }
        void ExecuteExploration(bool explore,Vector2Int cell)
        {
            if (exploration == null || !world.IsInside(cell)) return;
            string message=string.Empty;
            bool success = session!=null && (explore ? session.RequestSurvey(cell,out message) : session.RequestSample(cell,out message));
            if(session==null)message="基地尚未就绪";
            ShowToast(message,success);
        }
        static string ShortMessage(string message,bool success,bool explore)
        {
            if (success) return explore ? "区域已探明" : "气体样本已取得";
            if (string.IsNullOrEmpty(message)) return "暂时无法执行";
            if (message.Contains("已经探索")) return "区域已探明";
            if (message.Contains("先进行") || message.Contains("先取样")) return "请先取得气体样本";
            if (message.Contains("进入受阻")) return "气氛危险，请启用隔绝装备";
            if (message.Contains("范围") || message.Contains("距离")) return "超出当前探测范围";
            if (message.Contains("空隙") || message.Contains("标记")) return "此处无法取得气体样本";
            int stop = message.IndexOf('。'); if (stop >= 0) message = message.Substring(0,stop);
            return message.Length <= 40 ? message : message.Substring(0,39)+"…";
        }
        void ShowToast(string text,bool success) { if(string.IsNullOrEmpty(text))text=success?"已安排":"暂时无法执行";toast = text.Length > 72 ? text.Substring(0,71)+"…" : text; toastSuccess = success; toastTime = Time.unscaledTime; DeepInterfaceFeedback.Play(success); }
        void DrawToast()
        {
            if (string.IsNullOrEmpty(toast)) return;
            float age = Time.unscaledTime-toastTime; if (age > 3.4f) return;
            float alpha = Mathf.Clamp01(age*8)*Mathf.Clamp01((3.4f-age)*3);
            float width = Mathf.Clamp(body.CalcSize(new GUIContent(toast)).x+63,165,Mathf.Min(540,uiWidth-40));
            Rect rect = new Rect((uiWidth-width)*.5f,dockRect.y-51+(1-alpha)*5,width,34);
            Rounded(rect,WithAlpha(Panel,alpha),9);
            DrawIcon(toastSuccess ? Icon.Check : Icon.Warning,new Rect(rect.x+12,rect.y+8,18,18),WithAlpha(toastSuccess ? Mint : Amber,alpha));
            Label(new Rect(rect.x+41,rect.y,rect.width-53,34),toast,body,WithAlpha(White,alpha));
        }
        void RegisterHover(string id,Rect anchor,string text,string detail)
        {
            if (!anchor.Contains(pointer)) return;
            hoverCandidate = id; hoverTitle = text; hoverDetail = detail; hoverAnchor = anchor;
        }
        void DrawTooltip()
        {
            if (hoverCandidate != activeHover) { activeHover = hoverCandidate; hoverStarted = Time.unscaledTime; }
            if (string.IsNullOrEmpty(activeHover) || Time.unscaledTime-hoverStarted < .25f || draggingValve) return;
            float alpha = Mathf.Clamp01((Time.unscaledTime-hoverStarted-.25f)*10);
            float width = Mathf.Clamp(Mathf.Max(body.CalcSize(new GUIContent(hoverTitle)).x,small.CalcSize(new GUIContent(hoverDetail)).x)+28,142,282);
            var detailStyle=new GUIStyle(small){wordWrap=true};
            float detailHeight=Mathf.Clamp(detailStyle.CalcHeight(new GUIContent(hoverDetail),width-28),17,74);
            float height=detailHeight+35;
            float y = hoverAnchor.y-height-9; if (y < 8) y = hoverAnchor.yMax+10;
            Rect rect = new Rect(Mathf.Clamp(hoverAnchor.center.x-width*.5f,12,uiWidth-width-12),y,width,height);
            Rounded(new Rect(rect.x,rect.y+3,rect.width,rect.height),new Color(0,0,0,.14f*alpha),8);
            Rounded(rect,WithAlpha(Border,alpha),8); Rounded(Inset(rect,1),WithAlpha(Panel,alpha),7);
            Label(new Rect(rect.x+14,rect.y+7,width-28,19),hoverTitle,body,WithAlpha(White,alpha));
            Label(new Rect(rect.x+14,rect.y+27,width-28,detailHeight),hoverDetail,detailStyle,WithAlpha(Muted,alpha));
        }
        void FindHoveredWorldObject(bool blocked)
        {
            hoveredNode = null; hoveredLink = null;
            if (blocked || tool != ToolMode.None || colonyTool != ColonyTool.None) return;
            Vector2Int cell = world.WorldToCell(PointerWorld(pointer)); if (!Visible(cell)) return;
            float nearest = 29;
            foreach (GasNode node in network.nodes)
            {
                if (node == null || !Visible(world.WorldToCell(node.transform.position))) continue;
                float distance = Vector2.Distance(WorldPoint(node.transform.position),pointer);
                if (distance < nearest) { hoveredNode = node; nearest = distance; }
            }
            if (hoveredNode != null) return;
            nearest = 7;
            foreach (GasLink link in network.links)
            {
                if (!LinkVisible(link)) continue;
                LineRenderer line = link.GetComponent<LineRenderer>();
                int count = line != null && line.positionCount >= 2 ? line.positionCount : 2;
                for (int i = 1; i < count; i++)
                {
                    Vector2 a = WorldPoint(LinkPoint(link,line,i-1)), b = WorldPoint(LinkPoint(link,line,i));
                    float distance = SegmentDistance(pointer,a,b);
                    if (distance < nearest) { nearest = distance; hoveredLink = link; }
                }
            }
        }
        void DrawWorldAccents()
        {
            if (overlay == OverlayMode.Regions) DrawRegionEdges();
            if (overlay == OverlayMode.Pressure)
                foreach (GasNode node in network.nodes)
                {
                    if (node == null || !Visible(world.WorldToCell(node.transform.position))) continue;
                    Vector2 p = WorldPoint(node.transform.position);
                    Color color = node.PressureKPa > 200 ? Amber : Mint;
                    Rounded(new Rect(p.x-16,p.y+27,32,2),new Color(.14f,.21f,.23f,.8f),1);
                    Rounded(new Rect(p.x-16,p.y+27,32*Mathf.Clamp01((float)(node.PressureKPa/Math.Max(1,node.maxPressureKPa))),2),WithAlpha(color,.8f),1);
                }
            GasNode nodeAccent = hoveredNode != null ? hoveredNode : selectedNode;
            if (nodeAccent != null && Visible(world.WorldToCell(nodeAccent.transform.position)))
            {
                Vector2 p = WorldPoint(nodeAccent.transform.position);
                float half = Mathf.Clamp(1.35f*Screen.height/(viewCamera.orthographicSize*2*scale),20,66);
                Brackets(new Rect(p.x-half,p.y-half,half*2,half*2),WithAlpha(Mint,.74f),8);
                if (hoveredNode != null) RegisterHover("worldnode"+nodeAccent.GetInstanceID(),new Rect(p.x-29,p.y-29,58,58),NodeName(nodeAccent),nodeAccent.PressureKPa.ToString("0.0")+" kPa  ·  "+NodeStatus(nodeAccent));
            }
            if (hoveredLink != null)
            {
                Rect hit = new Rect(pointer.x-6,pointer.y-6,12,12);
                DrawIcon(Icon.Valve,new Rect(pointer.x+11,pointer.y+9,17,17),WithAlpha(Mint,.7f));
                RegisterHover("worldlink"+hoveredLink.GetInstanceID(),hit,LinkName(hoveredLink),Math.Abs(hoveredLink.lastFlowMolPerSecond).ToString("0.00")+" mol/s");
            }
            if (selectedNode == null && selectedLink == null && world.IsInside(selectedCell))
            {
                Vector2 p = WorldPoint(world.CellToWorld(selectedCell));
                float half = world.cellSize*Screen.height/(viewCamera.orthographicSize*4*scale);
                Brackets(new Rect(p.x-half,p.y-half,half*2,half*2),WithAlpha(Visible(selectedCell) ? Mint : Amber,.72f),Mathf.Min(5,half*.5f));
            }
            if (tool != ToolMode.None && !dockRect.Contains(pointer) && !(HasSelection && cardRect.Contains(pointer)))
                DrawIcon(tool == ToolMode.Sample ? Icon.Sample : Icon.Explore,new Rect(pointer.x+12,pointer.y+11,20,20),WithAlpha(Mint,.8f));
        }
        void DrawRegionEdges()
        {
            foreach (DeepPressureRegion region in world.Regions)
            {
                if (region == null) continue;
                for (int x = region.bounds.xMin; x < region.bounds.xMax; x++) { RegionEdge(new Vector2Int(x,region.bounds.yMin),0); RegionEdge(new Vector2Int(x,region.bounds.yMax-1),1); }
                for (int y = region.bounds.yMin; y < region.bounds.yMax; y++) { RegionEdge(new Vector2Int(region.bounds.xMin,y),2); RegionEdge(new Vector2Int(region.bounds.xMax-1,y),3); }
            }
        }
        void RegionEdge(Vector2Int cell,int edge)
        {
            if (!Visible(cell)) return;
            Vector2 p = WorldPoint(world.CellToWorld(cell));
            float half = world.cellSize*Screen.height/(viewCamera.orthographicSize*4*scale);
            Color color = WithAlpha(Mint,.26f);
            if (edge == 0 || edge == 1) Fill(new Rect(p.x-half+1,p.y+(edge == 0 ? half : -half),Mathf.Max(1,half*2-2),1),color);
            else Fill(new Rect(p.x+(edge == 2 ? -half : half),p.y-half+1,1,Mathf.Max(1,half*2-2)),color);
        }
        void HandleWorldInput(Event e,bool overInterface)
        {
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space) { SetColonyPause(!ColonyPaused); e.Use(); return; }
            if (overInterface || draggingValve) return;
            if (e.type == EventType.ScrollWheel)
            {
                Vector3 before = PointerWorld(pointer);
                viewCamera.orthographicSize = Mathf.Clamp(viewCamera.orthographicSize*Mathf.Pow(1.075f,e.delta.y),5,55);
                viewCamera.transform.position += before-PointerWorld(pointer); e.Use();
            }
            else if (e.type == EventType.MouseDrag && (e.button == 1 || e.button == 2))
            {
                float worldPerPixel = viewCamera.orthographicSize*2/Screen.height;
                viewCamera.transform.position += new Vector3(-e.delta.x*worldPerPixel,e.delta.y*worldPerPixel,0); e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 0)
            {
                selectedCell = world.WorldToCell(PointerWorld(pointer)); selectedNode = null; selectedLink = null;
                if (world.IsInside(selectedCell))
                {
                    if (tool != ToolMode.None) ExecuteExploration(tool == ToolMode.Explore,selectedCell);
                    else if (Visible(selectedCell)) { selectedNode = hoveredNode; if (selectedNode == null) selectedLink = hoveredLink; }
                }
                e.Use();
            }
        }
        bool HasRecordedSample(Vector2Int cell) { if (exploration == null) return false; var region = world.RegionAt(cell); return region != null && exploration.TryGetSample(region,out _); }
        bool Visible(Vector2Int cell) => world.IsInside(cell) && (exploration == null || exploration.IsVisible(cell));
        bool LinkVisible(GasLink link) => link != null && link.from != null && link.to != null && Visible(world.WorldToCell(link.from.transform.position)) && Visible(world.WorldToCell(link.to.transform.position));
        bool RoomVisible(DeepPressureRoom room) { if (exploration == null) return true; foreach (Vector2Int cell in room.cells) if (!Visible(cell)) return false; return true; }
        Vector2 WorldPoint(Vector3 position) { Vector3 p = viewCamera.WorldToScreenPoint(position); return new Vector2(p.x/scale,(Screen.height-p.y)/scale); }
        Vector3 PointerWorld(Vector2 point) => viewCamera.ScreenToWorldPoint(new Vector3(point.x*scale,Screen.height-point.y*scale,-viewCamera.transform.position.z));
        static Vector3 LinkPoint(GasLink link,LineRenderer renderer,int index)
        {
            if (renderer == null || renderer.positionCount < 2) return index == 0 ? link.from.transform.position : link.to.transform.position;
            Vector3 position = renderer.GetPosition(index); return renderer.useWorldSpace ? position : renderer.transform.TransformPoint(position);
        }
        static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b) { Vector2 d = b-a; float t = d.sqrMagnitude < 1e-8f ? 0 : Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude); return Vector2.Distance(p,a+d*t); }
        static string NodeName(GasNode node)
        {
            if (node == null) return "管路";
            if (HasChinese(node.displayName)) return node.displayName;
            switch (node.kind) { case GasNodeKind.Reservoir: return "高压气源"; case GasNodeKind.Regulator: return "调压机"; case GasNodeKind.Separator: return "气体分离机"; default: return node.initialComposition.x > .8f ? "氧气缓冲罐" : node.initialComposition.x < .01f ? "尾气缓冲罐" : "储气罐"; }
        }
        static string NodeStatus(GasNode node)
        {
            string state = node.status ?? string.Empty;
            if (state.Contains("Stopped")) return "出口受阻";
            if (state.Contains("Full")) return "已达储存上限";
            if (state.Contains("Empty")) return "等待供气";
            if (state.Contains("setpoint")) return "已达设定压力";
            if (state.Contains("Separating")) return "分离中";
            if (state.Contains("Flowing")) return "输送中";
            if (state.Contains("Disabled")) return "已停用";
            return "待机";
        }
        static string LinkName(GasLink link) => link.fromPort == GasOutputPort.OxygenProduct ? "氧气管路" : link.fromPort == GasOutputPort.TailGas ? "尾气管路" : "供气管路";
        static string TerrainName(TerrainKind kind) { switch (kind) { case TerrainKind.Soil: return "土壤"; case TerrainKind.Sandstone: return "砂岩"; case TerrainKind.Shale: return "页岩"; case TerrainKind.Basalt: return "玄武岩"; case TerrainKind.Metal: return "合金墙体"; default: return "空腔"; } }
        static string RegionName(DeepPressureRegion region)
        {
            if (HasChinese(region.displayName)) return region.displayName;
            string value = (region.displayName ?? string.Empty).ToUpperInvariant();
            if (value.Contains("HABITAT") || value.Contains("LIVING")) return "居住舱";
            if (value.Contains("PROCESS") || value.Contains("INDUSTR")) return "工艺舱";
            if (value.Contains("ARCHIVE") || value.Contains("OLD")) return "旧站遗迹";
            if (value.Contains("SHAFT")) return "联络竖井";
            if (value.Contains("RESERVOIR") || value.Contains("GAS")) return "气藏";
            return "地下区域";
        }
        static bool HasChinese(string text) { if (string.IsNullOrEmpty(text)) return false; foreach (char c in text) if (c >= '\u3400' && c <= '\u9fff') return true; return false; }
        void Metric(Rect rect,Icon icon,string text) { DrawIcon(icon,new Rect(rect.x,rect.y+3,17,17),Muted); Label(new Rect(rect.x+27,rect.y,rect.width-27,rect.height),text,body,White); }
        void Label(Rect rect,string text,GUIStyle style,Color color) { Color previous = GUI.color; GUI.color = color; GUI.Label(rect,text,style); GUI.color = previous; }
        bool Click(Rect rect) { Event e = Event.current; if ((pauseMenu&&!drawingPauseMenu)||e.type != EventType.MouseDown || e.button != 0 || !rect.Contains(pointer)) return false; e.Use(); return true; }
        static Rect Inset(Rect rect,float amount) => new Rect(rect.x+amount,rect.y+amount,rect.width-amount*2,rect.height-amount*2);
        static Color WithAlpha(Color color,float alpha) { color.a *= alpha; return color; }
        static void Fill(Rect rect,Color color) { if (rect.width <= 0 || rect.height <= 0) return; Color previous = GUI.color; GUI.color = color; GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color = previous; }
        static void DrawIcon(Icon icon,Rect rect,Color color) { Color previous = GUI.color; GUI.color = color; GUI.DrawTexture(rect,DeepUIIcons.Get(icon)); GUI.color = previous; }
        static void Rounded(Rect rect,Color color,float radius)
        {
            if (rect.width <= 0 || rect.height <= 0) return;
            radius = Mathf.Min(radius,Mathf.Min(rect.width,rect.height)*.5f);
            if (radius < .6f) { Fill(rect,color); return; }
            Color previous = GUI.color; GUI.color = color; Texture2D texture = DeepUIIcons.Rounded;
            float r = radius, u = .25f;
            GUI.DrawTextureWithTexCoords(new Rect(rect.x,rect.y,r,r),texture,new Rect(0,1-u,u,u));
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMax-r,rect.y,r,r),texture,new Rect(1-u,1-u,u,u));
            GUI.DrawTextureWithTexCoords(new Rect(rect.x,rect.yMax-r,r,r),texture,new Rect(0,0,u,u));
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMax-r,rect.yMax-r,r,r),texture,new Rect(1-u,0,u,u));
            GUI.DrawTextureWithTexCoords(new Rect(rect.x+r,rect.y,rect.width-r*2,r),texture,new Rect(u,1-u,1-u*2,u));
            GUI.DrawTextureWithTexCoords(new Rect(rect.x+r,rect.yMax-r,rect.width-r*2,r),texture,new Rect(u,0,1-u*2,u));
            GUI.DrawTextureWithTexCoords(new Rect(rect.x,rect.y+r,r,rect.height-r*2),texture,new Rect(0,u,u,1-u*2));
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMax-r,rect.y+r,r,rect.height-r*2),texture,new Rect(1-u,u,u,1-u*2));
            GUI.DrawTexture(new Rect(rect.x+r,rect.y+r,rect.width-r*2,rect.height-r*2),Texture2D.whiteTexture);
            GUI.color = previous;
        }
        static void Brackets(Rect rect,Color color,float length)
        {
            foreach (float x in new[] { rect.x,rect.xMax-1 }) foreach (float y in new[] { rect.y,rect.yMax-1 })
            { Fill(new Rect(x == rect.x ? x : x-length+1,y,length,1),color); Fill(new Rect(x,y == rect.y ? y : y-length+1,1,length),color); }
        }
        void OnDestroy() { if (interfaceFont != null) Destroy(interfaceFont); }
    }
}

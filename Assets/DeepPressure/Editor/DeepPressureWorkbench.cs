using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeepPressure.Editor
{
    public class DeepPressureWorkbench : EditorWindow
    {
        DeepPressureWorld world;
        TerrainKind paint = TerrainKind.Shale;
        int brushSize=1;
        bool painting;
        Vector2Int regionOrigin=new Vector2Int(5,13),regionSize=new Vector2Int(12,7);
        string regionName="New Region";
        GasNode from,to;
        GasOutputPort port;
        Vector2 scroll;
        string report="Select a generated level to begin.";
        [MenuItem("深压/关卡工作台",priority=1)]
        public static void Open(){GetWindow<DeepPressureWorkbench>("深压 · 关卡工作台").minSize=new Vector2(320,520);}
        void OnEnable(){SceneView.duringSceneGui+=DuringScene;Undo.undoRedoPerformed+=AfterUndo;}
        void OnDisable(){SceneView.duringSceneGui-=DuringScene;Undo.undoRedoPerformed-=AfterUndo;}
        void AfterUndo(){if(world!=null){world.SyncTerrainFromTilemap();world.RebuildRooms();SceneView.RepaintAll();}}
        void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Label("DEEP PRESSURE / LEVEL WORKBENCH",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("编辑模式制作实际关卡。生成会另存新场景，不覆盖已有手工关卡。Play 不生成地图。",MessageType.Info);
            if(GUILayout.Button("① 一键生成可编辑示范关卡",GUILayout.Height(30)))DeepPressureBuilder.Generate();
            world=(DeepPressureWorld)EditorGUILayout.ObjectField("关卡 World",world,typeof(DeepPressureWorld),true);
            if(world==null)world=FindObjectOfType<DeepPressureWorld>();
            using(new EditorGUI.DisabledScope(world==null||EditorApplication.isPlaying))
            {
                GUILayout.Space(8);GUILayout.Label("② 前景地形 · 四邻接自动拼接",EditorStyles.boldLabel);
                paint=(TerrainKind)EditorGUILayout.EnumPopup("笔刷 / Empty 擦除",paint);
                brushSize=EditorGUILayout.IntSlider("方形笔刷大小",brushSize,1,5);
                painting=GUILayout.Toggle(painting,"启用 Scene 画笔（左画 / Shift 擦 / Alt 导航）","Button");
                EditorGUILayout.HelpBox("同种材质连接；不同材质保留接缝。支持 Undo。也可把 Data/Terrain 资源放进 Tile Palette 绘制。",MessageType.None);
                GUILayout.Space(8);GUILayout.Label("③ 语义区域 · 稳定 ID + 网格矩形",EditorStyles.boldLabel);
                regionName=EditorGUILayout.TextField("区域名称",regionName);
                regionOrigin=EditorGUILayout.Vector2IntField("左下格坐标",regionOrigin);
                regionSize=EditorGUILayout.Vector2IntField("宽 / 高",regionSize);
                if(GUILayout.Button("添加区域（不挖空、不密封）"))
                {
                    var go=new GameObject(regionName);Undo.RegisterCreatedObjectUndo(go,"Add semantic region");go.transform.SetParent(world.transform,false);
                    var region=go.AddComponent<DeepPressureRegion>();region.stableId=Guid.NewGuid().ToString("N");region.displayName=regionName;
                    region.bounds=new RectInt(regionOrigin,new Vector2Int(Mathf.Max(1,regionSize.x),Mathf.Max(1,regionSize.y)));
                    Selection.activeGameObject=go;EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
                }
                GUILayout.Space(8);GUILayout.Label("④ 工业网络 · 显式端口",EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("复制现有设备后修改参数；素材不决定占格或流向。交叉不连接；只有下方创建的端点引用连通。",MessageType.None);
                from=(GasNode)EditorGUILayout.ObjectField("起点",from,typeof(GasNode),true);
                to=(GasNode)EditorGUILayout.ObjectField("终点",to,typeof(GasNode),true);
                port=(GasOutputPort)EditorGUILayout.EnumPopup("起点输出口",port);
                using(new EditorGUI.DisabledScope(from==null||to==null||from==to))
                    if(GUILayout.Button("连接端点 + 绘制直角管段"))MakeLink();
                GUILayout.Space(8);GUILayout.Label("⑤ 校验并保存",EditorStyles.boldLabel);
                if(GUILayout.Button("同步地形 / 重算房间 / 校验"))report=ValidateLevel(world);
                if(GUILayout.Button("运行气体守恒与区域自检")){try{report=DeepPressureSelfTests.RunAll();}catch(Exception e){report=e.ToString();}}
                if(GUILayout.Button("保存当前关卡"))EditorSceneManager.SaveScene(world.gameObject.scene);
            }
            EditorGUILayout.HelpBox(report,MessageType.None);
            EditorGUILayout.EndScrollView();
        }
        void DuringScene(SceneView view)
        {
            if(!painting||world==null||EditorApplication.isPlaying)return;
            Event e=Event.current; if(e.alt)return;
            if(world.terrain!=null && world.terrain.layoutGrid!=null) world.terrain.layoutGrid.cellSize=new Vector3(world.cellSize,world.cellSize,1);
            var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var plane=new Plane(Vector3.forward,world.transform.position);
            if(!plane.Raycast(ray,out float enter))return;
            Vector2Int p=world.WorldToCell(ray.GetPoint(enter));
            var min=world.transform.TransformPoint(new Vector3(p.x*world.cellSize,p.y*world.cellSize,0));
            Handles.color=e.shift||paint==TerrainKind.Empty?new Color(1,.3f,.25f,.8f):new Color(.3f,1,.8f,.8f);
            Handles.DrawWireCube(min+new Vector3(brushSize,brushSize,0)*world.cellSize*.5f,new Vector3(brushSize,brushSize,0)*world.cellSize);
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if((e.type==EventType.MouseDown||e.type==EventType.MouseDrag)&&e.button==0)
            {
                Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[]{world,world.terrain},"Paint terrain");
                TerrainKind k=e.shift?TerrainKind.Empty:paint;
                var tile=k==TerrainKind.Empty?null:DeepTerrainArtBaker.GetTile(k);
                for(int y=0;y<brushSize;y++)for(int x=0;x<brushSize;x++)
                {var cell=p+new Vector2Int(x,y);if(cell.x<0||cell.y<0||cell.x>=world.width||cell.y>=world.height)continue;world.terrain.SetTile(new Vector3Int(cell.x,cell.y,0),tile);}
                world.SyncTerrainFromTilemap();world.RebuildRooms();EditorUtility.SetDirty(world);EditorSceneManager.MarkSceneDirty(world.gameObject.scene);e.Use();
            }
            if(e.type==EventType.MouseMove)view.Repaint();
        }
        void MakeLink()
        {
            if(from.GetComponentInParent<DeepPressureWorld>()!=world||to.GetComponentInParent<DeepPressureWorld>()!=world){report="端点必须属于当前 World。";return;}
            var go=new GameObject(from.displayName+" → "+to.displayName);Undo.RegisterCreatedObjectUndo(go,"Connect gas ports");go.transform.SetParent(world.transform,false);
            var link=go.AddComponent<GasLink>();link.from=from;link.to=to;link.fromPort=port;
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(DeepPressureBuilder.Root+"/Materials/Unlit.mat");
            line.widthMultiplier=.16f;line.sortingOrder=5;line.startColor=line.endColor=new Color(.32f,.84f,.78f);line.positionCount=3;line.useWorldSpace=true;
            line.SetPositions(new[]{from.transform.position,new Vector3(to.transform.position.x,from.transform.position.y,0),to.transform.position});
            go.AddComponent<DeepPipeRoute>().CaptureEndpoints();
            DeepPipeVisualBaker.BakeAll(world);
            Selection.activeGameObject=go;EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
        }
        public static string ValidateLevel(DeepPressureWorld world)
        {
            if(world==null)return "No DeepPressureWorld in scene.";
            world.SyncTerrainFromTilemap();world.RebuildRooms();
            var issues=new System.Collections.Generic.List<string>();
            if(!world.ValidateTerrainAlignment(out string alignment))issues.Add(alignment);
            var regions=world.GetComponentsInChildren<DeepPressureRegion>();
            foreach(var group in regions.GroupBy(x=>x.stableId))if(string.IsNullOrWhiteSpace(group.Key)||group.Count()>1)issues.Add("区域 ID 为空或重复: "+group.Key);
            foreach(var region in regions)
                if(region.bounds.xMin<0||region.bounds.yMin<0||region.bounds.xMax>world.width||region.bounds.yMax>world.height||region.bounds.width<=0||region.bounds.height<=0)issues.Add("区域越界或面积无效: "+region.displayName);
            foreach(var node in world.GetComponentsInChildren<GasNode>())
            {var p=world.WorldToCell(node.transform.position);if(world.GetTerrain(p.x,p.y)!=TerrainKind.Empty)issues.Add("设备中心在实体地形内: "+node.displayName);}
            var occupied=new System.Collections.Generic.Dictionary<Vector2Int,DeepDevicePlacement>();
            foreach(var device in world.GetComponentsInChildren<DeepDevicePlacement>())
            {
                bool obstructed=false,overlap=false;
                foreach(var p in device.Bounds(world).allPositionsWithin)
                {
                    if(world.GetTerrain(p.x,p.y)!=TerrainKind.Empty)obstructed=true;
                    if(occupied.ContainsKey(p))overlap=true;else occupied[p]=device;
                }
                if(obstructed)issues.Add("设备占格穿墙或越界: "+device.name);
                if(overlap)issues.Add("设备占格与其他设备重叠: "+device.name);
            }
            foreach(var link in world.GetComponentsInChildren<GasLink>())if(link.from==null||link.to==null||link.from==link.to)issues.Add("无效管线: "+link.name);
            int open=world.Rooms.Count(x=>x.isOpen);
            string result=$"Regions {regions.Length} | Rooms {world.Rooms.Count} | Open rooms {open}\n"+(issues.Count==0?"PASS · 区域与管线基础校验通过。":string.Join("\n",issues));
            EditorUtility.SetDirty(world);Debug.Log(result);return result;
        }
        [MenuItem("深压/验证/当前关卡与守恒自检 %#F8")]
        public static void ValidateMenu()
        {
            Directory.CreateDirectory("../outputs");
            string result=DeepPressureSelfTests.RunAll()+"\n"+ValidateLevel(FindObjectOfType<DeepPressureWorld>());
            File.WriteAllText("../outputs/validation.txt",result);Debug.Log(result);
        }
        [MenuItem("深压/验证/保存运行画面 %#F9")]
        public static void Capture()
        {
            if(!EditorApplication.isPlaying){Debug.LogWarning("Enter Play mode first.");return;}
            Directory.CreateDirectory("../outputs");ScreenCapture.CaptureScreenshot(Path.GetFullPath("../outputs/DeepPressure-Game.png"));
        }
    }

    [EditorToolbarElement("DeepPressure/Generate",typeof(SceneView))]
    public class GenerateLevelButton : EditorToolbarButton
    {public GenerateLevelButton(){text="生成关卡";tooltip="生成并另存可编辑场景";clicked+=DeepPressureBuilder.Generate;}}
    [EditorToolbarElement("DeepPressure/Workbench",typeof(SceneView))]
    public class WorkbenchButton : EditorToolbarButton
    {public WorkbenchButton(){text="物品与建筑";clicked+=DeepContentWorkbench.Open;}}
    [EditorToolbarElement("DeepPressure/Validate",typeof(SceneView))]
    public class ValidateButton : EditorToolbarButton
    {public ValidateButton(){text="校验";clicked+=()=>DeepPressureWorkbench.ValidateLevel(UnityEngine.Object.FindObjectOfType<DeepPressureWorld>());}}
    [Overlay(typeof(SceneView),"Deep Pressure · 关卡工具",true)]
    public class DeepPressureToolbar : ToolbarOverlay
    {public DeepPressureToolbar():base("DeepPressure/Generate","DeepPressure/Workbench","DeepPressure/Validate") {}}
}

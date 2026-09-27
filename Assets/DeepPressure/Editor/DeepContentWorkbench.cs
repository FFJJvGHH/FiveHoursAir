using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepPressure.Editor
{
    /// <summary>Content-first authoring. Definition assets, never duplicated HUD tables, own the rules.</summary>
    public sealed class DeepContentWorkbench : EditorWindow
    {
        DeepGameplayCatalog catalog;
        DeepPressureWorld world;
        UnityEngine.Object selected;
        UnityEditor.Editor inspector;
        DeepBuildingDefinition placement;
        string search="",report="选择目录条目编辑，或将建筑预制体拖入 Scene。";
        int tab;
        Vector2 listScroll,detailScroll;
        const string DragKey="DeepPressure.ContentBuilding";
        readonly string[] tabs={"物品","建筑","科技","配方"};

        [MenuItem("深压/内容编辑器",priority=0)]
        public static void Open(){var window=GetWindow<DeepContentWorkbench>("深压 · 内容编辑器");window.minSize=new Vector2(820,560);}
        void OnEnable(){catalog=AssetDatabase.LoadAssetAtPath<DeepGameplayCatalog>(DeepCatalogBuilder.CatalogPath);SceneView.duringSceneGui+=DuringScene;}
        void OnDisable(){SceneView.duringSceneGui-=DuringScene;if(inspector!=null)DestroyImmediate(inspector);}
        void OnGUI()
        {
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("DEEP PRESSURE / CONTENT",EditorStyles.boldLabel,GUILayout.Width(210));
                catalog=(DeepGameplayCatalog)EditorGUILayout.ObjectField(catalog,typeof(DeepGameplayCatalog),false);
                using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if(GUILayout.Button("补齐基础内容",EditorStyles.toolbarButton,GUILayout.Width(95)))
                    {try{catalog=DeepCatalogBuilder.CreateOrUpdateCatalog();report="目录已补齐；已有配置和 Prefab 保持不变。";}catch(Exception exception){report=exception.Message;}}
                if(GUILayout.Button("检查目录",EditorStyles.toolbarButton,GUILayout.Width(75)))report=ValidateCatalog(catalog);
            }
            EditorGUILayout.HelpBox("数据源："+DeepCatalogBuilder.DataRoot+"。先定义物品、建筑、配方和科技，再摆入关卡；地形画笔作为辅助。",MessageType.None);
            if(catalog==null){EditorGUILayout.HelpBox("尚未选择内容目录。点击“补齐基础内容”创建独立资产。",MessageType.Info);return;}
            int next=GUILayout.Toolbar(tab,tabs);if(next!=tab){tab=next;selected=null;}
            using(new EditorGUILayout.HorizontalScope())
            {
                using(new EditorGUILayout.VerticalScope(GUILayout.Width(235)))
                {
                    search=EditorGUILayout.TextField("搜索",search);
                    listScroll=EditorGUILayout.BeginScrollView(listScroll);
                    foreach(var asset in Entries())DrawEntry(asset);
                    EditorGUILayout.EndScrollView();
                    using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                        if(GUILayout.Button("＋ 创建独立条目"))CreateEntry();
                }
                using(new EditorGUILayout.VerticalScope())
                {
                    detailScroll=EditorGUILayout.BeginScrollView(detailScroll);
                    if(selected==null)EditorGUILayout.HelpBox("从左侧选择内容。建筑列表支持拖放到 Scene；所有条件都可在资产中查看和修改。",MessageType.Info);
                    else DrawDetail();
                    EditorGUILayout.EndScrollView();
                }
            }
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                world=(DeepPressureWorld)EditorGUILayout.ObjectField("目标关卡",world,typeof(DeepPressureWorld),true);
                if(GUILayout.Button("地形 / 区域辅助",EditorStyles.toolbarButton,GUILayout.Width(115)))DeepPressureWorkbench.Open();
                using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if(GUILayout.Button("保存配置",EditorStyles.toolbarButton,GUILayout.Width(75))){AssetDatabase.SaveAssets();report="内容配置已保存。场景请使用 Unity 常规保存。";}
            }
            EditorGUILayout.HelpBox(report,MessageType.None);
        }

        IEnumerable<UnityEngine.Object> Entries()
        {
            IEnumerable<UnityEngine.Object> entries=tab==0?(catalog.items??Array.Empty<DeepItemDefinition>()):tab==1?(IEnumerable<UnityEngine.Object>)(catalog.buildings??Array.Empty<DeepBuildingDefinition>()):tab==2?(catalog.technologies??Array.Empty<DeepTechDefinition>()):(IEnumerable<UnityEngine.Object>)(catalog.recipes??Array.Empty<DeepRecipeDefinition>());
            return entries.Where(asset=>asset!=null&&(string.IsNullOrWhiteSpace(search)||(Label(asset)+" "+Id(asset)).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0));
        }
        void DrawEntry(UnityEngine.Object asset)
        {
            Rect rect=GUILayoutUtility.GetRect(210,50);
            if(selected==asset)EditorGUI.DrawRect(rect,new Color(.20f,.36f,.39f,.45f));
            Sprite icon=Icon(asset);if(icon!=null)GUI.DrawTextureWithTexCoords(new Rect(rect.x+5,rect.y+5,38,38),icon.texture,SpriteUV(icon),true);
            GUI.Label(new Rect(rect.x+50,rect.y+5,rect.width-54,20),Label(asset),EditorStyles.boldLabel);
            GUI.Label(new Rect(rect.x+50,rect.y+25,rect.width-54,18),Id(asset),EditorStyles.miniLabel);
            Event current=Event.current;
            if(rect.Contains(current.mousePosition)&&current.type==EventType.MouseDown&&current.button==0){selected=asset;current.Use();Repaint();}
            if(rect.Contains(current.mousePosition)&&current.type==EventType.MouseDrag&&asset is DeepBuildingDefinition definition&&definition.prefab!=null&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                DragAndDrop.PrepareStartDrag();DragAndDrop.objectReferences=new UnityEngine.Object[]{definition.prefab};DragAndDrop.SetGenericData(DragKey,definition);
                DragAndDrop.StartDrag(definition.displayName);current.Use();
            }
        }
        void DrawDetail()
        {
            GUILayout.Label(Label(selected),EditorStyles.largeLabel);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("定位资产",GUILayout.Width(80)))EditorGUIUtility.PingObject(selected);
                if(selected is DeepBuildingDefinition definition)
                {
                    using(new EditorGUI.DisabledScope(definition.prefab==null||EditorApplication.isPlayingOrWillChangePlaymode))
                        if(GUILayout.Button(placement==definition?"取消场景放置":"在 Scene 点击放置",GUILayout.Width(155)))placement=placement==definition?null:definition;
                    if(definition.prefab!=null&&GUILayout.Button("选择 Prefab",GUILayout.Width(90)))Selection.activeObject=definition.prefab;
                    using(new EditorGUI.DisabledScope(definition.prefab!=null||EditorApplication.isPlayingOrWillChangePlaymode))
                        if(GUILayout.Button("创建缺失 Prefab",GUILayout.Width(120)))
                        {try{DeepCatalogBuilder.CreateMissingPrefab(definition);AssetDatabase.SaveAssets();}catch(Exception exception){report=exception.Message;}}
                }
            }
            if(selected is DeepBuildingDefinition building)
            {
                EditorGUILayout.LabelField("施工",building.footprint.x+" × "+building.footprint.y+" 格 · "+building.workSeconds+" 秒 · "+Costs(building.cost));
                EditorGUILayout.LabelField("前置科技",TechLabel(building.requiredTechId));
                EditorGUILayout.LabelField("通行",building.blocksMovement?"实体阻挡":"可以从设备前方经过；仍占用建造空间");
                EditorGUILayout.LabelField("电力",building.powerGenerated+" 供给 / "+building.powerRequired+" 需求");
                if(building.role==DeepBuildingRole.Storage)EditorGUILayout.LabelField("仓容",building.storageCapacity+" 单位");
                DrawFootprint(building);
            }
            else if(selected is DeepTechDefinition technology)
            {
                EditorGUILayout.LabelField("科技分支",technology.branch+" · 第 "+(technology.tier+1)+" 层");
                EditorGUILayout.LabelField("研究前置",technology.prerequisiteIds==null||technology.prerequisiteIds.Length==0?"无":string.Join(" → ",technology.prerequisiteIds.Select(TechLabel)));
                EditorGUILayout.LabelField("投入",Costs(technology.cost)+" · "+technology.workSeconds+" 秒工时");
                string unlocks=string.Join("、",catalog.buildings.Where(x=>x!=null&&x.requiredTechId==technology.id).Select(x=>x.displayName)
                    .Concat(catalog.recipes.Where(x=>x!=null&&x.requiredTechId==technology.id).Select(x=>x.displayName+"（配方）")));
                EditorGUILayout.LabelField("实际解锁",string.IsNullOrEmpty(unlocks)?"尚未指定建筑或配方":unlocks);
            }
            else if(selected is DeepRecipeDefinition recipe)
            {
                EditorGUILayout.LabelField("输入",Costs(recipe.inputs));EditorGUILayout.LabelField("输出",Costs(recipe.outputs));
                var station=catalog.FindBuilding(recipe.requiredBuildingId);EditorGUILayout.LabelField("工作台",station==null?recipe.requiredBuildingId:station.displayName);
                EditorGUILayout.LabelField("前置科技",TechLabel(recipe.requiredTechId));
            }
            GUILayout.Space(8);EditorGUILayout.LabelField("配置资产（修改可 Undo）",EditorStyles.boldLabel);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {UnityEditor.Editor.CreateCachedEditor(selected,null,ref inspector);if(inspector!=null)inspector.OnInspectorGUI();}
        }
        void DrawFootprint(DeepBuildingDefinition definition)
        {
            Rect canvas=GUILayoutUtility.GetRect(300,160);EditorGUI.DrawRect(canvas,new Color(.12f,.15f,.18f));
            float scale=Mathf.Min((canvas.width-100)/Mathf.Max(1,definition.footprint.x),120f/Mathf.Max(1,definition.footprint.y));
            Vector2 origin=new Vector2(canvas.x+30,canvas.yMax-20);
            Handles.BeginGUI();Handles.color=new Color(.39f,.66f,.68f);
            for(int x=0;x<=definition.footprint.x;x++)Handles.DrawLine(origin+Vector2.right*x*scale,origin+new Vector2(x,-definition.footprint.y)*scale);
            for(int y=0;y<=definition.footprint.y;y++)Handles.DrawLine(origin+Vector2.down*y*scale,origin+new Vector2(definition.footprint.x,-y)*scale);
            foreach(var port in definition.ports??Array.Empty<DeepBuildingPort>())
            {Vector2 position=origin+new Vector2(port.localPosition.x,-port.localPosition.y)*scale;Handles.color=port.kind==DeepPortKind.GasIn?Color.cyan:port.kind==DeepPortKind.GasOut?new Color(1,.7f,.3f):Color.yellow;Handles.DrawSolidDisc(position,Vector3.forward,4);GUI.Label(new Rect(position.x+6,position.y-8,110,20),port.label,EditorStyles.miniLabel);}
            Handles.EndGUI();
        }

        void DuringScene(SceneView view)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Event current=Event.current;bool dragging=current.type==EventType.DragUpdated||current.type==EventType.DragPerform;
            var definition=dragging?DragAndDrop.GetGenericData(DragKey) as DeepBuildingDefinition:placement;
            if(definition==null)return;
            if(world==null)world=FindObjectOfType<DeepPressureWorld>();if(world==null){report="先指定目标关卡 World。";return;}
            if(current.alt)return;
            if(current.type==EventType.KeyDown&&current.keyCode==KeyCode.Escape){placement=null;current.Use();Repaint();return;}
            var ray=HandleUtility.GUIPointToWorldRay(current.mousePosition);var plane=new Plane(world.transform.forward,world.transform.position);
            if(!plane.Raycast(ray,out float distance))return;
            Vector2Int cell=world.WorldToCell(ray.GetPoint(distance));
            bool allowed=CanPlace(world,definition,cell,out string reason);
            Vector3 lower=world.CellToWorld(cell)-world.transform.TransformVector(new Vector3(.5f,.5f,0)*world.cellSize);
            Handles.color=allowed?new Color(.3f,1,.74f,.9f):new Color(1,.3f,.24f,.9f);
            var corners=new[]{lower,lower+world.transform.TransformVector(new Vector3(definition.footprint.x*world.cellSize,0,0)),lower+world.transform.TransformVector(new Vector3(definition.footprint.x*world.cellSize,definition.footprint.y*world.cellSize,0)),lower+world.transform.TransformVector(new Vector3(0,definition.footprint.y*world.cellSize,0)),lower};
            Handles.DrawAAPolyLine(3,corners);Handles.Label(corners[3],definition.displayName+" · "+(allowed?"点击放置 / Esc 取消":reason));
            if(dragging)
            {
                DragAndDrop.visualMode=allowed?DragAndDropVisualMode.Copy:DragAndDropVisualMode.Rejected;
                if(current.type==EventType.DragPerform&&allowed){DragAndDrop.AcceptDrag();Place(world,definition,cell);DragAndDrop.SetGenericData(DragKey,null);}
                current.Use();
            }
            else
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                if(current.type==EventType.MouseDown&&current.button==0){if(allowed)Place(world,definition,cell);else report=reason;current.Use();Repaint();}
            }
            if(current.type==EventType.MouseMove||dragging)view.Repaint();
        }
        void Place(DeepPressureWorld target,DeepBuildingDefinition definition,Vector2Int origin)
        {
            if(definition.prefab==null){report="定义没有 Prefab。";return;}
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(definition.prefab,target.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(instance,"Place authored building");instance.transform.SetParent(target.transform,false);
            instance.transform.position=target.CellToWorld(origin)-target.transform.TransformVector(new Vector3(.5f,.5f,0)*target.cellSize);
            instance.transform.localScale=Vector3.one*target.cellSize;
            Type type=DeepCatalogBuilder.BuildingInstanceType();Component component=type==null?null:instance.GetComponent(type);
            if(component!=null){var data=new SerializedObject(component);data.FindProperty("definition").objectReferenceValue=definition;data.FindProperty("origin").vector2IntValue=origin;data.ApplyModifiedPropertiesWithoutUndo();}
            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);Selection.activeGameObject=instance;report="已摆放 "+definition.displayName+" @ "+origin+"；场景实例与配置资产分离。";
        }
        static bool CanPlace(DeepPressureWorld world,DeepBuildingDefinition definition,Vector2Int origin,out string reason)
        {
            if(definition.prefab==null){reason="缺少 Prefab";return false;}
            RectInt bounds=new RectInt(origin,definition.footprint);
            foreach(Vector2Int cell in bounds.allPositionsWithin)
            {if(!world.IsInside(cell)){reason="超出地图";return false;}if(world.GetTerrain(cell.x,cell.y)!=TerrainKind.Empty){reason="占格与实体地形重叠";return false;}}
            Type type=DeepCatalogBuilder.BuildingInstanceType();
            if(type!=null)foreach(Component component in world.GetComponentsInChildren(type,true))
            {
                var data=new SerializedObject(component);var other=data.FindProperty("definition").objectReferenceValue as DeepBuildingDefinition;
                if(other==null)continue;RectInt occupied=new RectInt(data.FindProperty("origin").vector2IntValue,other.footprint);
                if(bounds.Overlaps(occupied)){reason="与已摆放建筑重叠";return false;}
            }
            if(definition.requiresFloor)
            {
                for(int x=origin.x;x<origin.x+definition.footprint.x;x++)
                {
                    if(world.GetTerrain(x,origin.y-1)!=TerrainKind.Empty)continue;
                    bool supported=false;
                    if(type!=null)foreach(Component component in world.GetComponentsInChildren(type,true))
                    {
                        var data=new SerializedObject(component);var other=data.FindProperty("definition").objectReferenceValue as DeepBuildingDefinition;
                        if(other==null||other.role!=DeepBuildingRole.Floor)continue;
                        if(new RectInt(data.FindProperty("origin").vector2IntValue,other.footprint).Contains(new Vector2Int(x,origin.y-1))){supported=true;break;}
                    }
                    if(!supported){reason="底部缺少地形或地板支撑";return false;}
                }
            }
            reason="可放置";return true;
        }

        void CreateEntry()
        {
            string folder=DeepCatalogBuilder.DataRoot+"/"+new[]{"Items","Buildings","Technologies","Recipes"}[tab];System.IO.Directory.CreateDirectory(folder);
            string id=new[]{"item","building","tech","recipe"}[tab]+"_"+Guid.NewGuid().ToString("N").Substring(0,8);
            ScriptableObject value;
            if(tab==0){var x=CreateInstance<DeepItemDefinition>();x.id=id;x.displayName="新物品";catalog.items=Append(catalog.items,x);value=x;}
            else if(tab==1){var x=CreateInstance<DeepBuildingDefinition>();x.id=id;x.displayName="新建筑";catalog.buildings=Append(catalog.buildings,x);value=x;}
            else if(tab==2){var x=CreateInstance<DeepTechDefinition>();x.id=id;x.displayName="新科技";catalog.technologies=Append(catalog.technologies,x);value=x;}
            else{var x=CreateInstance<DeepRecipeDefinition>();x.id=id;x.displayName="新配方";catalog.recipes=Append(catalog.recipes,x);value=x;}
            AssetDatabase.CreateAsset(value,folder+"/"+id+".asset");EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();selected=value;report="新条目已加入目录。请填写名称、数值和引用。";
        }
        static T[] Append<T>(T[] values,T value)=> (values??Array.Empty<T>()).Concat(new[]{value}).ToArray();
        string TechLabel(string id)=>string.IsNullOrEmpty(id)?"无":catalog.FindTech(id)!=null?catalog.FindTech(id).displayName:"未找到: "+id;
        static string Costs(DeepItemAmount[] costs)=>costs==null||costs.Length==0?"无":string.Join(" + ",costs.Select(cost=>(cost.item==null?"缺失物品":cost.item.displayName)+" × "+cost.amount));
        static string Label(UnityEngine.Object value)=>value is DeepItemDefinition a?a.displayName:value is DeepBuildingDefinition b?b.displayName:value is DeepTechDefinition c?c.displayName:((DeepRecipeDefinition)value).displayName;
        static string Id(UnityEngine.Object value)=>value is DeepItemDefinition a?a.id:value is DeepBuildingDefinition b?b.id:value is DeepTechDefinition c?c.id:((DeepRecipeDefinition)value).id;
        static Sprite Icon(UnityEngine.Object value)=>value is DeepItemDefinition a?a.icon:value is DeepBuildingDefinition b?b.icon:value is DeepTechDefinition c?c.icon:((DeepRecipeDefinition)value).icon;
        static Rect SpriteUV(Sprite sprite)=>new Rect(sprite.rect.x/sprite.texture.width,sprite.rect.y/sprite.texture.height,sprite.rect.width/sprite.texture.width,sprite.rect.height/sprite.texture.height);

        public static string ValidateCatalog(DeepGameplayCatalog catalog)
        {
            if(catalog==null)return "未选择目录。";
            var issues=new List<string>();
            CheckIds(catalog.items,issues);CheckIds(catalog.buildings,issues);CheckIds(catalog.technologies,issues);CheckIds(catalog.recipes,issues);
            foreach(var building in catalog.buildings??Array.Empty<DeepBuildingDefinition>())
            {
                if(building==null)continue;CheckCosts(building.displayName,building.cost,catalog,issues);
                if(building.prefab==null)issues.Add(building.displayName+" 缺少 Prefab");
                if(building.footprint.x<1||building.footprint.y<1||building.workSeconds<=0)issues.Add(building.displayName+" 占格或工时无效");
                if(!string.IsNullOrEmpty(building.requiredTechId)&&catalog.FindTech(building.requiredTechId)==null)issues.Add(building.displayName+" 前置科技不存在");
                if(building.role==DeepBuildingRole.Storage&&building.storageCapacity<=0)issues.Add(building.displayName+" 仓容必须为正数");
                var portIds=new HashSet<string>();
                foreach(var port in building.ports??Array.Empty<DeepBuildingPort>())
                {if(string.IsNullOrWhiteSpace(port.id)||!portIds.Add(port.id))issues.Add(building.displayName+" 端口ID为空或重复");if(port.localPosition.x<0||port.localPosition.y<0||port.localPosition.x>building.footprint.x||port.localPosition.y>building.footprint.y)issues.Add(building.displayName+" 端口在占格外");}
            }
            var done=new HashSet<string>();
            foreach(var tech in catalog.technologies??Array.Empty<DeepTechDefinition>())
            {
                if(tech==null)continue;CheckCosts(tech.displayName,tech.cost,catalog,issues);VisitTech(tech,catalog,new HashSet<string>(),done,issues);
                foreach(string id in tech.unlockBuildingIds??Array.Empty<string>())
                {var target=catalog.FindBuilding(id);if(target==null||target.requiredTechId!=tech.id)issues.Add(tech.displayName+" 建筑解锁摘要与实际条件不一致："+id);}
                foreach(string id in tech.unlockRecipeIds??Array.Empty<string>())
                {var target=catalog.FindRecipe(id);if(target==null||target.requiredTechId!=tech.id)issues.Add(tech.displayName+" 配方解锁摘要与实际条件不一致："+id);}
            }
            foreach(var recipe in catalog.recipes??Array.Empty<DeepRecipeDefinition>())
            {
                if(recipe==null)continue;CheckCosts(recipe.displayName+" 输入",recipe.inputs,catalog,issues);CheckCosts(recipe.displayName+" 输出",recipe.outputs,catalog,issues);
                if(recipe.inputs==null||recipe.inputs.Length==0||recipe.outputs==null||recipe.outputs.Length==0||recipe.workSeconds<=0)issues.Add(recipe.displayName+" 必须有输入、输出与正工时");
                if(catalog.FindBuilding(recipe.requiredBuildingId)==null)issues.Add(recipe.displayName+" 工作台ID无效");
                if(!string.IsNullOrEmpty(recipe.requiredTechId)&&catalog.FindTech(recipe.requiredTechId)==null)issues.Add(recipe.displayName+" 前置科技不存在");
            }
            return issues.Count==0?"通过：目录ID、成本引用、建筑占格、配方和科技依赖有效。":"发现 "+issues.Count+" 项：\n"+string.Join("\n",issues);
        }
        static void CheckIds<T>(T[] values,List<string> issues) where T:UnityEngine.Object
        {
            var ids=new HashSet<string>();foreach(T value in values??Array.Empty<T>())
            {if(value==null){issues.Add(typeof(T).Name+" 含空条目");continue;}string id=Id(value);if(string.IsNullOrWhiteSpace(id)||!ids.Add(id))issues.Add(typeof(T).Name+" ID为空或重复："+id);else if(id.Any(character=>!char.IsLetterOrDigit(character)&&character!='_'&&character!='-'))issues.Add(id+" ID只能含字母、数字、下划线或短横线");if(string.IsNullOrWhiteSpace(Label(value)))issues.Add(id+" 缺少显示名称");}
        }
        static void CheckCosts(string label,DeepItemAmount[] costs,DeepGameplayCatalog catalog,List<string> issues)
        {foreach(var cost in costs??Array.Empty<DeepItemAmount>())if(cost.item==null||cost.amount<=0||catalog.items==null||!catalog.items.Contains(cost.item))issues.Add(label+" 成本/产物物品引用或数量无效");}
        static void VisitTech(DeepTechDefinition tech,DeepGameplayCatalog catalog,HashSet<string> visiting,HashSet<string> done,List<string> issues)
        {
            if(done.Contains(tech.id))return;if(!visiting.Add(tech.id)){issues.Add("科技循环依赖："+tech.id);return;}
            foreach(string id in tech.prerequisiteIds??Array.Empty<string>())
            {var parent=catalog.FindTech(id);if(parent==null)issues.Add(tech.displayName+" 缺少前置 "+id);else VisitTech(parent,catalog,visiting,done,issues);}
            visiting.Remove(tech.id);done.Add(tech.id);
        }
    }
}

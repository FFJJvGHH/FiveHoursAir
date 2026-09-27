using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DeepPressure.Editor
{
    /// <summary>Creates missing starter content. Existing authored values and prefabs are preserved.</summary>
    public static class DeepCatalogBuilder
    {
        public const string DataRoot="Assets/DeepPressure/Data/Gameplay";
        public const string CatalogPath=DataRoot+"/GameplayCatalog.asset";
        public const string PrefabRoot=DataRoot+"/Prefabs";
        public const string InstanceTypeName="DeepPressure.DeepBuildingInstance";

        [MenuItem("深压/内容/创建或补齐基础目录")]
        public static void CreateFromMenu()
        {
            var catalog=CreateOrUpdateCatalog();
            Debug.Log("Content catalog ready: "+AssetDatabase.GetAssetPath(catalog)+". Existing authored values were preserved.");
        }

        public static DeepGameplayCatalog CreateOrUpdateCatalog()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before authoring catalog assets; the active game was not modified.");
            foreach(string folder in new[]{DataRoot,DataRoot+"/Items",DataRoot+"/Buildings",DataRoot+"/Technologies",DataRoot+"/Recipes",PrefabRoot})Directory.CreateDirectory(folder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var catalog=LoadOrCreate<DeepGameplayCatalog>(CatalogPath,out bool newCatalog);
            var items=new List<DeepItemDefinition>(catalog.items??Array.Empty<DeepItemDefinition>());
            var buildings=new List<DeepBuildingDefinition>(catalog.buildings??Array.Empty<DeepBuildingDefinition>());
            var technologies=new List<DeepTechDefinition>(catalog.technologies??Array.Empty<DeepTechDefinition>());
            var recipes=new List<DeepRecipeDefinition>(catalog.recipes??Array.Empty<DeepRecipeDefinition>());
            var ore=Item(items,"ore","矿石","地下矿层产出的混合矿料；送入合成台加工。",new Color(.71f,.56f,.39f),TerrainIcon("Basalt"));
            var alloy=Item(items,"alloy","合金","建筑框架、管道与机械的通用合金。",new Color(.54f,.73f,.79f),TerrainIcon("Metal"));
            var electronics=Item(items,"electronics","电子元件","控制器与仪表使用的标准电子模块。",new Color(.35f,.9f,.76f),Art("Props/console_Color.png"));
            var data=Item(items,"research_data","研究数据","解析电子模块获得的记录；研究时消耗。",new Color(.68f,.59f,.91f),Art("Props/console_Color.png"));
            var fuel=Item(items,"fuel","燃料块","开采页岩获得的可燃物。发电机持续消耗；矿料压制可补充燃料。",new Color(.91f,.59f,.26f),TerrainIcon("Shale"));

            var lamp=Building(buildings,"lamp","工作灯","照明",DeepBuildingRole.Light,new Vector2Int(1,1),Art("Props/vent_Color.png"),null,3,false,false,new[]{Cost(alloy,1),Cost(electronics,1)},
                "照亮工作区域。需要 1 单位电力，可以单独关闭。",0,1);
            var ladder=Building(buildings,"ladder","梯子","交通",DeepBuildingRole.Ladder,Vector2Int.one,TerrainIcon("Metal"),null,2,false,false,new[]{Cost(alloy,1)},"连接上下通路；需要连续梯段才能攀爬。");
            var floor=Building(buildings,"floor","地板","交通",DeepBuildingRole.Floor,Vector2Int.one,TerrainIcon("Metal"),null,3,false,true,new[]{Cost(ore,2),Cost(alloy,1)},"在空格中建造承重地板，提供站立与施工支撑。");
            var storage=Building(buildings,"storage","仓库","物流",DeepBuildingRole.Storage,new Vector2Int(3,2),Art("Props/crate_Color.png"),null,8,true,false,new[]{Cost(alloy,6)},"提供共享库存容量；输入和成品占用同一有限空间。",0,0,200);
            var generator=Building(buildings,"generator","燃料发电机","动力",DeepBuildingRole.Generator,new Vector2Int(3,3),Art("Industrial/compressor_Color.png"),null,12,true,false,new[]{Cost(alloy,8),Cost(electronics,2)},
                "提供 20 W；每 45 秒消耗 1 燃料。设备必须通过完工电线连接，空载自动待机。",20,0);
            var desk=Building(buildings,"research_bench","研究台","科研",DeepBuildingRole.Research,new Vector2Int(2,2),Art("Props/console_Color.png"),null,10,true,false,new[]{Cost(alloy,8),Cost(electronics,4)},
                "工人在此执行研究订单；需要 5 单位电力和足够的研究材料。",0,5);
            var fabricator=Building(buildings,"fabricator","合成台","制造",DeepBuildingRole.Fabricator,new Vector2Int(3,2),Art("Industrial/compressor_Color.png"),null,10,true,false,new[]{Cost(alloy,6),Cost(electronics,2)},
                "按配方处理矿石、合金与电子模块。输入不足或仓库满时暂停。",0,4);
            var tank=Building(buildings,"gas_tank","气体储罐","气体工业",DeepBuildingRole.GasTank,new Vector2Int(2,3),Art("Industrial/tank_Color.png"),"survey_basics",10,true,false,new[]{Cost(alloy,8)},"有限容积的气体缓冲；输入输出需要另接显式管线。");
            var regulator=Building(buildings,"gas_regulator","调压机","气体工业",DeepBuildingRole.GasRegulator,new Vector2Int(2,2),Art("Industrial/compressor_Color.png"),"pressure_engineering",14,true,false,new[]{Cost(alloy,10),Cost(electronics,3)},"限制下游压力；物理输入与输出端口独立配置。",0,2);
            var separator=Building(buildings,"gas_separator","分离塔","气体工业",DeepBuildingRole.GasSeparator,new Vector2Int(3,4),Art("Industrial/separator_Color.png"),"selective_separation",18,true,false,new[]{Cost(alloy,16),Cost(electronics,5)},"双出口组分分离；产品口与尾气口都需要有效接收端。",0,6);
            var battery=Building(buildings,"battery","蓄电池","动力",DeepBuildingRole.Battery,new Vector2Int(2,2),Art("Props/crate_Color.png"),"power_distribution",10,true,false,new[]{Cost(alloy,8),Cost(electronics,3)},"储存 600 J 电能，最大充放电 20 W。只为同一条电线网络供电，断线后保留剩余电量。");
            var intake=Building(buildings,"intake_pump","环境集气泵","气体工业",DeepBuildingRole.GasPump,new Vector2Int(2,2),Art("Industrial/compressor_Color.png"),null,10,true,false,new[]{Cost(alloy,6),Cost(electronics,2)},"从所在房间抽取混合气体，降低室内气压。消耗 10 W；出口需要接缓冲罐或处理设备。",0,10);
            var oxygenTank=Building(buildings,"oxygen_tank","氧气储罐","气体工业",DeepBuildingRole.GasTank,new Vector2Int(2,3),Art("Industrial/tank_Color.png"),"survey_basics",10,true,false,new[]{Cost(alloy,8)},"储存含氧产品，与原气、尾气分开管理。通过供气口给人员所在房间补气；储罐本身不产气。");
            var wasteTank=Building(buildings,"waste_tank","尾气储罐","气体工业",DeepBuildingRole.GasTank,new Vector2Int(2,3),Art("Industrial/tank_Color.png"),"pressure_engineering",10,true,false,new[]{Cost(alloy,8)},"收集分离尾气或过滤废气。有限容量；满罐会阻塞上游，需要另接排气口处理。");
            var vent=Building(buildings,"supply_vent","室内供气口","气体工业",DeepBuildingRole.Vent,Vector2Int.one,Art("Props/vent_Color.png"),null,4,false,false,new[]{Cost(alloy,2)},"将管道气体注入所在房间，氧分压达到 21 kPa 或总压达到 135 kPa 后关闭。连接氧气来源，维持人员呼吸。");
            var exhaust=Building(buildings,"exhaust_vent","尾气排放口","气体工业",DeepBuildingRole.Vent,Vector2Int.one,Art("Props/vent_Color.png"),"pressure_engineering",4,false,false,new[]{Cost(alloy,2)},"把管道尾气排入所在房间。请放置在隔离废气区；不会无条件删除气体。");
            var scrubber=Building(buildings,"co2_scrubber","二氧化碳收集器","气体工业",DeepBuildingRole.GasPump,new Vector2Int(2,2),Art("Industrial/separator_Color.png"),"pressure_engineering",12,true,false,new[]{Cost(alloy,8),Cost(electronics,3)},"消耗 10 W，从室内选择收集二氧化碳。需要尾气储罐和排放链，不能凭空销毁废气。",0,10);

            Tech(technologies,"survey_basics","地下测量","记录洞层与气体样本；解锁第一座气体缓冲罐。",tank.icon,25,Array.Empty<string>(),new[]{Cost(data,4)},new[]{tank.id});
            Tech(technologies,"pressure_engineering","压力工程","在测量基础上控制下游压力，保护供气系统。",regulator.icon,40,new[]{"survey_basics"},new[]{Cost(data,8),Cost(alloy,4)},new[]{regulator.id});
            Tech(technologies,"selective_separation","选择分离","建立独立产品与尾气处理链。必须同时管理两个出口。",separator.icon,60,new[]{"pressure_engineering","advanced_fabrication"},new[]{Cost(data,12),Cost(electronics,3)},new[]{separator.id});
            Recipe(recipes,"smelt_alloy","冶炼合金","将矿料转化为结构材料。",alloy.icon,new[]{Cost(ore,3)},new[]{Cost(alloy,1)},8,fabricator.id);
            Recipe(recipes,"assemble_electronics","组装电子元件","用合金材料组装标准电子模块（架空工艺）。",electronics.icon,new[]{Cost(alloy,2)},new[]{Cost(electronics,1)},12,fabricator.id);
            Recipe(recipes,"compile_research","解析研究数据","消耗电子模块以读取旧站记录。",data.icon,new[]{Cost(electronics,1)},new[]{Cost(data,2)},10,fabricator.id);
            Recipe(recipes,"press_fuel","压制燃料","从混合矿料中提取可燃组分：8 矿石 → 2 燃料。",fuel.icon,new[]{Cost(ore,8)},new[]{Cost(fuel,2)},12,fabricator.id);

            var advancedStorage=Building(buildings,"advanced_storage","分区仓库","物流",DeepBuildingRole.Storage,new Vector2Int(3,2),storage.icon,"colony_planning",12,true,false,new[]{Cost(alloy,10),Cost(electronics,1)},"提供 400 单位共享仓容，缓解采掘与生产堵塞。",0,0,400);
            var improvedGenerator=Building(buildings,"improved_generator","高效地热机","动力",DeepBuildingRole.Generator,new Vector2Int(3,3),generator.icon,"power_distribution",20,true,false,new[]{Cost(alloy,16),Cost(electronics,5)},"提供 45 单位电力，支持制造、研究与分离设备同时运行。",45,0);
            var deepStorage=Building(buildings,"deep_storage","深层物资库","物流",DeepBuildingRole.Storage,new Vector2Int(4,3),storage.icon,"deep_support",24,true,false,new[]{Cost(alloy,22),Cost(electronics,4)},"提供 1000 单位共享仓容，为连续工业生产建立储备。",0,0,1000);
            var pressureTank=Building(buildings,"high_pressure_tank","大型缓冲罐","气体工业",DeepBuildingRole.GasTank,new Vector2Int(3,4),tank.icon,"deep_support",22,true,false,new[]{Cost(alloy,20),Cost(electronics,2)},"24 立方米气体容积，缓冲分离链与供气端的流量差。",0,0);
            var precisionFabricator=Building(buildings,"precision_fabricator","精密制造台","制造",DeepBuildingRole.Fabricator,new Vector2Int(3,2),fabricator.icon,"industrial_efficiency",22,true,false,new[]{Cost(alloy,18),Cost(electronics,8)},"执行精密冶炼，以更少矿料与工时产出合金。需要 8 单位电力。",0,8);

            Tech(technologies,"colony_planning","殖民规划","整理建造与物资通路，为扩建基地建立仓储缓冲。",advancedStorage.icon,18,Array.Empty<string>(),new[]{Cost(ore,8)},new[]{advancedStorage.id});
            Tech(technologies,"power_distribution","能源分配","提升地热耦合效率，为多台工业设备提供稳定功率。",improvedGenerator.icon,35,new[]{"colony_planning"},new[]{Cost(data,6),Cost(alloy,4)},new[]{improvedGenerator.id});
            Tech(technologies,"deep_support","深层保障","整合大容量物资与气体缓冲设施，为深层生产做准备。",pressureTank.icon,55,new[]{"power_distribution","pressure_engineering"},new[]{Cost(data,10),Cost(electronics,3)},new[]{deepStorage.id,pressureTank.id});
            Tech(technologies,"material_processing","材料学","改善矿石分类与批量冶炼；9 矿石可产出 4 合金。",alloy.icon,20,Array.Empty<string>(),new[]{Cost(ore,10)},Array.Empty<string>());
            Tech(technologies,"advanced_fabrication","电子工程","提高电子模块装配及研究资料解码效率。",electronics.icon,38,new[]{"material_processing"},new[]{Cost(data,6),Cost(alloy,3)},Array.Empty<string>());
            Tech(technologies,"industrial_efficiency","高效生产","将能源与材料技术用于精密制造，缩短加工链。",precisionFabricator.icon,55,new[]{"advanced_fabrication","power_distribution"},new[]{Cost(data,10),Cost(electronics,3)},new[]{precisionFabricator.id});
            Recipe(recipes,"smelt_alloy_bulk","批量冶炼","更充分利用矿料：9 矿石 → 4 合金。",alloy.icon,new[]{Cost(ore,9)},new[]{Cost(alloy,4)},20,fabricator.id,"material_processing");
            Recipe(recipes,"assemble_electronics_batch","模块化装配","批量装配降低损耗：5 合金 → 3 电子元件。",electronics.icon,new[]{Cost(alloy,5)},new[]{Cost(electronics,3)},24,fabricator.id,"advanced_fabrication");
            Recipe(recipes,"compile_research_batch","深度资料解码","整合旧站档案：2 电子元件 → 6 研究数据。",data.icon,new[]{Cost(electronics,2)},new[]{Cost(data,6)},18,fabricator.id,"advanced_fabrication");
            Recipe(recipes,"precision_alloy","精密冶炼","在精密制造台上加工：6 矿石 → 4 合金。",alloy.icon,new[]{Cost(ore,6)},new[]{Cost(alloy,4)},12,precisionFabricator.id,"industrial_efficiency");

            if(catalog.contentRevision<2)
            {
                string[][] branches={new[]{"survey_basics","pressure_engineering","selective_separation"},new[]{"colony_planning","power_distribution","deep_support"},new[]{"material_processing","advanced_fabrication","industrial_efficiency"}};
                string[] labels={"气体工程","殖民工程","材料制造"};
                for(int branch=0;branch<branches.Length;branch++)for(int tier=0;tier<branches[branch].Length;tier++)
                {
                    var technology=technologies.First(x=>x.id==branches[branch][tier]);technology.branch=labels[branch];technology.tier=tier;
                    technology.displayName=technology.displayName.Replace("第一层 · ","").Replace("第二层 · ","").Replace("第三层 · ","");
                    technology.unlockRecipeIds=recipes.Where(x=>x!=null&&x.requiredTechId==technology.id).Select(x=>x.id).ToArray();
                    EditorUtility.SetDirty(technology);
                }
                technologies.First(x=>x.id=="selective_separation").prerequisiteIds=new[]{"pressure_engineering","advanced_fabrication"};
                pressureTank.gasStorageVolume=24;EditorUtility.SetDirty(pressureTank);
                catalog.contentRevision=2;
            }

            if(catalog.contentRevision<3)
            {
                generator.displayName="燃料发电机";generator.description="提供 20 W；每 45 秒消耗 1 燃料。连接完工电线才能供电，空载自动待机。";
                generator.fuelItem=fuel;generator.fuelUnitsPerSecond=1f/45;
                improvedGenerator.displayName="高效燃料机";improvedGenerator.description="提供 45 W；每 75 秒消耗 1 燃料。连接电池储存富余电量。";improvedGenerator.fuelItem=fuel;improvedGenerator.fuelUnitsPerSecond=1f/75;
                battery.batteryCapacity=600;battery.batteryTransferRate=20;battery.ports=new[]{Port("power_in","电网",DeepPortKind.PowerIn,new Vector2(.25f,.15f))};
                tank.displayName="原气缓冲罐";tank.description="有限容积的混合气体缓冲，不产气、不供氧。先接环境集气泵，再接分离或供气设施。";
                ConfigureGas(intake,DeepGasFacilityMode.Collect,DeepGasAcceptance.Any,true,4);
                ConfigureGas(vent,DeepGasFacilityMode.Supply,DeepGasAcceptance.Oxygen,true,3);
                ConfigureGas(exhaust,DeepGasFacilityMode.Exhaust,DeepGasAcceptance.Waste,true,4);
                ConfigureGas(scrubber,DeepGasFacilityMode.Scrub,DeepGasAcceptance.Waste,true,2);
                ConfigureGas(oxygenTank,DeepGasFacilityMode.Storage,DeepGasAcceptance.Oxygen,false,0);
                ConfigureGas(wasteTank,DeepGasFacilityMode.Storage,DeepGasAcceptance.Waste,false,0);
                foreach(var definition in new[]{generator,improvedGenerator,battery,tank,intake,vent,exhaust,scrubber,oxygenTank,wasteTank})EditorUtility.SetDirty(definition);
                var powerTech=technologies.First(x=>x.id=="power_distribution");powerTech.unlockBuildingIds=new[]{improvedGenerator.id,battery.id};powerTech.description="提高燃料利用效率并解锁蓄电池，在同一电线网络中缓冲负载。";EditorUtility.SetDirty(powerTech);
                var surveyTech=technologies.First(x=>x.id=="survey_basics");surveyTech.unlockBuildingIds=new[]{tank.id,oxygenTank.id};EditorUtility.SetDirty(surveyTech);
                var pressureTech=technologies.First(x=>x.id=="pressure_engineering");pressureTech.unlockBuildingIds=new[]{regulator.id,wasteTank.id,exhaust.id,scrubber.id};EditorUtility.SetDirty(pressureTech);
                catalog.contentRevision=3;
            }

            catalog.items=items.Where(x=>x!=null).Distinct().ToArray();
            catalog.buildings=buildings.Where(x=>x!=null).Distinct().ToArray();
            catalog.technologies=technologies.Where(x=>x!=null).Distinct().ToArray();
            catalog.recipes=recipes.Where(x=>x!=null).Distinct().ToArray();
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
            foreach(var definition in catalog.buildings)if(definition.prefab==null)CreateMissingPrefab(definition);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        static DeepItemAmount Cost(DeepItemDefinition item,int amount)=>new DeepItemAmount(item,amount);
        static void ConfigureGas(DeepBuildingDefinition definition,DeepGasFacilityMode mode,DeepGasAcceptance acceptance,bool exchange,float rate)
        {
            definition.gasMode=mode;definition.gasAcceptance=acceptance;definition.exchangesRoomGas=exchange;definition.gasTransferMolPerSecond=rate;
            bool outlet=mode==DeepGasFacilityMode.Supply||mode==DeepGasFacilityMode.Exhaust;
            var ports=new List<DeepBuildingPort>();
            if(mode==DeepGasFacilityMode.Storage||outlet)ports.Add(Port("gas_in","进气",DeepPortKind.GasIn,new Vector2(.15f,.5f)));
            if(!outlet)ports.Add(Port("gas_out","出气",DeepPortKind.GasOut,new Vector2(definition.footprint.x-.15f,.5f)));
            if(definition.powerRequired>0)ports.Add(Port("power_in","用电",DeepPortKind.PowerIn,new Vector2(.25f,.15f)));
            definition.ports=ports.ToArray();definition.gasStorageVolume=mode==DeepGasFacilityMode.Storage?8:2;
        }
        static T LoadOrCreate<T>(string path,out bool created) where T:ScriptableObject
        {
            var asset=AssetDatabase.LoadAssetAtPath<T>(path);created=asset==null;
            if(created){asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);}return asset;
        }
        static DeepItemDefinition Item(List<DeepItemDefinition> list,string id,string label,string description,Color tint,Sprite icon)
        {
            var item=list.FirstOrDefault(x=>x!=null&&x.id==id);
            if(item==null)
            {
                item=LoadOrCreate<DeepItemDefinition>(DataRoot+"/Items/"+id+".asset",out bool created);
                if(created){item.id=id;item.displayName=label;item.description=description;item.tint=tint;item.icon=icon;EditorUtility.SetDirty(item);}
                if(!list.Contains(item))list.Add(item);
            }
            return item;
        }
        static DeepBuildingDefinition Building(List<DeepBuildingDefinition> list,string id,string label,string category,DeepBuildingRole role,Vector2Int size,Sprite icon,string tech,float work,bool floor,bool blocking,DeepItemAmount[] costs,string description,float generation=0,float demand=0,int capacity=0)
        {
            var definition=list.FirstOrDefault(x=>x!=null&&x.id==id);
            if(definition==null)
            {
                definition=LoadOrCreate<DeepBuildingDefinition>(DataRoot+"/Buildings/"+id+".asset",out bool created);
                if(created)
                {
                    definition.id=id;definition.displayName=label;definition.category=category;definition.description=description;
                    definition.role=role;definition.footprint=size;definition.icon=icon;definition.requiredTechId=tech??"";
                    definition.workSeconds=work;definition.requiresFloor=floor;definition.blocksMovement=blocking;definition.cost=costs;
                    definition.powerGenerated=generation;definition.powerRequired=demand;definition.storageCapacity=capacity;
                    var ports=new List<DeepBuildingPort>();
                    if(role==DeepBuildingRole.GasTank||role==DeepBuildingRole.GasRegulator||role==DeepBuildingRole.GasSeparator)
                    {
                        ports.Add(Port("gas_in","进气",DeepPortKind.GasIn,new Vector2(0,size.y*.5f)));
                        ports.Add(Port("gas_out","产品",DeepPortKind.GasOut,new Vector2(size.x,size.y*.65f)));
                        if(role==DeepBuildingRole.GasSeparator)ports.Add(Port("tail_out","尾气",DeepPortKind.GasOut,new Vector2(size.x,size.y*.25f)));
                    }
                    if(demand>0)ports.Add(Port("power_in","用电",DeepPortKind.PowerIn,new Vector2(.25f,.15f)));
                    if(generation>0)ports.Add(Port("power_out","供电",DeepPortKind.PowerOut,new Vector2(size.x-.25f,.15f)));
                    definition.ports=ports.ToArray();EditorUtility.SetDirty(definition);
                }
                if(!list.Contains(definition))list.Add(definition);
            }
            return definition;
        }
        static DeepBuildingPort Port(string id,string label,DeepPortKind kind,Vector2 point)=>new DeepBuildingPort{id=id,label=label,kind=kind,localPosition=point};
        static void Tech(List<DeepTechDefinition> list,string id,string label,string description,Sprite icon,float work,string[] parents,DeepItemAmount[] costs,string[] unlocks)
        {
            if(list.Any(x=>x!=null&&x.id==id))return;
            var value=LoadOrCreate<DeepTechDefinition>(DataRoot+"/Technologies/"+id+".asset",out bool created);
            if(created){value.id=id;value.displayName=label;value.description=description;value.icon=icon;value.workSeconds=work;value.prerequisiteIds=parents;value.cost=costs;value.unlockBuildingIds=unlocks;EditorUtility.SetDirty(value);}
            if(!list.Contains(value))list.Add(value);
        }
        static void Recipe(List<DeepRecipeDefinition> list,string id,string label,string description,Sprite icon,DeepItemAmount[] inputs,DeepItemAmount[] outputs,float work,string building,string technology="")
        {
            if(list.Any(x=>x!=null&&x.id==id))return;
            var value=LoadOrCreate<DeepRecipeDefinition>(DataRoot+"/Recipes/"+id+".asset",out bool created);
            if(created){value.id=id;value.displayName=label;value.description=description;value.icon=icon;value.inputs=inputs;value.outputs=outputs;value.workSeconds=work;value.requiredBuildingId=building;value.requiredTechId=technology;EditorUtility.SetDirty(value);}
            if(!list.Contains(value))list.Add(value);
        }

        static Sprite Art(string relative)=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DeepPressure/Art/"+relative);
        static Sprite TerrainIcon(string material)=>AssetDatabase.LoadAllAssetsAtPath("Assets/DeepPressure/Art/TerrainV2/"+material+"_Color.png").OfType<Sprite>().FirstOrDefault(x=>x.name==material+"_v0_m15");
        static Sprite White()=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DeepPressure/Art/Utility/White.png");
        static Material Lit()=>AssetDatabase.LoadAssetAtPath<Material>("Assets/DeepPressure/Materials/IndustrialLit.mat")??AssetDatabase.LoadAssetAtPath<Material>("Assets/DeepPressure/Art/TerrainV2/TerrainHD2D.mat");
        static Material Unlit()=>AssetDatabase.LoadAssetAtPath<Material>("Assets/DeepPressure/Materials/Unlit.mat");
        public static Type BuildingInstanceType()=>TypeCache.GetTypesDerivedFrom<MonoBehaviour>().FirstOrDefault(type=>type.FullName==InstanceTypeName);

        public static void CreateMissingPrefab(DeepBuildingDefinition definition)
        {
            if(definition==null||definition.prefab!=null)return;
            if(string.IsNullOrWhiteSpace(definition.id)||definition.id.Any(character=>!char.IsLetterOrDigit(character)&&character!='_'&&character!='-'))throw new InvalidOperationException("Building IDs used for prefab filenames must contain letters, digits, '_' or '-' only.");
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Prefab authoring is disabled during Play.");
            Type type=BuildingInstanceType();
            if(type==null)throw new InvalidOperationException("DeepBuildingInstance is not compiled yet. Re-run catalog creation after runtime scripts finish importing.");
            string path=PrefabRoot+"/"+definition.id+".prefab";
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(existing!=null){definition.prefab=existing;EditorUtility.SetDirty(definition);return;}
            Scene preview=EditorSceneManager.NewPreviewScene();GameObject root=null;
            try
            {
                root=new GameObject(definition.displayName);root.SetActive(false);SceneManager.MoveGameObjectToScene(root,preview);
                var visual=new GameObject("Visual");visual.transform.SetParent(root.transform,false);
                float width=definition.footprint.x,height=definition.footprint.y;
                Sprite fallback=definition.icon!=null?definition.icon:White();
                var lights=new List<Light2D>();var glows=new List<SpriteRenderer>();
                switch(definition.role)
                {
                    case DeepBuildingRole.Ladder:
                        RectSprite(visual.transform,"Left rail",new Vector2(.23f,.5f),new Vector2(.08f,1),new Color(.56f,.65f,.68f));
                        RectSprite(visual.transform,"Right rail",new Vector2(.77f,.5f),new Vector2(.08f,1),new Color(.56f,.65f,.68f));
                        for(int i=0;i<4;i++)RectSprite(visual.transform,"Rung",new Vector2(.5f,.125f+i*.25f),new Vector2(.57f,.065f),new Color(.69f,.75f,.74f));
                        break;
                    case DeepBuildingRole.Floor:
                        var floor=Sprite(visual.transform,"Structural floor",TerrainIcon("Metal")??fallback,Lit());Fit(floor,new Vector2(.5f,.5f),new Vector2(1,1));
                        break;
                    case DeepBuildingRole.Light:
                        RectSprite(visual.transform,"Fixture",new Vector2(.5f,.68f),new Vector2(.82f,.19f),new Color(.23f,.29f,.32f));
                        var glow=RectSprite(visual.transform,"Emission strip",new Vector2(.5f,.59f),new Vector2(.61f,.065f),new Color(.75f,1,.94f));glow.sharedMaterial=Unlit();glows.Add(glow);
                        var lightObject=new GameObject("Work light");lightObject.transform.SetParent(visual.transform,false);lightObject.transform.localPosition=new Vector3(.5f,.45f,-1);
                        var light=lightObject.AddComponent<Light2D>();light.lightType=Light2D.LightType.Point;light.color=new Color(.60f,.90f,.85f);light.intensity=1.05f;light.pointLightInnerRadius=.35f;light.pointLightOuterRadius=5;
                        var lightData=new SerializedObject(light);lightData.FindProperty("m_NormalMapQuality").intValue=(int)Light2D.NormalMapQuality.Accurate;lightData.ApplyModifiedPropertiesWithoutUndo();lights.Add(light);
                        break;
                    default:
                        var main=Sprite(visual.transform,"Machine",fallback,Lit());Fit(main,new Vector2(width*.5f,height*.5f),new Vector2(width*.92f,height*.94f));
                        if(definition.role==DeepBuildingRole.Fabricator)
                        {
                            var controls=Sprite(visual.transform,"Control console",Art("Props/console_Color.png"),Lit());Fit(controls,new Vector2(width*.76f,height*.46f),new Vector2(width*.36f,height*.58f));controls.sortingOrder=11;
                        }
                        if(definition.role==DeepBuildingRole.Storage)
                        {
                            var second=Sprite(visual.transform,"Stacked bin",Art("Props/crate_Color.png"),Lit());Fit(second,new Vector2(width*.72f,height*.58f),new Vector2(width*.42f,height*.68f));second.sortingOrder=11;
                        }
                        break;
                }
                var instance=root.AddComponent(type);var data=new SerializedObject(instance);
                data.FindProperty("definition").objectReferenceValue=definition;
                var origin=data.FindProperty("origin");if(origin!=null)origin.vector2IntValue=Vector2Int.zero;
                var visualProperty=data.FindProperty("visualRoot");if(visualProperty!=null)visualProperty.objectReferenceValue=visual.transform;
                var constructed=data.FindProperty("isConstructed");if(constructed!=null)constructed.boolValue=true;
                var isOn=data.FindProperty("isOn");if(isOn!=null)isOn.boolValue=true;
                AssignArray(data.FindProperty("lights"),lights.Cast<UnityEngine.Object>().ToArray());AssignArray(data.FindProperty("glowRenderers"),glows.Cast<UnityEngine.Object>().ToArray());data.ApplyModifiedPropertiesWithoutUndo();
                if(definition.role==DeepBuildingRole.GasTank||definition.role==DeepBuildingRole.GasRegulator||definition.role==DeepBuildingRole.GasSeparator||definition.role==DeepBuildingRole.Vent||definition.role==DeepBuildingRole.GasPump)
                {
                    var node=root.AddComponent<GasNode>();node.displayName=definition.displayName;node.kind=definition.role==DeepBuildingRole.GasRegulator?GasNodeKind.Regulator:definition.role==DeepBuildingRole.GasSeparator?GasNodeKind.Separator:GasNodeKind.Storage;
                    node.volumeM3=definition.role==DeepBuildingRole.GasTank?definition.gasStorageVolume:2;node.initialPressureKPa=0;node.maxPressureKPa=600;node.targetPressureKPa=160;
                    var placement=root.AddComponent<DeepDevicePlacement>();placement.footprintOffset=Vector2Int.zero;placement.footprintSize=definition.footprint;
                    foreach(var port in definition.ports)
                    {if(port.kind==DeepPortKind.GasIn)placement.inletOffset=port.localPosition;else if(port.id=="tail_out")placement.tailOffset=port.localPosition;else if(port.kind==DeepPortKind.GasOut)placement.productOffset=port.localPosition;}
                }
                root.SetActive(true);
                definition.prefab=PrefabUtility.SaveAsPrefabAsset(root,path);EditorUtility.SetDirty(definition);
            }
            finally{if(root!=null)UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);}
        }
        static void AssignArray(SerializedProperty property,UnityEngine.Object[] values)
        {if(property==null)return;property.arraySize=values.Length;for(int i=0;i<values.Length;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
        static SpriteRenderer Sprite(Transform parent,string name,Sprite sprite,Material material)
        {var child=new GameObject(name);child.transform.SetParent(parent,false);var renderer=child.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sharedMaterial=material;renderer.sortingOrder=10;return renderer;}
        static SpriteRenderer RectSprite(Transform parent,string name,Vector2 center,Vector2 size,Color color)
        {var renderer=Sprite(parent,name,White(),Lit());renderer.color=color;Fit(renderer,center,size);return renderer;}
        static void Fit(SpriteRenderer renderer,Vector2 center,Vector2 size)
        {
            if(renderer.sprite==null)return;Bounds bounds=renderer.sprite.bounds;
            float scale=Mathf.Min(size.x/Mathf.Max(.001f,bounds.size.x),size.y/Mathf.Max(.001f,bounds.size.y));
            renderer.transform.localScale=new Vector3(scale,scale,1);renderer.transform.localPosition=new Vector3(center.x-bounds.center.x*scale,center.y-bounds.center.y*scale,0);
            // Geometry strips intentionally fill their authored rectangle; illustrated props keep aspect ratio.
            if(renderer.sprite==White())renderer.transform.localScale=new Vector3(size.x/Mathf.Max(.001f,bounds.size.x),size.y/Mathf.Max(.001f,bounds.size.y),1);
        }
    }
}

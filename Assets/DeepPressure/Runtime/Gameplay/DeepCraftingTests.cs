using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepPressure
{
    public static class DeepCraftingTests
    {
        public static string RunAll()
        {
            ExplicitStationAndQueue(); DisabledAndRemovedOutput(); BoundProductionSave();
            return "Station crafting: 3 groups passed. Explicit compatible constructed/operational equipment, physical delivery and arrival, fixed-machine FIFO, shutdown blocking, removal refunds, and station-bound automatic targets/save restoration.";
        }
        static void ExplicitStationAndQueue()
        {
            using(var f=new Fixture())
            {
                var near=f.Station(f.factory,new Vector2Int(6,1));var selected=f.Station(f.factory,new Vector2Int(15,1));
                Assert(!f.session.RequestCraftAt(null,f.recipe,1,out _),"Crafting requires an explicit real machine.");
                Assert(!f.session.RequestCraftAt(f.storage,f.recipe,1,out _),"A warehouse cannot impersonate the required fabrication device.");
                selected.isConstructed=false;Assert(!f.session.RequestCraftAt(selected,f.recipe,1,out _),"A construction plan cannot produce items.");selected.isConstructed=true;
                selected.isOn=false;Assert(!f.session.RequestCraftAt(selected,f.recipe,1,out _),"A powered-off machine cannot accept production as operational.");selected.isOn=true;
                Assert(f.session.RequestCraftAt(selected,f.recipe,1,out string reason),reason);var first=f.session.Orders[0];
                Assert(f.session.RequestCraftAt(selected,f.recipe,1,out reason),reason);var second=f.session.Orders[1];second.priority=9;
                var queue=f.session.CraftQueueFor(selected);Assert(queue.Count==2&&queue[0]==first&&queue[1]==second,"The machine queue exposes stable FIFO ordering.");
                f.session.Tick(.05f);Assert(first.targetBuilding==selected&&first.completedSeconds==0&&f.session.inventory.GetAmount(f.alloy)==0,"Selecting a distant machine does not fabricate remotely or switch to the nearer machine.");
                f.Wait(()=>first.completedSeconds>0,15,"Worker must reach and start at the selected device");
                Assert(first.targetBuilding==selected&&second.completedSeconds==0&&second.worker==null,"Higher global priority cannot overtake an older batch on the same device.");
                selected.isOn=false;float progress=first.completedSeconds;f.Run(2);
                Assert(first.targetBuilding==selected&&Mathf.Approximately(first.completedSeconds,progress)&&near.IsOperational,"A stopped machine blocks its own batch rather than switching machines.");
                selected.isOn=true;f.Run(15);
                Assert(first.state==DeepWorkState.Completed&&second.state==DeepWorkState.Completed&&f.session.inventory.GetAmount(f.alloy)==2,"Restarting completes the two reserved batches exactly once.");
                Assert(f.session.CraftQueueFor(selected).Count==0,"Completed batches leave the active device queue.");
            }
        }
        static void DisabledAndRemovedOutput()
        {
            using(var f=new Fixture())
            {
                var selected=f.Station(f.factory,new Vector2Int(6,1));f.Station(f.factory,new Vector2Int(13,1));
                f.recipe.outputs=new[]{new DeepItemAmount(f.alloy,5)};f.session.inventory.SetCapacity(40);
                Assert(f.session.RequestCraftAt(selected,f.recipe,1,out string reason),reason);var order=f.session.Orders[0];f.Run(12);
                Assert(order.Progress==1&&order.state==DeepWorkState.Blocked&&f.session.inventory.GetAmount(f.alloy)==0,"A full warehouse holds the completed batch at its real machine.");
                selected.isOn=false;Assert(f.session.inventory.TryConsume(f.ore,6),"Fixture frees output space.");f.Run(2);
                Assert(order.state==DeepWorkState.Blocked&&f.session.inventory.GetAmount(f.alloy)==0,"A completed blocked batch cannot bypass a shut-down machine during automatic retry.");
                int held=f.session.inventory.GetAmount(f.ore);f.session.Buildings.Remove(selected);UnityEngine.Object.DestroyImmediate(selected.gameObject);f.session.RebuildOccupancy();f.session.Tick(.1f);
                Assert(order.state==DeepWorkState.Cancelled&&f.session.inventory.GetAmount(f.ore)==held+2&&f.session.inventory.GetAmount(f.alloy)==0,"Removing the bound machine cancels and refunds the batch without teleporting it to another device.");
                f.Run(1);Assert(f.session.inventory.GetAmount(f.ore)==held+2,"Removal refunds only once.");
            }
        }
        static void BoundProductionSave()
        {
            using(var f=new Fixture())
            {
                var a=f.Station(f.factory,new Vector2Int(6,1));var b=f.Station(f.factory,new Vector2Int(14,1));
                Assert(f.session.SetProductionTargetAt(a,f.recipe,2,false,out string reason),reason);
                Assert(f.session.SetProductionTargetAt(b,f.recipe,2,true,out reason),reason);f.session.Tick(.1f);
                Assert(f.session.Orders.Count==1&&f.session.Orders[0].targetBuilding==b,"An inventory target queues work only at its configured machine.");
                var saved=JsonUtility.FromJson<DeepSaveData>(JsonUtility.ToJson(f.session.CaptureSaveState()));
                Assert(f.session.ValidateSaveState(saved,out reason),reason);
                f.session.Orders[0].targetBuilding=a;f.session.SetProductionTargetAt(b,f.recipe,99,false,out _);
                Assert(f.session.TryRestoreSaveState(saved,out reason),reason);
                Assert(f.session.Orders[0].targetBuilding==b&&f.session.ProductionTargetForAt(b,f.recipe.id).enabled&&f.session.ProductionTargetForAt(b,f.recipe.id).targetAmount==2&&!f.session.ProductionTargetForAt(a,f.recipe.id).enabled,"Orders and per-machine targets preserve their physical machine identities through load.");
                b.isOn=false;f.Run(3);Assert(f.session.Orders[0].targetBuilding==b&&f.session.inventory.GetAmount(f.alloy)==0,"A loaded target cannot fall back to an operational sibling machine.");
                var invalid=JsonUtility.FromJson<DeepSaveData>(JsonUtility.ToJson(saved));invalid.orders[0].targetBuildingId=null;
                Assert(!f.session.ValidateSaveState(invalid,out _),"Active manufacturing without a physical machine is rejected during save preflight.");
            }
        }
        sealed class Fixture:IDisposable
        {
            readonly GameObject root;readonly List<UnityEngine.Object> temporary=new List<UnityEngine.Object>();
            public readonly DeepPressureWorld world;public readonly DeepGameSession session;public readonly DeepWorker worker;
            public readonly DeepItemDefinition ore,alloy;public readonly DeepBuildingDefinition factory;public readonly DeepRecipeDefinition recipe;public readonly DeepBuildingInstance storage;
            public Fixture()
            {
                root=new GameObject("Station crafting regression");root.SetActive(false);
                world=root.AddComponent<DeepPressureWorld>();world.width=22;world.height=6;world.terrainKinds=new TerrainKind[132];
                for(int x=0;x<22;x++)world.SetTerrain(x,0,TerrainKind.Basalt);world.RebuildRooms();
                var person=new GameObject("Craft worker");person.transform.SetParent(root.transform);person.transform.position=new Vector3(1.5f,1,0);worker=person.AddComponent<DeepWorker>();worker.moveCellsPerSecond=5;
                session=root.AddComponent<DeepGameSession>();session.world=world;session.baseStorageCapacity=100;session.useWiredPower=false;
                ore=Asset<DeepItemDefinition>();ore.id="ore";alloy=Asset<DeepItemDefinition>();alloy.id="alloy";
                session.catalog=Asset<DeepGameplayCatalog>();session.catalog.items=new[]{ore,alloy};session.startingInventory=new[]{new DeepItemAmount(ore,40)};
                var depot=Definition("warehouse",DeepBuildingRole.Storage);factory=Definition("factory",DeepBuildingRole.Fabricator);
                session.catalog.buildings=new[]{depot,factory};recipe=Asset<DeepRecipeDefinition>();recipe.id=recipe.displayName="alloy_recipe";recipe.requiredBuildingId=factory.id;
                recipe.inputs=new[]{new DeepItemAmount(ore,2)};recipe.outputs=new[]{new DeepItemAmount(alloy,1)};recipe.workSeconds=.8f;session.catalog.recipes=new[]{recipe};
                root.SetActive(true);session.InitializeSession();storage=Station(depot,new Vector2Int(3,1));
            }
            T Asset<T>() where T:ScriptableObject {var result=ScriptableObject.CreateInstance<T>();temporary.Add(result);return result;}
            DeepBuildingDefinition Definition(string id,DeepBuildingRole role)
            {
                var definition=Asset<DeepBuildingDefinition>();definition.id=definition.displayName=id;definition.role=role;definition.blocksMovement=false;definition.footprint=new Vector2Int(2,2);
                var prefab=new GameObject("Fixture prefab "+id);prefab.SetActive(false);prefab.AddComponent<DeepBuildingInstance>();temporary.Add(prefab);definition.prefab=prefab;return definition;
            }
            public DeepBuildingInstance Station(DeepBuildingDefinition definition,Vector2Int origin)
            {
                var go=new GameObject(definition.id);go.transform.SetParent(root.transform);go.transform.position=session.BuildingPosition(origin);
                var building=go.AddComponent<DeepBuildingInstance>();building.definition=definition;building.origin=origin;building.session=session;session.Buildings.Add(building);session.RebuildOccupancy();return building;
            }
            public void Run(float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.05f);i++)session.Tick(.05f);}
            public void Wait(Func<bool> predicate,float seconds,string message){for(int i=0;i<Mathf.CeilToInt(seconds/.05f)&&!predicate();i++)session.Tick(.05f);Assert(predicate(),message);}
            public void Dispose(){UnityEngine.Object.DestroyImmediate(root);foreach(var item in temporary)if(item!=null)UnityEngine.Object.DestroyImmediate(item);}
        }
        static void Assert(bool condition,string message){if(!condition)throw new InvalidOperationException("Station crafting: "+message);}
    }
}

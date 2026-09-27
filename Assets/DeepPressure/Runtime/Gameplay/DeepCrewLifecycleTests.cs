using System;
using UnityEngine;
namespace DeepPressure
{
    public static class DeepCrewLifecycleTests
    {
        public static string RunAll()
        {
            var root=new GameObject("Crew lifecycle verification");root.SetActive(false);
            var catalog=ScriptableObject.CreateInstance<DeepGameplayCatalog>();
            var podDef=ScriptableObject.CreateInstance<DeepBuildingDefinition>();
            var ore=ScriptableObject.CreateInstance<DeepItemDefinition>();ore.id="ore";catalog.items=new[]{ore};
            try
            {
                var world=root.AddComponent<DeepPressureWorld>();world.width=16;world.height=5;world.terrainKinds=new TerrainKind[80];
                for(int x=0;x<16;x++)world.SetTerrain(x,0,TerrainKind.Basalt);
                world.defaultPressureKPa=100;world.defaultComposition=new Vector4(.21f,.79f,0,0);world.RebuildRooms();
                var session=root.AddComponent<DeepGameSession>();session.world=world;session.catalog=catalog;session.lifeSupportEnabled=true;session.hazardsEnabled=false;
                var go=new GameObject("Initial worker");go.transform.SetParent(root.transform);var worker=go.AddComponent<DeepWorker>();worker.session=session;worker.TeleportToCell(new Vector2Int(1,1));
                var podGo=new GameObject("Printing pod");podGo.transform.SetParent(root.transform);var pod=podGo.AddComponent<DeepBuildingInstance>();
                podDef.id="printing_pod";podDef.footprint=new Vector2Int(3,3);podDef.blocksMovement=false;podDef.prefab=podGo;pod.definition=podDef;pod.origin=new Vector2Int(5,1);catalog.buildings=new[]{podDef};
                root.SetActive(true);session.InitializeSession();session.CaptureInitialState();
                session.Tick(.2f);Assert(Mathf.Abs(session.OxygenDemandRate-.25f)<.0001f,"One living worker demands configured oxygen.");
                Assert(!session.TryPrintWorker(0,out _),"Pod requires its initial cooldown.");
                session.lifeSupportEnabled=false;session.Tick(180);Assert(session.PrintingReady,"Pod becomes ready by simulation time.");
                Assert(session.TryPrintWorker(1,out string reason),reason);Assert(session.AliveWorkerCount==2,"Printing explicitly adds one worker.");
                Assert(!session.TryPrintWorker(0,out _),"Selecting one candidate consumes the offer.");
                session.lifeSupportEnabled=true;session.Tick(.2f);Assert(Mathf.Abs(session.OxygenDemandRate-.5f)<.0001f,"Recruitment doubles real demand.");
                var saved=session.CaptureSaveState();Assert(session.ValidateSaveState(saved,out reason),reason);
                Assert(session.NewGame(out reason),reason);Assert(session.Workers.Count==1,"New game removes later recruits.");
                Assert(session.TryRestoreSaveState(saved,out reason),reason);Assert(session.Workers.Count==2&&session.AliveWorkerCount==2,"Load reconstructs missing recruits.");
                worker=session.Workers[0];Assert(session.RequestMove(worker,new Vector2Int(3,1),out reason),reason);
                var order=worker.currentOrder;session.DamageWorker(worker,100,"测试事故");Assert(!worker.IsAlive&&order.IsTerminal&&worker.currentOrder==null,"Death permanently stops and releases assigned work.");
                Assert(!session.RequestMove(worker,new Vector2Int(2,1),out _),"Dead workers cannot be commanded.");
                var living=session.Workers[1];living.airReserveSeconds=0;living.health=1;
                session.Atmosphere.Take(living.Cell,session.Atmosphere.Sample(living.Cell).Total);session.Tick(.3f);
                Assert(!living.IsAlive&&living.deathCause=="窒息"&&session.IsColonyLost,"Unprotected oxygen deprivation causes permanent death.");
                Assert(session.OxygenDemandRate==0,"Dead workers stop oxygen consumption.");
                var deadSave=session.CaptureSaveState();Assert(session.TryRestoreSaveState(deadSave,out reason),reason);Assert(session.DeadWorkerCount==2,"Death persists across save/load.");
                return "Crew lifecycle: respiration scales with population; timed optional printing; dynamic population save/new-game restoration; death releases work; suffocation and death persistence passed.";
            }
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(catalog);UnityEngine.Object.DestroyImmediate(podDef);UnityEngine.Object.DestroyImmediate(ore);}
        }
        static void Assert(bool result,string message){if(!result)throw new InvalidOperationException("Crew lifecycle: "+message);}
    }
}

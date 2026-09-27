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
                var visual=new GameObject("Worker sprite");visual.transform.SetParent(go.transform,false);worker.visualRenderer=visual.AddComponent<SpriteRenderer>();
                var presentation=go.AddComponent<DeepWorkerPresentation>();presentation.anchorsBaked=true;presentation.groundedFootLocalPosition=new Vector3(0,.03f,0);
                var podGo=new GameObject("Printing pod");podGo.transform.SetParent(root.transform);var pod=podGo.AddComponent<DeepBuildingInstance>();
                podDef.id="printing_pod";podDef.footprint=new Vector2Int(3,3);podDef.blocksMovement=false;podDef.prefab=podGo;pod.definition=podDef;pod.origin=new Vector2Int(5,1);catalog.buildings=new[]{podDef};
                root.SetActive(true);session.InitializeSession();session.CaptureInitialState();
                session.Tick(.2f);Assert(Mathf.Abs(session.OxygenDemandRate-session.breathingMolPerSecond)<.0001f,"One living worker demands configured oxygen.");
                Assert(!session.TryPrintWorker(0,out _),"Pod requires its initial cooldown.");
                session.lifeSupportEnabled=false;session.Tick(180);Assert(session.PrintingReady,"Pod becomes ready by simulation time.");
                Assert(session.TryPrintWorker(1,out string reason),reason);Assert(session.AliveWorkerCount==2,"Printing explicitly adds one worker.");
                foreach(var person in session.Workers)person.GetComponent<DeepWorkerPresentation>().RefreshStableSorting();
                var firstSorting=session.Workers[0].GetComponent<UnityEngine.Rendering.SortingGroup>();
                var secondSorting=session.Workers[1].GetComponent<UnityEngine.Rendering.SortingGroup>();
                Assert(firstSorting!=null&&secondSorting!=null&&firstSorting.sortingOrder!=secondSorting.sortingOrder,"Each complete worker has a unique order inside the shared crew sorting group.");
                Vector3 firstPosition=session.Workers[0].transform.position;int originalOrder=firstSorting.sortingOrder;
                for(int repeat=0;repeat<30;repeat++)foreach(var person in session.Workers)person.GetComponent<DeepWorkerPresentation>().RefreshStableSorting();
                Assert(firstSorting.sortingOrder==originalOrder&&session.Workers[0].transform.position==firstPosition,"Repeated sorting is stable and never offsets a worker's physical position.");
                Assert(session.Workers[0].transform.parent==session.Workers[1].transform.parent&&session.Workers[0].transform.parent.GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder==25,"Crew grouping preserves world order below work particles and pipe overlays.");
                Assert(!session.TryPrintWorker(0,out _),"Selecting one candidate consumes the offer.");
                session.lifeSupportEnabled=true;session.Tick(.2f);Assert(Mathf.Abs(session.OxygenDemandRate-2*session.breathingMolPerSecond)<.0001f,"Recruitment doubles real demand.");
                var saved=session.CaptureSaveState();Assert(session.ValidateSaveState(saved,out reason),reason);
                Assert(session.NewGame(out reason),reason);Assert(session.Workers.Count==1,"New game removes later recruits.");
                Assert(session.TryRestoreSaveState(saved,out reason),reason);Assert(session.Workers.Count==2&&session.AliveWorkerCount==2,"Load reconstructs missing recruits.");
                worker=session.Workers[0];Assert(session.RequestMove(worker,new Vector2Int(3,1),out reason),reason);
                var order=worker.currentOrder;session.DamageWorker(worker,100,"测试事故");Assert(!worker.IsAlive&&order.IsTerminal&&worker.currentOrder==null,"Death permanently stops and releases assigned work.");
                Assert(!session.RequestMove(worker,new Vector2Int(2,1),out _),"Dead workers cannot be commanded.");
                var living=session.Workers[1];living.airReserveSeconds=0;living.health=1;
                var breathing=session.BreathingCell(living.Cell);session.Atmosphere.Take(breathing,session.Atmosphere.Sample(breathing).Total);session.Tick(.3f);
                Assert(!living.IsAlive&&living.deathCause=="窒息"&&session.IsColonyLost,"Unprotected oxygen deprivation causes permanent death.");
                Assert(session.OxygenDemandRate==0,"Dead workers stop oxygen consumption.");
                var deadSave=session.CaptureSaveState();Assert(session.TryRestoreSaveState(deadSave,out reason),reason);Assert(session.DeadWorkerCount==2,"Death persists across save/load.");
                // Mimic the corpse pose already applied by LateUpdate, then print while paused.
                foreach(var corpse in session.Workers)corpse.visualRenderer.transform.localRotation=Quaternion.Euler(0,0,78);
                session.lifeSupportEnabled=false;session.Tick(180);session.paused=true;
                Assert(session.TryPrintWorker(0,out reason),reason);
                var arrival=session.Workers[session.Workers.Count-1];
                Assert(arrival.IsAlive&&Quaternion.Angle(arrival.visualRenderer.transform.localRotation,Quaternion.identity)<.01f,"An arrival cloned from a corpse is upright immediately, even before its first animation frame while paused.");
                Assert(arrival.currentOrder==null&&!arrival.automationPaused&&arrival.CapturePath().Length==0,"Arrival has no inherited corpse work or path state.");
                Assert(Quaternion.Angle(session.Workers[0].visualRenderer.transform.localRotation,Quaternion.identity)>70,"Resetting the new arrival does not rotate the original corpse.");
                foreach(var person in session.Workers)person.GetComponent<DeepWorkerPresentation>().RefreshStableSorting();
                Assert(arrival.GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder>session.Workers[0].GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder,"Living arrivals render consistently above corpses without interleaving their sprite parts.");
                return "Crew lifecycle: respiration scales with population; timed optional printing; dynamic population save/new-game restoration; death releases work; suffocation and death persistence; upright arrivals from corpse templates while paused; stable whole-character sorting above corpses passed.";
            }
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(catalog);UnityEngine.Object.DestroyImmediate(podDef);UnityEngine.Object.DestroyImmediate(ore);}
        }
        static void Assert(bool result,string message){if(!result)throw new InvalidOperationException("Crew lifecycle: "+message);}
    }
}

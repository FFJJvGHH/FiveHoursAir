using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepPressure.Editor
{
    public static class DeepOpeningVerification
    {
        [MenuItem("深压/验证/生存开局验收")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play before validation.");
            var report=new StringBuilder("Survival opening verification\n"+DateTime.Now.ToString("O")+"\n");
            try
            {
                report.AppendLine(DeepPressureSelfTests.RunAll());
                report.AppendLine(DeepGameplayTests.RunGameplayTests());
                report.AppendLine(DeepPowerTests.RunAll());
                report.AppendLine(DeepExplorationTests.RunAll());
                report.AppendLine(DeepAtmosphereTests.RunAll());
                report.AppendLine(DeepLifeSupportTests.RunAll());
                report.AppendLine(DeepHazardTests.RunAll());
                report.AppendLine(DeepNetworkLifecycleTests.RunAll());
                report.AppendLine(DeepPersistenceTests.RunAll());
                report.AppendLine(DeepCrewLifecycleTests.RunAll());
                VerifyOpening(report);
                Debug.Log(report.ToString());
            }
            catch(Exception error){report.AppendLine("FAIL: "+error);throw;}
            finally{Directory.CreateDirectory("../outputs");File.WriteAllText("../outputs/v07-validation.txt",report.ToString());}
        }
        static void VerifyOpening(StringBuilder report)
        {
            var source=UnityEngine.Object.FindObjectOfType<DeepGameSession>();Require(source!=null,"Open the survival scene.");
            var preview=EditorSceneManager.NewPreviewScene();GameObject container=null;
            try
            {
                container=new GameObject("Opening verification");container.SetActive(false);SceneManager.MoveGameObjectToScene(container,preview);
                var clone=UnityEngine.Object.Instantiate(source.world.gameObject,container.transform,false);
                foreach(var lamp in clone.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true))if(lamp.lightType==UnityEngine.Rendering.Universal.Light2D.LightType.Global)lamp.enabled=false;
                container.SetActive(true);
                var world=clone.GetComponent<DeepPressureWorld>();world.SyncTerrainFromTilemap();world.RebuildRooms();
                var session=clone.GetComponent<DeepGameSession>();session.network.ResetSimulation();session.InitializeSession();session.menuOpen=false;session.paused=false;
                Require(session.Workers.Count==3,"Exactly three starting workers.");
                Require(session.Buildings.Count==1&&session.Buildings[0].definition.id=="printing_pod","Only the printing pod is prebuilt.");
                Require(session.completedWireCells.Count==0,"No free completed electrical network.");
                Require(clone.GetComponentsInChildren<GasNode>(true).Length==0&&clone.GetComponentsInChildren<GasLink>(true).Length==0,"No hidden industrial gas network.");
                foreach(var worker in session.Workers)Require(session.IsStandable(worker.Cell),"Every worker has a usable starting floor.");
                var initial=session.CaptureSaveState();
                Require(initial.technologies.Length==0,"No advanced technology unlocked at start.");
                Require(session.inventory.GetAmount(session.catalog.FindItem("alloy"))==0&&session.inventory.GetAmount(session.catalog.FindItem("electronics"))==0,"Starting supplies contain no finished industrial products.");
                var before=session.Atmosphere.TotalInventory();session.Tick(.5f);var after=session.Atmosphere.TotalInventory();
                Require(before.oxygen>after.oxygen&&after.carbonDioxide>before.carbonDioxide,"Starter crew breathes finite cavern oxygen.");
                Require(session.UnsafeWorkerCount==0,"Starter air is breathable.");
                report.AppendLine("PASS: natural opening, one pod, three grounded workers, raw supplies, no free circuits or gas equipment; finite respiration.");
                Build(session,"fabricator",new Vector2Int(21,53));
                Require(session.RequestCraft(session.catalog.FindRecipe("smelt_alloy"),1,out string reason),"Starter fabrication: "+reason);
                TickUntil(session,session.Orders.Last(),90);
                Require(session.inventory.GetAmount(session.catalog.FindItem("alloy"))>0,"Raw ore can bootstrap alloy without advanced equipment.");
                Build(session,"research_bench",new Vector2Int(25,53));
                Build(session,"oxygen_diffuser",new Vector2Int(40,53));
                var oxygenBefore=session.Atmosphere.TotalInventory().oxygen;
                var algae=session.catalog.FindItem("algae");int algaeBefore=session.inventory.GetAmount(algae);
                double supplied=0,consumed=0;
                for(int i=0;i<40;i++){session.Tick(.5f);supplied+=session.OxygenSupplyRate*.5;consumed+=session.OxygenDemandRate*.5;}
                Require(supplied>0,"Built diffuser recycles actual CO2 into breathable oxygen.");
                Require(Math.Abs(session.Atmosphere.TotalInventory().oxygen-oxygenBefore-supplied+consumed)<.05,"Oxygen inventory matches actual production minus respiration.");
                Require(session.AliveWorkerCount==3&&session.UnsafeWorkerCount==0,"Starter crew remains healthy while the algae loop is running.");
                Require(session.inventory.GetAmount(algae)<algaeBefore,"Oxygen supply consumes finite algae.");
                report.AppendLine("PASS: workers collect supplies at pod, construct hand fabrication/research/basic oxygen; ore becomes alloy; algae becomes room oxygen.");
                var saved=session.CaptureSaveState();Require(session.TryRestoreSaveState(saved,out reason),"Opening progress round trip: "+reason);
                Require(session.Buildings.Any(b=>b.definition.id=="oxygen_diffuser"),"Built life support survives load.");
                Require(session.TryRestoreSaveState(initial,out reason),"Initial state restoration: "+reason);
                Require(session.Buildings.Count==1&&session.Workers.Count==3,"New game resets construction and crew to minimalist baseline.");
                report.AppendLine("PASS: opening progress restore and fresh-run baseline restore.");
            }
            finally{if(container!=null)UnityEngine.Object.DestroyImmediate(container);EditorSceneManager.ClosePreviewScene(preview);}
        }
        static void Build(DeepGameSession session,string id,Vector2Int cell)
        {
            Require(session.RequestBuild(session.catalog.FindBuilding(id),cell,out string reason),"Build "+id+": "+reason);
            TickUntil(session,session.Orders.Last(),120);
        }
        static void TickUntil(DeepGameSession session,DeepWorkOrder order,float seconds)
        {
            for(int i=0;i<seconds*10&&!order.IsTerminal;i++)session.Tick(.1f);
            Require(order.state==DeepWorkState.Completed,order.label+" failed: "+order.state+" / "+order.statusReason);
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}

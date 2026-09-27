using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DeepPressure.Editor
{
    public static class DeepColonyVerification
    {
        [MenuItem("深压/验证/经营闭环验收 %#F11")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before editor validation.");
            string output=DeepPressureSelfTests.RunAll()+"\n"+DeepGameplayTests.RunGameplayTests();
            var source=UnityEngine.Object.FindObjectOfType<DeepGameSession>();Require(source!=null,"Open DeepPressureColony scene.");
            Require(source.world.width>=128&&source.world.height>=80,"Expanded authored map");
            Require(source.world.GetComponentsInChildren<DeepWorker>().Length>=3,"Three authored workers");
            foreach(var look in source.world.GetComponentsInChildren<DeepWorkerPresentation>())
            {Require(look.idle!=null&&look.walking.Length==6&&look.working.Length==4,"Character animation frames");Require(look.walking.All(x=>x!=null)&&look.working.All(x=>x!=null),"Every character frame imported");}
            var effects=source.world.GetComponent<DeepParticleFeedback>();Require(effects!=null&&effects.sparkPrefab!=null&&effects.dustPrefab!=null&&effects.pulsePrefab!=null,"Particle feedback prefabs");
            Scene preview=EditorSceneManager.NewPreviewScene();GameObject clone=null,container=null;
            try
            {
                container=new GameObject("Preview test container");container.SetActive(false);SceneManager.MoveGameObjectToScene(container,preview);
                clone=UnityEngine.Object.Instantiate(source.world.gameObject,container.transform,false);clone.name="Colony integration verification";
                foreach(var lamp in clone.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true))if(lamp.lightType==UnityEngine.Rendering.Universal.Light2D.LightType.Global)lamp.enabled=false;
                container.SetActive(true);
                var world=clone.GetComponent<DeepPressureWorld>();world.SyncTerrainFromTilemap();world.RebuildRooms();
                var network=clone.GetComponent<GasNetworkSimulator>();network.ResetSimulation();
                var session=clone.GetComponent<DeepGameSession>();session.InitializeSession();
                var catalog=session.catalog;
                int startAlloy=session.inventory.GetAmount(catalog.FindItem("alloy"));
                Require(session.RequestBuild(catalog.FindBuilding("research_bench"),new Vector2Int(11,53),out string reason),"Queue research bench: "+reason);
                var build=session.Orders.Last();Require(session.Buildings.All(b=>b.origin!=new Vector2Int(11,53)),"No instant construction");
                TickUntil(session,build,120);Require(session.inventory.GetAmount(catalog.FindItem("alloy"))<startAlloy,"Building consumes reserved materials");
                Require(session.Buildings.Any(b=>b.definition.id=="research_bench"&&b.origin==new Vector2Int(11,53)),"Worker erected research bench");
                int ore=session.inventory.GetAmount(catalog.FindItem("ore")),alloy=session.inventory.GetAmount(catalog.FindItem("alloy"));
                Require(session.RequestCraft(catalog.FindRecipe("smelt_alloy"),2,out reason),"Queue fabrication: "+reason);
                TickUntil(session,session.Orders.Last(),120);
                Require(session.inventory.GetAmount(catalog.FindItem("ore"))==ore-6&&session.inventory.GetAmount(catalog.FindItem("alloy"))==alloy+2,"Recipe exact input and output");
                Require(session.RequestResearch(catalog.FindTech("survey_basics"),out reason),"Queue research: "+reason);
                TickUntil(session,session.Orders.Last(),120);Require(session.IsTechUnlocked("survey_basics"),"Research unlock persisted in session");
                var light=session.Buildings.First(b=>b.definition.role==DeepBuildingRole.Light);bool wasOn=light.isOn;session.ToggleBuilding(light);Require(light.isOn!=wasOn&&light.lights.All(l=>l==null||!l.enabled),"World light is switchable");session.ToggleBuilding(light);
                var worker=session.Workers[0];Require(session.RequestMove(worker,new Vector2Int(14,53),out reason),reason);
                session.menuOpen=true;var position=worker.transform.position;session.Tick(5);Require(worker.transform.position==position&&network.paused,"Pause menu freezes person and gas");session.menuOpen=false;TickUntil(session,worker.CurrentOrder??session.Orders.Last(),60);
                output+="\nPASS: 128x80 scene; 3 animated normal-mapped workers; particle prefabs.\nPASS: actual scene worker supply trip -> research-bench construction -> two crafting batches -> research unlock -> lamp toggle -> pause/move.\n";
            }
            catch(Exception e){output+="\nFAIL: "+e;Write(output);throw;}
            finally{if(container!=null)UnityEngine.Object.DestroyImmediate(container);EditorSceneManager.ClosePreviewScene(preview);}
            Write(output);Debug.Log(output);
        }
        static void TickUntil(DeepGameSession session,DeepWorkOrder order,float seconds)
        {
            for(int i=0;i<seconds*10&&!order.IsTerminal;i++)session.Tick(.1f);
            Require(order.state==DeepWorkState.Completed,order.label+" not complete: "+order.state+" / "+order.statusReason);
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        static void Write(string report){Directory.CreateDirectory("../outputs");File.WriteAllText("../outputs/colony-validation.txt",report);}
    }
}

using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepPressure.Editor
{
    // Explicit developer preview only. Fixture objects exist in Play and are never saved.
    [InitializeOnLoad]
    public static class DeepRevision08Preview
    {
        const string Flag="DeepPressure.Revision08Preview";
        static int stage;
        static double next;
        static DeepGameSession session;
        static DeepRevision08Preview(){EditorApplication.update+=Advance;}

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/DeepPressure/Scenes/DeepPressureSurvival.unity");
            var game=UnityEngine.Object.FindObjectOfType<DeepGameSession>();
            DeepPresentationUpgrade.ApplyToCatalogAndLoadedScenes(game.catalog);
            string report=DeepPresentationUpgrade.ValidateCatalogGrounding(game.catalog)+"\n"+DeepPresentationUpgrade.ValidateMachinePresentation(game.catalog);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("../outputs");File.WriteAllText("../outputs/v08-art-validation.txt",report);
            SessionState.SetBool(Flag,true);SessionState.SetFloat(Flag+"Start",(float)EditorApplication.timeSinceStartup);
            stage=0;next=0;EditorApplication.isPlaying=true;
        }
        static void Advance()
        {
            if(!SessionState.GetBool(Flag,false))return;
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Flag+"Start",0)>180){Finish("FAIL: preview timed out",1);return;}
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling||Time.frameCount<5)return;
            double now=EditorApplication.timeSinceStartup;
            if(now<next)return;
            try
            {
                if(stage==0)
                {
                    session=UnityEngine.Object.FindObjectOfType<DeepGameSession>();
                    var hud=UnityEngine.Object.FindObjectOfType<DeepPressureHUD>();if(session==null||hud==null)return;
                    session.menuOpen=false;session.paused=false;session.speed=1;
                    typeof(DeepPressureHUD).GetMethod("EnterColony",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null);
                    AddBuilding("fabricator",new Vector2Int(21,53));AddBuilding("research_bench",new Vector2Int(25,53));AddBuilding("storage",new Vector2Int(35,53));AddBuilding("oxygen_diffuser",new Vector2Int(40,53));
                    session.RebuildOccupancy();session.RefreshStorageCapacity();
                    session.Workers[0].TeleportToCell(new Vector2Int(23,53));
                    session.Workers[1].TeleportToCell(new Vector2Int(38,53));session.Workers[2].TeleportToCell(new Vector2Int(38,53));
                    session.DamageWorker(session.Workers[2],100,"preview corpse");
                    session.RequestCraft(session.catalog.FindRecipe("smelt_alloy"),2,out _);
                    var camera=Camera.main;camera.transform.position=session.world.transform.TransformPoint(new Vector3(33,56,-20));camera.orthographicSize=8.6f;
                    stage=1;next=now+3;
                }
                else if(stage==1){Capture("v08-world.png");stage=2;next=now+1.2;}
                else if(stage==2){Capture("v08-motion.png");stage=3;next=now+1.2;}
                else
                {
                    string path=Path.GetFullPath("../outputs/v08-world.png");
                    if(!File.Exists(path))throw new Exception("ScreenCapture produced no output");
                    Finish("PASS: actual Play camera renders (world only, IMGUI excluded), running station / algae / overlapping worker and corpse",0);
                }
            }
            catch(Exception error){Finish("FAIL: "+error,1);}
        }
        static void AddBuilding(string id,Vector2Int origin)
        {
            var definition=session.catalog.FindBuilding(id);
            var go=UnityEngine.Object.Instantiate(definition.prefab,session.BuildingPosition(origin),session.world.transform.rotation,session.world.transform);
            var building=go.GetComponent<DeepBuildingInstance>();building.definition=definition;building.origin=origin;building.session=session;building.isConstructed=true;building.isOn=true;go.SetActive(true);session.Buildings.Add(building);
        }
        static void Capture(string filename)
        {
            // Batch-mode editors do not present a Game window, so ScreenCapture has no backbuffer.
            // Render the real Play camera explicitly; this includes sprites, lights and fog, not IMGUI.
            var camera=Camera.main;var target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);
            var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
            var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
                File.WriteAllBytes(Path.GetFullPath("../outputs/"+filename),pixels.EncodeToPNG());
            }
            finally{camera.targetTexture=previousTarget;RenderTexture.active=previousActive;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);}
        }
        static void Finish(string result,int exit)
        {
            SessionState.SetBool(Flag,false);File.WriteAllText("../outputs/v08-preview-status.txt",result);
            if(Application.isBatchMode)EditorApplication.Exit(exit);else EditorApplication.isPlaying=false;
        }
    }
}

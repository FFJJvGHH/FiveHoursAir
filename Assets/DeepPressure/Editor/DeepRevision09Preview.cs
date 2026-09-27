using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepPressure.Editor
{
    [InitializeOnLoad]
    public static class DeepRevision09Preview
    {
        const string Flag="DeepPressure.Revision09Preview";
        const BindingFlags Instance=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static int stage;
        static double next;
        static DeepGameSession session;
        static DeepPressureHUD hud;
        static DeepBuildingInstance station;
        static EditorWindow view;
        static Vector2 injectedMouse;
        static readonly StringBuilder diagnostics=new StringBuilder();
        static DeepRevision09Preview(){EditorApplication.update+=Advance;}
        public static void Run()
        {
            Directory.CreateDirectory("../outputs");
            // Exercise a previously authored scene as well as generated prefabs.
            string scene="Assets/DeepPressure/Scenes/DeepPressureColony 15.unity";
            if(!File.Exists(scene))scene="Assets/DeepPressure/Scenes/DeepPressureSurvival.unity";
            EditorSceneManager.OpenScene(scene);
            SessionState.SetBool(Flag,true);SessionState.SetFloat(Flag+"Start",(float)EditorApplication.timeSinceStartup);
            stage=0;next=0;EditorApplication.isPlaying=true;
        }
        static void Advance()
        {
            if(!SessionState.GetBool(Flag,false))return;
            EditorApplication.QueuePlayerLoopUpdate();
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Flag+"Start",0)>150){Finish("FAIL: preview timed out",1);return;}
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling||Time.frameCount<6)return;
            if(view!=null)view.SendEvent(new Event{type=EventType.Repaint,mousePosition=new Vector2(-100,-100)});
            double now=EditorApplication.timeSinceStartup;if(now<next)return;
            try
            {
                if(stage==0)
                {
                    session=UnityEngine.Object.FindObjectOfType<DeepGameSession>();hud=UnityEngine.Object.FindObjectOfType<DeepPressureHUD>();
                    if(session==null||hud==null)return;
                    hud.gameObject.AddComponent<DeepReviewInputProbe>();
                    foreach(var save in UnityEngine.Object.FindObjectsOfType<DeepAutosave>()){save.session=null;save.enabled=false;}
                    Application.runInBackground=true;EditorApplication.isPaused=false;
                    session.paused=false;session.speed=1;InvokeHUD("EnterColony");
                    var camera=Camera.main;camera.transform.position=session.world.transform.TransformPoint(new Vector3(33,56,-20));camera.orthographicSize=8.6f;
                    var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                    view=EditorWindow.GetWindow(type);view.position=new Rect(0,0,1600,930);view.Show();view.Focus();
                    type.GetProperty("targetSize",Instance).SetValue(view,new Vector2(1600,900));
                    type.GetProperty("renderIMGUI",Instance).SetValue(view,true);
                    view.SendEvent(new Event{type=EventType.Repaint,mousePosition=new Vector2(-100,-100)});
                    stage=1;next=now+2;
                }
                else if(stage==1){Capture("v09-home.png");EditorGUIUtility.QueueGameViewInputEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.C});stage=2;next=now+.5;}
                else if(stage==2)
                {
                    if(typeof(DeepPressureHUD).GetField("colonyPanel",Instance).GetValue(hud).ToString()!="Craft")throw new Exception("C did not open the production directory");
                    Capture("v09-no-machine.png");
                    station=AddBuilding("fabricator",new Vector2Int(21,53));AddBuilding("research_bench",new Vector2Int(25,53));AddBuilding("storage",new Vector2Int(35,53));AddBuilding("oxygen_diffuser",new Vector2Int(40,53));
                    session.RebuildOccupancy();session.RefreshStorageCapacity();
                    var recipe=session.catalog.FindRecipe("smelt_alloy");
                    if(!session.RequestCraftAt(station,recipe,2,out string reason))throw new Exception(reason);
                    // Advance the real simulation explicitly in the headless editor.
                    for(int i=0;i<90;i++)session.Tick(.1f);
                    foreach(var motion in UnityEngine.Object.FindObjectsOfType<DeepMachineMotion>())motion.AdvanceVisuals(.1f);
                    InvokeHUD("OpenStationProduction",station);
                    stage=3;next=now+6;
                }
                else if(stage==3){Capture("v09-production.png");session.ToggleBuilding(station);stage=4;next=now+.5;}
                else if(stage==4){Capture("v09-stopped.png");InvokeHUD("OpenProductionDirectory");stage=5;next=now+.5;}
                else if(stage==5)
                {
                    Capture("v09-device-list.png");
                    var hit=(Rect)typeof(DeepPressureHUD).GetProperty("BuildSlotRect",Instance).GetValue(hud);
                    float scale=(float)typeof(DeepPressureHUD).GetField("scale",Instance).GetValue(hud);
                    var offset=(Vector2)view.GetType().GetProperty("gameMouseOffset",Instance).GetValue(view);
                    float mouseScale=(float)view.GetType().GetProperty("gameMouseScale",Instance).GetValue(view);
                    injectedMouse=hit.center*scale/mouseScale-offset;
                    view.SendEvent(new Event{type=EventType.MouseMove,mousePosition=injectedMouse});
                    view.SendEvent(new Event{type=EventType.MouseDown,button=0,mousePosition=injectedMouse});
                    diagnostics.AppendLine("GameView build click: "+injectedMouse+" game pixels "+hit.center*scale+" UI "+hit+" scale "+scale);
                    stage=6;next=now+.5;
                }
                else if(stage==6)
                {
                    view.SendEvent(new Event{type=EventType.MouseUp,button=0,mousePosition=injectedMouse});
                    if(typeof(DeepPressureHUD).GetField("colonyPanel",Instance).GetValue(hud).ToString()!="Build")throw new Exception("Clicking the standalone build slot did not open the catalogue");
                    if((float)typeof(DeepPressureHUD).GetProperty("PanelPresence",Instance).GetValue(hud)<.99f){next=now+.15;return;}
                    Capture("v09-build.png");stage=7;next=now+.2;
                }
                else Finish("PASS: actual GameView IMGUI renders; standalone build slot, empty production directory, selected machine, work queue, shutdown and building catalogue",0);
            }
            catch(Exception error){Finish("FAIL: "+error,1);}
        }
        static void InvokeHUD(string method,params object[] arguments)=>typeof(DeepPressureHUD).GetMethod(method,Instance).Invoke(hud,arguments);
        static DeepBuildingInstance AddBuilding(string id,Vector2Int origin)
        {
            var definition=session.catalog.FindBuilding(id);
            var go=UnityEngine.Object.Instantiate(definition.prefab,session.BuildingPosition(origin),session.world.transform.rotation,session.world.transform);
            var building=go.GetComponent<DeepBuildingInstance>();building.definition=definition;building.origin=origin;building.session=session;building.isConstructed=true;building.isOn=true;go.SetActive(true);session.Buildings.Add(building);return building;
        }
        static void Capture(string name)
        {
            var type=view.GetType();type.GetProperty("targetSize",Instance).SetValue(view,new Vector2(1600,900));
            // This enters Unity's real GameView OnGUI and renders the actual HUD, not a mockup.
            view.SendEvent(new Event{type=EventType.Repaint,mousePosition=new Vector2(-100,-100)});
            var target=type.GetField("m_RenderTexture",Instance).GetValue(view) as RenderTexture;
            if(target==null)throw new Exception("GameView did not return its rendered texture");
            var previous=RenderTexture.active;var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            try
            {
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
                if(SystemInfo.graphicsUVStartsAtTop)
                {
                    var raw=pixels.GetPixels32();
                    for(int y=0;y<target.height/2;y++)for(int x=0;x<target.width;x++)
                    {int a=y*target.width+x,b=(target.height-y-1)*target.width+x;var swap=raw[a];raw[a]=raw[b];raw[b]=swap;}
                    pixels.SetPixels32(raw);pixels.Apply();
                }
                File.WriteAllBytes(Path.GetFullPath("../outputs/"+name),pixels.EncodeToPNG());
                diagnostics.AppendLine(name+" "+target.width+"x"+target.height+" screen="+Screen.width+"x"+Screen.height+" simulation="+session.SimulationTime.ToString("0.00"));
            }
            finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(pixels);}
        }
        static void Finish(string result,int exit)
        {
            SessionState.SetBool(Flag,false);File.WriteAllText("../outputs/v09-preview-status.txt",result+"\n"+diagnostics);
            if(Application.isBatchMode)EditorApplication.Exit(exit);else EditorApplication.isPlaying=false;
        }
    }

    [DefaultExecutionOrder(110)]
    public sealed class DeepReviewInputProbe:MonoBehaviour
    {
        void OnGUI()
        {
            if(Event.current.rawType!=EventType.MouseDown)return;
            var hud=GetComponent<DeepPressureHUD>();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            Debug.Log("V09_POINTER raw="+Event.current.mousePosition+" ui="+typeof(DeepPressureHUD).GetField("pointer",flags).GetValue(hud)+" hit="+typeof(DeepPressureHUD).GetProperty("BuildSlotRect",flags).GetValue(hud)+" dpi="+EditorGUIUtility.pixelsPerPoint+" panel="+typeof(DeepPressureHUD).GetField("colonyPanel",flags).GetValue(hud)+" event="+Event.current.type);
        }
    }
}

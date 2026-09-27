using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DeepPressure.Editor
{
    // Temporary fixed-operation local QA bridge. Each action is explicit and reports to outputs.
    [InitializeOnLoad]
    public static class DeepV06Automation
    {
        static readonly string output=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","outputs"));
        static double next;
        static DeepV06Automation()
        {
            Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"v06-ready.txt"),DateTime.Now.ToString("O"));
            EditorApplication.update+=Poll;
            CompilationPipeline.assemblyCompilationFinished+=(assembly,messages)=>{foreach(var message in messages)if(message.type==CompilerMessageType.Error)File.AppendAllText(Path.Combine(output,"v06-compile-errors.txt"),message.message+"\n");};
            Application.logMessageReceived+=(message,trace,type)=>{if(type==LogType.Error||type==LogType.Exception)File.AppendAllText(Path.Combine(output,"v06-errors.txt"),message+"\n"+trace+"\n");};
        }
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            next=EditorApplication.timeSinceStartup+.5;string commandPath=Path.Combine(output,"v06-command.txt");if(!File.Exists(commandPath))return;
            string command=File.ReadAllText(commandPath).Trim();File.Delete(commandPath);
            try
            {
                var session=UnityEngine.Object.FindObjectOfType<DeepGameSession>();
                switch(command)
                {
                    case "status":File.WriteAllText(Path.Combine(output,"v06-status.txt"),"Scene "+SceneManager.GetActiveScene().path+"\nPlay "+EditorApplication.isPlaying+"\nSession "+(session!=null)+(session==null?"":"\nLifeSupport "+session.lifeSupportEnabled+"\nWired "+session.useWiredPower+"\nMenu "+session.menuOpen+"\nTime "+session.SimulationTime+"\nHUD "+session.GetComponent<DeepPressureHUD>().enabled+"\nPower "+session.PowerProduction+" / "+session.PowerDemand+"\nScreen "+Screen.width+"x"+Screen.height));break;
                    case "upgrade":
                        if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;File.WriteAllText(commandPath,"upgrade");return;}
                        DeepIndustrialAuthoring.Upgrade(session);
                        string scenePath=SceneManager.GetActiveScene().path.Contains("DeepPressurePlayable")?SceneManager.GetActiveScene().path:AssetDatabase.GenerateUniqueAssetPath("Assets/DeepPressure/Scenes/DeepPressurePlayable.unity");
                        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),scenePath);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scenePath,true)};break;
                    case "test":
                        if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;File.WriteAllText(commandPath,"test");return;}
                        RunTests();break;
                    case "play":ShowGameView();EditorApplication.isPlaying=true;break;
                    case "stop":EditorApplication.isPlaying=false;break;
                    case "start":
                        if(!session.NewGame(out string reason))throw new InvalidOperationException(reason);
                        var hud=session.GetComponent<DeepPressureHUD>();typeof(DeepPressureHUD).GetMethod("EnterColony",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null);session.paused=true;break;
                    case "research":SetPanel(session,"Research");break;
                    case "build-menu":SetPanel(session,"Build");break;
                    case "workers":SetPanel(session,"Workers");break;
                    case "game":SetPanel(session,"None");break;
                    case "capture":ShowGameView();ScreenCapture.CaptureScreenshot(Path.Combine(output,"v06-game.png"));break;
                    case "build":Build();break;
                    default:throw new InvalidOperationException("Unknown QA action: "+command);
                }
                File.WriteAllText(Path.Combine(output,"v06-result.txt"),"PASS "+command+" "+DateTime.Now.ToString("O"));
            }
            catch(Exception error){File.WriteAllText(Path.Combine(output,"v06-result.txt"),"FAIL "+command+"\n"+error);Debug.LogException(error);}
        }
        static void ShowGameView()
        {
            Type type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(type);view.Show();view.Repaint();
        }
        static void SetPanel(DeepGameSession session,string name)
        {
            var hud=session.GetComponent<DeepPressureHUD>();var field=typeof(DeepPressureHUD).GetField("colonyPanel",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(hud,Enum.Parse(field.FieldType,name));
        }
        static void RunTests()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before the isolated editor verification suite.");
            var report=new StringBuilder();int failures=0;
            void Check(string label,Func<string> test){try{report.AppendLine(label+": "+test());}catch(Exception e){failures++;report.AppendLine("FAIL "+label+"\n"+e);}}
            Check("Core",DeepPressureSelfTests.RunAll);Check("Gameplay",DeepGameplayTests.RunGameplayTests);Check("Power",DeepPowerTests.RunAll);
            Check("Exploration",DeepExplorationTests.RunAll);Check("Atmosphere",DeepAtmosphereTests.RunAll);Check("Hazards",DeepHazardTests.RunAll);Check("Persistence",DeepPersistenceTests.RunAll);
            Check("Grounding",()=>DeepPresentationUpgrade.ValidateCatalogGrounding(UnityEngine.Object.FindObjectOfType<DeepGameSession>().catalog));
            Check("Visual",()=>{string result=DeepVisualValidation.ValidateActiveScene(out bool ok);if(!ok)throw new Exception(result);return result;});
            File.WriteAllText(Path.Combine(output,"v06-validation.txt"),report.ToString());
            if(failures>0)throw new InvalidOperationException(failures+" validation groups failed. See v06-validation.txt");
        }
        static void Build()
        {
            string target=Path.Combine(output,"DeepPressure-v0.6","DeepPressure.exe");Directory.CreateDirectory(Path.GetDirectoryName(target));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{SceneManager.GetActiveScene().path},locationPathName=target,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            File.WriteAllText(Path.Combine(output,"v06-build.txt"),report.summary.result+"\n"+target+"\nErrors "+report.summary.totalErrors);
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Build failed: "+report.summary.result);
        }
    }
}

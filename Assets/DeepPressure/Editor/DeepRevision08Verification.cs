using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepPressure.Editor
{
    public static class DeepRevision08Verification
    {
        const string ScenePath="Assets/DeepPressure/Scenes/DeepPressureSurvival.unity";
        [MenuItem("深压/验证/生存与交互 v0.8 验收")]
        public static void Run()
        {RunChecks(false);}
        [MenuItem("深压/验证/设备生产与界面 v0.9 验收")]
        public static void RunRevision09()
        {RunChecks(true);}
        public static void ValidateAndPreview09()
        {
            if(!RunChecks(true,false)){if(Application.isBatchMode)EditorApplication.Exit(1);return;}
            DeepRevision09Preview.Run();
        }
        static bool RunChecks(bool revision09,bool exitWhenDone=true)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play before validation.");
            Directory.CreateDirectory("../outputs");
            var report=new StringBuilder("DeepPressure "+(revision09?"v0.9":"v0.8")+"\n"+DateTime.Now.ToString("O")+"\n");
            int failures=0;
            Check(report,ref failures,"Gas network",DeepPressureSelfTests.RunAll);
            Check(report,ref failures,"Colony / navigation",DeepGameplayTests.RunGameplayTests);
            Check(report,ref failures,"Device production",DeepCraftingTests.RunAll);
            Check(report,ref failures,"Power",DeepPowerTests.RunAll);
            Check(report,ref failures,"Exploration memory",DeepExplorationTests.RunAll);
            Check(report,ref failures,"Atmosphere",DeepAtmosphereTests.RunAll);
            Check(report,ref failures,"Life support",DeepLifeSupportTests.RunAll);
            Check(report,ref failures,"Hazards",DeepHazardTests.RunAll);
            Check(report,ref failures,"Network lifecycle",DeepNetworkLifecycleTests.RunAll);
            Check(report,ref failures,"Persistence",DeepPersistenceTests.RunAll);
            Check(report,ref failures,"Crew lifecycle",DeepCrewLifecycleTests.RunAll);
            Check(report,ref failures,"Speed cycle",()=>{
                float speed=1;foreach(float expected in new[]{2f,4f,6f,1f,2f,4f,6f,1f}){speed=DeepPressureHUD.NextSpeed(speed);if(speed!=expected)throw new Exception("Speed cycle mismatch");}
                return "PASS: 1 → 2 → 4 → 6 → 1, twice";
            });
            EditorSceneManager.OpenScene(ScenePath);
            if(revision09)
            {
                var session=UnityEngine.Object.FindObjectOfType<DeepGameSession>();
                DeepPresentationUpgrade.ApplyToCatalogAndLoadedScenes(session.catalog);
                Check(report,ref failures,"Grounding",()=>DeepPresentationUpgrade.ValidateCatalogGrounding(session.catalog));
                Check(report,ref failures,"Machine feedback",()=>DeepPresentationUpgrade.ValidateMachinePresentation(session.catalog));
                var gameView=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                var members=new StringBuilder();
                foreach(var method in gameView.GetMethods(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public))
                    if(method.Name.Contains("Render")||method.Name.Contains("Size")||method.Name.Contains("Texture"))members.AppendLine(method.ToString());
                foreach(var field in gameView.GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public))members.AppendLine(field.ToString());
                File.WriteAllText("../outputs/v09-gameview-api.txt",members.ToString());
            }
            var authored=UnityEngine.Object.FindObjectOfType<DeepGameSession>();
            authored.simulationTuning=DeepSimulationTuning.LoadDefault();authored.paused=false;authored.speed=1;
            EditorUtility.SetDirty(authored);EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            if(failures==0)Check(report,ref failures,"Authored survival opening",()=>{DeepOpeningVerification.Run();return "PASS: opening construction / finite respiration / algae CO2 recycling / saved progress";});
            Directory.CreateDirectory("../outputs");
            report.AppendLine(failures==0?"PASS: all revision checks":"FAILURES: "+failures);
            File.WriteAllText(revision09?"../outputs/v09-validation.txt":"../outputs/v08-validation.txt",report.ToString());
            Debug.Log(report.ToString());
            if(Application.isBatchMode&&exitWhenDone)EditorApplication.Exit(failures==0?0:1);
            return failures==0;
        }
        static void Check(StringBuilder report,ref int failures,string label,Func<string> test)
        {
            try{report.AppendLine(label+": "+test());}
            catch(Exception error){failures++;report.AppendLine("FAIL "+label+": "+error);}
        }
    }
}

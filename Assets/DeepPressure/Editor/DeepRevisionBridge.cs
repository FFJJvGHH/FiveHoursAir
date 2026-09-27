using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace DeepPressure.Editor
{
    // Explicit validation tools; no background commands or editor automation.
    public static class DeepRevisionBridge
    {
        [MenuItem("深压/验证/交互与存档完整验收 %#F9")]
        public static void RunAll()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请先退出 Play，再运行编辑器验证。");
            foreach(var building in UnityEngine.Object.FindObjectsOfType<DeepBuildingInstance>())
                if(building.definition!=null&&building.definition.id=="printing_pod"){DeepOpeningVerification.Run();return;}
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","outputs","revision-full-validation.txt"));
            var report=new StringBuilder("深压 v0.4 完整验收\n"+DateTime.Now.ToString("O")+"\n");
            try
            {
                report.AppendLine(DeepPressureSelfTests.RunAll());
                report.AppendLine(DeepGameplayTests.RunGameplayTests());
                report.AppendLine(DeepPersistenceTests.RunAll());
                report.AppendLine(DeepPowerTests.RunAll());
                report.AppendLine(DeepExplorationTests.RunAll());
                report.AppendLine(DeepAtmosphereTests.RunAll());
                report.AppendLine(DeepLifeSupportTests.RunAll());
                report.AppendLine(DeepHazardTests.RunAll());
                report.AppendLine(DeepNetworkLifecycleTests.RunAll());
                DeepColonyVerification.Run();report.AppendLine("PASS: actual scene colony workflow");
                report.AppendLine(DeepPresentationUpgrade.ValidateCatalogGrounding(UnityEngine.Object.FindObjectOfType<DeepGameSession>().catalog));
                report.AppendLine(DeepVisualValidation.ValidateActiveScene(out bool visual));
                if(!visual)throw new InvalidOperationException("视觉资产或物体归属验证未通过。");
                report.AppendLine("PASS: all automated checks. Inspect the Game view and exercise menu/save/load separately.");
                Debug.Log(report.ToString());
            }
            catch(Exception error){report.AppendLine("FAIL: "+error);throw;}
            finally{Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,report.ToString());}
        }
        [MenuItem("深压/关卡/完成交互版本升级并保存")]
        public static void UpgradeAndSave()
        {
            DeepIndustrialAuthoring.Upgrade(UnityEngine.Object.FindObjectOfType<DeepGameSession>());
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("地形、科技目录与完整灯具已升级并保存。可从 Play 的主菜单开始工程。");
        }
    }
}

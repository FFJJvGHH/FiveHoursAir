using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DeepPressure.Editor
{
    [InitializeOnLoad] public static class DeepV07RunOnce
    {
        static DeepV07RunOnce(){EditorApplication.update+=Run;}
        static void Run()
        {
            if(!File.Exists("../outputs/v07-run.request")||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            EditorApplication.update-=Run;bool onlyTest=File.ReadAllText("../outputs/v07-run.request").Trim()=="test";File.Delete("../outputs/v07-run.request");
            try
            {
                File.WriteAllText("../outputs/v07-status.txt","Building survival scene");
                var active=SceneManager.GetActiveScene();
                if(active.isDirty)EditorSceneManager.SaveScene(active,AssetDatabase.GenerateUniqueAssetPath("Assets/DeepPressure/Scenes/BeforeSurvival.unity"),true);
                if(!onlyTest)DeepPressureBuilder.Build(false);
                var scene=SceneManager.GetActiveScene();
                if(!onlyTest)EditorSceneManager.SaveScene(scene,AssetDatabase.GenerateUniqueAssetPath("Assets/DeepPressure/Scenes/DeepPressureSurvival.unity"));
                EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scene.path,true)};
                File.WriteAllText("../outputs/v07-status.txt","Scene saved: "+scene.path+"; testing");
                DeepOpeningVerification.Run();
                File.WriteAllText("../outputs/v07-status.txt","PASS: "+scene.path);
                EditorApplication.isPlaying=true;
            }
            catch(Exception e){File.WriteAllText("../outputs/v07-status.txt","FAIL: "+e);Debug.LogException(e);}
        }
    }
}

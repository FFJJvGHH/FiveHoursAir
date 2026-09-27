using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DeepPressure.Editor
{
    [InitializeOnLoad]
    public static class DeepAirDemoRuntimeProbe
    {
        const string Request="../outputs/air-demo-probe.request";
        const string Report="../outputs/air-demo-probe-result.txt";
        static double started;
        static bool pending;
        static DeepPressureHUD source;
        static DeepAirDemoRuntimeProbe(){EditorApplication.update+=Run;}
        [MenuItem("深压/测试/空气演示 — 验证进入与返回")]
        static void StartProbe()
        {
            if(!Application.isPlaying){Debug.LogWarning("[AirDemoConsole] 请先点击 Play。");return;}
            Directory.CreateDirectory("../outputs");File.WriteAllText(Request,"test");
            EditorApplication.update-=Run;EditorApplication.update+=Run;
        }
        static void CaptureLog(string message,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error)File.AppendAllText(Report,"\n"+type+": "+message+"\n"+stack);}
        static void Run()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||!Application.isPlaying)return;
            if(pending)
            {
                if(EditorApplication.timeSinceStartup-started<2)return;
                File.AppendAllText(Report,"\nAFTER 2 SECONDS active="+DeepAirDemo.IsActive+" scene="+SceneManager.GetActiveScene().name);
                DeepAirDemoConsole.Instance.ExitDemo();
                File.AppendAllText(Report,"\nRETURN active="+DeepAirDemo.IsActive+" sourceRestored="+(source!=null&&source.gameObject.activeInHierarchy));
                pending=false;
                EditorApplication.update-=Run;
                return;
            }
            if(!File.Exists(Request))return;
            File.Delete(Request);
            try
            {
                source=UnityEngine.Object.FindObjectOfType<DeepPressureHUD>();
                File.WriteAllText(Report,"SOURCE="+SceneManager.GetActiveScene().name+" HUD="+(source!=null));
                Application.logMessageReceived+=CaptureLog;
                DeepAirDemoConsole.Instance.TestSequence();
                Application.logMessageReceived-=CaptureLog;
                File.AppendAllText(Report,"\nSEQUENCE "+(DeepAirDemo.IsActive?"PASS":"FAIL")+" scene="+SceneManager.GetActiveScene().name);
                started=EditorApplication.timeSinceStartup;pending=true;
            }
            catch(Exception error){File.AppendAllText(Report,"\nFAIL "+error);Debug.LogException(error);}
        }
    }
}

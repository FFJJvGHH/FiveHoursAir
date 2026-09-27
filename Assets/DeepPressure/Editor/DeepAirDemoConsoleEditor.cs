using UnityEditor;
using UnityEngine;
namespace DeepPressure.Editor
{
    public static class DeepAirDemoConsoleEditor
    {
        [MenuItem("深压/测试/空气演示 — 直接进入")]
        public static void Enter(){if(Ready())DeepAirDemoConsole.Instance.EnterDemo();}
        [MenuItem("深压/测试/空气演示 — 测试 abcabc")]
        public static void Sequence(){if(Ready())DeepAirDemoConsole.Instance.TestSequence();}
        [MenuItem("深压/测试/空气演示 — 返回基地")]
        public static void Exit(){if(Ready())DeepAirDemoConsole.Instance.ExitDemo();}
        static bool Ready(){if(Application.isPlaying)return true;Debug.LogWarning("[AirDemoConsole] 请先点击 Play，然后运行测试菜单。");return false;}
    }
}

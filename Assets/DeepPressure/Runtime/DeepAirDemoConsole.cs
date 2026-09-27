using UnityEngine;

namespace DeepPressure
{
    [DefaultExecutionOrder(-500)]
    public sealed class DeepAirDemoConsole : MonoBehaviour
    {
        static DeepAirDemoConsole instance;
        const string Sequence="abcabc";
        int matched;
        float lastInput;
        int consumedFrame=-1;
        public bool TextInputFocused { get; set; }
        public static DeepAirDemoConsole Instance
        {
            get
            {
                if(instance==null&&Application.isPlaying)
                {
                    instance=FindObjectOfType<DeepAirDemoConsole>();
                    if(instance==null)instance=new GameObject("AirDemoConsole").AddComponent<DeepAirDemoConsole>();
                }
                return instance;
            }
        }
        public static bool ConsumedInputThisFrame=>instance!=null&&instance.consumedFrame==Time.frameCount;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic(){instance=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap(){var console=Instance;}
        void Awake()
        {
            if(instance!=null&&instance!=this){Destroy(gameObject);return;}
            instance=this;DontDestroyOnLoad(gameObject);
        }
        void Update()
        {
            if(DeepAirDemo.IsActive)return;
            if(TextInputFocused||Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl)||Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt)){matched=0;return;}
            // Physical key transitions work while paused and are independent of IMGUI character events.
            if(Input.GetKeyDown(KeyCode.A))Feed('a');
            else if(Input.GetKeyDown(KeyCode.B))Feed('b');
            else if(Input.GetKeyDown(KeyCode.C))Feed('c');
            else if(Input.anyKeyDown)matched=0;
        }
        public bool Feed(char key)
        {
            if(Time.realtimeSinceStartup-lastInput>5)matched=0;
            key=char.ToLowerInvariant(key);lastInput=Time.realtimeSinceStartup;
            matched=key==Sequence[matched]?matched+1:key=='a'?1:0;
            if(matched>0)consumedFrame=Time.frameCount;
            if(matched<Sequence.Length)return false;
            matched=0;return EnterDemo();
        }
        [ContextMenu("测试/直接进入空气演示")]
        public void TestDirectEntry(){EnterDemo();}
        public bool EnterDemo()
        {
            if(DeepAirDemo.IsActive)return true;
            var hud=FindObjectOfType<DeepPressureHUD>();
            if(hud==null){Debug.LogError("[AirDemoConsole] 当前场景没有 DeepPressureHUD。",this);return false;}
            DeepAirDemo.Enter(hud);
            if(DeepAirDemo.IsActive){Debug.Log("[AirDemoConsole] PASS：已进入独立空气演示场景。",this);return true;}
            Debug.LogError("[AirDemoConsole] 演示创建失败，查看上方异常。",this);return false;
        }
        [ContextMenu("测试/输入 abcabc")]
        public void TestSequence()
        {
            matched=0;bool entered=false;foreach(char key in Sequence)entered=Feed(key);
            Debug.Log("[AirDemoConsole] abcabc "+(entered?"PASS":"FAIL"),this);
        }
        [ContextMenu("测试/退出空气演示")]
        public void ExitDemo(){DeepAirDemo.Exit();}
    }
}

using UnityEngine;
namespace DeepPressure
{
    // Uses real time, so 4x simulation does not generate four times as many disk writes.
    public sealed class DeepAutosave : MonoBehaviour
    {
        public DeepGameSession session;
        [Min(30)] public float intervalSeconds = 120;
        float elapsed;
        public void ResetTimer() => elapsed = 0;
        void Update()
        {
            if (session == null || !session.HasPlayableSession || session.IsSimulationPaused) return;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < Mathf.Max(30,intervalSeconds)) return;
            elapsed = 0; if (!session.SaveGame("autosave",out string message)) Debug.LogWarning(message,this);
        }
        void OnApplicationPause(bool paused) { if (paused) SaveOnExit(); }
        void OnApplicationQuit() => SaveOnExit();
        void SaveOnExit()
        {
            if (session != null && session.HasPlayableSession && !session.SaveGame("autosave",out string message)) Debug.LogWarning(message,this);
        }
    }
}

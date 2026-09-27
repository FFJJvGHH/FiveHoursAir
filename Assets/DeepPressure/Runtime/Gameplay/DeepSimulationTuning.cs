using UnityEngine;

namespace DeepPressure
{
    /// <summary>One Inspector asset owns the survival pace. Units are game seconds:
    /// a colony cycle is 600 s, so 0.07 mol/s is 1.344 kg O2/person/cycle, about
    /// 1.6 times NASA's 0.84 kg/day reference, intentionally more demanding.</summary>
    [CreateAssetMenu(menuName="深压/生存模拟参数",fileName="DeepSimulationTuning")]
    public sealed class DeepSimulationTuning : ScriptableObject
    {
        [Header("人物代谢 · mol / 游戏秒")]
        [Min(.001f),Tooltip("默认每600秒周期消耗1.344kg氧气；增大可加强生存压力。")] public float breathingMolPerSecond=.07f;
        [Header("空气流动与开局")]
        [Range(.05f,3)] public float diffusionPerSecond=2.4f;
        [Range(0,.02f),Tooltip("旧版零CO₂初始健康空气：以等量氮气替换为微量CO₂，不增压。")] public float initialCarbonDioxideFraction=.0004f;
        [Header("藻类光合作用 · 每消耗1 mol CO₂释放1 mol O₂")]
        [Min(.01f)] public float algaeConversionMolPerSecond=1.5f;
        [Min(1)] public float cultureCapacityMol=30;
        [Range(1,6)] public int algaeAirReachCells=3;
        [Min(1)] public float algaeTargetOxygenKPa=23;
        [Header("氧气 / 气压 · kPa，急性游戏阈值")]
        [Min(1)] public float minimumPressureKPa=30;
        [Min(1)] public float maximumPressureKPa=180;
        [Min(0)] public float oxygenWarningKPa=19.5f;
        [Min(0)] public float minimumOxygenKPa=16;
        [Min(0)] public float maximumOxygenKPa=32;
        [Header("CO₂分压 · 预警 / 不适 / 急性危险")]
        [Min(0)] public float carbonWarningKPa=.5f;
        [Min(0)] public float carbonImpairmentKPa=2;
        [Min(0)] public float carbonDangerKPa=4;
        [Range(.05f,1)] public float warningWorkEfficiency=.85f;
        [Range(.05f,1)] public float impairedWorkEfficiency=.65f;
        [Header("独立环境损伤 · 不消耗呼吸储备，设为0可仅保留不适")]
        [Min(0),Tooltip("达到CO₂危险分压后的基础每秒伤害，随分压升高，死因为二氧化碳中毒。")] public float carbonToxicityDamagePerSecond=.6f;
        [Range(.001f,.5f)] public float processVaporDangerFraction=.015f;
        [Min(0)] public float processVaporDamagePerSecond=.8f;
        [Min(0),Tooltip("超出安全总压的比例 × 此每秒伤害；与凿穿时的瞬时冲击分开。")] public float pressureExposureDamagePerSecond=.35f;
        [Min(0),Tooltip("超出高氧阈值的比例 × 此每秒伤害；氧够时呼吸储备仍恢复。")] public float oxygenExcessDamagePerSecond=.2f;
        [Header("应急呼吸储备与恢复")]
        [Min(1)] public float airReserveSeconds=90;
        [Min(.1f)] public float airReserveRecoveryPerSecond=4;
        [Min(0)] public float suffocationDamagePerSecond=5;
        [Header("凿穿隔层 · 压差冲击")]
        [Min(1)] public float pressureShockThresholdKPa=40;
        [Min(0),Tooltip("伤害 = 此系数 × (压差/阈值)² × 距离衰减。")] public float pressureShockDamageScale=9;

        static DeepSimulationTuning fallback;
        public static DeepSimulationTuning LoadDefault()
        {
            var saved=Resources.Load<DeepSimulationTuning>("DeepSimulationTuning");
            if(saved!=null)return saved;
            if(fallback==null){fallback=CreateInstance<DeepSimulationTuning>();fallback.hideFlags=HideFlags.HideAndDontSave;}
            return fallback;
        }
    }
}

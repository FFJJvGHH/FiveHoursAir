using UnityEngine;

namespace DeepPressure
{
    public sealed partial class DeepPressureHUD
    {
        bool HandleHiddenDemoInput(Event e)
        {
            var console=DeepAirDemoConsole.Instance;
            if(console!=null)console.TextInputFocused=colonyPanel==ColonyPanel.Research&&GUI.GetNameOfFocusedControl()=="ResearchSearch";
            if(e.type!=EventType.KeyDown||!DeepAirDemoConsole.ConsumedInputThisFrame)return false;
            e.Use();return true;
        }
    }
}

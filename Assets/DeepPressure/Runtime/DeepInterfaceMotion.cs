using System.Collections.Generic;
using UnityEngine;
using Icon = DeepPressure.DeepUIIcons.Icon;

namespace DeepPressure
{
    public sealed partial class DeepPressureHUD
    {
        sealed class ControlMotion { public float hover, pressedAt = -10, updatedAt, resultAt=-10; public bool success=true; }
        readonly Dictionary<Rect, ControlMotion> controlMotion = new Dictionary<Rect, ControlMotion>();
        ControlMotion lastActivatedControl;
        ColonyPanel lastMotionPanel;
        ColonyPanel presentedPanel;
        float panelOpenedAt, panelClosedAt=-10;
        bool ShowBuildSlot => session != null && session.catalog != null && !pauseMenu;
        Rect BuildSlotRect => new Rect(dockRect.x-88,dockRect.y,76,72);
        readonly List<Texture2D> interfaceTextures=new List<Texture2D>();
        void InitializeInterfaceSkin()
        {
            interfaceSkin=Instantiate(GUI.skin);interfaceSkin.name="DeepPressure interface";
            Texture2D track=InterfaceTexture(new Color(.045f,.075f,.08f)),thumb=InterfaceTexture(new Color(.23f,.34f,.33f)),active=InterfaceTexture(new Color(.43f,.66f,.57f));
            interfaceSkin.verticalScrollbar=new GUIStyle{fixedWidth=7,normal={background=track},margin=new RectOffset(3,0,0,0)};
            interfaceSkin.verticalScrollbarThumb=new GUIStyle{normal={background=thumb},hover={background=active},active={background=active},border=new RectOffset(),padding=new RectOffset()};
            interfaceSkin.verticalScrollbarUpButton=new GUIStyle{fixedHeight=0};interfaceSkin.verticalScrollbarDownButton=new GUIStyle{fixedHeight=0};
            interfaceSkin.horizontalScrollbar=new GUIStyle{fixedHeight=7,normal={background=track},margin=new RectOffset(0,0,3,0)};
            interfaceSkin.horizontalScrollbarThumb=new GUIStyle{normal={background=thumb},hover={background=active},active={background=active},border=new RectOffset(),padding=new RectOffset()};
            interfaceSkin.horizontalScrollbarLeftButton=new GUIStyle{fixedWidth=0};interfaceSkin.horizontalScrollbarRightButton=new GUIStyle{fixedWidth=0};
        }
        Texture2D InterfaceTexture(Color color)
        {
            var texture=new Texture2D(1,1,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};texture.SetPixel(0,0,color);texture.Apply();interfaceTextures.Add(texture);return texture;
        }
        void ReleaseInterfaceSkin()
        {
            foreach(var texture in interfaceTextures)if(texture!=null){if(Application.isPlaying)Destroy(texture);else DestroyImmediate(texture);}
            interfaceTextures.Clear();if(interfaceSkin!=null){if(Application.isPlaying)Destroy(interfaceSkin);else DestroyImmediate(interfaceSkin);}
        }
        static float EaseOut(float value){value=Mathf.Clamp01(value);return 1-(1-value)*(1-value)*(1-value);}
        float PanelPresence=>colonyPanel!=ColonyPanel.None?EaseOut((Time.unscaledTime-panelOpenedAt)/.22f):1-Mathf.Pow(Mathf.Clamp01((Time.unscaledTime-panelClosedAt)/.16f),2);

        ControlMotion MotionFor(Rect rect)
        {
            var key=new Rect(GUIUtility.GUIToScreenPoint(rect.position),rect.size);
            if(!controlMotion.TryGetValue(key,out var motion))
            {
                if(controlMotion.Count>768)controlMotion.Clear();
                motion=new ControlMotion{updatedAt=Time.unscaledTime};controlMotion[key]=motion;
            }
            if(Event.current.type==EventType.Repaint)
            {
                float dt=Mathf.Min(.05f,Time.unscaledTime-motion.updatedAt);motion.updatedAt=Time.unscaledTime;
                bool available=GUI.enabled&&(!pauseMenu||drawingPauseMenu);
                float target=available&&rect.Contains(pointer)?1:0;
                motion.hover=Mathf.MoveTowards(motion.hover,target,dt/(target>0?.09f:.16f));
            }
            return motion;
        }
        Rect ButtonVisual(Rect rect)
        {
            var motion=MotionFor(rect);
            float pulse=1-EaseOut((Time.unscaledTime-motion.pressedAt)/.18f);
            float inset=Mathf.Min(2,rect.height*.04f)*pulse;
            var visual=Inset(rect,inset);visual.y-=motion.hover*(1-pulse);
            return visual;
        }
        void ConfirmControl(bool success)
        {
            if(lastActivatedControl==null||Time.unscaledTime-lastActivatedControl.pressedAt>.35f)return;
            lastActivatedControl.success=success;lastActivatedControl.resultAt=Time.unscaledTime;
        }

        // Feedback is rendered in the same GUI group as the hit target, including scroll views.
        // Hit rectangles never bounce or move as a consequence of hover/press feedback.
        void ControlFeedback(Rect rect, bool pressed)
        {
            var motion=MotionFor(rect);
            if (pressed){motion.pressedAt=Time.unscaledTime;lastActivatedControl=motion;}
            if (Event.current.type != EventType.Repaint) return;
            float now = Time.unscaledTime;
            bool available = GUI.enabled && (!pauseMenu || drawingPauseMenu);
            float pulse = available ? 1 - EaseOut((now - motion.pressedAt) / .18f) : 0;
            float result=available?1-EaseOut((now-motion.resultAt)/.42f):0;
            float h = Mathf.SmoothStep(0, 1, motion.hover);
            if (h + pulse + result < .001f) return;
            var tint=result>0&&!motion.success?Amber:Mint;
            Rounded(Inset(rect, 1), WithAlpha(tint, .075f*h+.18f*pulse+.12f*result), Mathf.Min(6, rect.height / 4));
            float width=(rect.width-12)*Mathf.Max(h,EaseOut((now-motion.pressedAt)/.18f));
            Rounded(new Rect(rect.center.x-width*.5f,rect.yMax-2,width,1.5f),WithAlpha(tint,.55f*h+.35f*pulse+.5f*result),.75f);
        }

        void DrawBuildSlot()
        {
            if(!ShowBuildSlot)return;
            var hit=BuildSlotRect;var r=ButtonVisual(hit);bool active=colonyPanel==ColonyPanel.Build||colonyTool==ColonyTool.Build;
            Rounded(r,active?Mint:Border,9);Rounded(Inset(r,1),active?new Color(.16f,.28f,.25f):Panel,8);
            DrawIcon(Icon.Build,new Rect(r.center.x-14,r.y+10,28,28),Mint);
            Label(new Rect(r.x,r.y+43,r.width,22),"建造  B",tiny,White);
            RegisterHover("build-slot",hit,"建造设施","打开建筑目录，选择后在世界中放置。");
            if(Click(hit))ActivateColonyTool(0);
        }

    }
}

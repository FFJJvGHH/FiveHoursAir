using UnityEngine;
using Icon=DeepPressure.DeepUIIcons.Icon;

namespace DeepPressure
{
    public sealed partial class DeepPressureHUD
    {
        void DrawPrintingPodCard(float x,ref float y)
        {
            float demand=session.AliveWorkerCount*Mathf.Max(0,session.breathingMolPerSecond);
            Label(new Rect(x,y,220,25),session.PrintingReady?"打印就绪":"下次打印  "+TimeLabel(Mathf.Ceil(session.PrintCooldownRemaining)),body,session.PrintingReady?Mint:Muted);y+=32;
            Label(new Rect(x,y,220,22),"在岗 "+session.AliveWorkerCount+" 人  ·  耗氧 "+demand.ToString("0.00")+" mol/s",small,White);y+=26;
            Label(new Rect(x,y,220,22),"新增 1 人  +"+session.breathingMolPerSecond.ToString("0.00")+" mol/s",small,Amber);y+=31;
            if(!session.PrintingReady)
            {
                Label(new Rect(x,y,220,42),"人员打印或领取补给后，\n开始下一轮冷却。",new GUIStyle(small){wordWrap=true},Muted);return;
            }
            var candidates=session.GetPrintingCandidates();
            for(int i=0;i<candidates.Length;i++)
            {
                var candidate=candidates[i];int index=i;
                Rect row=new Rect(x,y,220,66);Rounded(row,new Color(.085f,.145f,.16f),7);
                DrawIcon(Icon.Person,new Rect(x+8,y+12,22,22),Mint);
                Label(new Rect(x+39,y+6,130,23),candidate.displayName,body,White);
                Label(new Rect(x+39,y+31,119,22),candidate.specialty,small,Muted);
                Rect accept=new Rect(x+165,y+18,48,29);ActionButton(accept,Icon.Play,"",true);
                RegisterHover("printcandidate"+i,row,candidate.displayName,candidate.specialty+" · 工作效率 "+candidate.workSpeed.ToString("0.00")+"× · 耗氧 +"+session.breathingMolPerSecond.ToString("0.00")+" mol/s");
                if(Click(row)){bool ok=session.TryPrintWorker(index,out string reason);ShowToast(reason,ok);}
                y+=73;
            }
            bool hasAlgae=session.catalog.FindItem("algae")!=null;
            Rect supply=new Rect(x,y,220,32);ActionButton(supply,Icon.Storage,hasAlgae?"领取补给 · 藻类 +12":"领取补给 · 原矿 +20",true);
            if(Click(supply)){bool ok=session.SkipPrinting(out string reason);ShowToast(reason,ok);}
        }
    }
}

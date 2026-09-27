using System;
using UnityEngine;

namespace DeepPressure
{
    [Serializable] public sealed class DeepPrintCandidate
    {
        public string displayName, specialty;
        public float workSpeed, moveSpeed;
        public int digPreference, buildPreference, researchPreference;
    }

    public sealed partial class DeepGameSession
    {
        [Header("Colony population")]
        public DeepWorker workerPrefab;
        [Min(1)] public float printingIntervalSeconds = 180;
        [Min(0)] public float suffocationDamagePerSecond = 5;
        float nextPrintingTime = 180;
        int printingGeneration;
        public int AliveWorkerCount { get { int count=0;foreach(var w in Workers)if(w!=null&&w.IsAlive)count++;return count; } }
        public int DeadWorkerCount { get { int count=0;foreach(var w in Workers)if(w!=null&&!w.IsAlive)count++;return count; } }
        public bool IsColonyLost => Workers.Count>0&&AliveWorkerCount==0;
        public DeepBuildingInstance PrintingPod
        {
            get { foreach(var b in Buildings)if(b!=null&&b.isConstructed&&b.definition!=null&&b.definition.id=="printing_pod")return b;return null; }
        }
        public float PrintCooldownRemaining => Mathf.Max(0,nextPrintingTime-SimulationTime);
        public bool PrintingReady => PrintingPod!=null&&PrintingPod.IsOperational&&PrintCooldownRemaining<=0;

        public DeepPrintCandidate[] GetPrintingCandidates()
        {
            string[] names={"米拉","罗伊","阿辰","诺拉","小麦","伊森","露娜","奥托","沐沐"};
            int offset=(printingGeneration%3)*3;
            return new[] {
                new DeepPrintCandidate { displayName=names[offset],specialty="采掘",workSpeed=1.15f,moveSpeed=2.1f,digPreference=3,buildPreference=2,researchPreference=1 },
                new DeepPrintCandidate { displayName=names[offset+1],specialty="建造",workSpeed=1.05f,moveSpeed=2.5f,digPreference=2,buildPreference=3,researchPreference=1 },
                new DeepPrintCandidate { displayName=names[offset+2],specialty="研究",workSpeed=1.1f,moveSpeed=2.2f,digPreference=1,buildPreference=2,researchPreference=3 }
            };
        }

        public bool TryPrintWorker(int candidateIndex,out string reason)
        {
            InitializeSession();
            if(!CanUsePrintingPod(out reason))return false;
            var candidates=GetPrintingCandidates();
            if(candidateIndex<0||candidateIndex>=candidates.Length){reason="请选择一名候选人员";return false;}
            var pod=PrintingPod;Vector2Int spawn=pod.origin;bool found=false;
            // New arrivals appear at the pod, only on a real standable, unoccupied tile.
            for(int radius=0;radius<=5&&!found;radius++)
                for(int x=pod.origin.x-radius;x<=pod.origin.x+pod.definition.footprint.x-1+radius;x++)
                {
                    var cell=new Vector2Int(x,pod.origin.y);if(!IsStandable(cell))continue;
                    bool occupied=false;foreach(var existing in Workers)if(existing!=null&&existing.IsAlive&&existing.Cell==cell){occupied=true;break;}
                    if(occupied)continue;spawn=cell;found=true;break;
                }
            if(!found){reason="打印舱周围没有可站立的空位";return false;}
            var candidate=candidates[candidateIndex];
            var worker=CreateWorkerObject();
            worker.displayName=candidate.displayName;worker.name="人员 · "+candidate.displayName;
            worker.health=100;worker.deathCause=null;worker.diedAtSeconds=-1;worker.airReserveSeconds=SimulationTuning.airReserveSeconds;
            worker.environmentUnsafe=false;worker.breathingUnsafe=false;worker.environmentCondition=null;worker.environmentEfficiency=1;worker.automationPaused=false;
            worker.currentOrder=null;worker.SetPath(null);worker.nextWorkSearchTime=0;
            worker.workSpeed=candidate.workSpeed;worker.moveCellsPerSecond=candidate.moveSpeed;
            worker.digPreference=candidate.digPreference;worker.buildPreference=candidate.buildPreference;worker.researchPreference=candidate.researchPreference;
            worker.craftPreference=worker.pipePreference=2;
            SetObjectId(worker,"w:printed:"+Guid.NewGuid().ToString("N"));
            worker.TeleportToCell(spawn);worker.GetComponent<DeepWorkerPresentation>()?.ResetLivingPose();worker.gameObject.SetActive(true);Workers.Add(worker);
            CompletePrintingChoice();RefreshExplorationVisibility();
            reason=candidate.displayName+" 已加入 · 氧气需求 +"+breathingMolPerSecond.ToString("0.00")+" mol/s";return true;
        }

        public bool SkipPrinting(out string reason)
        {
            InitializeSession();if(!CanUsePrintingPod(out reason))return false;
            var item=catalog==null?null:catalog.FindItem("algae");int amount=12;
            if(item==null){item=catalog==null?null:catalog.FindItem("ore");amount=20;}
            if(item==null||!inventory.TryAdd(item,amount)){reason="仓库空间不足，补给尚未领取";return false;}
            CompletePrintingChoice();reason="已领取 "+(string.IsNullOrEmpty(item.displayName)?item.id:item.displayName)+" ×"+amount;return true;
        }
        bool CanUsePrintingPod(out string reason)
        {
            if(PrintingPod==null){reason="没有人员打印舱";return false;}
            if(!PrintingPod.IsOperational){reason="人员打印舱已关闭";return false;}
            if(PrintCooldownRemaining>0){reason="下一次打印："+PrintCooldownRemaining.ToString("0")+" 秒";return false;}
            reason=string.Empty;return true;
        }
        void CompletePrintingChoice()
        {
            printingGeneration++;nextPrintingTime=SimulationTime+Mathf.Max(1,printingIntervalSeconds);
            OxygenDemandRate=AliveWorkerCount*Mathf.Max(0,breathingMolPerSecond);
        }
        DeepWorker CreateWorkerObject()
        {
            DeepWorker template=workerPrefab;
            if(template==null)foreach(var worker in Workers)if(worker!=null&&(template==null||worker.IsAlive)){template=worker;if(worker.IsAlive)break;}
            DeepWorker result;
            if(template!=null)result=Instantiate(template,world.transform);
            else {var go=new GameObject("人员");go.transform.SetParent(world.transform);result=go.AddComponent<DeepWorker>();}
            result.session=this;result.currentOrder=null;result.SetPath(null);
            result.transform.localRotation=Quaternion.identity;
            result.GetComponent<DeepWorkerPresentation>()?.ResetLivingPose();return result;
        }
        public void DamageWorker(DeepWorker worker,float amount,string cause)
        {
            if(worker==null||!worker.IsAlive||!Workers.Contains(worker)||amount<=0)return;
            worker.health=Mathf.Max(0,worker.health-amount);
            if(worker.IsAlive)return;
            worker.deathCause=string.IsNullOrEmpty(cause)?"伤重":cause;worker.diedAtSeconds=SimulationTime;
            // Return reserved stock and release placement/station ownership immediately.
            foreach(var order in Orders)
            {
                if(order.IsTerminal)continue;
                if(order.worker==worker||order.requestedWorker==worker)CancelOrder(order);
            }
            worker.currentOrder=null;worker.SetPath(null);worker.automationPaused=true;
            worker.environmentUnsafe=false;worker.breathingUnsafe=false;worker.environmentCondition=null;worker.environmentEfficiency=1;
            OxygenDemandRate=AliveWorkerCount*Mathf.Max(0,breathingMolPerSecond);
        }
    }
}

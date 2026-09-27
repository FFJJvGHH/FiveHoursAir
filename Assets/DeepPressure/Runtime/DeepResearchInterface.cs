using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Icon=DeepPressure.DeepUIIcons.Icon;
namespace DeepPressure
{
    public sealed partial class DeepPressureHUD
    {
        string researchQuery="";
        float techSelectionTime;
        void DrawResearchPanel()
        {
            var techs=session.catalog.technologies.Where(t=>t!=null).ToArray();
            if(techs.Length==0)return;
            if(inspectedTech==null)inspectedTech=techs.FirstOrDefault(t=>!session.IsTechUnlocked(t.id))??techs[0];
            float detailWidth=Mathf.Clamp(colonyRect.width*.25f,270,330);
            Rect search=new Rect(colonyRect.x+18,colonyRect.y+53,Mathf.Min(340,colonyRect.width-detailWidth-60),30);
            GUI.SetNextControlName("ResearchSearch");
            researchQuery=GUI.TextField(search,researchQuery,new GUIStyle(GUI.skin.textField){font=interfaceFont,fontSize=12,padding=new RectOffset(10,10,6,4)});
            if(string.IsNullOrEmpty(researchQuery)&&GUI.GetNameOfFocusedControl()!="ResearchSearch")Label(Inset(search,8),"搜索科技、设备或工艺…",small,Muted);
            Label(new Rect(search.xMax+18,search.y,245,30),"点击图标查看方案  ·  Enter 安排研究",small,Muted);
            Rect viewport=new Rect(colonyRect.x+14,search.yMax+18,colonyRect.width-detailWidth-40,colonyRect.height-117);
            Rect details=new Rect(viewport.xMax+16,colonyRect.y+54,detailWidth-4,colonyRect.height-72);
            Fill(new Rect(details.x-8,details.y,1,details.height),Border);
            string[] branches=techs.Select(t=>string.IsNullOrEmpty(t.branch)?"工程基础":t.branch).Distinct().ToArray();
            var depth=new Dictionary<string,int>();foreach(var tech in techs)depth[tech.id]=0;
            for(int pass=0;pass<techs.Length;pass++)foreach(var tech in techs)foreach(string id in tech.prerequisiteIds??Array.Empty<string>())if(depth.TryGetValue(id,out int parent))depth[tech.id]=Mathf.Min(techs.Length,Mathf.Max(depth[tech.id],parent+1));
            int columns=depth.Values.Max()+1;float columnWidth=Mathf.Max(174,(viewport.width-86)/columns),rowHeight=Mathf.Max(146,(viewport.height-24)/Mathf.Max(1,branches.Length));
            var cards=new Dictionary<string,Rect>();var counts=new Dictionary<string,int>();
            foreach(var tech in techs)
            {
                int row=Array.IndexOf(branches,tech.branch),col=depth[tech.id];string key=row+":"+col;
                int sub=counts.TryGetValue(key,out int n)?n:0;counts[key]=sub+1;
                cards[tech.id]=new Rect(92+col*columnWidth,14+Mathf.Max(0,row)*rowHeight+sub*130,columnWidth-26,113);
            }
            var relevant=new HashSet<string>();Action<DeepTechDefinition> ancestors=null;
            ancestors=t=>{if(t==null||!relevant.Add(t.id))return;foreach(string id in t.prerequisiteIds??Array.Empty<string>())ancestors(session.catalog.FindTech(id));};ancestors(inspectedTech);
            Vector2 previousPointer=pointer;researchScroll=GUI.BeginScrollView(viewport,researchScroll,new Rect(0,0,Mathf.Max(viewport.width-17,92+columns*columnWidth),Mathf.Max(viewport.height-17,cards.Values.Max(r=>r.yMax)+25)));
            pointer=previousPointer-viewport.position+researchScroll;
            for(int i=0;i<branches.Length;i++)
            {
                Color branchColor=i==0?Mint:i==1?Amber:new Color(.63f,.70f,.93f);
                Fill(new Rect(0,10+i*rowHeight,80,112),new Color(.05f,.084f,.10f));
                DrawIcon(i==0?Icon.Air:i==1?Icon.Power:Icon.Craft,new Rect(23,25+i*rowHeight,32,32),branchColor);
                Label(new Rect(3,68+i*rowHeight,76,22),branches[i],tiny,branchColor);
            }
            foreach(var tech in techs)foreach(string id in tech.prerequisiteIds??Array.Empty<string>())
            {
                if(!cards.TryGetValue(id,out Rect a))continue;Rect b=cards[tech.id];
                bool focus=relevant.Contains(id)&&relevant.Contains(tech.id);Color line=focus?WithAlpha(Mint,.7f):WithAlpha(Muted,.19f);
                float route=a.xMax+10+(Array.IndexOf(techs,tech)%3)*3;
                Fill(new Rect(a.xMax,a.y+35,route-a.xMax,focus?2:1),line);
                Fill(new Rect(route,Mathf.Min(a.y+35,b.y+35),focus?2:1,Mathf.Abs(b.y-a.y)),line);
                Fill(new Rect(route,b.y+35,b.x-route,focus?2:1),line);
            }
            foreach(var tech in techs)
            {
                Rect r=cards[tech.id];bool complete=session.IsTechUnlocked(tech.id),available=session.CanResearch(tech,out _),selected=tech==inspectedTech;
                var order=session.Orders.FirstOrDefault(o=>!o.IsTerminal&&o.technology==tech);
                bool match=TechMatches(tech,researchQuery);float alpha=match?1:.22f;
                Color accent=complete?Mint:order!=null?Amber:available?White:Muted;
                if(selected){Rounded(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),WithAlpha(Mint,.7f),10);Rounded(new Rect(r.x-1,r.y-1,r.width+2,r.height+2),Panel,9);}
                Rounded(r,WithAlpha(selected?new Color(.09f,.16f,.18f):new Color(.044f,.080f,.095f),alpha),8);
                Rect emblem=new Rect(r.x+12,r.y+10,45,45);SemanticIcon(DeepSemanticIcons.ForTechnology(tech),emblem,WithAlpha(accent,alpha));
                Label(new Rect(r.x+66,r.y+8,r.width-71,45),ShortTech(tech.displayName),new GUIStyle(body){fontSize=12,wordWrap=true},WithAlpha(White,alpha));
                DrawIcon(complete?Icon.Check:order!=null?Icon.Research:available?Icon.Play:Icon.Lock,new Rect(r.x+18,r.y+68,18,18),WithAlpha(accent,alpha));
                float ix=r.x+49;
                foreach(var id in (tech.unlockBuildingIds??Array.Empty<string>()).Take(3))
                {var def=session.catalog.FindBuilding(id);if(def==null)continue;SemanticIcon(DeepSemanticIcons.ForBuilding(def),new Rect(ix,r.y+65,27,27),WithAlpha(accent,alpha));ix+=33;}
                if(ix<r.x+50&&(tech.unlockRecipeIds??Array.Empty<string>()).Length>0)DrawIcon(Icon.Craft,new Rect(ix,r.y+65,27,27),WithAlpha(accent,alpha));
                if(order!=null)Fill(new Rect(r.x+10,r.yMax-7,(r.width-20)*order.Progress,3),Mint);
                if(Click(r)){inspectedTech=tech;techSelectionTime=Time.unscaledTime;GUI.FocusControl(null);}
            }
            GUI.EndScrollView();pointer=previousPointer;
            DrawTechDetail(details,inspectedTech);
            Event e=Event.current;
            if(e.type==EventType.KeyDown&&GUI.GetNameOfFocusedControl()!="ResearchSearch")
            {
                int index=Array.IndexOf(techs,inspectedTech);
                if(e.keyCode==KeyCode.RightArrow||e.keyCode==KeyCode.DownArrow){inspectedTech=techs[(index+1)%techs.Length];e.Use();}
                else if(e.keyCode==KeyCode.LeftArrow||e.keyCode==KeyCode.UpArrow){inspectedTech=techs[(index+techs.Length-1)%techs.Length];e.Use();}
                else if(e.keyCode==KeyCode.Return){bool ok=session.RequestResearch(inspectedTech,out string reason);ShowToast(reason,ok);e.Use();}
            }
        }
        bool TechMatches(DeepTechDefinition tech,string query)
        {
            if(string.IsNullOrWhiteSpace(query))return true;
            string text=tech.displayName+" "+tech.description+" "+tech.branch;
            foreach(string id in tech.unlockBuildingIds??Array.Empty<string>())text+=" "+session.catalog.FindBuilding(id)?.displayName;
            foreach(string id in tech.unlockRecipeIds??Array.Empty<string>())text+=" "+session.catalog.FindRecipe(id)?.displayName;
            return text.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0;
        }
        static string ShortTech(string name){int separator=name.IndexOf('·');return separator>=0?name.Substring(separator+1).Trim():name;}
        void DrawTechDetail(Rect r,DeepTechDefinition tech)
        {
            if(tech==null)return;float x=r.x+14,w=r.width-28,y=r.y+10;
            SemanticIcon(DeepSemanticIcons.ForTechnology(tech),new Rect(x,y,52,52),Mint);
            Label(new Rect(x+66,y+1,w-67,50),ShortTech(tech.displayName),new GUIStyle(title){fontSize=19,wordWrap=true},White);y+=69;
            Label(new Rect(x,y,w,59),tech.description,new GUIStyle(body){wordWrap=true,alignment=TextAnchor.UpperLeft},Muted);y+=65;
            Label(new Rect(x,y,w,25),"解锁工程",small,Mint);y+=32;
            foreach(string id in tech.unlockBuildingIds??Array.Empty<string>())
            {
                var def=session.catalog.FindBuilding(id);if(def==null)continue;
                SemanticIcon(DeepSemanticIcons.ForBuilding(def),new Rect(x,y,33,33),White);Label(new Rect(x+46,y,w-46,30),def.displayName,body,White);y+=42;
            }
            foreach(string id in tech.unlockRecipeIds??Array.Empty<string>())
            {var recipe=session.catalog.FindRecipe(id);if(recipe==null)continue;DrawIcon(Icon.Craft,new Rect(x,y,27,27),Mint);Label(new Rect(x+40,y,w-40,29),recipe.displayName,small,White);y+=35;}
            y+=9;Fill(new Rect(x,y,w,1),Border);y+=13;
            foreach(string id in tech.prerequisiteIds??Array.Empty<string>())
            {
                bool done=session.IsTechUnlocked(id);Rect prerequisite=new Rect(x,y,w,27);DrawIcon(done?Icon.Check:Icon.Lock,new Rect(x,y+4,17,17),done?Mint:Amber);
                Label(new Rect(x+28,y,w-28,27),ShortTech(TechName(id)),small,done?Muted:White);
                if(Click(prerequisite))inspectedTech=session.catalog.FindTech(id);y+=31;
            }
            Label(new Rect(x,y,w,43),CostText(tech.cost),new GUIStyle(small){wordWrap=true},Muted);y+=45;
            bool complete=session.IsTechUnlocked(tech.id),can=session.CanResearch(tech,out string reason);var order=session.Orders.FirstOrDefault(o=>!o.IsTerminal&&o.technology==tech);
            float bottom=Mathf.Max(y,r.yMax-115);
            Label(new Rect(x,bottom,w,48),complete?"相关设施与工艺现已可用":order!=null?order.statusReason:can?"约 "+tech.workSeconds.ToString("0")+"秒 · 由工程员现场研究":reason,new GUIStyle(small){wordWrap=true},can||complete?Mint:Amber);
            Rect button=new Rect(x,bottom+55,w,39);ActionButton(button,complete?Icon.Check:Icon.Research,complete?"已掌握":order!=null?"研究中 "+(order.Progress*100).ToString("0")+"%":"安排研究",can);
            if(Click(button)&&can){bool ok=session.RequestResearch(tech,out string result);ShowToast(result,ok);}
            if(order!=null){Rect cancel=new Rect(x,bottom+99,w,23);SmallButton(cancel,"取消并返还预留材料",()=>{bool ok=session.CancelOrder(order,out string result);ShowToast(result,ok);});}
        }
    }
}

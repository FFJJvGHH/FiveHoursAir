using System.Collections.Generic;
using UnityEngine;

namespace DeepPressure
{
    /// <summary>Dedicated, code-native engineering symbols, legible at 24–32 px.
    /// World artwork stays on building definitions; UI never squeezes a padded machine render into a tiny button.</summary>
    public static class DeepSemanticIcons
    {
        static readonly Dictionary<string,Texture2D> cache=new Dictionary<string,Texture2D>();
        public static Texture2D ForTechnology(DeepTechDefinition technology)=>Get(technology==null?"research":technology.id);
        public static Texture2D ForBuilding(DeepBuildingDefinition building)
        {
            if(building==null)return Get("build");
            return Get(building.id);
        }
        public static Texture2D Get(string id)
        {
            id=id??"research";
            if(cache.TryGetValue(id,out var result)&&result!=null)return result;
            var c=new Symbol();
            switch(id)
            {
                case "survey_basics":
                    c.Circle(27,38,16);c.Line(39,26,52,12,5);c.Line(17,37,23,43);c.Line(23,43,29,32);c.Line(29,32,38,41);break;
                case "pressure_engineering":case "gas_regulator":
                    c.Arc(32,31,21,-32,212);c.Line(14,14,50,14);c.Line(32,31,43,43,5);c.Disc(32,31,4);c.Line(13,42,17,39);c.Line(32,51,32,46);break;
                case "selective_separation":case "gas_separator":case "co2_scrubber":
                    c.Line(10,32,25,32,5);c.Line(25,32,40,47,5);c.Line(25,32,40,17,5);c.Line(40,47,53,47,5);c.Line(40,17,53,17,5);c.Circle(43,17,6);c.Disc(43,47,6);break;
                case "colony_planning":
                    c.Box(11,14,19,17);c.Box(35,14,19,17);c.Box(22,39,19,15);c.Line(20,32,20,38);c.Line(20,38,43,38);c.Line(43,38,43,32);break;
                case "power_distribution":case "generator":case "improved_generator":
                    c.Path(4,33,55,23,37,34,37,29,20,44,40,34,40,33,55);c.Line(14,28,14,12);c.Line(14,12,51,12);c.Line(51,12,51,28);c.Disc(14,28,4);c.Disc(51,28,4);break;
                case "deep_support":
                    c.Box(16,14,32,34);c.Line(23,20,23,42);c.Line(32,20,32,42);c.Line(41,20,41,42);c.Line(11,10,53,10,5);c.Path(4,25,54,32,59,39,54);break;
                case "material_processing":
                    c.Path(4,11,21,17,39,37,47,51,33,45,15,11,21);c.Line(17,39,29,27);c.Line(29,27,51,33);c.Line(29,27,45,15);c.Line(14,53,27,53);c.Line(20,47,20,59);break;
                case "advanced_fabrication":case "research_bench":
                    c.Box(19,19,26,26);c.Box(26,26,12,12);for(int k=0;k<3;k++){float v=23+k*9;c.Line(v,10,v,18);c.Line(v,46,v,54);c.Line(10,v,18,v);c.Line(46,v,54,v);}break;
                case "industrial_efficiency":case "precision_fabricator":
                    c.Arc(32,32,20,30,320);c.Path(4,40,50,51,50,51,39);c.Path(4,35,46,23,28,33,28,29,17,43,35,33,35);break;
                case "gas_tank":case "high_pressure_tank":case "oxygen_tank":case "waste_tank":case "battery":
                    c.Box(18,12,28,39);c.Line(24,56,40,56,5);c.Line(23,21,41,21);c.Line(23,30,41,30);c.Line(23,39,41,39);break;
                case "storage":case "advanced_storage":case "deep_storage":
                    c.Box(11,13,42,38);c.Line(11,32,53,32);c.Line(25,42,39,42,5);c.Line(25,23,39,23,5);break;
                case "fabricator":
                    c.Path(4,11,13,11,44,25,35,37,44,37,31,53,31,53,13,11,13);c.Line(46,41,46,55,6);c.Line(19,23,25,23);c.Line(34,23,40,23);break;
                case "lamp":
                    c.Line(14,48,50,48,5);c.Line(20,40,44,40,5);c.Line(32,33,32,13);c.Line(21,31,14,18);c.Line(43,31,50,18);break;
                case "ladder":
                    c.Line(20,9,20,55,5);c.Line(44,9,44,55,5);for(int y=16;y<54;y+=10)c.Line(20,y,44,y,4);break;
                case "floor":
                    c.Box(9,22,46,20);c.Line(24,23,24,41);c.Line(40,23,40,41);c.Line(10,15,54,15);break;
                case "wire":
                    c.Line(10,42,29,42,5);c.Line(29,42,29,21,5);c.Line(29,21,52,21,5);c.Circle(10,42,5);c.Circle(52,21,5);break;
                case "oxygen_vent":case "air_vent":case "supply_vent":case "exhaust_vent":
                    c.Box(10,16,20,32);for(int y=24;y<=40;y+=8)c.Line(14,y,25,y);c.Line(36,38,54,38);c.Line(36,25,50,25);c.Path(3,48,44,54,38,48,32);break;
                case "intake_pump":
                    c.Circle(34,33,18);c.Line(8,33,24,33,5);c.Path(4,18,40,25,33,18,26);c.Line(48,22,56,22,5);c.Line(23,11,48,11,5);break;
                default:return DeepUIIcons.Get(DeepUIIcons.Icon.Research);
            }
            result=new Texture2D(64,64,TextureFormat.RGBA32,false,true){name="Engineering symbol • "+id,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            result.SetPixels(c.pixels);result.Apply(false,true);cache[id]=result;return result;
        }
        sealed class Symbol
        {
            public readonly Color[] pixels=new Color[4096];
            public void Line(float ax,float ay,float bx,float by,float width=4)
            {
                Vector2 a=new Vector2(ax,ay),d=new Vector2(bx-ax,by-ay);float length=d.sqrMagnitude;
                for(int y=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(ay,by)-width));y<Mathf.Min(64,Mathf.CeilToInt(Mathf.Max(ay,by)+width));y++)
                    for(int x=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(ax,bx)-width));x<Mathf.Min(64,Mathf.CeilToInt(Mathf.Max(ax,bx)+width));x++)
                    {
                        Vector2 p=new Vector2(x+.5f,y+.5f);float t=length==0?0:Mathf.Clamp01(Vector2.Dot(p-a,d)/length);
                        Put(x,y,Mathf.Clamp01(width*.5f+.5f-Vector2.Distance(p,a+d*t)));
                    }
            }
            public void Path(float width,params float[] p){for(int i=2;i+1<p.Length;i+=2)Line(p[i-2],p[i-1],p[i],p[i+1],width);}
            public void Box(float x,float y,float w,float h)=>Path(4,x,y,x+w,y,x+w,y+h,x,y+h,x,y);
            public void Arc(float x,float y,float r,float start,float end)
            {
                int n=Mathf.CeilToInt(Mathf.Abs(end-start)/7);Vector2 last=new Vector2(x+Mathf.Cos(start*Mathf.Deg2Rad)*r,y+Mathf.Sin(start*Mathf.Deg2Rad)*r);
                for(int i=1;i<=n;i++){float a=Mathf.Lerp(start,end,(float)i/n)*Mathf.Deg2Rad;Vector2 next=new Vector2(x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r);Line(last.x,last.y,next.x,next.y);last=next;}
            }
            public void Circle(float x,float y,float r)=>Arc(x,y,r,0,360);
            public void Disc(float x,float y,float r){for(int j=0;j<64;j++)for(int i=0;i<64;i++)Put(i,j,Mathf.Clamp01(r+.5f-Vector2.Distance(new Vector2(i+.5f,j+.5f),new Vector2(x,y))));}
            void Put(int x,int y,float a){if(a>pixels[y*64+x].a)pixels[y*64+x]=new Color(1,1,1,a);}
        }
    }
}

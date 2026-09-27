using System.Collections.Generic;
using UnityEngine;

namespace DeepPressure
{
    /// <summary>Small, anti-aliased geometric interface symbols. No font glyphs or external icon dependencies.</summary>
    public static class DeepUIIcons
    {
        public enum Icon { Mark, Air, Pressure, Layers, Sample, Explore, Shield, Pause, Play, Speed, Close, Temperature, Volume, Flow, Valve, Check, Warning, Drop, Build, Dig, People, Person, Research, Lock, Material, Power, Storage, Craft, Settings, Home, Lamp }
        static readonly Dictionary<Icon, Texture2D> Icons = new Dictionary<Icon, Texture2D>();
        static Texture2D rounded;
        public static Texture2D Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int size = 64;
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    Vector2 q = new Vector2(Mathf.Abs(x + .5f - 32) - 20, Mathf.Abs(y + .5f - 32) - 20);
                    float d = new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude + Mathf.Min(Mathf.Max(q.x,q.y),0) - 11;
                    pixels[y * size + x] = new Color(1,1,1,Mathf.Clamp01(.5f - d));
                }
                rounded = Texture(pixels,size,"Deep Pressure rounded UI"); return rounded;
            }
        }
        public static Texture2D Get(Icon icon)
        {
            if (Icons.TryGetValue(icon,out Texture2D result) && result != null) return result;
            var c = new Canvas();
            switch (icon)
            {
                case Icon.Mark:
                    c.Path(3.1f,new Vector2(14,48),new Vector2(32,54),new Vector2(50,48));
                    c.Path(3.1f,new Vector2(14,36),new Vector2(32,42),new Vector2(50,36));
                    c.Path(3.1f,new Vector2(14,24),new Vector2(32,30),new Vector2(50,24));
                    c.Line(32,10,32,20,3.1f); c.Disk(32,7,2); break;
                case Icon.Air:
                    c.Line(10,40,43,40); c.Arc(43,46,6,-90,180);
                    c.Line(9,30,50,30); c.Arc(50,24,6,90,-180);
                    c.Line(15,20,31,20); c.Arc(31,14,6,90,-130); break;
                case Icon.Pressure:
                    c.Arc(32,29,21,-35,215); c.Line(14,14,50,14);
                    c.Line(32,29,43,42,3.4f); c.Disk(32,29,3.5f);
                    c.Line(17,43,20,40,2); c.Line(32,49,32,45,2); c.Line(47,43,44,40,2); break;
                case Icon.Layers:
                    c.Path(2.8f,new Vector2(11,39),new Vector2(32,50),new Vector2(53,39),new Vector2(32,28),new Vector2(11,39));
                    c.Path(2.8f,new Vector2(12,29),new Vector2(32,18),new Vector2(52,29));
                    c.Path(2.8f,new Vector2(12,19),new Vector2(32,8),new Vector2(52,19)); break;
                case Icon.Sample:
                    c.Path(3,new Vector2(23,50),new Vector2(43,50));
                    c.Path(3,new Vector2(27,48),new Vector2(27,22));
                    c.Path(3,new Vector2(39,48),new Vector2(39,22));
                    c.Arc(33,22,6,180,360); c.Line(28,29,38,29,2.5f);
                    c.Line(34,39,39,39,2); c.Line(34,34,39,34,2);
                    c.Path(2.5f,new Vector2(16,26),new Vector2(12,18),new Vector2(13,13),new Vector2(17,11),new Vector2(21,14),new Vector2(21,18),new Vector2(16,26)); break;
                case Icon.Explore:
                    c.Circle(28,36,16,3); c.Line(40,24,52,12,4);
                    c.Line(28,28,28,44,2.5f); c.Line(20,36,36,36,2.5f); break;
                case Icon.Shield:
                    c.Path(2.8f,new Vector2(32,53),new Vector2(49,46),new Vector2(47,29),new Vector2(42,18),new Vector2(32,10),new Vector2(22,18),new Vector2(17,29),new Vector2(15,46),new Vector2(32,53));
                    c.Path(3,new Vector2(23,32),new Vector2(30,25),new Vector2(41,39)); break;
                case Icon.Pause:
                    c.Line(24,17,24,47,5); c.Line(40,17,40,47,5); break;
                case Icon.Play:
                    c.Path(3.2f,new Vector2(24,16),new Vector2(24,48),new Vector2(49,32),new Vector2(24,16)); break;
                case Icon.Speed:
                    c.Path(3.5f,new Vector2(13,17),new Vector2(29,32),new Vector2(13,47));
                    c.Path(3.5f,new Vector2(34,17),new Vector2(50,32),new Vector2(34,47)); break;
                case Icon.Close:
                    c.Line(21,21,43,43,3); c.Line(21,43,43,21,3); break;
                case Icon.Temperature:
                    c.Path(3,new Vector2(27,27),new Vector2(27,47)); c.Path(3,new Vector2(37,27),new Vector2(37,47));
                    c.Arc(32,47,5,0,180); c.Arc(32,20,9,125,415);
                    c.Line(32,19,32,39,3); c.Disk(32,20,4); c.Line(42,39,47,39,2); c.Line(42,32,47,32,2); break;
                case Icon.Volume:
                    c.Path(2.7f,new Vector2(13,43),new Vector2(32,53),new Vector2(51,43),new Vector2(51,22),new Vector2(32,11),new Vector2(13,22),new Vector2(13,43),new Vector2(32,32),new Vector2(51,43));
                    c.Line(32,32,32,11,2.7f); break;
                case Icon.Flow:
                    c.Line(11,40,50,40,3); c.Path(3,new Vector2(41,49),new Vector2(50,40),new Vector2(41,31));
                    c.Line(53,22,14,22,3); c.Path(3,new Vector2(23,31),new Vector2(14,22),new Vector2(23,13)); break;
                case Icon.Valve:
                    c.Circle(32,28,15,2.8f); c.Line(7,28,17,28,3); c.Line(47,28,57,28,3);
                    c.Line(32,43,32,52,3); c.Line(21,53,43,53,3); c.Line(23,19,41,37,2.8f); break;
                case Icon.Check:
                    c.Path(4,new Vector2(15,31),new Vector2(27,19),new Vector2(49,45)); break;
                case Icon.Warning:
                    c.Path(2.8f,new Vector2(32,52),new Vector2(55,13),new Vector2(9,13),new Vector2(32,52));
                    c.Line(32,36,32,26,3); c.Disk(32,20,1.8f); break;
                case Icon.Drop:
                    c.Path(2.8f,new Vector2(32,53),new Vector2(18,31),new Vector2(16,24));
                    c.Arc(32,24,16,180,360); c.Path(2.8f,new Vector2(48,24),new Vector2(46,31),new Vector2(32,53)); break;
                case Icon.Build:
                    c.Path(3,new Vector2(11,13),new Vector2(11,39),new Vector2(31,53),new Vector2(52,39),new Vector2(52,13),new Vector2(11,13));
                    c.Line(32,19,32,37);c.Line(23,28,41,28);break;
                case Icon.Dig:
                    c.Line(17,10,44,48,5);c.Path(3,new Vector2(15,48),new Vector2(27,54),new Vector2(41,51),new Vector2(53,37));break;
                case Icon.Person:
                    c.Circle(32,44,8);c.Arc(32,17,18,0,180);c.Line(14,17,50,17);break;
                case Icon.People:
                    c.Circle(25,43,7);c.Circle(45,39,6);c.Arc(24,17,16,0,180);c.Arc(45,17,12,0,150);c.Line(9,17,56,17);break;
                case Icon.Research:
                    c.Circle(32,32,9);c.Arc(32,32,23,20,160,2);c.Arc(32,32,23,200,340,2);c.Line(10,17,53,47,2);c.Line(11,47,53,17,2);c.Disk(52,46,3);c.Disk(13,18,3);break;
                case Icon.Lock:
                    c.Path(3,new Vector2(17,14),new Vector2(47,14),new Vector2(47,37),new Vector2(17,37),new Vector2(17,14));c.Arc(32,40,10,0,180);c.Line(22,37,22,40);c.Line(42,37,42,40);c.Disk(32,27,3);c.Line(32,27,32,22);break;
                case Icon.Material:
                    c.Path(3,new Vector2(12,20),new Vector2(18,45),new Vector2(38,52),new Vector2(52,36),new Vector2(46,14),new Vector2(12,20));c.Path(2,new Vector2(18,45),new Vector2(32,29),new Vector2(52,36));c.Line(32,29,46,14,2);break;
                case Icon.Power:
                    c.Path(3,new Vector2(35,54),new Vector2(17,30),new Vector2(31,30),new Vector2(27,10),new Vector2(49,37),new Vector2(35,37),new Vector2(35,54));break;
                case Icon.Storage:
                    c.Path(3,new Vector2(11,16),new Vector2(53,16),new Vector2(53,47),new Vector2(11,47),new Vector2(11,16));c.Line(11,36,53,36);c.Line(27,41,37,41);c.Line(27,26,37,26);break;
                case Icon.Craft:
                    c.Path(3,new Vector2(13,14),new Vector2(13,45),new Vector2(25,37),new Vector2(37,46),new Vector2(37,33),new Vector2(51,33),new Vector2(51,14),new Vector2(13,14));c.Line(43,43,43,55,4);c.Line(20,23,25,23);c.Line(33,23,38,23);break;
                case Icon.Settings:
                    c.Circle(32,32,15);c.Circle(32,32,5);for(int i=0;i<8;i++){float a=i*Mathf.PI/4;c.Line(32+Mathf.Cos(a)*16,32+Mathf.Sin(a)*16,32+Mathf.Cos(a)*23,32+Mathf.Sin(a)*23,5);}break;
                case Icon.Home:
                    c.Path(3,new Vector2(9,33),new Vector2(32,53),new Vector2(55,33));c.Path(3,new Vector2(16,34),new Vector2(16,12),new Vector2(48,12),new Vector2(48,34));c.Path(3,new Vector2(27,12),new Vector2(27,28),new Vector2(37,28),new Vector2(37,12));break;
                case Icon.Lamp:
                    c.Arc(32,38,15,-30,210);c.Path(3,new Vector2(19,30),new Vector2(25,20),new Vector2(39,20),new Vector2(45,30));c.Line(25,14,39,14);c.Line(32,57,32,62,2);c.Line(10,44,5,46,2);c.Line(54,44,59,46,2);break;
            }
            result = Texture(c.pixels,64,"Deep Pressure icon " + icon); Icons[icon] = result; return result;
        }
        static Texture2D Texture(Color[] pixels,int size,string name)
        {
            var texture = new Texture2D(size,size,TextureFormat.RGBA32,false,true) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels(pixels); texture.Apply(false,true); return texture;
        }
        sealed class Canvas
        {
            public readonly Color[] pixels = new Color[64 * 64];
            public void Line(float ax,float ay,float bx,float by,float width = 3)
            {
                Vector2 a = new Vector2(ax,ay), delta = new Vector2(bx-ax,by-ay); float length = delta.sqrMagnitude;
                for (int y = Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(ay,by)-width)); y < Mathf.Min(64,Mathf.CeilToInt(Mathf.Max(ay,by)+width)); y++)
                    for (int x = Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(ax,bx)-width)); x < Mathf.Min(64,Mathf.CeilToInt(Mathf.Max(ax,bx)+width)); x++)
                    {
                        Vector2 p = new Vector2(x+.5f,y+.5f); float t = length == 0 ? 0 : Mathf.Clamp01(Vector2.Dot(p-a,delta)/length);
                        Put(x,y,Mathf.Clamp01(width*.5f+.5f-Vector2.Distance(p,a+delta*t)));
                    }
            }
            public void Path(float width,params Vector2[] points) { for (int i = 1; i < points.Length; i++) Line(points[i-1].x,points[i-1].y,points[i].x,points[i].y,width); }
            public void Arc(float x,float y,float radius,float start,float end,float width = 3)
            {
                int steps = Mathf.Max(2,Mathf.CeilToInt(Mathf.Abs(end-start)/7)); Vector2 previous = new Vector2(x+Mathf.Cos(start*Mathf.Deg2Rad)*radius,y+Mathf.Sin(start*Mathf.Deg2Rad)*radius);
                for (int i = 1; i <= steps; i++)
                {
                    float angle = Mathf.Lerp(start,end,(float)i/steps)*Mathf.Deg2Rad; Vector2 next = new Vector2(x+Mathf.Cos(angle)*radius,y+Mathf.Sin(angle)*radius);
                    Line(previous.x,previous.y,next.x,next.y,width); previous = next;
                }
            }
            public void Circle(float x,float y,float radius,float width = 3) => Arc(x,y,radius,0,360,width);
            public void Disk(float cx,float cy,float radius)
            {
                for (int y = Mathf.Max(0,Mathf.FloorToInt(cy-radius-1)); y < Mathf.Min(64,Mathf.CeilToInt(cy+radius+1)); y++)
                    for (int x = Mathf.Max(0,Mathf.FloorToInt(cx-radius-1)); x < Mathf.Min(64,Mathf.CeilToInt(cx+radius+1)); x++)
                        Put(x,y,Mathf.Clamp01(radius+.5f-Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(cx,cy))));
            }
            void Put(int x,int y,float alpha) { int index = y*64+x; if (alpha > pixels[index].a) pixels[index] = new Color(1,1,1,alpha); }
        }
    }
}

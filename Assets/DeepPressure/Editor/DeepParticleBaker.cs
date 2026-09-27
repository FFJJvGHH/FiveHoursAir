using System.IO;
using UnityEditor;
using UnityEngine;
namespace DeepPressure.Editor
{
    public static class DeepParticleBaker
    {
        const string Folder="Assets/DeepPressure/Art/Feedback";
        public static void Attach(DeepPressureWorld world)
        {
            Directory.CreateDirectory(Folder);
            var feedback=world.GetComponent<DeepParticleFeedback>()??world.gameObject.AddComponent<DeepParticleFeedback>();
            feedback.sparkPrefab=Create("ConstructionSparks",false,false);
            feedback.dustPrefab=Create("MiningDust",true,false);
            feedback.pulsePrefab=Create("CommandPulse",false,true);
        }
        static ParticleSystem Create(string name,bool dust,bool ring)
        {
            string texturePath=Folder+"/"+(ring?"Ring":"Soft")+".png";
            if(!File.Exists(texturePath))
            {
                var t=new Texture2D(64,64,TextureFormat.RGBA32,false);
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)
                {float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),Vector2.one*32)/32;float a=ring?Mathf.Exp(-Mathf.Pow((d-.75f)*27,2)):Mathf.Exp(-d*d*6)*Mathf.Clamp01(1-d);t.SetPixel(x,y,new Color(1,1,1,a));}
                t.Apply();File.WriteAllBytes(texturePath,t.EncodeToPNG());Object.DestroyImmediate(t);AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
            }
            string materialPath=Folder+"/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(mat==null){mat=new Material(Shader.Find("DeepPressure/FeedbackParticle"));AssetDatabase.CreateAsset(mat,materialPath);}
            mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);mat.SetFloat("_DstBlend",dust?10:1);
            string path=Folder+"/"+name+".prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing.GetComponent<ParticleSystem>();
            var go=new GameObject(name);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=false;main.playOnAwake=false;main.duration=.15f;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startLifetime=ring?.85f:dust?.8f:.45f;main.startSpeed=ring?0:dust?.65f:2.1f;main.maxParticles=64;
            main.startSize=ring?.8f:dust?.18f:.07f;main.gravityModifier=ring?0:dust?-.08f:.32f;
            var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)(ring?1:dust?12:15))});
            var shape=ps.shape;shape.enabled=!ring;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=.05f;
            var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.1f),new GradientAlphaKey(0,1)});color.color=gradient;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,ring?.25f:.8f,1,ring?4:dust?2:.05f));
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=mat;renderer.sortingOrder=40;
            var saved=PrefabUtility.SaveAsPrefabAsset(go,path);Object.DestroyImmediate(go);AssetDatabase.SaveAssets();return saved.GetComponent<ParticleSystem>();
        }
    }
}

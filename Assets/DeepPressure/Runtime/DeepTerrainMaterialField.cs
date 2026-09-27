using UnityEngine;
using UnityEngine.Tilemaps;

namespace DeepPressure
{
    /// <summary>Shares editable tile materials with the terrain shader so strata seams can wander across the logical grid.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class DeepTerrainMaterialField : MonoBehaviour
    {
        public DeepPressureWorld world;
        Texture2D palette;
        TilemapRenderer terrainRenderer;
        MaterialPropertyBlock properties;
        bool dirty = true;
        static readonly int PaletteId = Shader.PropertyToID("_TerrainPaletteTex");
        static readonly int EnabledId = Shader.PropertyToID("_TerrainBlendEnabled");
        static readonly int SizeId = Shader.PropertyToID("_TerrainMapSize");
        static readonly int MatrixId = Shader.PropertyToID("_TerrainWorldToLocal");

        void OnEnable() { Tilemap.tilemapTileChanged += TilesChanged; dirty=true; }
        void OnDisable()
        {
            Tilemap.tilemapTileChanged -= TilesChanged;
            if(terrainRenderer!=null)
            {
                if(properties==null)properties=new MaterialPropertyBlock();
                terrainRenderer.GetPropertyBlock(properties);properties.SetFloat(EnabledId,0);terrainRenderer.SetPropertyBlock(properties);
            }
            ReleaseTexture();
        }
        void OnValidate() { dirty=true; }
        void TilesChanged(Tilemap map, Tilemap.SyncTile[] changes) { if(world!=null && map==world.terrain)dirty=true; }
        void LateUpdate() { RefreshField(); }
        public void RefreshField()
        {
            if(world==null)world=GetComponent<DeepPressureWorld>();
            if(world==null || world.terrain==null)return;
            if(terrainRenderer==null)terrainRenderer=world.terrain.GetComponent<TilemapRenderer>();
            if(terrainRenderer==null)return;
            if(palette==null || palette.width!=world.width || palette.height!=world.height)
            {
                ReleaseTexture();
                palette=new Texture2D(world.width,world.height,TextureFormat.RGBA32,false,true)
                { name="Terrain material palette field",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave };
                dirty=true;
            }
            if(dirty)
            {
                var colors=new Color32[world.width*world.height];
                for(int y=0;y<world.height;y++)for(int x=0;x<world.width;x++)
                {
                    var tile=world.terrain.GetTile<DeepTerrainTile>(new Vector3Int(x,y,0));
                    colors[y*world.width+x]=MaterialColor(tile==null?TerrainKind.Empty:tile.kind);
                }
                palette.SetPixels32(colors);palette.Apply(false,false);dirty=false;
            }
            if(properties==null)properties=new MaterialPropertyBlock();
            terrainRenderer.GetPropertyBlock(properties);
            properties.SetTexture(PaletteId,palette);properties.SetFloat(EnabledId,1);
            properties.SetVector(SizeId,new Vector4(world.width,world.height,world.cellSize,0));
            properties.SetMatrix(MatrixId,world.transform.worldToLocalMatrix);terrainRenderer.SetPropertyBlock(properties);
        }
        static Color MaterialColor(TerrainKind kind)
        {
            Color color;
            switch(kind)
            {
                case TerrainKind.Soil: color=new Color(.38f,.295f,.245f);break;
                case TerrainKind.Sandstone: color=new Color(.48f,.405f,.315f);break;
                case TerrainKind.Shale: color=new Color(.335f,.305f,.415f);break;
                case TerrainKind.Basalt: color=new Color(.275f,.37f,.415f);break;
                default:return Color.clear;
            }
            return color.linear;
        }
        void ReleaseTexture()
        {
            if(palette==null)return;
            if(Application.isPlaying)Destroy(palette);else DestroyImmediate(palette);
            palette=null;
        }
    }
}

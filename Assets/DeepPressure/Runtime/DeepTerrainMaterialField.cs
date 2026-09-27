using UnityEngine;
using UnityEngine.Tilemaps;

namespace DeepPressure
{
    /// <summary>Shares editable tile materials with the terrain shader so strata seams can wander across the logical grid.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class DeepTerrainMaterialField : MonoBehaviour
    {
        public DeepPressureWorld world;
        Texture2D palette,sourcePalette;
        TilemapRenderer terrainRenderer;
        MaterialPropertyBlock properties;
        bool dirty = true;
        static readonly int PaletteId = Shader.PropertyToID("_TerrainPaletteTex");
        static readonly int SourcePaletteId = Shader.PropertyToID("_TerrainSourcePaletteTex");
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
            if(palette==null || sourcePalette==null || palette.width!=world.width || palette.height!=world.height)
            {
                ReleaseTexture();
                palette=new Texture2D(world.width,world.height,TextureFormat.RGBA32,false,true)
                { name="Terrain material palette field",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave };
                sourcePalette=new Texture2D(world.width,world.height,TextureFormat.RGBA32,false,true)
                { name="Authored terrain source palette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave };
                dirty=true;
            }
            if(dirty)
            {
                var colors=new Color32[world.width*world.height];
                var sourceColors=new Color32[colors.Length];
                for(int y=0;y<world.height;y++)for(int x=0;x<world.width;x++)
                {
                    var tile=world.terrain.GetTile<DeepTerrainTile>(new Vector3Int(x,y,0));
                    var kind=tile==null?TerrainKind.Empty:tile.kind;
                    colors[y*world.width+x]=DeepArtPalette.Terrain(kind).linear;
                    sourceColors[y*world.width+x]=DeepArtPalette.SourceTerrain(kind).linear;
                }
                palette.SetPixels32(colors);palette.Apply(false,false);
                sourcePalette.SetPixels32(sourceColors);sourcePalette.Apply(false,false);dirty=false;
            }
            if(properties==null)properties=new MaterialPropertyBlock();
            terrainRenderer.GetPropertyBlock(properties);
            properties.SetTexture(PaletteId,palette);properties.SetFloat(EnabledId,1);
            properties.SetTexture(SourcePaletteId,sourcePalette);
            properties.SetVector(SizeId,new Vector4(world.width,world.height,world.cellSize,0));
            properties.SetMatrix(MatrixId,world.transform.worldToLocalMatrix);terrainRenderer.SetPropertyBlock(properties);
        }
        void ReleaseTexture()
        {
            if(palette==null)return;
            if(Application.isPlaying)Destroy(palette);else DestroyImmediate(palette);
            palette=null;
            if(sourcePalette!=null){if(Application.isPlaying)Destroy(sourcePalette);else DestroyImmediate(sourcePalette);sourcePalette=null;}
        }
    }
}

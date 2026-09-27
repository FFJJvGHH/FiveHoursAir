using UnityEngine;

namespace DeepPressure
{
    /// <summary>World art roles, in sRGB. Terrain structure and all normal/AO data stay authored.</summary>
    public static class DeepArtPalette
    {
        public static readonly Color MachineTint=new Color(.96f,.99f,.96f);
        public static readonly Color AmbientLight=new Color(.79f,.86f,.83f);
        public static readonly Color WorkLight=new Color(.86f,.91f,.82f);
        public static readonly Color Activity=new Color(.49f,.79f,.68f);
        public static readonly Color Confirmation=new Color(.90f,.79f,.53f);
        public static readonly Color Warning=new Color(.91f,.53f,.32f);
        public static Color SourceTerrain(TerrainKind kind)
        {
            switch(kind)
            {
                case TerrainKind.Soil:return new Color(.38f,.295f,.245f);
                case TerrainKind.Sandstone:return new Color(.48f,.405f,.315f);
                case TerrainKind.Shale:return new Color(.335f,.305f,.415f);
                case TerrainKind.Basalt:return new Color(.275f,.37f,.415f);
                default:return Color.clear;
            }
        }
        public static Color Terrain(TerrainKind kind)
        {
            switch(kind)
            {
                case TerrainKind.Soil:return new Color(.43f,.395f,.335f); // warm mineral soil
                case TerrainKind.Sandstone:return new Color(.52f,.48f,.385f); // lighter ochre strata
                case TerrainKind.Shale:return new Color(.30f,.36f,.36f); // slate, not purple
                case TerrainKind.Basalt:return new Color(.235f,.305f,.325f); // deepest blue-grey
                default:return Color.clear;
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace DeepPressure
{
    [CreateAssetMenu(menuName = "Deep Pressure/Terrain tile")]
    public class DeepTerrainTile : TileBase
    {
        public TerrainKind kind;
        public Sprite[] variants = new Sprite[16];
        [Tooltip("Each variation contains 47 blob masks, or 16 legacy cardinal masks. Chosen by stable world-cell hash.")]
        [Min(1)] public int variationCount = 1;
        public int variationSeed = 1709;
        [Tooltip("47 blob masks include all four diagonals, so inside corners respond to excavation.")]
        public bool useDiagonalConnections;
        [Tooltip("Solid neighbors of another material keep the common edge filled, avoiding dark grid seams.")]
        public bool connectAcrossMaterials;
        [Header("Independent design parameters, not real geology")]
        public float densityKgM3 = 2000;
        public float excavationResistance = 1;
        [Range(0, 1)] public float porosity = .1f;
        public float permeability = .01f;
        static readonly Vector3Int[] Directions = { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left,
            new Vector3Int(1,1,0), new Vector3Int(1,-1,0), new Vector3Int(-1,-1,0), new Vector3Int(-1,1,0) };
        public static readonly int[] BlobMasks = CreateBlobMasks();
        static readonly int[] BlobIndices = CreateBlobIndices();
        public static int NormalizeBlobMask(int mask)
        {
            if ((mask & 3) != 3) mask &= ~16;
            if ((mask & 6) != 6) mask &= ~32;
            if ((mask & 12) != 12) mask &= ~64;
            if ((mask & 9) != 9) mask &= ~128;
            return mask & 255;
        }
        public static int BlobIndex(int mask) => BlobIndices[NormalizeBlobMask(mask)];
        static int[] CreateBlobMasks()
        {
            var result = new List<int>();
            for (int mask = 0; mask < 256; mask++) if (NormalizeBlobMask(mask) == mask) result.Add(mask);
            return result.ToArray();
        }
        static int[] CreateBlobIndices()
        {
            var result = new int[256];
            for (int index = 0; index < BlobMasks.Length; index++) result[BlobMasks[index]] = index;
            return result;
        }
        public int ConnectionMask(Vector3Int position, ITilemap map)
        {
            int mask = 0;
            for (int i = 0; i < (useDiagonalConnections ? 8 : 4); i++)
            {
                var neighbor = map.GetTile<DeepTerrainTile>(position + Directions[i]);
                if (neighbor != null && (neighbor.kind == kind || (connectAcrossMaterials && neighbor.kind != TerrainKind.Empty))) mask |= 1 << i;
            }
            return useDiagonalConnections ? NormalizeBlobMask(mask) : mask;
        }
        public override void RefreshTile(Vector3Int position, ITilemap map)
        {
            map.RefreshTile(position);
            foreach (var direction in Directions) map.RefreshTile(position + direction);
        }
        public override void GetTileData(Vector3Int position, ITilemap map, ref TileData data)
        {
            int mask = ConnectionMask(position, map);
            int masksPerVariation = useDiagonalConnections ? BlobMasks.Length : 16;
            int count = variants == null ? 0 : Mathf.Min(Mathf.Max(1, variationCount), variants.Length / masksPerVariation);
            uint hash = StableCellHash(position, variationSeed);
            int spriteIndex = (useDiagonalConnections ? BlobIndex(mask) : mask) + (count > 0 ? (int)(hash % (uint)count) * masksPerVariation : 0);
            data.sprite = variants != null && variants.Length > spriteIndex ? variants[spriteIndex] : null;
            data.color = Color.white;
            data.transform = Matrix4x4.identity;
            data.flags = TileFlags.LockAll;
            data.colliderType = Tile.ColliderType.Grid;
        }
        public static uint StableCellHash(Vector3Int cell, int seed)
        {
            unchecked
            {
                uint value = (uint)cell.x * 374761393u + (uint)cell.y * 668265263u + (uint)seed * 2246822519u;
                value = (value ^ (value >> 13)) * 1274126177u;
                return value ^ (value >> 16);
            }
        }
    }
}

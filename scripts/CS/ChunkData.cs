using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Pure C# data container for a single chunk.
/// Uses Palette Compression to store voxel data efficiently.
/// </summary>
public class ChunkData
{
    public const int CHUNK_SIZE = 16;
    public const int CHUNK_VOLUME = CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE;

    // Block types - must match GDScript enum for now
    public enum BlockType
    {
        AIR = 0,
        DIRT = 1,
        GRASS = 2,
        STONE = 3,
        SAND = 4,
        SNOW = 5,
        ICE = 6
    }

    // The Palette stores the unique block types used in this chunk.
    // Index 0 is always AIR (0).
    public List<int> Palette { get; private set; }

    // The Indices array stores the index into the Palette for each voxel.
    // We use byte, which supports up to 256 unique block types per chunk.
    // If a chunk needs more than 256 types, we would need to upgrade to ushort,
    // but for a voxel game, 256 unique blocks *in a single 16x16x16 chunk* is extremely rare.
    public byte[] Indices { get; private set; }

    public ChunkData()
    {
        Palette = new List<int>();
        Palette.Add(0); // Ensure Air is always at index 0
        
        Indices = new byte[CHUNK_VOLUME];
        // Default value of byte is 0, which maps to Palette[0] (Air), so chunk is empty by default.
    }

    public int GetVoxel(int x, int y, int z)
    {
        if (!IsValidCoordinate(x, y, z))
            return 0; // Return Air if out of bounds

        int index = GetIndex(x, y, z);
        int paletteIndex = Indices[index];
        return Palette[paletteIndex];
    }

    public void SetVoxel(int x, int y, int z, int blockType)
    {
        if (!IsValidCoordinate(x, y, z))
            return;

        int index = GetIndex(x, y, z);
        int currentPaletteIndex = Indices[index];
        int currentBlockType = Palette[currentPaletteIndex];

        // Optimization: If block is already same, do nothing
        if (currentBlockType == blockType)
            return;

        // Find or Add blockType to Palette
        int newPaletteIndex = Palette.IndexOf(blockType);
        if (newPaletteIndex == -1)
        {
            // Block type not in palette yet
            if (Palette.Count >= 255)
            {
                // Fallback or Error: Palette full. 
                // In a real engine, we might upgrade to ushort indices here.
                // For now, we just print an error and ignore.
                GD.PrintErr("Chunk Palette Full! Cannot add more unique block types.");
                return;
            }

            Palette.Add(blockType);
            newPaletteIndex = Palette.Count - 1;
        }

        Indices[index] = (byte)newPaletteIndex;
        
        // Optional: Clean up unused palette entries? 
        // Usually not worth the CPU cost on every SetVoxel. 
        // Better to do a "OptimizePalette()" pass occasionally if needed.
    }

    private bool IsValidCoordinate(int x, int y, int z)
    {
        return x >= 0 && x < CHUNK_SIZE &&
               y >= 0 && y < CHUNK_SIZE &&
               z >= 0 && z < CHUNK_SIZE;
    }

    private int GetIndex(int x, int y, int z)
    {
        // Standard flat index: x + (y * SIZE) + (z * SIZE * SIZE)
        // Optimization: Z-curve or Morton code could be used here for better cache locality,
        // but standard linear is fine for now.
        return x + (y * CHUNK_SIZE) + (z * CHUNK_SIZE * CHUNK_SIZE);
    }
    
    // Phase 3: Renderer Resources
    public Rid MeshRid { get; set; }
    public Rid FoliageMeshRid { get; set; } // Separate mesh for transparent foliage
    public Rid BodyRid { get; set; } // For collision
    public bool HasMesh { get; set; } = false;
    
    // Phase 4: Decoupled Physics
    // We store the raw collision geometry here so we can generate the BodyRid lazily
    public Vector3[] CollisionVertices { get; set; }
    
    // Foliage data
    public List<(Vector3I position, int foliageType)> FoliagePlacements { get; set; } = new List<(Vector3I, int)>();

    public void Dispose()
    {
        // Clean up GPU resources
        if (MeshRid.IsValid)
        {
            RenderingServer.FreeRid(MeshRid);
            MeshRid = new Rid(); // Invalidate
        }
        
        if (BodyRid.IsValid)
        {
            PhysicsServer3D.FreeRid(BodyRid);
            BodyRid = new Rid();
        }
        
        if (FoliageMeshRid.IsValid)
        {
            RenderingServer.FreeRid(FoliageMeshRid);
            FoliageMeshRid = new Rid();
        }
        
        CollisionVertices = null; // Help GC
        FoliagePlacements?.Clear();
    }
}

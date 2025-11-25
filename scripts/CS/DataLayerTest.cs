using Godot;
using System;
using System.Diagnostics;

/// <summary>
/// Simple unit test to verify the Data Layer (ChunkData & WorldData).
/// Attaches to a node in the scene to run on startup.
/// </summary>
public partial class DataLayerTest : Node
{
    public override void _Ready()
    {
        GD.Print("--- Starting Data Layer Tests ---");
        
        TestChunkData();
        TestWorldData();
        
        GD.Print("--- Data Layer Tests Completed ---");
    }

    private void TestChunkData()
    {
        GD.Print("Testing ChunkData Palette Compression...");
        
        var chunk = new ChunkData();
        
        // 1. Verify initial state (Air)
        if (chunk.GetVoxel(0, 0, 0) != 0)
            GD.PrintErr("FAIL: Initial voxel is not 0 (Air)");
        
        // 2. Set some blocks
        chunk.SetVoxel(0, 0, 0, 1); // Dirt
        chunk.SetVoxel(1, 0, 0, 2); // Grass
        chunk.SetVoxel(2, 0, 0, 1); // Dirt (Reuse palette)
        
        // 3. Verify values
        if (chunk.GetVoxel(0, 0, 0) != 1) GD.PrintErr("FAIL: (0,0,0) should be 1");
        if (chunk.GetVoxel(1, 0, 0) != 2) GD.PrintErr("FAIL: (1,0,0) should be 2");
        if (chunk.GetVoxel(2, 0, 0) != 1) GD.PrintErr("FAIL: (2,0,0) should be 1");
        
        // 4. Verify Palette Size
        // Should contain: 0 (Air), 1 (Dirt), 2 (Grass) -> Count = 3
        if (chunk.Palette.Count != 3)
            GD.PrintErr($"FAIL: Palette count is {chunk.Palette.Count}, expected 3");
        else
            GD.Print("PASS: Palette compression working (3 entries for mixed blocks).");
            
        // 5. Memory Stress Test (Fill with one block)
        chunk = new ChunkData();
        for(int x=0; x<16; x++)
            for(int y=0; y<16; y++)
                for(int z=0; z<16; z++)
                    chunk.SetVoxel(x, y, z, 55); // Stone
                    
        if (chunk.Palette.Count != 2) // Air + Stone
            GD.PrintErr($"FAIL: Uniform chunk palette count is {chunk.Palette.Count}, expected 2");
        else
            GD.Print("PASS: Uniform chunk palette optimized.");
    }

    private void TestWorldData()
    {
        GD.Print("Testing WorldData Dictionary...");
        
        var world = new WorldData();
        var pos = new Vector3I(10, 0, 10);
        var chunk = new ChunkData();
        chunk.SetVoxel(0,0,0, 99);
        
        world.AddChunk(pos, chunk);
        
        if (world.TryGetChunk(pos, out var retrieved))
        {
            if (retrieved.GetVoxel(0,0,0) == 99)
                GD.Print("PASS: Chunk retrieval successful.");
            else
                GD.PrintErr("FAIL: Retrieved chunk data mismatch.");
        }
        else
        {
            GD.PrintErr("FAIL: Could not retrieve chunk.");
        }
    }
}

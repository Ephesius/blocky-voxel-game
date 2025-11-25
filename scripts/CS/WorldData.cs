using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Global container for the infinite world data.
/// Thread-safe access patterns should be enforced by the caller (ChunkManager).
/// </summary>
public class WorldData
{
    // The Infinite World Storage
    // Key: Chunk Coordinate (Vector3I)
    // Value: Chunk Data (Pure C#)
    public Dictionary<Vector3I, ChunkData> Chunks { get; private set; } = new Dictionary<Vector3I, ChunkData>();

    public bool TryGetChunk(Vector3I pos, out ChunkData chunk)
    {
        return Chunks.TryGetValue(pos, out chunk);
    }

    public void AddChunk(Vector3I pos, ChunkData chunk)
    {
        if (Chunks.ContainsKey(pos))
        {
            Chunks[pos] = chunk; // Overwrite
        }
        else
        {
            Chunks.Add(pos, chunk);
        }
    }

    public void RemoveChunk(Vector3I pos)
    {
        Chunks.Remove(pos);
    }
    
    public void Clear()
    {
        Chunks.Clear();
    }
}

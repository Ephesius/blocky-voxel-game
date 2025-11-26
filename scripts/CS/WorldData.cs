using System.Collections.Concurrent;
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
    public ConcurrentDictionary<Vector3I, ChunkData> Chunks { get; private set; } = new ConcurrentDictionary<Vector3I, ChunkData>();

    public bool TryGetChunk(Vector3I pos, out ChunkData chunk)
    {
        return Chunks.TryGetValue(pos, out chunk);
    }

    public void AddChunk(Vector3I pos, ChunkData chunk)
    {
        Chunks.AddOrUpdate(pos, chunk, (key, oldValue) => chunk);
    }

    public void RemoveChunk(Vector3I pos)
    {
        Chunks.TryRemove(pos, out _);
    }
    
    public void Clear()
    {
        Chunks.Clear();
    }
}

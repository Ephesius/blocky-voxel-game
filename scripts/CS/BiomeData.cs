using Godot;
using System;

/// <summary>
/// Definitions for the different biomes in the game.
/// </summary>
public class BiomeData
{
    public enum BiomeType
    {
        Tundra,
        Taiga,
        Grassland,
        TemperateForest,
        Desert,
        Jungle
    }

    public struct BiomeDefinition
    {
        public BiomeType Type;
        public int SurfaceBlock;    // Top layer (e.g., Grass, Sand, Snow)
        public int SubSurfaceBlock; // Layer below surface (e.g., Dirt, Sand)
        public int DeepBlock;       // Deep stone layer (usually Stone)

        public BiomeDefinition(BiomeType type, int surface, int subSurface, int deep = (int)ChunkData.BlockType.STONE)
        {
            Type = type;
            SurfaceBlock = surface;
            SubSurfaceBlock = subSurface;
            DeepBlock = deep;
        }
    }

    // Static lookup for biome definitions
    public static BiomeDefinition GetDefinition(BiomeType type)
    {
        switch (type)
        {
            case BiomeType.Tundra:
                return new BiomeDefinition(type, (int)ChunkData.BlockType.SNOW, (int)ChunkData.BlockType.DIRT);
            case BiomeType.Taiga:
                return new BiomeDefinition(type, (int)ChunkData.BlockType.GRASS, (int)ChunkData.BlockType.DIRT);
            case BiomeType.Grassland:
                return new BiomeDefinition(type, (int)ChunkData.BlockType.GRASS, (int)ChunkData.BlockType.DIRT);
            case BiomeType.TemperateForest:
                return new BiomeDefinition(type, (int)ChunkData.BlockType.GRASS, (int)ChunkData.BlockType.DIRT);
            case BiomeType.Desert:
                return new BiomeDefinition(type, (int)ChunkData.BlockType.SAND, (int)ChunkData.BlockType.SAND);
            case BiomeType.Jungle:
                return new BiomeDefinition(type, (int)ChunkData.BlockType.GRASS, (int)ChunkData.BlockType.DIRT);
            default:
                return new BiomeDefinition(type, (int)ChunkData.BlockType.GRASS, (int)ChunkData.BlockType.DIRT);
        }
    }
}

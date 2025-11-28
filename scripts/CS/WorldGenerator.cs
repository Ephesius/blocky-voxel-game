using Godot;
using System;

[GlobalClass]
public partial class WorldGenerator : RefCounted
{
	private WorldConfig _config;
	private FastNoiseLite _noise;
	
	public WorldGenerator(WorldConfig config)
	{
		_config = config;
		
		// Initialize noise generator
		_noise = new FastNoiseLite();
		_noise.Seed = config.SeedValue;
		_noise.Frequency = config.NoiseFrequency;
	}
	
	// Generate voxel data for a chunk at the given position
	public void GenerateChunk(ChunkData chunk, Vector3I chunkPos)
	{
		var chunkSize = _config.ChunkSize;
		
		// Calculate world offset for this chunk
		int worldXOffset = chunkPos.X * chunkSize.X;
		int worldYOffset = chunkPos.Y * chunkSize.Y;
		int worldZOffset = chunkPos.Z * chunkSize.Z;
		
		for (int x = 0; x < chunkSize.X; x++)
		{
			for (int z = 0; z < chunkSize.Z; z++)
			{
				// Use world coordinates for noise sampling
				int worldX = worldXOffset + x;
				int worldZ = worldZOffset + z;
				
				// Generate height based on noise
				float noiseValue = _noise.GetNoise2D(worldX, worldZ);
				int height = (int)(_config.BaseHeight + (noiseValue * _config.TerrainHeightMultiplier));
				
				// Fill blocks up to the height
				for (int y = 0; y < chunkSize.Y; y++)
				{
					int worldY = worldYOffset + y;
					
					if (worldY < height)
					{
						// Determine block type based on depth
						int blockType;
						if (worldY == height - 1)
							blockType = (int)ChunkData.BlockType.GRASS;
						else if (worldY > height - 4)
							blockType = (int)ChunkData.BlockType.DIRT;
						else
							blockType = (int)ChunkData.BlockType.STONE;
							
						chunk.SetVoxel(x, y, z, blockType);
					}
				}
			}
		}
	}
	

}

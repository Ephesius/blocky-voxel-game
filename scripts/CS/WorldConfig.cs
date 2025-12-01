using Godot;
using System;

[GlobalClass]
public partial class WorldConfig : Resource
{
	// World dimensions in blocks
	[Export] public Vector3I WorldSize { get; set; } = new Vector3I(4096, 256, 4096);
	
	// Chunk dimensions in blocks
	[Export] public Vector3I ChunkSize { get; set; } = new Vector3I(16, 16, 16);
	
	// How many chunks to load around the player (radius)
	[Export] public int ViewDistance { get; set; } = 8;
	
	// Terrain generation settings
	[ExportGroup("Terrain Generation")]
	[Export] public int SeedValue { get; set; } = 23456;
	[Export] public float NoiseFrequency { get; set; } = 0.005f;
	[Export] public float TerrainHeightMultiplier { get; set; } = 24.0f;
	[Export] public float BaseHeight { get; set; } = 64.0f; // Base terrain height in blocks
	
	// Climate Generation
	[ExportGroup("Climate Generation")]
	[Export] public float TemperatureFrequency { get; set; } = 0.005f;
	[Export] public float HumidityFrequency { get; set; } = 0.005f;
	[Export] public int EquatorZ { get; set; } = 0;
	[Export] public float TemperatureDropPerBlock { get; set; } = 0.0008f; // How much temp drops per block from equator
	[Export] public float GlobalTemperatureOffset { get; set; } = 0.0f;
	
	// Thresholds
	[ExportGroup("Climate Thresholds")]
	[Export] public float TempColdThreshold { get; set; } = 0.3f;
	[Export] public float TempHotThreshold { get; set; } = 0.7f;
	[Export] public float HumidityDryThreshold { get; set; } = 0.4f;
	
	// Terrain Variation
	[ExportGroup("Terrain Variation")]
	[Export] public float TerrainTypeFrequency { get; set; } = 0.005f;
	[Export] public float FlatlandHeightMultiplier { get; set; } = 4.0f;
	[Export] public float HillsHeightMultiplier { get; set; } = 16.0f;
	[Export] public float MountainHeightMultiplier { get; set; } = 250.0f;
	
	// Calculated properties
	public Vector3I GetWorldSizeInChunks()
	{
		return new Vector3I(
			WorldSize.X / ChunkSize.X,
			WorldSize.Y / ChunkSize.Y,
			WorldSize.Z / ChunkSize.Z
		);
	}
	
	public bool IsChunkInBounds(Vector3I chunkPos)
	{
		var worldChunks = GetWorldSizeInChunks();
		return chunkPos.X >= 0 && chunkPos.X < worldChunks.X &&
		       chunkPos.Y >= 0 && chunkPos.Y < worldChunks.Y &&
		       chunkPos.Z >= 0 && chunkPos.Z < worldChunks.Z;
	}
}

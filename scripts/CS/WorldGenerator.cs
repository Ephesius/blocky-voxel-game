using Godot;
using System;

[GlobalClass]
public partial class WorldGenerator : RefCounted
{
	private WorldConfig _config;
	private FastNoiseLite _noise;
	private FastNoiseLite _temperatureNoise;
	private FastNoiseLite _humidityNoise;
	private FastNoiseLite _terrainTypeNoise;
	private Random _foliageRandom;
	
	public WorldGenerator(WorldConfig config)
	{
		_config = config;
		
		// Initialize noise generator
		_noise = new FastNoiseLite();
		_noise.Seed = config.SeedValue;
		_noise.Frequency = config.NoiseFrequency;
		
		// Initialize climate noises
		_temperatureNoise = new FastNoiseLite();
		_temperatureNoise.Seed = config.SeedValue + 1;
		_temperatureNoise.Frequency = config.TemperatureFrequency;
		
		_humidityNoise = new FastNoiseLite();
		_humidityNoise.Seed = config.SeedValue + 2;
		_humidityNoise.Frequency = config.HumidityFrequency;
		
		// Initialize terrain type noise
		_terrainTypeNoise = new FastNoiseLite();
		_terrainTypeNoise.Seed = config.SeedValue + 3;
		_terrainTypeNoise.Frequency = config.TerrainTypeFrequency;
		
		// Initialize foliage random generator
		_foliageRandom = new Random(config.FoliageSeed);
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
				
				// Determine biome for this column
				BiomeData.BiomeDefinition biome = GetBiome(worldX, worldZ);
				
				// Generate height with terrain variation
				float terrainTypeValue = _terrainTypeNoise.GetNoise2D(worldX, worldZ);
				float heightMultiplier = GetHeightMultiplier(terrainTypeValue);
				float noiseValue = _noise.GetNoise2D(worldX, worldZ);
				int height = (int)(_config.BaseHeight + (noiseValue * heightMultiplier));
				
				// Fill blocks up to the height
				for (int y = 0; y < chunkSize.Y; y++)
				{
					int worldY = worldYOffset + y;
					
					if (worldY < height)
					{
						// Determine block type based on depth and biome
						int blockType;
						if (worldY == height - 1)
							blockType = biome.SurfaceBlock;
						else if (worldY > height - 4)
							blockType = biome.SubSurfaceBlock;
						else
							blockType = biome.DeepBlock;
							
						chunk.SetVoxel(x, y, z, blockType);
					}
				}
				
				// Place foliage on the surface
				PlaceFoliage(chunk, biome.Type, x, z, height, worldYOffset, worldX, worldZ);
			}
		}
	}
	
	private BiomeData.BiomeDefinition GetBiome(int worldX, int worldZ)
	{
		// 1. Calculate base temperature based on latitude (distance from equator)
		float distFromEquator = Math.Abs(worldZ - _config.EquatorZ);
		float baseTemp = 1.0f - (distFromEquator * _config.TemperatureDropPerBlock);
		
		// 2. Add noise variation
		float tempNoise = _temperatureNoise.GetNoise2D(worldX, worldZ);
		float temperature = baseTemp + (tempNoise * 0.2f) + _config.GlobalTemperatureOffset;
		temperature = Mathf.Clamp(temperature, 0.0f, 1.0f);
		
		// 3. Calculate humidity (pure noise)
		float humidityNoise = _humidityNoise.GetNoise2D(worldX, worldZ);
		// Normalize noise from [-1, 1] to [0, 1] roughly
		float humidity = (humidityNoise + 1.0f) * 0.5f;
		humidity = Mathf.Clamp(humidity, 0.0f, 1.0f);
		
		// 4. Determine Biome Type
		BiomeData.BiomeType type;
		
		if (temperature < _config.TempColdThreshold) // Cold
		{
			if (humidity < _config.HumidityDryThreshold)
				type = BiomeData.BiomeType.Tundra;
			else
				type = BiomeData.BiomeType.Taiga;
		}
		else if (temperature < _config.TempHotThreshold) // Temperate
		{
			if (humidity < _config.HumidityDryThreshold)
				type = BiomeData.BiomeType.Grassland;
			else
				type = BiomeData.BiomeType.TemperateForest;
		}
		else // Hot
		{
			if (humidity < _config.HumidityDryThreshold)
				type = BiomeData.BiomeType.Desert;
			else
				type = BiomeData.BiomeType.Jungle;
		}
		
		return BiomeData.GetDefinition(type);
	}
	
	public (float Temperature, float Humidity, BiomeData.BiomeType Biome) GetClimateData(int worldX, int worldZ)
	{
		// 1. Calculate base temperature based on latitude (distance from equator)
		float distFromEquator = Math.Abs(worldZ - _config.EquatorZ);
		float baseTemp = 1.0f - (distFromEquator * _config.TemperatureDropPerBlock);
		
		// 2. Add noise variation
		float tempNoise = _temperatureNoise.GetNoise2D(worldX, worldZ);
		float temperature = baseTemp + (tempNoise * 0.2f) + _config.GlobalTemperatureOffset;
		temperature = Mathf.Clamp(temperature, 0.0f, 1.0f);
		
		// 3. Calculate humidity (pure noise)
		float humidityNoise = _humidityNoise.GetNoise2D(worldX, worldZ);
		// Normalize noise from [-1, 1] to [0, 1] roughly
		float humidity = (humidityNoise + 1.0f) * 0.5f;
		humidity = Mathf.Clamp(humidity, 0.0f, 1.0f);
		
		// 4. Determine Biome Type
		BiomeData.BiomeType type;
		
		if (temperature < _config.TempColdThreshold) // Cold
		{
			if (humidity < _config.HumidityDryThreshold)
				type = BiomeData.BiomeType.Tundra;
			else
				type = BiomeData.BiomeType.Taiga;
		}
		else if (temperature < _config.TempHotThreshold) // Temperate
		{
			if (humidity < _config.HumidityDryThreshold)
				type = BiomeData.BiomeType.Grassland;
			else
				type = BiomeData.BiomeType.TemperateForest;
		}
		else // Hot
		{
			if (humidity < _config.HumidityDryThreshold)
				type = BiomeData.BiomeType.Desert;
			else
				type = BiomeData.BiomeType.Jungle;
		}
		
		return (temperature, humidity, type);
	}
	
	private float GetHeightMultiplier(float terrainTypeValue)
	{
		// Map noise value (-1 to 1) to terrain types:
		// -1.0 to -0.2: Flatlands
		// -0.2 to 0.2: Hills (transition)
		// 0.2 to 1.0: Mountains
		
		if (terrainTypeValue < -0.2f)
		{
			// Flatlands zone: interpolate from Flatland to Hills
			float t = Mathf.InverseLerp(-1.0f, -0.2f, terrainTypeValue);
			return Mathf.Lerp(_config.FlatlandHeightMultiplier, _config.HillsHeightMultiplier, t);
		}
		else if (terrainTypeValue < 0.2f)
		{
			// Hills zone: interpolate from Hills to Mountains
			float t = Mathf.InverseLerp(-0.2f, 0.2f, terrainTypeValue);
			return Mathf.Lerp(_config.HillsHeightMultiplier, _config.MountainHeightMultiplier, t * 0.5f); // Slow transition
		}
		else
		{
			// Mountains zone: interpolate within mountain range
			float t = Mathf.InverseLerp(0.2f, 1.0f, terrainTypeValue);
			return Mathf.Lerp(_config.HillsHeightMultiplier, _config.MountainHeightMultiplier, 0.5f + t * 0.5f);
		}
	}
	
	private void PlaceFoliage(ChunkData chunk, BiomeData.BiomeType biomeType, int x, int z, int surfaceHeight, int chunkYOffset, int worldX, int worldZ)
	{
		// Skip if density is 0
		if (_config.FoliageDensity <= 0)
			return;
		
		// Check if surface height is actually within this chunk's Y range
		// Chunk contains Y values from chunkYOffset to chunkYOffset + CHUNK_SIZE - 1
		if (surfaceHeight < chunkYOffset || surfaceHeight >= chunkYOffset + ChunkData.CHUNK_SIZE)
			return; // Surface is not in this chunk, skip foliage
		
		// Calculate local Y position within the chunk
		int localY = surfaceHeight - chunkYOffset;
		
		// Deterministic random based on world position
		int seed = worldX * 73856093 ^ worldZ * 19349663;
		var localRandom = new Random(seed ^ _config.FoliageSeed);
		
		// Roll for foliage placement
		if (localRandom.NextDouble() > _config.FoliageDensity)
			return;
		
		// Get foliage types for this biome
		var (foliageType1, foliageType2) = FoliageData.GetFoliageForBiome(biomeType);
		
		// Randomly pick one of the two foliage types
		int foliageType = localRandom.Next(0, 2) == 0 ? (int)foliageType1 : (int)foliageType2;
		
		// Place foliage at surface height
		chunk.FoliagePlacements.Add((new Vector3I(x, localY, z), foliageType));
	}

}

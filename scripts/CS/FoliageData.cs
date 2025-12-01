using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Defines foliage types and their mapping to biomes.
/// </summary>
public static class FoliageData
{
	public enum FoliageType
	{
		// Tundra (Cold + Dry)
		TundraDeadGrass = 0,
		TundraLichen = 1,
		
		// Taiga (Cold + Wet)
		TaigaFern = 2,
		TaigaBerryBush = 3,
		
		// Grassland (Temperate + Dry)
		GrasslandTallGrass = 4,
		GrasslandWildflower = 5,
		
		// Temperate Forest (Temperate + Wet)
		ForestFern = 6,
		ForestMushroom = 7,
		
		// Desert (Hot + Dry)
		DesertDeadBush = 8,
		DesertCactus = 9,
		
		// Jungle (Hot + Wet)
		JungleLargeFern = 10,
		JungleTropicalPlant = 11
	}
	
	// Mapping of texture filenames to foliage type indices
	public static readonly Dictionary<FoliageType, string> FoliageTextures = new Dictionary<FoliageType, string>
	{
		{ FoliageType.TundraDeadGrass, "tundra_dead_grass.png" },
		{ FoliageType.TundraLichen, "tundra_lichen.png" },
		{ FoliageType.TaigaFern, "taiga_fern.png" },
		{ FoliageType.TaigaBerryBush, "taiga_berry_bush.png" },
		{ FoliageType.GrasslandTallGrass, "grassland_tall_grass.png" },
		{ FoliageType.GrasslandWildflower, "grassland_wildflower.png" },
		{ FoliageType.ForestFern, "forest_fern.png" },
		{ FoliageType.ForestMushroom, "forest_mushroom.png" },
		{ FoliageType.DesertDeadBush, "desert_dead_bush.png" },
		{ FoliageType.DesertCactus, "desert_cactus.png" },
		{ FoliageType.JungleLargeFern, "jungle_large_fern.png" },
		{ FoliageType.JungleTropicalPlant, "jungle_tropical_plant.png" }
	};
	
	// Get foliage types for a specific biome
	public static (FoliageType, FoliageType) GetFoliageForBiome(BiomeData.BiomeType biome)
	{
		switch (biome)
		{
			case BiomeData.BiomeType.Tundra:
				return (FoliageType.TundraDeadGrass, FoliageType.TundraLichen);
			case BiomeData.BiomeType.Taiga:
				return (FoliageType.TaigaFern, FoliageType.TaigaBerryBush);
			case BiomeData.BiomeType.Grassland:
				return (FoliageType.GrasslandTallGrass, FoliageType.GrasslandWildflower);
			case BiomeData.BiomeType.TemperateForest:
				return (FoliageType.ForestFern, FoliageType.ForestMushroom);
			case BiomeData.BiomeType.Desert:
				return (FoliageType.DesertDeadBush, FoliageType.DesertCactus);
			case BiomeData.BiomeType.Jungle:
				return (FoliageType.JungleLargeFern, FoliageType.JungleTropicalPlant);
			default:
				return (FoliageType.GrasslandTallGrass, FoliageType.GrasslandWildflower);
		}
	}
}

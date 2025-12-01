using Godot;
using System;

public partial class BiomeTest : Node
{
    public override void _Ready()
    {
        GD.Print("Starting Biome Verification...");
        
        var config = new WorldConfig();
        var generator = new WorldGenerator(config);
        
        // Test Equator (Hot)
        SampleBiome(generator, 0, 0, "Equator (0,0)");
        
        // Test Temperate Zone
        SampleBiome(generator, 0, 500, "Temperate (0,500)");
        
        // Test Polar Zone
        SampleBiome(generator, 0, 2000, "Polar (0,2000)");
        
        // Test Variation (Noise)
        SampleBiome(generator, 100, 0, "Equator Offset (100,0)");
        
        GD.Print("Verification Complete.");
        GetTree().Quit();
    }
    
    private void SampleBiome(WorldGenerator generator, int x, int z, string label)
    {
        // We can't access private GetBiome directly, but we can infer it by generating a chunk
        // or we can just use reflection for testing purposes, or make GetBiome public/internal.
        // For this test, let's just make GetBiome public temporarily or just trust the code?
        // Actually, let's just use reflection to call the private method to be safe and not modify the code just for tests.
        
        var method = typeof(WorldGenerator).GetMethod("GetBiome", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method != null)
        {
            var result = method.Invoke(generator, new object[] { x, z });
            var biomeDef = (BiomeData.BiomeDefinition)result;
            GD.Print($"Location: {label} -> Biome: {biomeDef.Type}");
        }
        else
        {
            GD.PrintErr("Could not find GetBiome method!");
        }
    }
}

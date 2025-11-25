using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

[GlobalClass]
public partial class VoxelWorld : Node3D
{
	private WorldConfig _config;
	private WorldGenerator _generator;
	private List<Chunk> _allChunks = new List<Chunk>();
	private Node3D _player;
	private float _collisionDistance = 2.0f;
	
	public override void _Ready()
	{
		// Create default config
		_config = new WorldConfig();
		
		// Create generator
		_generator = new WorldGenerator(_config);
		
		// Spawn chunks
		SpawnInitialChunks();
		
		// Find player reference
		CallDeferred(nameof(FindPlayer));
	}
	
	private void FindPlayer()
	{
		_player = GetNodeOrNull<Node3D>("/root/Main/Player");
		
		if (_player != null)
		{
			UpdateCollisionAroundPlayer();
		}
	}
	
	private void SpawnInitialChunks()
	{
		var stopwatch = Stopwatch.StartNew();
		
		int viewDist = _config.ViewDistance;
		var chunkSize = _config.ChunkSize;
		
		// Calculate vertical layers to generate
		int maxYLayer = 5; // Generate layers 0-4 (80 blocks tall)
		
		// Player spawn point (in world coordinates)
		var playerSpawn = new Vector3(0, 70, 0);
		var spawnChunkPos = new Vector3I(
			(int)Mathf.Floor(playerSpawn.X / chunkSize.X),
			(int)Mathf.Floor(playerSpawn.Y / chunkSize.Y),
			(int)Mathf.Floor(playerSpawn.Z / chunkSize.Z)
		);
		
		GD.Print("========================================");
		GD.Print("Starting chunk generation...");
		GD.Print($"View distance: {viewDist} chunks");
		GD.Print($"Vertical layers: {maxYLayer}");
		GD.Print($"Player spawn chunk: ({spawnChunkPos.X}, {spawnChunkPos.Y}, {spawnChunkPos.Z})");
		
		// First pass: Create all chunks
		var afterCreation = stopwatch.ElapsedMilliseconds;
		
		for (int x = -viewDist; x <= viewDist; x++)
		{
			for (int y = 0; y < maxYLayer; y++)
			{
				for (int z = -viewDist; z <= viewDist; z++)
				{
					var chunkPos = new Vector3I(x, y, z);
					var chunk = new Chunk();
					
					// Set chunk position
					chunk.ChunkPos = chunkPos;
					
					// Set world reference for neighbor lookups
					chunk.World = this;
					
					// Set world position (in world coordinates)
					var worldPos = new Vector3(
						chunkPos.X * chunkSize.X,
						chunkPos.Y * chunkSize.Y,
						chunkPos.Z * chunkSize.Z
					);
					
					chunk.Name = $"Chunk_{chunkPos.X}_{chunkPos.Y}_{chunkPos.Z}";
					AddChild(chunk);
					chunk.GlobalPosition = worldPos;
					_allChunks.Add(chunk);
				}
			}
		}
		
				
		afterCreation = stopwatch.ElapsedMilliseconds;
		GD.Print($"Created {_allChunks.Count} chunk nodes in {afterCreation} ms");
		
		// Second pass: Generate and set terrain data (no meshes yet)
		foreach (var chunk in _allChunks)
		{
			var voxelData = _generator.GenerateChunkData(chunk.ChunkPos);
			chunk.Voxels = voxelData; // Set data without triggering mesh generation
		}
		
		var afterData = stopwatch.ElapsedMilliseconds;
		GD.Print($"Generated terrain data for {_allChunks.Count} chunks in {afterData - afterCreation} ms");
		
		// Third pass: Generate meshes now that all neighbor data exists
		foreach (var chunk in _allChunks)
		{
			chunk.UpdateMesh();
		}
		
		var afterMesh = stopwatch.ElapsedMilliseconds;
		GD.Print($"Generated meshes for {_allChunks.Count} chunks in {afterMesh - afterData} ms");

		
		// Enable collision for nearby chunks
		GD.Print($"Enabling collision for chunks within {_collisionDistance} distance of player...");
		int collisionEnabled = 0;
		
		foreach (var chunk in _allChunks)
		{
			float distance = new Vector3(
				chunk.ChunkPos.X - spawnChunkPos.X,
				chunk.ChunkPos.Y - spawnChunkPos.Y,
				chunk.ChunkPos.Z - spawnChunkPos.Z
			).Length();
			
			if (distance <= _collisionDistance)
			{
				chunk.UpdateCollision(true);
				collisionEnabled++;
			}
		}
		
		var afterCollision = stopwatch.ElapsedMilliseconds;
		GD.Print($"Enabled collision for {collisionEnabled} chunks in {afterCollision - afterData} ms");
		
		var totalTime = stopwatch.ElapsedMilliseconds;
		GD.Print("========================================");
		GD.Print($"TOTAL LOAD TIME: {totalTime} ms ({totalTime / 1000.0:F2} seconds)");
		GD.Print("========================================");
	}
	
	private void UpdateCollisionAroundPlayer()
	{
		if (_player == null)
			return;
		
		var chunkSize = _config.ChunkSize;
		
		// Get player's chunk position
		var playerChunkPos = new Vector3I(
			(int)Mathf.Floor(_player.GlobalPosition.X / chunkSize.X),
			(int)Mathf.Floor(_player.GlobalPosition.Y / chunkSize.Y),
			(int)Mathf.Floor(_player.GlobalPosition.Z / chunkSize.Z)
		);
		
		// Update collision for all chunks based on distance to player
		foreach (var chunk in _allChunks)
		{
			float distance = new Vector3(
				chunk.ChunkPos.X - playerChunkPos.X,
				chunk.ChunkPos.Y - playerChunkPos.Y,
				chunk.ChunkPos.Z - playerChunkPos.Z
			).Length();
			
			bool shouldHaveCollision = distance <= _collisionDistance;
			chunk.UpdateCollision(shouldHaveCollision);
		}
	}
	
	public override void _Process(double delta)
	{
		// Update collision every frame (could optimize to every N frames if needed)
		if (_player != null)
		{
			UpdateCollisionAroundPlayer();
		}
	}
}

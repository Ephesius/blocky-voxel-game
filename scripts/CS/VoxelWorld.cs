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
		
		// Create generator (still needed for now)
		_generator = new WorldGenerator(_config);
		
		// Initialize ChunkManager
		var chunkManager = new ChunkManager();
		chunkManager.Name = "ChunkManager";
		AddChild(chunkManager);
		
		GD.Print("VoxelWorld: Initialized ChunkManager.");
		
		// Find player reference
		CallDeferred(nameof(FindPlayer));
	}
	
	private void FindPlayer()
	{
		_player = GetNodeOrNull<Node3D>("/root/Main/Player");
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

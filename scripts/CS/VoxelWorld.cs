using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

[GlobalClass]
public partial class VoxelWorld : Node3D
{
	private WorldConfig _config;
	private Node3D _player;
	private float _collisionDistance = 2.0f;
	
	public override void _Ready()
	{
		// Create default config
		_config = new WorldConfig();
		
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
		
		// TODO: Update collision for chunks based on distance to player
		// This functionality will be handled by ChunkManager in the future
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

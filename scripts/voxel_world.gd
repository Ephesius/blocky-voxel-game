extends Node3D

var config: WorldConfig
var generator: WorldGenerator
var all_chunks: Array = []  # Store all chunks for collision updates (can't type as Array[Chunk] with C#)
var player: Node3D  # Reference to player
var collision_distance: float = 6.0  # Chunks within this distance get collision

func _ready() -> void:
	# Create default config
	config = WorldConfig.new()
	
	# Create generator
	generator = WorldGenerator.new(config)
	
	# For now, spawn chunks in a fixed area around origin for testing
	# Later we'll make this dynamic based on player position
	_spawn_initial_chunks()
	
	# Find player reference
	await get_tree().process_frame  # Wait for player to be ready
	player = get_node_or_null("/root/Main/Player")
	
	# Update collision periodically
	if player != null:
		_update_collision_around_player()

func _spawn_initial_chunks() -> void:
	var start_time: float = Time.get_ticks_msec()
	
	var view_dist: int = config.view_distance
	var chunk_size: Vector3i = config.chunk_size
	
	# Calculate vertical layers to generate (for now, just bottom few layers)
	var max_y_layer: int = 5  # Generate layers 0-4 (80 blocks tall)
	
	# Player spawn point (in world coordinates)
	var player_spawn: Vector3 = Vector3(0, 70, 0)
	var spawn_chunk_pos: Vector3i = Vector3i(
		int(floor(player_spawn.x / chunk_size.x)),
		int(floor(player_spawn.y / chunk_size.y)),
		int(floor(player_spawn.z / chunk_size.z))
	)
	
	print("========================================")
	print("Starting chunk generation...")
	print("View distance: %d chunks" % view_dist)
	print("Vertical layers: %d" % max_y_layer)
	print("Player spawn chunk: (%d, %d, %d)" % [spawn_chunk_pos.x, spawn_chunk_pos.y, spawn_chunk_pos.z])
	
	# First pass: Create all chunks (using C# Chunk class)
	for x: int in range(-view_dist, view_dist + 1):
		for y: int in range(max_y_layer):
			for z: int in range(-view_dist, view_dist + 1):
				var chunk_pos: Vector3i = Vector3i(x, y, z)
				var chunk: Chunk = Chunk.new()  # Instantiate C# Chunk
				
				# Set chunk position (C# property)
				chunk.ChunkPos = chunk_pos
				
				# Set world reference for neighbor lookups (C# property)
				chunk.World = self
				
				# Set world position (in world coordinates)
				var world_pos: Vector3 = Vector3(
					chunk_pos.x * chunk_size.x,
					chunk_pos.y * chunk_size.y,
					chunk_pos.z * chunk_size.z
				)
				
				chunk.name = "Chunk_%d_%d_%d" % [chunk_pos.x, chunk_pos.y, chunk_pos.z]
				add_child(chunk)
				chunk.global_position = world_pos
				all_chunks.append(chunk)
	
	var after_creation: float = Time.get_ticks_msec()
	print("Created %d chunk nodes in %.2f ms" % [all_chunks.size(), after_creation - start_time])
	
	# Second pass: Generate terrain data for all chunks
	for chunk in all_chunks:
		var voxel_data: Array[int] = generator.generate_chunk_data(chunk.ChunkPos)
		chunk.SetVoxelData(voxel_data)
	
	var after_data: float = Time.get_ticks_msec()
	print("Generated terrain data and meshes for %d chunks in %.2f ms" % [all_chunks.size(), after_data - after_creation])
	
		
	# Enable collision for nearby chunks
	print("Enabling collision for chunks within %.1f distance of player..." % collision_distance)
	var collision_enabled: int = 0
	for chunk in all_chunks:
		var distance: float = Vector3(
			float(chunk.ChunkPos.x - spawn_chunk_pos.x),
			float(chunk.ChunkPos.y - spawn_chunk_pos.y),
			float(chunk.ChunkPos.z - spawn_chunk_pos.z)
		).length()
		
		if distance <= collision_distance:
			chunk.UpdateCollision(true)
			collision_enabled += 1
	
	var after_collision: float = Time.get_ticks_msec()
	print("Enabled collision for %d chunks in %.2f ms" % [collision_enabled, after_collision - after_data])
	
	var total_time: float = after_collision - start_time
	print("========================================")
	print("TOTAL LOAD TIME: %.2f ms (%.2f seconds)" % [total_time, total_time / 1000.0])
	print("========================================")


func _update_collision_around_player() -> void:
	if player == null:
		return
	
	# Get player's chunk position
	var player_chunk_pos: Vector3i = Vector3i(
		int(floor(player.global_position.x / config.chunk_size.x)),
		int(floor(player.global_position.y / config.chunk_size.y)),
		int(floor(player.global_position.z / config.chunk_size.z))
	)
	
	# Update collision for all chunks based on distance to player
	for chunk in all_chunks:
		var distance: float = Vector3(
			float(chunk.ChunkPos.x - player_chunk_pos.x),
			float(chunk.ChunkPos.y - player_chunk_pos.y),
			float(chunk.ChunkPos.z - player_chunk_pos.z)
		).length()
		
		var should_have_collision: bool = distance <= collision_distance
		chunk.UpdateCollision(should_have_collision)

func _process(_delta: float) -> void:
	# Update collision every frame (could optimize to every N frames if needed)
	if player != null:
		_update_collision_around_player()

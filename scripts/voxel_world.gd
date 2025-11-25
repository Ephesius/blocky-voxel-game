extends Node3D

var config: WorldConfig
var generator: WorldGenerator
var chunk_script: GDScript = preload("res://scripts/chunk.gd")
var all_chunks: Array[Chunk] = []  # Store all chunks for collision updates
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
	var view_dist: int = config.view_distance
	var chunk_size: Vector3i = config.chunk_size
	
	# Calculate vertical layers to generate (for now, just bottom few layers)
	var max_y_layer: int = 4  # Generate layers 0-3 (64 blocks tall)
	
	# First pass: Create all chunks
	var chunks: Array[Chunk] = []
	for x: int in range(-view_dist, view_dist + 1):
		for y: int in range(max_y_layer):
			for z: int in range(-view_dist, view_dist + 1):
				var chunk_pos: Vector3i = Vector3i(x, y, z)
				var chunk: Chunk = chunk_script.new()
				
				# Set chunk position
				chunk.chunk_pos = chunk_pos
				
				# Set world reference for neighbor lookups
				chunk.world = self
				
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
	
	# Second pass: Generate terrain data for all chunks
	for chunk in all_chunks:
		var voxel_data: Array[int] = generator.generate_chunk_data(chunk.chunk_pos)
		chunk.voxels = voxel_data
	
	# Third pass: Generate meshes now that all neighbor data exists
	for chunk in all_chunks:
		chunk._update_mesh()

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
			float(chunk.chunk_pos.x - player_chunk_pos.x),
			float(chunk.chunk_pos.y - player_chunk_pos.y),
			float(chunk.chunk_pos.z - player_chunk_pos.z)
		).length()
		
		var should_have_collision: bool = distance <= collision_distance
		chunk.update_collision(should_have_collision)

func _process(_delta: float) -> void:
	# Update collision every frame (could optimize to every N frames if needed)
	if player != null:
		_update_collision_around_player()

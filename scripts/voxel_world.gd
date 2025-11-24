extends Node3D

var config: WorldConfig
var generator: WorldGenerator
var chunk_script: GDScript = preload("res://scripts/chunk.gd")

func _ready() -> void:
	# Create default config
	config = WorldConfig.new()
	
	# Create generator
	generator = WorldGenerator.new(config)
	
	# For now, spawn chunks in a fixed area around origin for testing
	# Later we'll make this dynamic based on player position
	_spawn_initial_chunks()

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
				chunks.append(chunk)
	
	# Second pass: Generate terrain data for all chunks
	for chunk in chunks:
		var voxel_data: Array[int] = generator.generate_chunk_data(chunk.chunk_pos)
		chunk.voxels = voxel_data
	
	# Third pass: Generate meshes now that all neighbor data exists
	for chunk in chunks:
		chunk._update_mesh()

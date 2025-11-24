extends Node3D

const CHUNK_SIZE: int = 16
var chunk_script: GDScript = preload("res://scripts/chunk.gd")

func _ready() -> void:
	# Spawn a 4x4 grid of chunks centered around origin
	var grid_size: int = 4
	var half_grid: int = grid_size / 2
	
	# First pass: Create all chunks and add them to the scene
	var chunks: Array[Chunk] = []
	for x: int in range(grid_size):
		for z: int in range(grid_size):
			var chunk: Chunk = chunk_script.new()
			
			# Set chunk position (in chunk coordinates)
			chunk.chunk_pos = Vector2i(x - half_grid, z - half_grid)
			
			# Set world reference for neighbor lookups
			chunk.world = self
			
			# Set world position (in world coordinates)
			var world_pos: Vector3 = Vector3(
				(x - half_grid) * CHUNK_SIZE,
				0,
				(z - half_grid) * CHUNK_SIZE
			)
			
			chunk.name = "Chunk_%d_%d" % [x - half_grid, z - half_grid]
			add_child(chunk)
			chunk.global_position = world_pos
			chunks.append(chunk)
	
	# Second pass: Generate terrain data for all chunks
	for chunk in chunks:
		chunk._generate_data()
	
	# Third pass: Generate meshes now that all neighbor data exists
	for chunk in chunks:
		chunk._update_mesh()

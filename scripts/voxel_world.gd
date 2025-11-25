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
	await _spawn_initial_chunks()
	
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
	
	var after_creation: float = Time.get_ticks_msec()
	print("Created %d chunk nodes in %.2f ms" % [all_chunks.size(), after_creation - start_time])
	
	# Second pass: Generate terrain data for all chunks
	for chunk in all_chunks:
		var voxel_data: Array[int] = generator.generate_chunk_data(chunk.chunk_pos)
		chunk.voxels = voxel_data
	
	var after_data: float = Time.get_ticks_msec()
	print("Generated terrain data for %d chunks in %.2f ms" % [all_chunks.size(), after_data - after_creation])
	
	# Third pass: Sort chunks by distance to spawn, then generate meshes with priority
	print("Sorting chunks by distance to spawn...")
	
	# Create array of [chunk, distance] pairs
	var chunk_distances: Array = []
	for chunk in all_chunks:
		var distance: float = Vector3(
			float(chunk.chunk_pos.x - spawn_chunk_pos.x),
			float(chunk.chunk_pos.y - spawn_chunk_pos.y),
			float(chunk.chunk_pos.z - spawn_chunk_pos.z)
		).length()
		chunk_distances.append({"chunk": chunk, "distance": distance})
	
	# Sort by distance (nearest first)
	chunk_distances.sort_custom(func(a, b): return a["distance"] < b["distance"])
	
	var after_sort: float = Time.get_ticks_msec()
	print("Sorted chunks in %.2f ms" % (after_sort - after_data))
	
	# Generate meshes with priority system
	var total_chunks: int = chunk_distances.size()
	var processed: int = 0
	
	# Critical zone: Generate synchronously (within 2 chunks of spawn)
	var critical_radius: float = 2.0
	var critical_count: int = 0
	
	print("Generating critical chunks (within %.1f chunks of spawn) synchronously..." % critical_radius)
	
	for item in chunk_distances:
		if item["distance"] <= critical_radius:
			var chunk: Chunk = item["chunk"]
			chunk._update_mesh()
			# Enable collision immediately for critical chunks
			chunk.update_collision(true)
			critical_count += 1
			processed += 1
		else:
			break  # Stop once we're past critical zone
	
	var after_critical: float = Time.get_ticks_msec()
	print("Generated %d critical chunks in %.2f ms" % [critical_count, after_critical - after_sort])
	
	# Remaining chunks: Generate asynchronously in small batches
	var batch_size: int = 5  # Smaller batches to reduce stutter
	print("Starting async mesh generation for remaining %d chunks (%d per frame)..." % [total_chunks - critical_count, batch_size])
	
	for i in range(critical_count, total_chunks, batch_size):
		var batch_end: int = min(i + batch_size, total_chunks)
		
		# Process this batch
		for j in range(i, batch_end):
			var chunk: Chunk = chunk_distances[j]["chunk"]
			chunk._update_mesh()
			
			# Enable collision immediately if within collision distance
			var distance: float = chunk_distances[j]["distance"]
			if distance <= collision_distance:
				chunk.update_collision(true)
			
			processed += 1
		
		# Yield control back to the engine every batch
		await get_tree().process_frame
		
		# Print progress every 100 chunks
		if processed % 100 == 0 or processed == total_chunks:
			print("  Meshes generated: %d / %d (%.1f%%)" % [processed, total_chunks, (float(processed) / total_chunks) * 100.0])
	
	var after_mesh: float = Time.get_ticks_msec()
	var total_time: float = after_mesh - start_time
	print("Async mesh generation complete in %.2f ms" % (after_mesh - after_critical))
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

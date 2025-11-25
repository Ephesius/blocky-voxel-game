class_name WorldConfig extends Resource

# World dimensions in blocks
@export var world_size: Vector3i = Vector3i(4096, 256, 4096)

# Chunk dimensions in blocks
@export var chunk_size: Vector3i = Vector3i(16, 16, 16)

# How many chunks to load around the player (radius)
@export var view_distance: int = 8

# Terrain generation settings
@export_group("Terrain Generation")
@export var seed_value: int = 12345
@export var noise_frequency: float = 0.02
@export var terrain_height_multiplier: float = 24.0
@export var base_height: float = 64.0  # Base terrain height in blocks

# Calculated properties
func get_world_size_in_chunks() -> Vector3i:
	return Vector3i(
		world_size.x / chunk_size.x,
		world_size.y / chunk_size.y,
		world_size.z / chunk_size.z
	)

func is_chunk_in_bounds(chunk_pos: Vector3i) -> bool:
	var world_chunks: Vector3i = get_world_size_in_chunks()
	return (
		chunk_pos.x >= 0 and chunk_pos.x < world_chunks.x and
		chunk_pos.y >= 0 and chunk_pos.y < world_chunks.y and
		chunk_pos.z >= 0 and chunk_pos.z < world_chunks.z
	)

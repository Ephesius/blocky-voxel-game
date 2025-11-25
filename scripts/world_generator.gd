class_name WorldGenerator extends RefCounted

# Block types - must match Chunk.cs enum values
enum BlockType {
	AIR = 0,
	DIRT = 1,
	GRASS = 2,
	STONE = 3
}

var config: WorldConfig
var noise: FastNoiseLite

func _init(world_config: WorldConfig) -> void:
	config = world_config
	
	# Initialize noise generator
	noise = FastNoiseLite.new()
	noise.seed = config.seed_value
	noise.frequency = config.noise_frequency

# Generate voxel data for a chunk at the given position
func generate_chunk_data(chunk_pos: Vector3i) -> Array[int]:
	var chunk_size: Vector3i = config.chunk_size
	var voxels: Array[int] = []
	voxels.resize(chunk_size.x * chunk_size.y * chunk_size.z)
	voxels.fill(BlockType.AIR)
	
	# Calculate world offset for this chunk
	var world_x_offset: int = chunk_pos.x * chunk_size.x
	var world_y_offset: int = chunk_pos.y * chunk_size.y
	var world_z_offset: int = chunk_pos.z * chunk_size.z
	
	for x: int in range(chunk_size.x):
		for z: int in range(chunk_size.z):
			# Use world coordinates for noise sampling
			var world_x: int = world_x_offset + x
			var world_z: int = world_z_offset + z
			
			# Generate height based on noise
			var noise_value: float = noise.get_noise_2d(world_x, world_z)
			var height: int = int(config.base_height + (noise_value * config.terrain_height_multiplier))
			
			# Fill blocks up to the height
			for y: int in range(chunk_size.y):
				var world_y: int = world_y_offset + y
				
				if world_y < height:
					var index: int = _get_index(x, y, z, chunk_size)
					
					# Determine block type based on depth
					if world_y == height - 1:
						voxels[index] = BlockType.GRASS
					elif world_y > height - 4:
						voxels[index] = BlockType.DIRT
					else:
						voxels[index] = BlockType.STONE
	
	return voxels

func _get_index(x: int, y: int, z: int, chunk_size: Vector3i) -> int:
	return x + (y * chunk_size.x) + (z * chunk_size.x * chunk_size.y)

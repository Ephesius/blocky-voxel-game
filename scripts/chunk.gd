class_name Chunk extends Node3D

const CHUNK_SIZE: int = 16

# Voxel types
enum BlockType {
	AIR = 0,
	DIRT = 1,
	GRASS = 2,
	STONE = 3
}

# 3D array flattened or dictionary? 
# For a 16x16x16 chunk, a flat array is fast and simple.
# Size = 16^3 = 4096 integers.
var voxels: Array[int] = []
var chunk_pos: Vector2i  # Chunk position in chunk coordinates (not world)
var world: Node3D  # Reference to voxel_world for neighbor lookups

var mesh_instance: MeshInstance3D
static var noise: FastNoiseLite  # Shared across all chunks

func _ready() -> void:
	mesh_instance = MeshInstance3D.new()
	add_child(mesh_instance)
	
	# Initialize noise if not already done
	if noise == null:
		noise = FastNoiseLite.new()
		noise.seed = 12345  # Fixed seed for consistent world
		noise.frequency = 0.02
	
	# Initialize empty voxels
	voxels.resize(CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE)
	voxels.fill(BlockType.AIR)

func generate_chunk() -> void:
	# Generate terrain data and mesh
	_generate_data()
	_update_mesh()

func _generate_data() -> void:
	# Calculate world offset for this chunk
	var world_x_offset: int = chunk_pos.x * CHUNK_SIZE
	var world_z_offset: int = chunk_pos.y * CHUNK_SIZE
	
	for x: int in range(CHUNK_SIZE):
		for z: int in range(CHUNK_SIZE):
			# Use world coordinates for noise sampling
			var world_x: int = world_x_offset + x
			var world_z: int = world_z_offset + z
			
			# Generate height based on noise (range 0-24)
			var height: int = int((noise.get_noise_2d(world_x, world_z) + 1.0) * 0.5 * 24)
			
			for y: int in range(CHUNK_SIZE):
				if y < height:
					var index: int = _get_index(x, y, z)
					if y == height - 1:
						voxels[index] = BlockType.GRASS
					elif y > height - 4:
						voxels[index] = BlockType.DIRT
					else:
						voxels[index] = BlockType.STONE

func _update_mesh() -> void:
	var surface_tool: SurfaceTool = SurfaceTool.new()
	surface_tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	
	for x: int in range(CHUNK_SIZE):
		for y: int in range(CHUNK_SIZE):
			for z: int in range(CHUNK_SIZE):
				var type: int = _get_voxel(x, y, z)
				if type != BlockType.AIR:
					_create_block_faces(surface_tool, x, y, z, type)
	
	# Don't generate normals - we want sharp edges, not smooth
	surface_tool.index()
	
	var mesh = surface_tool.commit()
	
	# Create material with backface culling enabled
	var material: StandardMaterial3D = StandardMaterial3D.new()
	material.vertex_color_use_as_albedo = true
	material.cull_mode = BaseMaterial3D.CULL_BACK
	mesh.surface_set_material(0, material)
	
	mesh_instance.mesh = mesh
	mesh_instance.create_trimesh_collision()

func _create_block_faces(surface_tool: SurfaceTool, x: int, y: int, z: int, type: int) -> void:
	var color: Color = Color.WHITE
	match type:
		BlockType.GRASS: color = Color.GREEN
		BlockType.DIRT: color = Color.SADDLE_BROWN
		BlockType.STONE: color = Color.GRAY
	
	surface_tool.set_color(color)
	
	# Define the 8 corners of the cube
	var v000: Vector3 = Vector3(x, y, z)
	var v100: Vector3 = Vector3(x + 1, y, z)
	var v010: Vector3 = Vector3(x, y + 1, z)
	var v110: Vector3 = Vector3(x + 1, y + 1, z)
	var v001: Vector3 = Vector3(x, y, z + 1)
	var v101: Vector3 = Vector3(x + 1, y, z + 1)
	var v011: Vector3 = Vector3(x, y + 1, z + 1)
	var v111: Vector3 = Vector3(x + 1, y + 1, z + 1)
	
	# Top face (Y+)
	if _is_transparent(x, y + 1, z):
		surface_tool.add_vertex(v010)
		surface_tool.add_vertex(v110)
		surface_tool.add_vertex(v111)
		surface_tool.add_vertex(v010)
		surface_tool.add_vertex(v111)
		surface_tool.add_vertex(v011)
	
	# Bottom face (Y-)
	if _is_transparent(x, y - 1, z):
		surface_tool.add_vertex(v000)
		surface_tool.add_vertex(v001)
		surface_tool.add_vertex(v101)
		surface_tool.add_vertex(v000)
		surface_tool.add_vertex(v101)
		surface_tool.add_vertex(v100)
	
	# Front face (Z+)
	if _is_transparent(x, y, z + 1):
		surface_tool.add_vertex(v001)
		surface_tool.add_vertex(v011)
		surface_tool.add_vertex(v111)
		surface_tool.add_vertex(v001)
		surface_tool.add_vertex(v111)
		surface_tool.add_vertex(v101)
	
	# Back face (Z-)
	if _is_transparent(x, y, z - 1):
		surface_tool.add_vertex(v100)
		surface_tool.add_vertex(v110)
		surface_tool.add_vertex(v010)
		surface_tool.add_vertex(v100)
		surface_tool.add_vertex(v010)
		surface_tool.add_vertex(v000)
	
	# Right face (X+)
	if _is_transparent(x + 1, y, z):
		surface_tool.add_vertex(v100)
		surface_tool.add_vertex(v101)
		surface_tool.add_vertex(v111)
		surface_tool.add_vertex(v100)
		surface_tool.add_vertex(v111)
		surface_tool.add_vertex(v110)
	
	# Left face (X-)
	if _is_transparent(x - 1, y, z):
		surface_tool.add_vertex(v000)
		surface_tool.add_vertex(v010)
		surface_tool.add_vertex(v011)
		surface_tool.add_vertex(v000)
		surface_tool.add_vertex(v011)
		surface_tool.add_vertex(v001)

func _get_index(x: int, y: int, z: int) -> int:
	return x + (y * CHUNK_SIZE) + (z * CHUNK_SIZE * CHUNK_SIZE)

func _get_voxel(x: int, y: int, z: int) -> int:
	# Check if coordinates are within this chunk
	if x >= 0 and x < CHUNK_SIZE and y >= 0 and y < CHUNK_SIZE and z >= 0 and z < CHUNK_SIZE:
		return voxels[_get_index(x, y, z)]
	
	# Y out of bounds - always return AIR
	if y < 0 or y >= CHUNK_SIZE:
		return BlockType.AIR
	
	# Out of bounds in X or Z - check neighboring chunk if we have a world reference
	if world != null:
		var neighbor_chunk_pos: Vector2i = Vector2i(chunk_pos.x, chunk_pos.y)  # Explicit copy
		var local_x: int = x
		var local_z: int = z
		
		# Only handle single-axis neighbors (not diagonal)
		# Adjust chunk position and local coordinates for X
		if x < 0:
			neighbor_chunk_pos.x -= 1
			local_x = CHUNK_SIZE - 1  # Last column of neighbor
		elif x >= CHUNK_SIZE:
			neighbor_chunk_pos.x += 1
			local_x = 0  # First column of neighbor
		
		# Adjust chunk position and local coordinates for Z
		if z < 0:
			neighbor_chunk_pos.y -= 1
			local_z = CHUNK_SIZE - 1  # Last row of neighbor
		elif z >= CHUNK_SIZE:
			neighbor_chunk_pos.y += 1
			local_z = 0  # First row of neighbor
		
		# Find the neighbor chunk
		var chunk_name: String = "Chunk_%d_%d" % [neighbor_chunk_pos.x, neighbor_chunk_pos.y]
		var neighbor: Node = world.get_node_or_null(chunk_name)
		
		if neighbor != null:
			# Direct array access to avoid recursion
			if local_x >= 0 and local_x < CHUNK_SIZE and local_z >= 0 and local_z < CHUNK_SIZE:
				var idx: int = local_x + (y * CHUNK_SIZE) + (local_z * CHUNK_SIZE * CHUNK_SIZE)
				return neighbor.voxels[idx]
	
	# Default to AIR if no neighbor
	return BlockType.AIR

func _is_transparent(x: int, y: int, z: int) -> bool:
	return _get_voxel(x, y, z) == BlockType.AIR

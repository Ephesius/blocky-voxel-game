extends Node3D
class_name Chunk

const CHUNK_SIZE = 16

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

var mesh_instance: MeshInstance3D
var noise: FastNoiseLite

func _ready() -> void:
	mesh_instance = MeshInstance3D.new()
	add_child(mesh_instance)
	
	# Initialize empty voxels
	voxels.resize(CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE)
	voxels.fill(BlockType.AIR)
	
	# Temporary: Generate some noise data
	_generate_data()
	_update_mesh()

func _generate_data() -> void:
	noise = FastNoiseLite.new()
	noise.seed = randi()
	noise.frequency = 0.05
	
	for x in range(CHUNK_SIZE):
		for z in range(CHUNK_SIZE):
			var height = int((noise.get_noise_2d(x, z) + 1.0) * 0.5 * CHUNK_SIZE)
			for y in range(CHUNK_SIZE):
				if y < height:
					var index = _get_index(x, y, z)
					if y == height - 1:
						voxels[index] = BlockType.GRASS
					elif y > height - 4:
						voxels[index] = BlockType.DIRT
					else:
						voxels[index] = BlockType.STONE

func _update_mesh() -> void:
	var st = SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	
	for x in range(CHUNK_SIZE):
		for y in range(CHUNK_SIZE):
			for z in range(CHUNK_SIZE):
				var type = _get_voxel(x, y, z)
				if type != BlockType.AIR:
					_create_block_faces(st, x, y, z, type)
	
	# Don't generate normals - we want sharp edges, not smooth
	st.index()
	
	var mesh = st.commit()
	
	# Create material with no backface culling for debugging
	var material = StandardMaterial3D.new()
	material.vertex_color_use_as_albedo = true
	material.cull_mode = BaseMaterial3D.CULL_DISABLED
	mesh.surface_set_material(0, material)
	
	mesh_instance.mesh = mesh
	mesh_instance.create_trimesh_collision()

func _create_block_faces(st: SurfaceTool, x: int, y: int, z: int, type: int) -> void:
	var color = Color.WHITE
	match type:
		BlockType.GRASS: color = Color.GREEN
		BlockType.DIRT: color = Color.SADDLE_BROWN
		BlockType.STONE: color = Color.GRAY
	
	st.set_color(color)
	
	# Define the 8 corners of the cube
	var v000 = Vector3(x, y, z)
	var v100 = Vector3(x + 1, y, z)
	var v010 = Vector3(x, y + 1, z)
	var v110 = Vector3(x + 1, y + 1, z)
	var v001 = Vector3(x, y, z + 1)
	var v101 = Vector3(x + 1, y, z + 1)
	var v011 = Vector3(x, y + 1, z + 1)
	var v111 = Vector3(x + 1, y + 1, z + 1)
	
	# Top face (Y+)
	if _is_transparent(x, y + 1, z):
		st.add_vertex(v010)
		st.add_vertex(v110)
		st.add_vertex(v111)
		st.add_vertex(v010)
		st.add_vertex(v111)
		st.add_vertex(v011)
	
	# Bottom face (Y-)
	if _is_transparent(x, y - 1, z):
		st.add_vertex(v000)
		st.add_vertex(v001)
		st.add_vertex(v101)
		st.add_vertex(v000)
		st.add_vertex(v101)
		st.add_vertex(v100)
	
	# Front face (Z+)
	if _is_transparent(x, y, z + 1):
		st.add_vertex(v001)
		st.add_vertex(v011)
		st.add_vertex(v111)
		st.add_vertex(v001)
		st.add_vertex(v111)
		st.add_vertex(v101)
	
	# Back face (Z-)
	if _is_transparent(x, y, z - 1):
		st.add_vertex(v100)
		st.add_vertex(v110)
		st.add_vertex(v010)
		st.add_vertex(v100)
		st.add_vertex(v010)
		st.add_vertex(v000)
	
	# Right face (X+)
	if _is_transparent(x + 1, y, z):
		st.add_vertex(v100)
		st.add_vertex(v101)
		st.add_vertex(v111)
		st.add_vertex(v100)
		st.add_vertex(v111)
		st.add_vertex(v110)
	
	# Left face (X-)
	if _is_transparent(x - 1, y, z):
		st.add_vertex(v000)
		st.add_vertex(v010)
		st.add_vertex(v011)
		st.add_vertex(v000)
		st.add_vertex(v011)
		st.add_vertex(v001)

func _get_index(x: int, y: int, z: int) -> int:
	return x + (y * CHUNK_SIZE) + (z * CHUNK_SIZE * CHUNK_SIZE)

func _get_voxel(x: int, y: int, z: int) -> int:
	if x < 0 or x >= CHUNK_SIZE or y < 0 or y >= CHUNK_SIZE or z < 0 or z >= CHUNK_SIZE:
		return BlockType.AIR
	return voxels[_get_index(x, y, z)]

func _is_transparent(x: int, y: int, z: int) -> bool:
	return _get_voxel(x, y, z) == BlockType.AIR

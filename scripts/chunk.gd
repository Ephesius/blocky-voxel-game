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
	
	# Simple material (placeholder)
	var material = StandardMaterial3D.new()
	material.vertex_color_use_as_albedo = true
	st.set_material(material)
	
	for x in range(CHUNK_SIZE):
		for y in range(CHUNK_SIZE):
			for z in range(CHUNK_SIZE):
				var type = _get_voxel(x, y, z)
				if type != BlockType.AIR:
					_create_block_faces(st, x, y, z, type)
	
	st.index()
	mesh_instance.mesh = st.commit()
	# Create collision
	mesh_instance.create_trimesh_collision()

func _create_block_faces(st: SurfaceTool, x: int, y: int, z: int, type: int) -> void:
	var color = Color.WHITE
	match type:
		BlockType.GRASS: color = Color.GREEN
		BlockType.DIRT: color = Color.SADDLE_BROWN
		BlockType.STONE: color = Color.GRAY
	
	st.set_color(color)
	
	# Check neighbors (Simple Culling)
	# Top
	if _is_transparent(x, y + 1, z):
		st.set_normal(Vector3.UP)
		st.set_uv(Vector2(0, 0)); st.add_vertex(Vector3(x, y + 1, z))
		st.set_uv(Vector2(1, 0)); st.add_vertex(Vector3(x + 1, y + 1, z))
		st.set_uv(Vector2(1, 1)); st.add_vertex(Vector3(x + 1, y + 1, z + 1))
		
		st.set_uv(Vector2(0, 0)); st.add_vertex(Vector3(x, y + 1, z))
		st.set_uv(Vector2(1, 1)); st.add_vertex(Vector3(x + 1, y + 1, z + 1))
		st.set_uv(Vector2(0, 1)); st.add_vertex(Vector3(x, y + 1, z + 1))

	# Bottom
	if _is_transparent(x, y - 1, z):
		st.set_normal(Vector3.DOWN)
		st.set_uv(Vector2(0, 0)); st.add_vertex(Vector3(x, y, z + 1))
		st.set_uv(Vector2(1, 0)); st.add_vertex(Vector3(x + 1, y, z + 1))
		st.set_uv(Vector2(1, 1)); st.add_vertex(Vector3(x + 1, y, z))
		
		st.set_uv(Vector2(0, 0)); st.add_vertex(Vector3(x, y, z + 1))
		st.set_uv(Vector2(1, 1)); st.add_vertex(Vector3(x + 1, y, z))
		st.set_uv(Vector2(0, 1)); st.add_vertex(Vector3(x, y, z))

	# Left
	if _is_transparent(x - 1, y, z):
		st.set_normal(Vector3.LEFT)
		st.set_uv(Vector2(0, 0)); st.add_vertex(Vector3(x, y, z + 1))
		st.set_uv(Vector2(1, 0)); st.add_vertex(Vector3(x, y, z))
		st.set_uv(Vector2(1, 1)); st.add_vertex(Vector3(x, y + 1, z))
		
		st.set_uv(Vector2(0, 0)); st.add_vertex(Vector3(x, y, z + 1))
		st.set_uv(Vector2(1, 1)); st.add_vertex(Vector3(x, y + 1, z))
		st.set_uv(Vector2(0, 1)); st.add_vertex(Vector3(x, y + 1, z + 1))

	# Right
	if _is_transparent(x + 1, y, z):
		st.set_normal(Vector3.RIGHT)
		st.set_uv(Vector2(0, 0)); st.add_vertex(Vector3(x + 1, y + 1, z))
		st.set_uv(Vector2(1, 0)); st.add_vertex(Vector3(x + 1, y, z))
		st.set_uv(Vector2(1, 1)); st.add_vertex(Vector3(x + 1, y, z + 1))
		
		st.set_uv(Vector2(0, 0)); st.add_vertex(Vector3(x + 1, y + 1, z))
		st.set_uv(Vector2(1, 1)); st.add_vertex(Vector3(x + 1, y, z + 1))
		st.set_uv(Vector2(0, 1)); st.add_vertex(Vector3(x + 1, y + 1, z + 1))

	# Front
	if _is_transparent(x, y, z + 1):
		st.set_normal(Vector3.BACK)
		st.set_uv(Vector2(0, 0)); st.add_vertex(Vector3(x, y + 1, z + 1))
		st.set_uv(Vector2(1, 0)); st.add_vertex(Vector3(x + 1, y + 1, z + 1))
		st.set_uv(Vector2(1, 1)); st.add_vertex(Vector3(x + 1, y, z + 1))
		
		st.set_uv(Vector2(0, 0)); st.add_vertex(Vector3(x, y + 1, z + 1))
		st.set_uv(Vector2(1, 1)); st.add_vertex(Vector3(x + 1, y, z + 1))
		st.set_uv(Vector2(0, 1)); st.add_vertex(Vector3(x, y, z + 1))

	# Back
	if _is_transparent(x, y, z - 1):
		st.set_normal(Vector3.FORWARD)
		st.set_uv(Vector2(0, 0)); st.add_vertex(Vector3(x + 1, y + 1, z))
		st.set_uv(Vector2(1, 0)); st.add_vertex(Vector3(x, y + 1, z))
		st.set_uv(Vector2(1, 1)); st.add_vertex(Vector3(x, y, z))
		
		st.set_uv(Vector2(0, 0)); st.add_vertex(Vector3(x + 1, y + 1, z))
		st.set_uv(Vector2(1, 1)); st.add_vertex(Vector3(x, y, z))
		st.set_uv(Vector2(0, 1)); st.add_vertex(Vector3(x + 1, y, z))

func _get_index(x: int, y: int, z: int) -> int:
	return x + (y * CHUNK_SIZE) + (z * CHUNK_SIZE * CHUNK_SIZE)

func _get_voxel(x: int, y: int, z: int) -> int:
	if x < 0 or x >= CHUNK_SIZE or y < 0 or y >= CHUNK_SIZE or z < 0 or z >= CHUNK_SIZE:
		return BlockType.AIR
	return voxels[_get_index(x, y, z)]

func _is_transparent(x: int, y: int, z: int) -> bool:
	return _get_voxel(x, y, z) == BlockType.AIR

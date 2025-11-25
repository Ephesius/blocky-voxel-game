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
var chunk_pos: Vector3i  # Chunk position in chunk coordinates (not world)
var world: Node3D  # Reference to voxel_world for neighbor lookups

var mesh_instance: MeshInstance3D
var wireframe_instance: MeshInstance3D
var show_wireframe: bool = true  # Toggle for debugging
var has_collision: bool = false  # Track collision state

func _ready() -> void:
	mesh_instance = MeshInstance3D.new()
	add_child(mesh_instance)
	
	# Create wireframe mesh instance
	wireframe_instance = MeshInstance3D.new()
	add_child(wireframe_instance)
	
	# Initialize empty voxels array
	voxels.resize(CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE)
	voxels.fill(BlockType.AIR)

func set_voxel_data(data: Array[int]) -> void:
	# Set the voxel data for this chunk
	voxels = data
	_update_mesh()

func update_collision(enable: bool) -> void:
	# Only update if state is changing
	if enable == has_collision:
		return
	
	if enable and mesh_instance.mesh != null and mesh_instance.mesh.get_surface_count() > 0:
		mesh_instance.create_trimesh_collision()
		has_collision = true
	elif not enable and has_collision:
		# Remove collision if it exists
		for child in mesh_instance.get_children():
			if child is StaticBody3D:
				child.queue_free()
		has_collision = false



func _update_mesh() -> void:
	var surface_tool: SurfaceTool = SurfaceTool.new()
	surface_tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	
	# Naive meshing: one quad per visible face
	# Much simpler and faster than greedy meshing for GDScript
	_naive_mesh(surface_tool)
	
	# Index the mesh for better performance
	surface_tool.index()
	
	var mesh: ArrayMesh = surface_tool.commit()
	
	# Only set material and collision if the mesh has surfaces
	if mesh.get_surface_count() > 0:
		# Create material with backface culling enabled
		var material: StandardMaterial3D = StandardMaterial3D.new()
		material.vertex_color_use_as_albedo = true
		material.cull_mode = BaseMaterial3D.CULL_BACK
		mesh.surface_set_material(0, material)
		
		mesh_instance.mesh = mesh
		# Note: Collision is now created separately via update_collision()
	else:
		# Empty chunk - just set an empty mesh
		mesh_instance.mesh = mesh

func _naive_mesh(surface_tool: SurfaceTool) -> void:
	# Simple naive meshing: iterate through all blocks
	# For each solid block, check each face and add a quad if exposed
	
	# Face definitions: [offset, axis, direction, normal]
	# We'll check 6 faces: +X, -X, +Y, -Y, +Z, -Z
	var faces: Array = [
		{"offset": Vector3i(1, 0, 0), "vertices": _get_face_vertices(0, 1)},   # +X
		{"offset": Vector3i(-1, 0, 0), "vertices": _get_face_vertices(0, -1)}, # -X
		{"offset": Vector3i(0, 1, 0), "vertices": _get_face_vertices(1, 1)},   # +Y
		{"offset": Vector3i(0, -1, 0), "vertices": _get_face_vertices(1, -1)}, # -Y
		{"offset": Vector3i(0, 0, 1), "vertices": _get_face_vertices(2, 1)},   # +Z
		{"offset": Vector3i(0, 0, -1), "vertices": _get_face_vertices(2, -1)}  # -Z
	]
	
	# Iterate through all voxels
	for x in range(CHUNK_SIZE):
		for y in range(CHUNK_SIZE):
			for z in range(CHUNK_SIZE):
				var block_type: int = voxels[_get_index(x, y, z)]
				
				# Skip air blocks
				if block_type == BlockType.AIR:
					continue
				
				# Set color based on block type
				var color: Color = _get_block_color(block_type)
				surface_tool.set_color(color)
				
				# Check each face
				for face in faces:
					var neighbor_pos: Vector3i = Vector3i(x, y, z) + face["offset"]
					var neighbor: int = _get_voxel(neighbor_pos.x, neighbor_pos.y, neighbor_pos.z)
					
					# If neighbor is air, this face is exposed
					if neighbor == BlockType.AIR:
						_add_face(surface_tool, Vector3(x, y, z), face["vertices"])

func _get_block_color(block_type: int) -> Color:
	match block_type:
		BlockType.GRASS: return Color.GREEN
		BlockType.DIRT: return Color.SADDLE_BROWN
		BlockType.STONE: return Color.GRAY
		_: return Color.WHITE

func _get_face_vertices(axis: int, direction: int) -> Array:
	# Returns the 4 vertices for a face in local block coordinates (0-1 range)
	# Axis: 0=X, 1=Y, 2=Z
	# Direction: 1=positive, -1=negative
	
	var verts: Array = []
	
	if axis == 0:  # X faces (YZ plane)
		var x_offset: float = 1.0 if direction > 0 else 0.0
		if direction > 0:
			# +X face (facing right)
			verts = [
				Vector3(x_offset, 0, 0),
				Vector3(x_offset, 0, 1),
				Vector3(x_offset, 1, 1),
				Vector3(x_offset, 1, 0)
			]
		else:
			# -X face (facing left)
			verts = [
				Vector3(x_offset, 0, 1),
				Vector3(x_offset, 0, 0),
				Vector3(x_offset, 1, 0),
				Vector3(x_offset, 1, 1)
			]
	elif axis == 1:  # Y faces (XZ plane)
		var y_offset: float = 1.0 if direction > 0 else 0.0
		if direction > 0:
			# +Y face (facing up)
			verts = [
				Vector3(0, y_offset, 0),
				Vector3(1, y_offset, 0),
				Vector3(1, y_offset, 1),
				Vector3(0, y_offset, 1)
			]
		else:
			# -Y face (facing down)
			verts = [
				Vector3(0, y_offset, 1),
				Vector3(1, y_offset, 1),
				Vector3(1, y_offset, 0),
				Vector3(0, y_offset, 0)
			]
	else:  # Z faces (XY plane)
		var z_offset: float = 1.0 if direction > 0 else 0.0
		if direction > 0:
			# +Z face (facing forward)
			verts = [
				Vector3(0, 0, z_offset),
				Vector3(0, 1, z_offset),
				Vector3(1, 1, z_offset),
				Vector3(1, 0, z_offset)
			]
		else:
			# -Z face (facing back)
			verts = [
				Vector3(1, 0, z_offset),
				Vector3(1, 1, z_offset),
				Vector3(0, 1, z_offset),
				Vector3(0, 0, z_offset)
			]
	
	return verts

func _add_face(surface_tool: SurfaceTool, block_pos: Vector3, vertices: Array) -> void:
	# Add a quad (2 triangles) for this face
	# vertices[0-3] are in local block space (0-1)
	# block_pos is the block position in chunk space
	
	var v1: Vector3 = block_pos + vertices[0]
	var v2: Vector3 = block_pos + vertices[1]
	var v3: Vector3 = block_pos + vertices[2]
	var v4: Vector3 = block_pos + vertices[3]
	
	# First triangle
	surface_tool.add_vertex(v1)
	surface_tool.add_vertex(v2)
	surface_tool.add_vertex(v3)
	
	# Second triangle
	surface_tool.add_vertex(v1)
	surface_tool.add_vertex(v3)
	surface_tool.add_vertex(v4)


func _create_wireframe(quads: Array) -> void:
	# Create a line mesh for the wireframe
	var line_tool: SurfaceTool = SurfaceTool.new()
	line_tool.begin(Mesh.PRIMITIVE_LINES)
	
	# Set wireframe color (black)
	line_tool.set_color(Color.BLACK)
	
	for quad in quads:
		var v1: Vector3 = quad[0]
		var v2: Vector3 = quad[1]
		var v3: Vector3 = quad[2]
		var v4: Vector3 = quad[3]
		
		# Draw the 4 edges of the quad
		line_tool.add_vertex(v1)
		line_tool.add_vertex(v2)
		
		line_tool.add_vertex(v2)
		line_tool.add_vertex(v3)
		
		line_tool.add_vertex(v3)
		line_tool.add_vertex(v4)
		
		line_tool.add_vertex(v4)
		line_tool.add_vertex(v1)
	
	var wireframe_mesh: ArrayMesh = line_tool.commit()
	
	# Create material for wireframe
	var wireframe_material: StandardMaterial3D = StandardMaterial3D.new()
	wireframe_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	wireframe_material.albedo_color = Color.BLACK
	wireframe_material.disable_receive_shadows = true
	wireframe_mesh.surface_set_material(0, wireframe_material)
	
	wireframe_instance.mesh = wireframe_mesh

func _get_index(x: int, y: int, z: int) -> int:
	return x + (y * CHUNK_SIZE) + (z * CHUNK_SIZE * CHUNK_SIZE)

func _get_voxel(x: int, y: int, z: int) -> int:
	# Check if coordinates are within this chunk
	if x >= 0 and x < CHUNK_SIZE and y >= 0 and y < CHUNK_SIZE and z >= 0 and z < CHUNK_SIZE:
		return voxels[_get_index(x, y, z)]
	
	# Out of bounds - check neighboring chunk if we have a world reference
	if world != null:
		var neighbor_chunk_pos: Vector3i = Vector3i(chunk_pos.x, chunk_pos.y, chunk_pos.z)  # Explicit copy
		var local_x: int = x
		var local_y: int = y
		var local_z: int = z
		
		# Adjust chunk position and local coordinates for X
		if x < 0:
			neighbor_chunk_pos.x -= 1
			local_x = CHUNK_SIZE - 1
		elif x >= CHUNK_SIZE:
			neighbor_chunk_pos.x += 1
			local_x = 0
		
		# Adjust chunk position and local coordinates for Y
		if y < 0:
			neighbor_chunk_pos.y -= 1
			local_y = CHUNK_SIZE - 1
		elif y >= CHUNK_SIZE:
			neighbor_chunk_pos.y += 1
			local_y = 0
		
		# Adjust chunk position and local coordinates for Z
		if z < 0:
			neighbor_chunk_pos.z -= 1
			local_z = CHUNK_SIZE - 1
		elif z >= CHUNK_SIZE:
			neighbor_chunk_pos.z += 1
			local_z = 0
		
		# Find the neighbor chunk
		var chunk_name: String = "Chunk_%d_%d_%d" % [neighbor_chunk_pos.x, neighbor_chunk_pos.y, neighbor_chunk_pos.z]
		var neighbor: Node = world.get_node_or_null(chunk_name)
		
		if neighbor != null:
			# Direct array access to avoid recursion
			if local_x >= 0 and local_x < CHUNK_SIZE and local_y >= 0 and local_y < CHUNK_SIZE and local_z >= 0 and local_z < CHUNK_SIZE:
				var idx: int = local_x + (local_y * CHUNK_SIZE) + (local_z * CHUNK_SIZE * CHUNK_SIZE)
				return neighbor.voxels[idx]
	
	# Default to AIR if no neighbor
	return BlockType.AIR

func _is_transparent(x: int, y: int, z: int) -> bool:
	return _get_voxel(x, y, z) == BlockType.AIR

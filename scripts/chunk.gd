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
	# Add or remove collision based on distance to player
	if enable and mesh_instance.mesh != null:
		if not mesh_instance.get_child_count() > 0:  # Check if collision doesn't exist
			mesh_instance.create_trimesh_collision()
	elif not enable:
		# Remove collision if it exists
		for child in mesh_instance.get_children():
			if child is StaticBody3D:
				child.queue_free()



func _update_mesh() -> void:
	var surface_tool: SurfaceTool = SurfaceTool.new()
	surface_tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	
	# Use greedy meshing for each axis
	var quads: Array = []  # Store quad data for wireframe
	_greedy_mesh(surface_tool, quads)
	
	# Don't generate normals - we want sharp edges, not smooth
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
		
		# Generate wireframe if enabled
		if show_wireframe:
			_create_wireframe(quads)
	else:
		# Empty chunk - just set an empty mesh
		mesh_instance.mesh = mesh

func _greedy_mesh(surface_tool: SurfaceTool, quads: Array) -> void:
	# Process each of the 6 face directions
	# For each direction, we'll create a 2D slice and greedily merge faces
	
	# Axis directions: 0=X, 1=Y, 2=Z
	# Face directions: -1=negative, +1=positive
	for axis: int in range(3):
		for direction: int in [-1, 1]:
			_greedy_mesh_axis(surface_tool, axis, direction, quads)

func _greedy_mesh_axis(surface_tool: SurfaceTool, axis: int, direction: int, quads: Array) -> void:
	# Create a 2D mask for this slice
	var mask: Array = []
	mask.resize(CHUNK_SIZE * CHUNK_SIZE)
	
	# Determine the two axes perpendicular to the main axis
	var u_axis: int = (axis + 1) % 3
	var v_axis: int = (axis + 2) % 3
	
	# Iterate through each slice along the main axis
	for d: int in range(CHUNK_SIZE):
		# Clear the mask
		for i: int in range(CHUNK_SIZE * CHUNK_SIZE):
			mask[i] = null
		
		# Build the mask for this slice
		for u: int in range(CHUNK_SIZE):
			for v: int in range(CHUNK_SIZE):
				# Get the position in 3D space
				var pos: Vector3i = Vector3i.ZERO
				pos[axis] = d
				pos[u_axis] = u
				pos[v_axis] = v
				
				# Check if we should render a face here
				var current_block: int = _get_voxel(pos.x, pos.y, pos.z)
				
				if current_block != BlockType.AIR:
					# Check the neighbor in the direction we're facing
					var neighbor_pos: Vector3i = pos
					neighbor_pos[axis] += direction
					var neighbor_block: int = _get_voxel(neighbor_pos.x, neighbor_pos.y, neighbor_pos.z)
					
					# If neighbor is air or transparent, we need to render this face
					if neighbor_block == BlockType.AIR:
						mask[u + v * CHUNK_SIZE] = current_block
		
		# Generate mesh from mask using greedy algorithm
		_generate_mesh_from_mask(surface_tool, mask, axis, direction, d, u_axis, v_axis, quads)

func _generate_mesh_from_mask(surface_tool: SurfaceTool, mask: Array, axis: int, direction: int, d: int, u_axis: int, v_axis: int, quads: Array) -> void:
	# Greedy meshing: expand rectangles as much as possible
	for v: int in range(CHUNK_SIZE):
		for u: int in range(CHUNK_SIZE):
			var block_type = mask[u + v * CHUNK_SIZE]
			
			if block_type == null:
				continue
			
			# Compute width (u direction)
			var width: int = 1
			while u + width < CHUNK_SIZE and mask[u + width + v * CHUNK_SIZE] == block_type:
				width += 1
			
			# Compute height (v direction)
			var height: int = 1
			var done: bool = false
			while v + height < CHUNK_SIZE and not done:
				# Check if we can extend in the v direction
				for k: int in range(width):
					if mask[u + k + (v + height) * CHUNK_SIZE] != block_type:
						done = true
						break
				if not done:
					height += 1
			
			# Create the quad for this rectangle
			_create_greedy_quad(surface_tool, block_type, axis, direction, d, u, v, width, height, u_axis, v_axis, quads)
			
			# Clear the mask for the area we just meshed
			for j: int in range(height):
				for i: int in range(width):
					mask[u + i + (v + j) * CHUNK_SIZE] = null

func _create_greedy_quad(surface_tool: SurfaceTool, block_type: int, axis: int, direction: int, d: int, u: int, v: int, width: int, height: int, u_axis: int, v_axis: int, quads: Array) -> void:
	# Set color based on block type
	var color: Color = Color.WHITE
	match block_type:
		BlockType.GRASS: color = Color.GREEN
		BlockType.DIRT: color = Color.SADDLE_BROWN
		BlockType.STONE: color = Color.GRAY
	
	surface_tool.set_color(color)
	
	# Create the four corners of the quad
	var v1: Vector3 = Vector3.ZERO
	var v2: Vector3 = Vector3.ZERO
	var v3: Vector3 = Vector3.ZERO
	var v4: Vector3 = Vector3.ZERO
	
	# Position the quad based on axis and direction
	# For positive direction, we want the far side (d+1)
	# For negative direction, we want the near side (d)
	var offset: float = float(d + 1) if direction > 0 else float(d)
	
	v1[axis] = offset
	v1[u_axis] = u
	v1[v_axis] = v
	
	v2[axis] = offset
	v2[u_axis] = u + width
	v2[v_axis] = v
	
	v3[axis] = offset
	v3[u_axis] = u + width
	v3[v_axis] = v + height
	
	v4[axis] = offset
	v4[u_axis] = u
	v4[v_axis] = v + height
	
	# Add vertices in correct winding order based on direction
	if direction > 0:
		# Positive direction - counter-clockwise when viewed from outside
		surface_tool.add_vertex(v1)
		surface_tool.add_vertex(v4)
		surface_tool.add_vertex(v3)
		surface_tool.add_vertex(v1)
		surface_tool.add_vertex(v3)
		surface_tool.add_vertex(v2)
	else:
		# Negative direction - counter-clockwise when viewed from outside
		surface_tool.add_vertex(v1)
		surface_tool.add_vertex(v2)
		surface_tool.add_vertex(v3)
		surface_tool.add_vertex(v1)
		surface_tool.add_vertex(v3)
		surface_tool.add_vertex(v4)
	
	# Store quad vertices for wireframe
	quads.append([v1, v2, v3, v4])

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

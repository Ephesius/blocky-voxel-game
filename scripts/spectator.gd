extends Camera3D

@export var move_speed: float = 10.0
@export var look_sensitivity: float = 0.002
@export var sprint_multiplier: float = 2.0

var _velocity: Vector3 = Vector3.ZERO

func _ready() -> void:
	Input.set_mouse_mode(Input.MOUSE_MODE_CAPTURED)

func _input(event: InputEvent) -> void:
	if event is InputEventMouseMotion and Input.get_mouse_mode() == Input.MOUSE_MODE_CAPTURED:
		rotate_y(-event.relative.x * look_sensitivity)
		rotate_x(-event.relative.y * look_sensitivity)
		rotation.x = clamp(rotation.x, deg_to_rad(-90), deg_to_rad(90))
	
	if event.is_action_pressed("ui_cancel"):
		if Input.get_mouse_mode() == Input.MOUSE_MODE_CAPTURED:
			Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)
		else:
			Input.set_mouse_mode(Input.MOUSE_MODE_CAPTURED)

func _process(delta: float) -> void:
	var input_dir := Input.get_vector("ui_left", "ui_right", "ui_up", "ui_down")
	var direction := (transform.basis * Vector3(input_dir.x, 0, input_dir.y)).normalized()
	
	var current_speed := move_speed
	if Input.is_key_pressed(KEY_SHIFT):
		current_speed *= sprint_multiplier
		
	# Vertical movement
	var vertical_input := 0.0
	if Input.is_key_pressed(KEY_E):
		vertical_input += 1.0
	if Input.is_key_pressed(KEY_Q):
		vertical_input -= 1.0
	
	# Apply movement relative to camera orientation for horizontal, global up/down for vertical
	# Actually for a free cam, we usually want to move in the direction we are looking
	# But standard WASD is usually "planar" relative to view, or full free flight.
	# Let's do full free flight (moving forward moves in the direction the camera is facing)
	
	var forward = -global_transform.basis.z
	var right = global_transform.basis.x
	var up = global_transform.basis.y
	
	var move_vec = (right * input_dir.x + forward * -input_dir.y).normalized()
	
	# Add vertical movement (Q/E) relative to world up, or camera up? 
	# Usually Q/E is absolute up/down in world space for level editors/spectators
	var vertical_vec = Vector3.UP * vertical_input
	
	global_position += (move_vec + vertical_vec) * current_speed * delta

extends Camera3D

@export var move_speed: float = 10.0
@export var look_sensitivity: float = 0.003
@export var sprint_multiplier: float = 2.0

var yaw: float = 0.0
var pitch: float = 0.0

func _ready() -> void:
	Input.set_mouse_mode(Input.MOUSE_MODE_CAPTURED)

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion and Input.get_mouse_mode() == Input.MOUSE_MODE_CAPTURED:
		yaw -= event.relative.x * look_sensitivity
		pitch -= event.relative.y * look_sensitivity
		pitch = clamp(pitch, -PI/2, PI/2)
	
	if event.is_action_pressed("ui_cancel"):
		if Input.get_mouse_mode() == Input.MOUSE_MODE_CAPTURED:
			Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)
		else:
			Input.set_mouse_mode(Input.MOUSE_MODE_CAPTURED)

func _process(delta: float) -> void:
	# Apply rotation
	rotation.y = yaw
	rotation.x = pitch
	
	# Get input
	var input_dir := Vector3.ZERO
	
	if Input.is_key_pressed(KEY_W):
		input_dir.z += 1
	if Input.is_key_pressed(KEY_S):
		input_dir.z -= 1
	if Input.is_key_pressed(KEY_A):
		input_dir.x -= 1
	if Input.is_key_pressed(KEY_D):
		input_dir.x += 1
	
	# Vertical movement
	if Input.is_key_pressed(KEY_E):
		input_dir.y += 1
	if Input.is_key_pressed(KEY_Q):
		input_dir.y -= 1
	
	# Normalize to prevent faster diagonal movement
	if input_dir.length() > 0:
		input_dir = input_dir.normalized()
	
	# Apply speed
	var speed := move_speed
	if Input.is_key_pressed(KEY_SHIFT):
		speed *= sprint_multiplier
	
	# Transform direction to world space (only for horizontal movement)
	var h_rot := rotation.y
	var forward := Vector3(sin(h_rot), 0, cos(h_rot))
	var right := Vector3(cos(h_rot), 0, -sin(h_rot))
	
	var movement := forward * -input_dir.z + right * input_dir.x
	movement.y = input_dir.y  # Keep vertical movement in world space
	
	global_position += movement * speed * delta

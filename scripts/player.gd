extends CharacterBody3D

@export var walk_speed: float = 5.0
@export var sprint_speed: float = 8.0
@export var jump_velocity: float = 6.0
@export var fly_speed: float = 10.0
@export var fly_sprint_speed: float = 20.0
@export var look_sensitivity: float = 0.003
@export var gravity: float = 20.0

var yaw: float = 0.0
var pitch: float = 0.0
var is_flying: bool = false

@onready var camera: Camera3D = $Camera3D

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
	
	# Toggle fly mode with F key
	if event is InputEventKey and event.pressed and event.keycode == KEY_F:
		is_flying = !is_flying
		print("Fly mode: ", "ON" if is_flying else "OFF")

func _physics_process(delta: float) -> void:
	# Apply rotation to the body (yaw) and camera (pitch)
	rotation.y = yaw
	camera.rotation.x = pitch
	
	# Get input direction
	var input_dir := Vector3.ZERO
	
	if Input.is_key_pressed(KEY_W):
		input_dir.z += 1
	if Input.is_key_pressed(KEY_S):
		input_dir.z -= 1
	if Input.is_key_pressed(KEY_A):
		input_dir.x -= 1
	if Input.is_key_pressed(KEY_D):
		input_dir.x += 1
	
	if is_flying:
		_handle_flying(delta, input_dir)
	else:
		_handle_walking(delta, input_dir)
	
	move_and_slide()

func _handle_walking(delta: float, input_dir: Vector3) -> void:
	# Apply gravity
	if not is_on_floor():
		velocity.y -= gravity * delta
	
	# Handle jump
	if Input.is_key_pressed(KEY_SPACE) and is_on_floor():
		velocity.y = jump_velocity
	
	# Get movement speed
	var speed := walk_speed
	if Input.is_key_pressed(KEY_SHIFT):
		speed = sprint_speed
	
	# Calculate movement direction (only horizontal)
	var h_rot := rotation.y
	var forward := Vector3(sin(h_rot), 0, cos(h_rot))
	var right := Vector3(cos(h_rot), 0, -sin(h_rot))
	
	var movement := forward * -input_dir.z + right * input_dir.x
	
	if movement.length() > 0:
		movement = movement.normalized()
	
	# Apply horizontal velocity
	velocity.x = movement.x * speed
	velocity.z = movement.z * speed

func _handle_flying(_delta: float, input_dir: Vector3) -> void:
	# Vertical movement in fly mode
	if Input.is_key_pressed(KEY_SPACE):
		input_dir.y += 1
	if Input.is_key_pressed(KEY_CTRL):
		input_dir.y -= 1
	
	# Get movement speed
	var speed := fly_speed
	if Input.is_key_pressed(KEY_SHIFT):
		speed = fly_sprint_speed
	
	# Calculate movement direction
	var h_rot := rotation.y
	var forward := Vector3(sin(h_rot), 0, cos(h_rot))
	var right := Vector3(cos(h_rot), 0, -sin(h_rot))
	
	var movement := forward * -input_dir.z + right * input_dir.x
	movement.y = input_dir.y
	
	if movement.length() > 0:
		movement = movement.normalized()
	
	# Apply velocity (no gravity in fly mode)
	velocity = movement * speed

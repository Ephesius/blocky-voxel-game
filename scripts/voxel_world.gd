extends Node3D

var chunk_script = preload("res://scripts/chunk.gd")

func _ready() -> void:
	var chunk = chunk_script.new()
	chunk.name = "Chunk_0_0"
	add_child(chunk)
	chunk.global_position = Vector3.ZERO

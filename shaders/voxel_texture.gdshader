shader_type spatial;
render_mode cull_back, depth_draw_opaque;

// Texture2DArray containing all block textures
uniform sampler2DArray texture_array : source_color, filter_nearest, repeat_enable;

// Vertex attributes
varying vec2 uv_coords;
varying float texture_layer;

void vertex() {
	// Pass UV coordinates to fragment shader
	uv_coords = UV;
	
	// Pass texture layer index via COLOR.r (encoded as 0-1 range)
	// We'll multiply by 255 in fragment shader to get layer index
	texture_layer = COLOR.r * 255.0;
}

void fragment() {
	// Sample from Texture2DArray using UV and layer index
	// vec3 constructor: (u, v, layer)
	// Use fract(uv_coords) to manually force tiling if hardware repeat fails
	vec4 tex_color = texture(texture_array, vec3(fract(uv_coords), texture_layer));
	
	// Set albedo (base color)
	// DEBUG: Visualize UVs
	// If UVs are correct (0..N), you should see repeating red/green gradients.
	// If UVs are wrong (0..1), you will see a single red/green gradient across the whole face.
	// ALBEDO = vec3(fract(uv_coords.x), fract(uv_coords.y), 0.0);
	
	// Original code (commented out for debug)
	ALBEDO = tex_color.rgb;
	
	// DO NOT write to ALPHA for solid blocks! 
	// Writing to ALPHA switches Godot to the transparent pipeline, causing sorting issues.
	// ALPHA = tex_color.a; 
}

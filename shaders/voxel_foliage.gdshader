shader_type spatial;
render_mode cull_disabled, depth_draw_opaque, blend_mix;

// Texture2DArray containing all foliage textures
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
	vec4 tex_color = texture(texture_array, vec3(uv_coords, texture_layer));
	
	// Alpha cutout for foliage transparency
	if (tex_color.a < 0.5) {
		discard;
	}
	
	// Set albedo (base color)
	ALBEDO = tex_color.rgb;
	ALPHA = tex_color.a;
}

using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Generates cross-quad billboard meshes for foliage.
/// </summary>
public static class FoliageMesher
{
	public struct MeshData
	{
		public Vector3[] Vertices;
		public Vector3[] Normals;
		public Vector2[] UVs;
		public Color[] Colors;
		public int[] Indices;
	}
	
	public static MeshData GenerateMesh(List<(Vector3I position, int foliageType)> placements)
	{
		if (placements == null || placements.Count == 0)
			return new MeshData();
		
		var vertices = new List<Vector3>();
		var normals = new List<Vector3>();
		var uvs = new List<Vector2>();
		var colors = new List<Color>();
		var indices = new List<int>();
		
		foreach (var (pos, foliageType) in placements)
		{
			GenerateCrossQuad(pos, foliageType, vertices, normals, uvs, colors, indices);
		}
		
		return new MeshData
		{
			Vertices = vertices.ToArray(),
			Normals = normals.ToArray(),
			UVs = uvs.ToArray(),
			Colors = colors.ToArray(),
			Indices = indices.ToArray()
		};
	}
	
	private static void GenerateCrossQuad(Vector3I pos, int foliageType, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<Color> colors, List<int> indices)
	{
		// Cross-quad: Two intersecting quads forming an X shape
		// Each quad is 1 block wide and 1 block tall
		
		Vector3 center = new Vector3(pos.X + 0.5f, pos.Y, pos.Z + 0.5f);
		float halfSize = 0.5f;
		float height = 1.0f;
		
		int baseIndex = vertices.Count;
		
		// Quad 1: NW-SE diagonal (from -X,-Z to +X,+Z)
		// Bottom-left
		vertices.Add(center + new Vector3(-halfSize, 0, -halfSize));
		// Bottom-right
		vertices.Add(center + new Vector3(halfSize, 0, halfSize));
		// Top-right
		vertices.Add(center + new Vector3(halfSize, height, halfSize));
		// Top-left
		vertices.Add(center + new Vector3(-halfSize, height, -halfSize));
		
		// Quad 2: NE-SW diagonal (from +X,-Z to -X,+Z)
		// Bottom-left
		vertices.Add(center + new Vector3(halfSize, 0, -halfSize));
		// Bottom-right
		vertices.Add(center + new Vector3(-halfSize, 0, halfSize));
		// Top-right
		vertices.Add(center + new Vector3(-halfSize, height, halfSize));
		// Top-left
		vertices.Add(center + new Vector3(halfSize, height, -halfSize));
		
		// Normals (pointing outward from quad planes)
		Vector3 normal1 = new Vector3(1, 0, 1).Normalized();
		Vector3 normal2 = new Vector3(-1, 0, 1).Normalized();
		
		for (int i = 0; i < 4; i++)
		{
			normals.Add(normal1);
		}
		for (int i = 0; i < 4; i++)
		{
			normals.Add(normal2);
		}
		
		// UVs (same for both quads - full texture)
		for (int i = 0; i < 2; i++)
		{
			uvs.Add(new Vector2(0, 1)); // Bottom-left
			uvs.Add(new Vector2(1, 1)); // Bottom-right
			uvs.Add(new Vector2(1, 0)); // Top-right
			uvs.Add(new Vector2(0, 0)); // Top-left
		}
		
		// Colors (encode foliage type in R channel for shader)
		Color foliageColor = new Color(foliageType / 255.0f, 0, 0, 1);
		for (int i = 0; i < 8; i++)
		{
			colors.Add(foliageColor);
		}
		
		// Indices (two triangles per quad, two quads = 4 triangles = 12 indices)
		// Quad 1
		indices.Add(baseIndex + 0);
		indices.Add(baseIndex + 1);
		indices.Add(baseIndex + 2);
		
		indices.Add(baseIndex + 0);
		indices.Add(baseIndex + 2);
		indices.Add(baseIndex + 3);
		
		// Quad 2
		indices.Add(baseIndex + 4);
		indices.Add(baseIndex + 5);
		indices.Add(baseIndex + 6);
		
		indices.Add(baseIndex + 4);
		indices.Add(baseIndex + 6);
		indices.Add(baseIndex + 7);
	}
}

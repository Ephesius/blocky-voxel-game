using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class Chunk : Node3D
{
	public const int CHUNK_SIZE = 16;
	
	// Block types - must match GDScript enum for now
	public enum BlockType
	{
		AIR = 0,
		DIRT = 1,
		GRASS = 2,
		STONE = 3
	}
	
	// Public properties
	public Vector3I ChunkPos { get; set; }
	public Node3D World { get; set; }
	public int[] Voxels { get; set; } // Public setter to allow direct data setting
	
	// Private fields
	private MeshInstance3D _meshInstance;
	private bool _hasCollision = false;
	private float _collisionDistance = 6.0f;
	
	// Face direction offsets
	private static readonly Vector3I[] FaceOffsets = new Vector3I[]
	{
		new Vector3I(1, 0, 0),   // +X
		new Vector3I(-1, 0, 0),  // -X
		new Vector3I(0, 1, 0),   // +Y
		new Vector3I(0, -1, 0),  // -Y
		new Vector3I(0, 0, 1),   // +Z
		new Vector3I(0, 0, -1)   // -Z
	};
	
	public override void _Ready()
	{
		// Create mesh instance
		_meshInstance = new MeshInstance3D();
		AddChild(_meshInstance);
		
		// Initialize voxel array
		Voxels = new int[CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE];
		Array.Fill(Voxels, (int)BlockType.AIR);
	}
	
	public void SetVoxelData(int[] data)
	{
		if (data == null || data.Length != CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE)
		{
			GD.PrintErr($"Chunk {ChunkPos}: Invalid voxel data");
			return;
		}
		
		Voxels = data;
		UpdateMesh();
	}
	
	public void UpdateMesh()
	{
		// Generate mesh using optimized C# code
		var mesh = GenerateMesh();
		
		if (mesh == null)
		{
			GD.PrintErr($"Failed to generate mesh for chunk {ChunkPos}");
			return;
		}
		
		// Set material if mesh has surfaces
		if (mesh.GetSurfaceCount() > 0)
		{
			var material = new StandardMaterial3D
			{
				VertexColorUseAsAlbedo = true,
				CullMode = BaseMaterial3D.CullModeEnum.Back
			};
			mesh.SurfaceSetMaterial(0, material);
		}
		
		_meshInstance.Mesh = mesh;
	}
	
	private ArrayMesh GenerateMesh()
	{
		var vertices = new List<Vector3>();
		var colors = new List<Color>();
		var indices = new List<int>();
		
		// Iterate through all blocks
		for (int x = 0; x < CHUNK_SIZE; x++)
		{
			for (int y = 0; y < CHUNK_SIZE; y++)
			{
				for (int z = 0; z < CHUNK_SIZE; z++)
				{
					int blockType = Voxels[GetIndex(x, y, z)];
					
					// Skip air blocks
					if (blockType == (int)BlockType.AIR)
						continue;
					
					Color blockColor = GetBlockColor(blockType);
					
					// Check each face
					for (int faceIndex = 0; faceIndex < 6; faceIndex++)
					{
						Vector3I neighborPos = new Vector3I(x, y, z) + FaceOffsets[faceIndex];
						int neighbor = GetVoxel(neighborPos.X, neighborPos.Y, neighborPos.Z);
						
						// If neighbor is air, add this face
						if (neighbor == (int)BlockType.AIR)
						{
							AddFace(vertices, colors, indices, new Vector3(x, y, z), faceIndex, blockColor);
						}
					}
				}
			}
		}
		
		// Create mesh
		if (vertices.Count == 0)
			return new ArrayMesh();
		
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
		arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
		arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
		
		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		
		return mesh;
	}
	
	private void AddFace(List<Vector3> vertices, List<Color> colors, List<int> indices,
		Vector3 blockPos, int faceIndex, Color color)
	{
		Vector3[] faceVertices = GetFaceVertices(faceIndex);
		int vertexStart = vertices.Count;
		
		// Add the 4 vertices for this face
		for (int i = 0; i < 4; i++)
		{
			vertices.Add(blockPos + faceVertices[i]);
			colors.Add(color);
		}
		
		// Add indices for 2 triangles
		indices.Add(vertexStart + 0);
		indices.Add(vertexStart + 1);
		indices.Add(vertexStart + 2);
		
		indices.Add(vertexStart + 0);
		indices.Add(vertexStart + 2);
		indices.Add(vertexStart + 3);
	}
	
	private static Vector3[] GetFaceVertices(int faceIndex)
	{
		return faceIndex switch
		{
			0 => new Vector3[] { new(1, 0, 0), new(1, 0, 1), new(1, 1, 1), new(1, 1, 0) }, // +X
			1 => new Vector3[] { new(0, 0, 1), new(0, 0, 0), new(0, 1, 0), new(0, 1, 1) }, // -X
			2 => new Vector3[] { new(0, 1, 0), new(1, 1, 0), new(1, 1, 1), new(0, 1, 1) }, // +Y
			3 => new Vector3[] { new(0, 0, 1), new(1, 0, 1), new(1, 0, 0), new(0, 0, 0) }, // -Y
			4 => new Vector3[] { new(0, 0, 1), new(0, 1, 1), new(1, 1, 1), new(1, 0, 1) }, // +Z
			5 => new Vector3[] { new(1, 0, 0), new(1, 1, 0), new(0, 1, 0), new(0, 0, 0) }, // -Z
			_ => new Vector3[4]
		};
	}
	
	private static Color GetBlockColor(int blockType)
	{
		return blockType switch
		{
			(int)BlockType.GRASS => Colors.Green,
			(int)BlockType.DIRT => new Color(0.55f, 0.27f, 0.07f),
			(int)BlockType.STONE => Colors.Gray,
			_ => Colors.White
		};
	}
	
	public int GetVoxel(int x, int y, int z)
	{
		// Check if coordinates are within this chunk
		if (x >= 0 && x < CHUNK_SIZE && y >= 0 && y < CHUNK_SIZE && z >= 0 && z < CHUNK_SIZE)
		{
			return Voxels[GetIndex(x, y, z)];
		}
		
		// Out of bounds - check neighboring chunk
		if (World != null)
		{
			Vector3I neighborChunkPos = ChunkPos;
			Vector3I localPos = new Vector3I(x, y, z);
			
			// Adjust chunk position and local coordinates
			if (x < 0)
			{
				neighborChunkPos.X -= 1;
				localPos.X = CHUNK_SIZE - 1;
			}
			else if (x >= CHUNK_SIZE)
			{
				neighborChunkPos.X += 1;
				localPos.X = 0;
			}
			
			if (y < 0)
			{
				neighborChunkPos.Y -= 1;
				localPos.Y = CHUNK_SIZE - 1;
			}
			else if (y >= CHUNK_SIZE)
			{
				neighborChunkPos.Y += 1;
				localPos.Y = 0;
			}
			
			if (z < 0)
			{
				neighborChunkPos.Z -= 1;
				localPos.Z = CHUNK_SIZE - 1;
			}
			else if (z >= CHUNK_SIZE)
			{
				neighborChunkPos.Z += 1;
				localPos.Z = 0;
			}
			
			// Try to find the neighbor chunk (look for C# Chunk)
			string chunkName = $"Chunk_{neighborChunkPos.X}_{neighborChunkPos.Y}_{neighborChunkPos.Z}";
			var neighbor = World.GetNodeOrNull<Chunk>(chunkName);
			
			if (neighbor != null && neighbor.Voxels != null)
			{
				return neighbor.Voxels[GetIndex(localPos.X, localPos.Y, localPos.Z)];
			}
		}
		
		// Default to air if no neighbor found
		return (int)BlockType.AIR;
	}
	
	public void UpdateCollision(bool enable)
	{
		// Only update if state is changing
		if (enable == _hasCollision)
			return;
		
		if (enable && _meshInstance.Mesh != null && _meshInstance.Mesh.GetSurfaceCount() > 0)
		{
			_meshInstance.CreateTrimeshCollision();
			_hasCollision = true;
		}
		else if (!enable && _hasCollision)
		{
			// Remove collision if it exists
			foreach (var child in _meshInstance.GetChildren())
			{
				if (child is StaticBody3D)
				{
					child.QueueFree();
				}
			}
			_hasCollision = false;
		}
	}
	
	private static int GetIndex(int x, int y, int z)
	{
		return x + (y * CHUNK_SIZE) + (z * CHUNK_SIZE * CHUNK_SIZE);
	}
}

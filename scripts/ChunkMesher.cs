using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class ChunkMesher : RefCounted
{
	private const int CHUNK_SIZE = 16;
	
	// Block types (must match GDScript enum)
	private enum BlockType
	{
		AIR = 0,
		DIRT = 1,
		GRASS = 2,
		STONE = 3
	}
	
	// Face direction vectors
	private static readonly Vector3I[] FaceOffsets = new Vector3I[]
	{
		new Vector3I(1, 0, 0),   // +X
		new Vector3I(-1, 0, 0),  // -X
		new Vector3I(0, 1, 0),   // +Y
		new Vector3I(0, -1, 0),  // -Y
		new Vector3I(0, 0, 1),   // +Z
		new Vector3I(0, 0, -1)   // -Z
	};
	
	// Instance method for GDScript compatibility
	public ArrayMesh CreateMesh(int[] voxels, Vector3I chunkPos, Node3D world)
	{
		return GenerateMesh(voxels, chunkPos, world);
	}
	
	// Generate mesh for a chunk
	// Returns an ArrayMesh ready to be used
	public static ArrayMesh GenerateMesh(int[] voxels, Vector3I chunkPos, Node3D world)
	{
		if (voxels == null || voxels.Length != CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE)
		{
			GD.PrintErr("ChunkMesher: Invalid voxel data");
			return null;
		}
		
		// Lists to store mesh data
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
					int blockType = voxels[GetIndex(x, y, z)];
					
					// Skip air blocks
					if (blockType == (int)BlockType.AIR)
						continue;
					
					Color blockColor = GetBlockColor(blockType);
					
					// Check each face
					for (int faceIndex = 0; faceIndex < 6; faceIndex++)
					{
						Vector3I neighborPos = new Vector3I(x, y, z) + FaceOffsets[faceIndex];
						int neighbor = GetVoxel(voxels, neighborPos, chunkPos, world);
						
						// If neighbor is air, add this face
						if (neighbor == (int)BlockType.AIR)
						{
							AddFace(vertices, colors, indices, new Vector3(x, y, z), faceIndex, blockColor);
						}
					}
				}
			}
		}
		
		// Create the mesh
		if (vertices.Count == 0)
			return new ArrayMesh(); // Empty mesh
		
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
		arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
		arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
		
		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		
		return mesh;
	}
	
	private static void AddFace(List<Vector3> vertices, List<Color> colors, List<int> indices, 
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
		// Return the 4 vertices for each face
		// Vertices are in correct winding order for backface culling
		switch (faceIndex)
		{
			case 0: // +X (right)
				return new Vector3[]
				{
					new Vector3(1, 0, 0),
					new Vector3(1, 0, 1),
					new Vector3(1, 1, 1),
					new Vector3(1, 1, 0)
				};
			case 1: // -X (left)
				return new Vector3[]
				{
					new Vector3(0, 0, 1),
					new Vector3(0, 0, 0),
					new Vector3(0, 1, 0),
					new Vector3(0, 1, 1)
				};
			case 2: // +Y (top)
				return new Vector3[]
				{
					new Vector3(0, 1, 0),
					new Vector3(1, 1, 0),
					new Vector3(1, 1, 1),
					new Vector3(0, 1, 1)
				};
			case 3: // -Y (bottom)
				return new Vector3[]
				{
					new Vector3(0, 0, 1),
					new Vector3(1, 0, 1),
					new Vector3(1, 0, 0),
					new Vector3(0, 0, 0)
				};
			case 4: // +Z (forward)
				return new Vector3[]
				{
					new Vector3(0, 0, 1),
					new Vector3(0, 1, 1),
					new Vector3(1, 1, 1),
					new Vector3(1, 0, 1)
				};
			case 5: // -Z (back)
				return new Vector3[]
				{
					new Vector3(1, 0, 0),
					new Vector3(1, 1, 0),
					new Vector3(0, 1, 0),
					new Vector3(0, 0, 0)
				};
			default:
				return new Vector3[4];
		}
	}
	
	private static Color GetBlockColor(int blockType)
	{
		return blockType switch
		{
			(int)BlockType.GRASS => Colors.Green,
			(int)BlockType.DIRT => new Color(0.55f, 0.27f, 0.07f), // Saddle brown
			(int)BlockType.STONE => Colors.Gray,
			_ => Colors.White
		};
	}
	
	private static int GetIndex(int x, int y, int z)
	{
		return x + (y * CHUNK_SIZE) + (z * CHUNK_SIZE * CHUNK_SIZE);
	}
	
	private static int GetVoxel(int[] voxels, Vector3I pos, Vector3I chunkPos, Node3D world)
	{
		// Check if position is within this chunk
		if (pos.X >= 0 && pos.X < CHUNK_SIZE && 
			pos.Y >= 0 && pos.Y < CHUNK_SIZE && 
			pos.Z >= 0 && pos.Z < CHUNK_SIZE)
		{
			return voxels[GetIndex(pos.X, pos.Y, pos.Z)];
		}
		
		// Out of bounds - check neighboring chunk
		if (world != null)
		{
			Vector3I neighborChunkPos = chunkPos;
			Vector3I localPos = pos;
			
			// Adjust chunk position and local coordinates
			if (pos.X < 0)
			{
				neighborChunkPos.X -= 1;
				localPos.X = CHUNK_SIZE - 1;
			}
			else if (pos.X >= CHUNK_SIZE)
			{
				neighborChunkPos.X += 1;
				localPos.X = 0;
			}
			
			if (pos.Y < 0)
			{
				neighborChunkPos.Y -= 1;
				localPos.Y = CHUNK_SIZE - 1;
			}
			else if (pos.Y >= CHUNK_SIZE)
			{
				neighborChunkPos.Y += 1;
				localPos.Y = 0;
			}
			
			if (pos.Z < 0)
			{
				neighborChunkPos.Z -= 1;
				localPos.Z = CHUNK_SIZE - 1;
			}
			else if (pos.Z >= CHUNK_SIZE)
			{
				neighborChunkPos.Z += 1;
				localPos.Z = 0;
			}
			
			// Try to find the neighbor chunk
			string chunkName = $"Chunk_{neighborChunkPos.X}_{neighborChunkPos.Y}_{neighborChunkPos.Z}";
			var neighbor = world.GetNodeOrNull(chunkName);
			
			if (neighbor != null)
			{
				// Get voxels array from neighbor
				var voxelsProperty = neighbor.Get("voxels");
				if (voxelsProperty.VariantType == Variant.Type.Array)
				{
					var neighborVoxels = voxelsProperty.AsInt32Array();
					if (neighborVoxels != null && neighborVoxels.Length == CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE)
					{
						return neighborVoxels[GetIndex(localPos.X, localPos.Y, localPos.Z)];
					}
				}
			}
		}
		
		// Default to air if no neighbor found
		return (int)BlockType.AIR;
	}
}

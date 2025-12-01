using Godot;
using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// Static helper class to generate meshes from ChunkData using Greedy Meshing.
/// This runs on background threads, so it must be stateless and thread-safe.
/// </summary>
public static class GreedyMesher
{
    // Struct to hold the raw mesh data (no Godot objects)
    public struct MeshData
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public Vector2[] UVs;
        public Color[] Colors;
        public int[] Indices;
        public Vector3[] CollisionVertices; // Unrolled triangle soup for physics
    }

    // Direction vectors for the 6 faces
    private static readonly Vector3I[] FaceNormals = new Vector3I[]
    {
        new Vector3I(0, 0, 1),  // Back   (Z+)
        new Vector3I(0, 0, -1), // Front  (Z-)
        new Vector3I(-1, 0, 0), // Left   (X-)
        new Vector3I(1, 0, 0),  // Right  (X+)
        new Vector3I(0, -1, 0), // Bottom (Y-)
        new Vector3I(0, 1, 0)   // Top    (Y+)
    };

    // Thread-local buffers to avoid allocations
    private class MeshBuffers
    {
        public List<Vector3> Vertices = new List<Vector3>();
        public List<Vector3> Normals = new List<Vector3>();
        public List<Vector2> UVs = new List<Vector2>();
        public List<Color> Colors = new List<Color>();
        public List<int> Indices = new List<int>();
        public List<Vector3> CollisionVertices = new List<Vector3>();
        public int[] Mask = new int[ChunkData.CHUNK_SIZE * ChunkData.CHUNK_SIZE];

        public void Clear()
        {
            Vertices.Clear();
            Normals.Clear();
            UVs.Clear();
            Colors.Clear();
            Indices.Clear();
            CollisionVertices.Clear();
            // Mask is fully overwritten each slice, so no need to clear it
        }
    }

    private static readonly ThreadLocal<MeshBuffers> _threadBuffers = new ThreadLocal<MeshBuffers>(() => new MeshBuffers());

    public static MeshData GenerateMesh(ChunkData chunk, ChunkData[] neighbors)
    {
        var buffers = _threadBuffers.Value;
        buffers.Clear();

        var vertices = buffers.Vertices;
        var normals = buffers.Normals;
        var uvs = buffers.UVs;
        var colors = buffers.Colors;
        var indices = buffers.Indices;
        var collisionVertices = buffers.CollisionVertices;
        var mask = buffers.Mask;

        // We sweep over each axis (X, Y, Z)
        for (int d = 0; d < 3; d++)
        {
            int i, j, k, l, w, h;
            int u = (d + 1) % 3;
            int v = (d + 2) % 3;
            
            int[] x = new int[3];
            int[] q = new int[3];
            
            // Mask is reused from buffers
            
            q[d] = 1;

            // Iterate through the dimension being swept
            for (x[d] = -1; x[d] < ChunkData.CHUNK_SIZE; )
            {
                int n = 0;
                
                // Create the mask for this slice
                for (x[v] = 0; x[v] < ChunkData.CHUNK_SIZE; ++x[v])
                {
                    for (x[u] = 0; x[u] < ChunkData.CHUNK_SIZE; ++x[u])
                    {
                        // Get block at current position
                        int blockCurrent = (x[d] >= 0) ? chunk.GetVoxel(x[0], x[1], x[2]) : 
                            GetNeighborVoxel(x[0], x[1], x[2], neighbors, d, false);
                            
                        // Get block at next position (in direction d)
                        int blockNext = (x[d] < ChunkData.CHUNK_SIZE - 1) ? chunk.GetVoxel(x[0] + q[0], x[1] + q[1], x[2] + q[2]) :
                            GetNeighborVoxel(x[0] + q[0], x[1] + q[1], x[2] + q[2], neighbors, d, true);

                        // Visible face logic:
                        // If both are air (0), no face.
                        // If both are same opaque block, no face (culled).
                        // If one is air and other is block, we have a face.
                        
                        bool currentIsOpaque = blockCurrent > 0; // Simplified opacity check
                        bool nextIsOpaque = blockNext > 0;

                        if (currentIsOpaque == nextIsOpaque)
                        {
                            mask[n++] = 0;
                        }
                        else if (currentIsOpaque)
                        {
                            mask[n++] = blockCurrent;
                        }
                        else
                        {
                            mask[n++] = -blockNext; // Negative to indicate back-face
                        }
                    }
                }

                ++x[d];
                n = 0;

                // Generate mesh from mask
                for (j = 0; j < ChunkData.CHUNK_SIZE; ++j)
                {
                    for (i = 0; i < ChunkData.CHUNK_SIZE; )
                    {
                        if (mask[n] != 0)
                        {
                            int width, height;
                            int type = mask[n];

                            // Compute width
                            for (width = 1; i + width < ChunkData.CHUNK_SIZE && mask[n + width] == type; ++width) { }

                            // Compute height
                            bool done = false;
                            for (height = 1; j + height < ChunkData.CHUNK_SIZE; ++height)
                            {
                                for (k = 0; k < width; ++k)
                                {
                                    if (mask[n + k + height * ChunkData.CHUNK_SIZE] != type)
                                    {
                                        done = true;
                                        break;
                                    }
                                }
                                if (done) break;
                            }

                            // Add Quad
                            x[u] = i;
                            x[v] = j;
                            
                            int[] du = new int[3]; du[u] = width;
                            int[] dv = new int[3]; dv[v] = height;

                            // Determine corners
                            Vector3 v1 = new Vector3(x[0], x[1], x[2]);
                            Vector3 v2 = new Vector3(x[0] + du[0], x[1] + du[1], x[2] + du[2]);
                            Vector3 v3 = new Vector3(x[0] + du[0] + dv[0], x[1] + du[1] + dv[1], x[2] + du[2] + dv[2]);
                            Vector3 v4 = new Vector3(x[0] + dv[0], x[1] + dv[1], x[2] + dv[2]);

                            // Capture face direction before type is sanitized
                            bool isPositive = type > 0;

                            // Determine normal and winding order
                            Vector3 normal;
                            if (isPositive) // Front face
                            {
                                normal = new Vector3(q[0], q[1], q[2]);
                                // Revert to original winding (Clockwise?) to ensure correct normal direction
                                AddQuad(vertices, indices, collisionVertices, v1, v4, v3, v2);
                            }
                            else // Back face
                            {
                                normal = new Vector3(-q[0], -q[1], -q[2]);
                                type = -type; // Restore positive type
                                AddQuad(vertices, indices, collisionVertices, v1, v2, v3, v4);
                            }


                            // Add attributes
                            // Encode texture indices into Color array (R channel)
                            // Shader expects 0-1 range, so we divide by 255.0
                            int textureLayer = GetTextureLayerForBlockType(Math.Abs(type));
                            Color encodedColor = new Color(textureLayer / 255.0f, 0, 0, 1);
                            
                            for (int c = 0; c < 4; c++)
                            {
                                normals.Add(normal);
                                colors.Add(encodedColor); 
                            }
                            
                            // Calculate UVs for greedy quad: tile texture across width × height
                            // Pass 'd' (axis) and 'isPositive' to handle orientation correctly
                            AddQuadUVs(uvs, width, height, d, isPositive);


                            // Clear mask
                            for (l = 0; l < height; ++l)
                            {
                                for (k = 0; k < width; ++k)
                                {
                                    mask[n + k + l * ChunkData.CHUNK_SIZE] = 0;
                                }
                            }

                            i += width;
                            n += width;
                        }
                        else
                        {
                            i++;
                            n++;
                        }
                    }
                }
            }
        }

        return new MeshData
        {
            Vertices = vertices.ToArray(),
            Normals = normals.ToArray(),
            UVs = uvs.ToArray(),
            Colors = colors.ToArray(),
            Indices = indices.ToArray(),
            CollisionVertices = collisionVertices.ToArray()
        };
    }

    private static void AddQuad(List<Vector3> verts, List<int> indices, List<Vector3> collisionVerts, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4)
    {
        int startIndex = verts.Count;
        verts.Add(v1);
        verts.Add(v2);
        verts.Add(v3);
        verts.Add(v4);

        indices.Add(startIndex);
        indices.Add(startIndex + 1);
        indices.Add(startIndex + 2);
        indices.Add(startIndex);
        indices.Add(startIndex + 2);
        indices.Add(startIndex + 3);
        
        // Add unrolled triangles for collision
        collisionVerts.Add(v1);
        collisionVerts.Add(v2);
        collisionVerts.Add(v3);
        
        collisionVerts.Add(v1);
        collisionVerts.Add(v3);
        collisionVerts.Add(v4);
    }

    private static Color GetColor(int type)
    {
        return type switch
        {
            1 => new Color(0.55f, 0.27f, 0.07f), // Dirt
            2 => Colors.Green, // Grass
            3 => Colors.Gray, // Stone
            _ => Colors.Magenta // Error
        };
    }
    
    /// <summary>
    /// Adds UV coordinates for a greedy-meshed quad.
    /// UVs tile the texture across the quad's width and height.
    /// </summary>
    private static void AddQuadUVs(List<Vector2> uvs, int width, int height, int axis, bool isPositiveFace)
    {
        
        float uSize = width;
        float vSize = height;
        

        
        // Add UVs for the 4 corners of the quad
        // We must match the vertex winding order used in GenerateMesh!
        
        if (isPositiveFace) // Positive Face (Swapped Winding: v1, v4, v3, v2)
        {
            // v1 -> (0,0)
            // v4 -> (0, vSize)
            // v3 -> (uSize, vSize)
            // v2 -> (uSize, 0)
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(0, vSize));
            uvs.Add(new Vector2(uSize, vSize));
            uvs.Add(new Vector2(uSize, 0));
        }
        else // Negative Face (Standard Winding: v1, v2, v3, v4)
        {
            // v1 -> (0,0)
            // v2 -> (uSize, 0)
            // v3 -> (uSize, vSize)
            // v4 -> (0, vSize)
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(uSize, 0));
            uvs.Add(new Vector2(uSize, vSize));
            uvs.Add(new Vector2(0, vSize));
        }
    }
    
    /// <summary>
    /// Maps block type to texture layer index.
    /// Must match the TextureManager's layer mapping.
    /// </summary>
    private static int GetTextureLayerForBlockType(int blockType)
    {
        return blockType switch
        {
            1 => 0, // DIRT -> Layer 0
            2 => 1, // GRASS -> Layer 1
            3 => 2, // STONE -> Layer 2
            4 => 3, // SAND -> Layer 3
            5 => 4, // SNOW -> Layer 4
            6 => 5, // ICE -> Layer 5
            _ => 0  // Default to dirt for unknown types
        };
    }

    
    // Helper to get voxel from neighbor chunks if coordinate is out of bounds
    private static int GetNeighborVoxel(int x, int y, int z, ChunkData[] neighbors, int axis, bool isNext)
    {
        // neighbors array order: 
        // 0: X- (Left), 1: X+ (Right)
        // 2: Y- (Bottom), 3: Y+ (Top)
        // 4: Z- (Front), 5: Z+ (Back)
        
        int neighborIndex = -1;
        int localX = x, localY = y, localZ = z;

        if (axis == 0) // X Axis
        {
            if (!isNext && x < 0) { neighborIndex = 0; localX += ChunkData.CHUNK_SIZE; }
            else if (isNext && x >= ChunkData.CHUNK_SIZE) { neighborIndex = 1; localX -= ChunkData.CHUNK_SIZE; }
        }
        else if (axis == 1) // Y Axis
        {
            if (!isNext && y < 0) { neighborIndex = 2; localY += ChunkData.CHUNK_SIZE; }
            else if (isNext && y >= ChunkData.CHUNK_SIZE) { neighborIndex = 3; localY -= ChunkData.CHUNK_SIZE; }
        }
        else // Z Axis
        {
            if (!isNext && z < 0) { neighborIndex = 4; localZ += ChunkData.CHUNK_SIZE; }
            else if (isNext && z >= ChunkData.CHUNK_SIZE) { neighborIndex = 5; localZ -= ChunkData.CHUNK_SIZE; }
        }

        if (neighborIndex != -1 && neighbors[neighborIndex] != null)
        {
            return neighbors[neighborIndex].GetVoxel(localX, localY, localZ);
        }

        return 0; // Default to Air if neighbor missing
    }
}

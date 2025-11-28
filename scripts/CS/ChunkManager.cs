using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

/// <summary>
/// The Brain of the Voxel Engine.
/// Manages the background worker thread, generation queues, and main-thread upload budget.
/// </summary>
public partial class ChunkManager : Node
{
    // Configuration
    private int _viewDistance = 8;
    private const int MAX_MS_PER_FRAME = 5; // Budget for main thread uploads

    // State
    private WorldData _worldData;
    private WorldGenerator _generator;
    private Vector3I _lastPlayerChunkPos = new Vector3I(int.MaxValue, int.MaxValue, int.MaxValue);
    private bool _isRunning = true;

    // Threading
    private List<Thread> _workerThreads = new List<Thread>();
    // Queues
    private ConcurrentQueue<Vector3I> _generationQueue = new ConcurrentQueue<Vector3I>();
    private ConcurrentQueue<Vector3I> _meshQueue = new ConcurrentQueue<Vector3I>();
    private ConcurrentQueue<(Vector3I, GreedyMesher.MeshData)> _uploadQueue = new ConcurrentQueue<(Vector3I, GreedyMesher.MeshData)>();
    
    // We use a HashSet to quickly check if a chunk is already queued to avoid duplicates
    private HashSet<Vector3I> _queuedChunks = new HashSet<Vector3I>();
    private object _queueLock = new object();
    
    // Instances
    private Dictionary<Vector3I, Rid> _chunkInstances = new Dictionary<Vector3I, Rid>();
    
    // Rendering
    private TextureManager _textureManager;
    private ShaderMaterial _voxelMaterial;

    public override void _Ready()
    {
        _worldData = new WorldData();
        _generator = new WorldGenerator(new WorldConfig()); 

        // Initialize Texture System
        _textureManager = new TextureManager();
        _textureManager.LoadTextures("res://assets/textures/blocks");
        
        // Initialize Shader Material
        var shader = GD.Load<Shader>("res://shaders/voxel_texture.gdshader");
        _voxelMaterial = new ShaderMaterial();
        _voxelMaterial.Shader = shader;
        _voxelMaterial.SetShaderParameter("texture_array", _textureManager.TextureArray);
        
        GD.Print("ChunkManager: Texture system and shader initialized.");
        
        // Add Debug UI
        var debugUI = new DebugUI();
        debugUI.Name = "DebugUI";
        AddChild(debugUI);

        // Start background workers
        // Use ProcessorCount - 1 to leave one core for the main thread, but minimum 1 worker
        int threadCount = Math.Max(1, System.Environment.ProcessorCount - 1);
        
        for (int i = 0; i < threadCount; i++)
        {
            var thread = new Thread(WorkerLoop);
            thread.IsBackground = true;
            thread.Start();
            _workerThreads.Add(thread);
        }
        
        GD.Print($"ChunkManager: Started {threadCount} worker threads.");
    }

    public override void _Process(double delta)
    {
        UpdatePlayerPosition();
        ProcessUploadQueue();
    }

    public override void _ExitTree()
    {
        _isRunning = false;
        
        foreach (var thread in _workerThreads)
        {
            if (thread != null && thread.IsAlive)
            {
                thread.Join(100); // Wait briefly for thread to stop
            }
        }
        _workerThreads.Clear();
        
        // Cleanup all chunks
        foreach(var chunk in _worldData.Chunks.Values)
        {
            chunk.Dispose();
        }
    }

    private void UpdatePlayerPosition()
    {
        // For now, assume player is at (0,0,0) or find player node
        // In a real game, we'd get the player's actual position
        var player = GetTree().Root.GetNodeOrNull<Node3D>("Main/Player");
        Vector3 playerPos = player != null ? player.GlobalPosition : Vector3.Zero;

        Vector3I currentChunkPos = new Vector3I(
            Mathf.FloorToInt(playerPos.X / 16f),
            Mathf.FloorToInt(playerPos.Y / 16f),
            Mathf.FloorToInt(playerPos.Z / 16f)
        );

        if (currentChunkPos != _lastPlayerChunkPos)
        {
            _lastPlayerChunkPos = currentChunkPos;
            QueueChunksAround(currentChunkPos);
            UpdateCollisionArea(currentChunkPos);
        }
    }
    
    private int _collisionDistance = 2;
    
    private void UpdateCollisionArea(Vector3I center)
    {
        // 1. Create collision for nearby chunks
        for (int x = -_collisionDistance; x <= _collisionDistance; x++)
        {
            for (int y = -_collisionDistance; y <= _collisionDistance; y++)
            {
                for (int z = -_collisionDistance; z <= _collisionDistance; z++)
                {
                    Vector3I chunkPos = center + new Vector3I(x, y, z);
                    
                    if (_worldData.TryGetChunk(chunkPos, out ChunkData chunk))
                    {
                        // If we have geometry but no body, create it!
                        if (chunk.CollisionVertices != null && chunk.CollisionVertices.Length > 0 && !chunk.BodyRid.IsValid)
                        {
                            CreateChunkCollision(chunk, chunkPos);
                        }
                    }
                }
            }
        }
        
        // 2. (Optional) Cleanup distant collision?
        // For now, we can leave them or implement a cleanup loop. 
        // Given the user wants performance, let's leave them for now (memory is cheap, CPU is expensive).
        // If we wanted to clean up: iterate all chunks, if dist > _collisionDistance + 1, free BodyRid.
    }
    
    private void CreateChunkCollision(ChunkData chunk, Vector3I pos)
    {
        chunk.BodyRid = PhysicsServer3D.BodyCreate();
        PhysicsServer3D.BodySetMode(chunk.BodyRid, PhysicsServer3D.BodyMode.Static);
        PhysicsServer3D.BodySetSpace(chunk.BodyRid, GetViewport().World3D.Space);
        
        // Create Shape
        Rid shapeRid = PhysicsServer3D.ConcavePolygonShapeCreate();
        
        // Godot 4 PhysicsServer3D.ShapeSetData for ConcavePolygon expects a Dictionary with "faces"
        var shapeData = new Godot.Collections.Dictionary();
        shapeData["faces"] = chunk.CollisionVertices;
        
        PhysicsServer3D.ShapeSetData(shapeRid, shapeData); 
        
        // Add shape to body
        PhysicsServer3D.BodyAddShape(chunk.BodyRid, shapeRid);
        
        // Set Transform
        Transform3D transform = new Transform3D(Basis.Identity, new Vector3(pos.X * 16, pos.Y * 16, pos.Z * 16));
        PhysicsServer3D.BodySetState(chunk.BodyRid, PhysicsServer3D.BodyState.Transform, transform);
    }

    private void QueueChunksAround(Vector3I center)
    {
        int dist = _viewDistance;
        var chunksToQueue = new List<Vector3I>();

        // 1. Collect all potential chunks
        for (int x = -dist; x <= dist; x++)
        {
            for (int z = -dist; z <= dist; z++)
            {
                for (int y = -dist; y <= dist; y++) 
                {
                    Vector3I offset = new Vector3I(x, y, z);
                    Vector3I chunkPos = center + offset;
                    
                    // Check if already exists
                    if (_worldData.Chunks.ContainsKey(chunkPos))
                        continue;

                    // Thread-safe check if already queued
                    lock (_queueLock)
                    {
                        if (_queuedChunks.Contains(chunkPos))
                            continue;
                    }
                    
                    chunksToQueue.Add(chunkPos);
                }
            }
        }
        
        // 2. Sort by distance to center (closest first)
        // We use squared distance to avoid Sqrt
        chunksToQueue.Sort((a, b) => 
        {
            float distA = (a - center).LengthSquared();
            float distB = (b - center).LengthSquared();
            return distA.CompareTo(distB);
        });
        
        // 3. Enqueue sorted chunks
        int queuedCount = 0;
        foreach (var chunkPos in chunksToQueue)
        {
            lock (_queueLock)
            {
                // Double check (though unlikely to change in this single thread context)
                if (_queuedChunks.Contains(chunkPos))
                    continue;
                    
                _queuedChunks.Add(chunkPos);
                _generationQueue.Enqueue(chunkPos);
                queuedCount++;
            }
        }
        
        if (queuedCount > 0)
            GD.Print($"ChunkManager: Queued {queuedCount} new chunks for generation.");
    }

    private void WorkerLoop()
    {
        while (_isRunning)
        {
            bool didWork = false;

            // 1. Process Generation Queue
            if (_generationQueue.TryDequeue(out Vector3I chunkPos))
            {
                GenerateChunk(chunkPos);
                
                // After generation, queue THIS chunk for meshing
                _meshQueue.Enqueue(chunkPos);
                
                // CRITICAL FIX: Queue neighbors for re-meshing!
                // If we don't do this, the neighbor might have generated when *we* didn't exist yet,
                // so it drew a face on the border. Now that we exist, it needs to hide that face.
                Vector3I[] neighborOffsets = { 
                    new(-1,0,0), new(1,0,0), 
                    new(0,-1,0), new(0,1,0), 
                    new(0,0,-1), new(0,0,1) 
                };

                foreach (var offset in neighborOffsets)
                {
                    Vector3I neighborPos = chunkPos + offset;
                    // If neighbor exists, it needs to update its mesh to "see" us
                    if (_worldData.Chunks.ContainsKey(neighborPos))
                    {
                        _meshQueue.Enqueue(neighborPos);
                    }
                }
                
                didWork = true;
            }

            // 2. Process Meshing Queue (Phase 3)
            if (_meshQueue.TryDequeue(out Vector3I meshPos))
            {
                // Remove from queue tracker only after we pick it up for meshing
                // (Actually, we should remove it when it enters generation, but let's keep it simple)
                lock (_queueLock)
                {
                    _queuedChunks.Remove(meshPos);
                }
                
                MeshChunk(meshPos);
                didWork = true;
            }

            // If no work, sleep briefly to save CPU
            if (!didWork)
            {
                Thread.Sleep(5);
            }
        }
    }

    private void GenerateChunk(Vector3I pos)
    {
        // This runs on the background thread!
        // 1. Create Data
        var data = _generator.GenerateChunkData(pos); // Returns int[] currently, need to adapt
        
        // Adapt old generator to new ChunkData (Temporary adapter until we rewrite generator)
        var chunkData = new ChunkData();
        // For now, just fill with the data from the old generator
        // In Phase 3 or 4 we will rewrite WorldGenerator to write directly to ChunkData
        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                for (int z = 0; z < 16; z++)
                {
                    int index = x + (y * 16) + (z * 256);
                    chunkData.SetVoxel(x, y, z, data[index]);
                }
            }
        }

        // 2. Store in WorldData
        // Dictionary is not thread-safe for writing, so we lock it
        lock (_worldData.Chunks)
        {
            _worldData.AddChunk(pos, chunkData);
        }
        
        // GD.Print($"Generated Chunk {pos} on Thread {Thread.CurrentThread.ManagedThreadId}");
    }
    
    private void MeshChunk(Vector3I pos)
    {
        ChunkData chunk;
        if (!_worldData.TryGetChunk(pos, out chunk))
            return;
            
        // Get Neighbors
        ChunkData[] neighbors = new ChunkData[6];
        // 0: X-, 1: X+, 2: Y-, 3: Y+, 4: Z-, 5: Z+
        Vector3I[] offsets = { 
            new(-1,0,0), new(1,0,0), 
            new(0,-1,0), new(0,1,0), 
            new(0,0,-1), new(0,0,1) 
        };
        
        for(int i=0; i<6; i++)
        {
            _worldData.TryGetChunk(pos + offsets[i], out neighbors[i]);
        }
        
        // Generate Mesh Data
        var meshData = GreedyMesher.GenerateMesh(chunk, neighbors);

        // ALWAYS enqueue, even if empty!
        // If the mesh is empty (e.g. fully underground), we still need to upload it
        // to CLEAR the old mesh that might have had border faces.
        _uploadQueue.Enqueue((pos, meshData));
    }
    
    [Export]
    public int MaxChunksPerFrame { get; set; } = 16; // Limit uploads to prevent driver stalls

    private void ProcessUploadQueue()
    {
        var stopwatch = Stopwatch.StartNew();
        int chunksProcessed = 0;
        
        while (_uploadQueue.TryDequeue(out var item))
        {
            Vector3I pos = item.Item1;
            GreedyMesher.MeshData data = item.Item2;
            
            if (_worldData.TryGetChunk(pos, out ChunkData chunk))
            {
                UploadMesh(chunk, pos, data);
                chunksProcessed++;
            }
            
            // Break if we exceed time budget OR chunk count limit
            if (chunksProcessed >= MaxChunksPerFrame || stopwatch.ElapsedMilliseconds > MAX_MS_PER_FRAME)
                break;
        }
    }
    
    private void UploadMesh(ChunkData chunk, Vector3I pos, GreedyMesher.MeshData data)
    {
        // 1. Create/Update Mesh RID
        if (!chunk.MeshRid.IsValid)
        {
            chunk.MeshRid = RenderingServer.MeshCreate();
        }
        else
        {
            RenderingServer.MeshClear(chunk.MeshRid);
        }
        
        // Set Surface
        if (data.Vertices.Length > 0)
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = data.Vertices;
            arrays[(int)Mesh.ArrayType.Normal] = data.Normals;
            arrays[(int)Mesh.ArrayType.TexUV] = data.UVs; // Pass UVs
            
            // Encode texture indices into Color array (R channel)
            // Shader expects 0-1 range, so we divide by 255.0
            // Pre-calculated in worker thread now!
            arrays[(int)Mesh.ArrayType.Color] = data.Colors;
            
            arrays[(int)Mesh.ArrayType.Index] = data.Indices;
            
            RenderingServer.MeshAddSurfaceFromArrays(chunk.MeshRid, RenderingServer.PrimitiveType.Triangles, arrays);
            
            // Apply Shader Material
            RenderingServer.MeshSurfaceSetMaterial(chunk.MeshRid, 0, _voxelMaterial.GetRid());
        }
        
        // 2. Create Instance if needed
        if (!_chunkInstances.ContainsKey(pos))
        {
            Rid instanceRid = RenderingServer.InstanceCreate();
            RenderingServer.InstanceSetBase(instanceRid, chunk.MeshRid);
            RenderingServer.InstanceSetScenario(instanceRid, GetViewport().World3D.Scenario);
            
            // Set Transform
            Transform3D transform = new Transform3D(Basis.Identity, new Vector3(pos.X * 16, pos.Y * 16, pos.Z * 16));
            RenderingServer.InstanceSetTransform(instanceRid, transform);
            
            _chunkInstances[pos] = instanceRid;
        }
        
        // 3. Store Collision Data (But don't create body yet!)
        chunk.CollisionVertices = data.CollisionVertices;
        
        // If this chunk is ALREADY close to the player (e.g. initial load), we might want to create it now.
        // But to keep it simple, we'll let the next UpdateCollisionArea call handle it.
        // However, UpdateCollisionArea is called in _Process, so it will pick it up next frame.
        // Optimization: If we are very close, do it now? No, let's stick to the decoupled plan.
    }
}

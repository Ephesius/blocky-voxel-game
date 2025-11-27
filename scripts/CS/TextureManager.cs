using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Manages loading and organizing block textures into a Texture2DArray.
/// Provides mapping from block types to texture layer indices.
/// </summary>
public partial class TextureManager : RefCounted
{
	public Texture2DArray TextureArray { get; private set; }
	
	private Dictionary<ChunkData.BlockType, int> _blockToLayer = new Dictionary<ChunkData.BlockType, int>();
	private const int TEXTURE_SIZE = 16; // All textures must be 16x16
	
	/// <summary>
	/// Loads block textures from the specified directory and builds a Texture2DArray.
	/// </summary>
	/// <param name="texturePath">Path to textures directory (e.g., "res://assets/textures/blocks")</param>
	public void LoadTextures(string texturePath)
	{
		GD.Print($"TextureManager: Loading textures from {texturePath}");
		
		// Define texture mapping: BlockType -> filename
		var textureFiles = new Dictionary<ChunkData.BlockType, string>
		{
			{ ChunkData.BlockType.DIRT, "dirt.png" },
			{ ChunkData.BlockType.GRASS, "grass.png" },
			{ ChunkData.BlockType.STONE, "stone.png" }
			// AIR doesn't need a texture
		};
		
		var loadedImages = new List<Image>();
		var layerIndex = 0;
		
		// Load each texture
		foreach (var kvp in textureFiles)
		{
			var blockType = kvp.Key;
			var filename = kvp.Value;
			var fullPath = $"{texturePath}/{filename}";
			
			var image = LoadAndValidateTexture(fullPath);
			if (image != null)
			{
				loadedImages.Add(image);
				_blockToLayer[blockType] = layerIndex;
				GD.Print($"  Loaded {filename} -> Layer {layerIndex} (BlockType: {blockType})");
				layerIndex++;
			}
			else
			{
				GD.PrintErr($"  Failed to load {filename} for {blockType}");
			}
		}
		
		// Build Texture2DArray
		if (loadedImages.Count > 0)
		{
			BuildTextureArray(loadedImages);
			GD.Print($"TextureManager: Texture2DArray created with {loadedImages.Count} layers");
		}
		else
		{
			GD.PrintErr("TextureManager: No textures loaded! Using fallback.");
			CreateFallbackTexture();
		}
	}
	
	/// <summary>
	/// Gets the texture layer index for a given block type.
	/// </summary>
	public int GetTextureLayer(ChunkData.BlockType blockType)
	{
		if (_blockToLayer.TryGetValue(blockType, out int layer))
		{
			return layer;
		}
		
		// Fallback to first layer if not found
		GD.PrintErr($"TextureManager: No texture layer for {blockType}, using layer 0");
		return 0;
	}
	
	/// <summary>
	/// Loads and validates a texture file.
	/// </summary>
	private Image LoadAndValidateTexture(string path)
	{
		if (!FileAccess.FileExists(path))
		{
			GD.PrintErr($"TextureManager: Texture file not found: {path}");
			return null;
		}
		
		// Use GD.Load to load the texture as a Resource (works in export)
		var texture = GD.Load<Texture2D>(path);
		if (texture == null)
		{
			GD.PrintErr($"TextureManager: Failed to load texture resource: {path}");
			return null;
		}
		
		var image = texture.GetImage();
		if (image == null)
		{
			GD.PrintErr($"TextureManager: Failed to get image data from texture: {path}");
			return null;
		}
		
		// Validate size
		if (image.GetWidth() != TEXTURE_SIZE || image.GetHeight() != TEXTURE_SIZE)
		{
			GD.PrintErr($"TextureManager: Texture {path} is {image.GetWidth()}x{image.GetHeight()}, expected {TEXTURE_SIZE}x{TEXTURE_SIZE}. Resizing...");
			image.Resize(TEXTURE_SIZE, TEXTURE_SIZE, Image.Interpolation.Nearest);
		}
		
		// Ensure correct format for Texture2DArray
		if (image.GetFormat() != Image.Format.Rgba8)
		{
			image.Convert(Image.Format.Rgba8);
		}
		
		return image;
	}
	
	/// <summary>
	/// Builds the Texture2DArray from loaded images.
	/// </summary>
	private void BuildTextureArray(List<Image> images)
	{
		if (images.Count == 0)
			return;
		
		// Create Texture2DArray
		TextureArray = new Texture2DArray();
		
		// Create image data for all layers
		var imageData = new Godot.Collections.Array<Image>();
		foreach (var img in images)
		{
			imageData.Add(img);
		}
		
		// Create the array texture
		TextureArray.CreateFromImages(imageData);
		
		// Optional: Set texture filtering
		// TextureArray.SetFilter(Texture2DArray.FilterEnum.Nearest); // Pixel-perfect for blocky style
	}
	
	/// <summary>
	/// Creates a fallback magenta checkerboard texture if loading fails.
	/// </summary>
	private void CreateFallbackTexture()
	{
		var fallbackImage = Image.Create(TEXTURE_SIZE, TEXTURE_SIZE, false, Image.Format.Rgba8);
		
		// Create magenta checkerboard pattern
		for (int y = 0; y < TEXTURE_SIZE; y++)
		{
			for (int x = 0; x < TEXTURE_SIZE; x++)
			{
				bool isCheckerSquare = ((x / 4) + (y / 4)) % 2 == 0;
				var color = isCheckerSquare ? Colors.Magenta : Colors.Black;
				fallbackImage.SetPixel(x, y, color);
			}
		}
		
		var imageData = new Godot.Collections.Array<Image> { fallbackImage };
		TextureArray = new Texture2DArray();
		TextureArray.CreateFromImages(imageData);
		
		// Map all block types to fallback
		_blockToLayer[ChunkData.BlockType.DIRT] = 0;
		_blockToLayer[ChunkData.BlockType.GRASS] = 0;
		_blockToLayer[ChunkData.BlockType.STONE] = 0;
		
		GD.PrintErr("TextureManager: Using fallback magenta checkerboard texture");
	}
}

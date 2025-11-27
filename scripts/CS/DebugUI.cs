using Godot;
using System;

public partial class DebugUI : Control
{
    private Label _label;
    private Node3D _player;
    private Camera3D _camera;
    private VoxelWorld _world;

    public override void _Ready()
    {
        _label = new Label();
        AddChild(_label);
        _label.Position = new Vector2(10, 10);
        _label.Modulate = Colors.Yellow;
        
        // Find dependencies
        _player = GetNodeOrNull<Node3D>("/root/Main/Player");
        _camera = _player?.GetNodeOrNull<Camera3D>("Camera3D"); // Adjust path if needed
    }

    public override void _Process(double delta)
    {
        if (_player == null || _camera == null)
        {
            _player = GetNodeOrNull<Node3D>("/root/Main/Player");
            if (_player != null)
                _camera = _player.GetNodeOrNull<Camera3D>("Camera3D"); // Try to find camera again
                
            if (_camera == null)
                 _camera = GetViewport().GetCamera3D(); // Fallback to viewport camera
                 
            return;
        }

        var pos = _player.GlobalPosition;
        var forward = -_camera.GlobalTransform.Basis.Z;
        
        string dirStr = GetDirectionString(forward);
        
        string text = $"Pos: {pos.X:F1}, {pos.Y:F1}, {pos.Z:F1}\n";
        text += $"Facing: {dirStr}\n";
        text += $"Chunk: {Mathf.FloorToInt(pos.X / 16)}, {Mathf.FloorToInt(pos.Y / 16)}, {Mathf.FloorToInt(pos.Z / 16)}\n";
        
        _label.Text = text;
    }
    
    private string GetDirectionString(Vector3 dir)
    {
        float absX = Mathf.Abs(dir.X);
        float absY = Mathf.Abs(dir.Y);
        float absZ = Mathf.Abs(dir.Z);
        
        if (absX > absY && absX > absZ)
            return dir.X > 0 ? "+X (Right)" : "-X (Left)";
        if (absY > absX && absY > absZ)
            return dir.Y > 0 ? "+Y (Top)" : "-Y (Bottom)";
        return dir.Z > 0 ? "+Z (Back)" : "-Z (Front)";
    }
}

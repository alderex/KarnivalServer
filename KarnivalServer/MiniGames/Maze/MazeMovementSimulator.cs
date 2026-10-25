/// <summary>Adapts the server layout to the shared prediction/authority collision rules.</summary>
public sealed class MazeMovementSimulator : MazeMotion
{
    public MazeMovementSimulator(MazeLayout layout, float playerRadius, float wallThickness)
        : base(layout.GridSize, layout.Walls, playerRadius, wallThickness) { }
}

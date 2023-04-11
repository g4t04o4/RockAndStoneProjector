namespace RockAndStoneProjector;

/// <summary>
/// Трёхмерная точка на модели
/// </summary>
public class Point3D
{
    public int X { get; }

    public int Y { get; }

    public int Z { get; }

    public Point3D(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}
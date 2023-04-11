namespace RockAndStoneProjector;

/// <summary>
/// Трёхмерная точка на модели
/// </summary>
public class Point3D
{
    public int X { get; set; }

    public int Y { get; set; }

    /// <summary>
    /// Вертикальная координата
    /// </summary>
    public int Z { get; set; }

    public Point3D(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}
namespace RockAndStoneProjector;

/// <summary>
/// Класс для горизонтального слайса с изображения
/// </summary>
public class DoublePoint
{
    /// <summary>
    /// Вертикальная координата
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Левая горизонтальная координата
    /// </summary>
    public int X0 { get; set; }

    /// <summary>
    /// Правая горизонтальная координата
    /// </summary>
    public int X1 { get; set; }

    public DoublePoint(int y, int x0, int x1)
    {
        Y = y;
        X0 = x0;
        X1 = x1;
    }
}
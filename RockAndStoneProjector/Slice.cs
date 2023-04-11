namespace RockAndStoneProjector;

/// <summary>
/// Класс для горизонтального слайса с изображения
/// </summary>
public class Slice
{
    /// <summary>
    /// Вертикальная координата
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Левая горизонтальная координата
    /// </summary>
    public int Xl { get; set; }

    /// <summary>
    /// Правая горизонтальная координата
    /// </summary>
    public int Xr { get; set; }

    public Slice(int y, int xl, int xr)
    {
        Y = y;
        Xl = xl;
        Xr = xr;
    }
}
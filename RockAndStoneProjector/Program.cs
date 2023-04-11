using System.Diagnostics;

namespace RockAndStoneProjector;

internal static class Program
{
    private const string Path = @"C:\Images\tall_one";

    private static void Main(string[] args)
    {
        Projector.GenerateModel(Path, 4, 5);
    }
}
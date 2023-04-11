using System.Diagnostics;

namespace RockAndStoneProjector;

internal static class Program
{
    private static void Main(string[] args)
    {
        // string? path = @"C:\Images\tall_one";
        string? path = @"C:\Images\stoika";

        int step = 4,
            alphastep = 5;

        var projector = new Projector(step, alphastep);

        projector.GenerateModel(path);
    }
}
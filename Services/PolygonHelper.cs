using BlazorApp_ProductosAPI.Models;

namespace BlazorApp_ProductosAPI.Services;

public static class PolygonHelper
{
    public static bool LooksNormalized(List<Point> pts)
        => pts.Count > 0 && pts.All(p => p.X >= 0 && p.Y >= 0 && p.X <= 1.05 && p.Y <= 1.05);

    public static List<Point> NormalizeIfNeeded(List<Point> pts, double naturalW, double naturalH)
    {
        if (naturalW <= 0 || naturalH <= 0) return pts;
        if (!LooksNormalized(pts)) return pts;
        return pts.Select(p => new Point(
            (float)(p.X * naturalW),
            (float)(p.Y * naturalH)
        )).ToList();
    }
}


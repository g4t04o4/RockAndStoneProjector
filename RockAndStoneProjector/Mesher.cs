using g3;

namespace RockAndStoneProjector;

class PWNImplicit : BoundedImplicitFunction3d
{
    public PointAABBTree3 Spatial;

    public AxisAlignedBox3d Bounds()
    {
        return Spatial.Bounds;
    }

    public double Value(ref Vector3d pt)
    {
        return -(Spatial.FastWindingNumber(pt) - 0.5);
    }
}

public class Mesher
{
    public static List<Point3D> GetPointCloud(string path)
    {
        var pointCloud = new List<Point3D>();

        string[] points = File.ReadAllText(path).Split(Environment.NewLine.ToCharArray());

        foreach (var point in points)
        {
            string[] xyz = point.Split();
            Point3D p = new Point3D(Int32.Parse(xyz[0]), Int32.Parse(xyz[1]), Int32.Parse(xyz[2]));
            pointCloud.Add(p);
        }

        return pointCloud;
    }

    public static DMesh3 GetDMesh3FromFile(string path)
    {
        DMesh3 pointSet = new DMesh3();

        string[] points = File.ReadAllText(path).Split(Environment.NewLine.ToCharArray());

        foreach (var point in points)
        {
            if (string.IsNullOrEmpty(point))
                continue;

            string[] xyz = point.Split();

            var p = new Vector3d(Double.Parse(xyz[0]), Double.Parse(xyz[1]), Double.Parse(xyz[2]));

            pointSet.AppendVertex(p);
        }

        return pointSet;
    }

    public static void Mesh(string filepath)
    {
        DMesh3 pointSet = GetDMesh3FromFile(filepath);

        MeshNormals.QuickCompute(pointSet);

        PointAABBTree3 pointBVTree = new PointAABBTree3(pointSet);


        double[] areas = new double[pointSet.MaxVertexID];

        foreach (int vid in pointSet.VertexIndices())
        {
            pointBVTree.PointFilterF = (i) => { return i != vid; };

            int near_vid = pointBVTree.FindNearestPoint(pointSet.GetVertex(vid));

            double dist = pointSet.GetVertex(vid).Distance(pointSet.GetVertex(near_vid));

            areas[vid] = Circle2d.RadiusArea(dist);
        }

        pointBVTree.FWNAreaEstimateF = (vid) => { return areas[vid]; };

        MarchingCubes mc = new MarchingCubes();
        mc.Implicit = new PWNImplicit() { Spatial = pointBVTree };
        mc.IsoValue = 0.0;
        mc.CubeSize = pointBVTree.Bounds.MaxDim / 128;
        mc.Bounds = pointBVTree.Bounds.Expanded(mc.CubeSize * 3);
        mc.RootMode = MarchingCubes.RootfindingModes.Bisection;
        mc.Generate();

        DMesh3 resultMesh = mc.Mesh;


        string somepath = @"C:\Images\stoika\mesh.stl";

        IOWriteResult result = StandardMeshWriter.WriteFile(somepath,
            new List<WriteMesh>() { new WriteMesh(resultMesh) }, WriteOptions.Defaults);
    }
}
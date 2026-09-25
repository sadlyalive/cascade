using Unity.Mathematics;
using UnityEngine;

namespace Cascade.UI.Wireframe
{
    /// <summary>
    ///     Builds unit <see cref="MeshTopology.Lines" /> meshes in code. Unity's model importer silently deletes loose
    ///     edges, so this is the next easiest approach for tiny and conventionally-shaped edge-only meshes.
    /// </summary>
    public static class WireMesh
    {
        private static readonly Color32 HintColor = new(224, 224, 255, 255);

        // statics do not regenerate per play test, but unity still destroys meshes after a test
        // cache meshes and regenerate them if nonexistent on fetch
        private static Mesh _axes, _cube, _sphere;

        public static Mesh Axes   => _axes ? _axes : _axes = BuildAxes();
        public static Mesh Cube   => _cube ? _cube : _cube = BuildCube();
        public static Mesh Sphere => _sphere ? _sphere : _sphere = BuildSphere();

        private static Mesh BuildAxes()
        {
            var frame = new WireframeBuilder();

            frame.AddSegment(Vector3.zero, Vector3.right,   new Color32(255, 64,  64,  255)); // x
            frame.AddSegment(Vector3.zero, Vector3.up,      new Color32(64,  255, 64,  255)); // y
            frame.AddSegment(Vector3.zero, Vector3.forward, new Color32(64,  128, 255, 255)); // z

            return frame.Build("Axes");
        }

        /// <summary>Creates 12 edges of a <c>1x1x1</c> cube.</summary>
        private static Mesh BuildCube()
        {
            var frame = new WireframeBuilder();

            // bit 0/1/2 of the corner index pick the sign on x/y/z, so two corners share an edge iff they differ in one
            // bit
            for (var a = 0; a < 8; a++)
            {
                for (var bit = 1; bit < 8; bit <<= 1)
                {
                    var b = a | bit;

                    if (b == a)
                        continue; // only walk each edge from its low end

                    frame.AddSegment(Corner(a), Corner(b), HintColor);
                }
            }

            return frame.Build("Cube");

            static Vector3 Corner(int i)
            {
                return new Vector3((i & 1) == 0 ? -.5f : .5f, (i & 2) == 0 ? -.5f : .5f, (i & 4) == 0 ? -.5f : .5f);
            }
        }

        /// <summary>Creates three orthogonal <c>r = 1</c> circles for each axis.</summary>
        private static Mesh BuildSphere()
        {
            const int segments = 64;

            var frame = new WireframeBuilder();

            for (var i = 0; i < segments; i++)
            {
                var a = Point(i);
                var b = Point(i + 1);

                frame.AddSegment(new Vector3(a.x, a.y, 0f),  new Vector3(b.x, b.y, 0f),  HintColor); // xy
                frame.AddSegment(new Vector3(a.x, 0f,  a.y), new Vector3(b.x, 0f,  b.y), HintColor); // xz
                frame.AddSegment(new Vector3(0f,  a.x, a.y), new Vector3(0f,  b.x, b.y), HintColor); // yz
            }

            return frame.Build("Sphere");

            static float2 Point(int i)
            {
                const float unitCircle = Mathf.PI * 2f;

                var t = i * unitCircle / segments;
                return new float2(Mathf.Cos(t), Mathf.Sin(t));
            }
        }
    }
}
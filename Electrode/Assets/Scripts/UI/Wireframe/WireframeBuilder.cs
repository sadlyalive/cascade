using System.Collections.Generic;
using UnityEngine;

namespace Cascade.UI.Wireframe
{
    public class WireframeBuilder
    {
        private readonly List<Color32> _colors   = new();
        private readonly List<Vector3> _vertices = new();

        // each segment owns its two vertices so per-segment coloring is trivial
        // wasteful in theory but acceptable for a few hundred vertices
        public void AddSegment(Vector3 a, Vector3 b, Color32 color)
        {
            _vertices.Add(a);
            _vertices.Add(b);
            _colors.Add(color);
            _colors.Add(color);
        }

        public Mesh Build(string name)
        {
            var indices = new int[_vertices.Count];

            for (var i = 0; i < indices.Length; i++)
                indices[i] = i;

            var mesh = new Mesh { name = "Wireframe " + name };

            mesh.SetVertices(_vertices);
            mesh.SetColors(_colors);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.UploadMeshData(true); // never modified; drop CPU copy

            return mesh;
        }
    }
}
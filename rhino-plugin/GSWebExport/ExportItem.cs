using System.Collections.Generic;
using System.Drawing;

namespace GSWebExport
{
    /// <summary>One renderable object, already meshed and transformed into world coordinates.</summary>
    internal sealed class ExportItem
    {
        // Plain arrays so the file writers do not depend on Rhino's native geometry library.
        public float[] Positions;      // x,y,z per vertex (world coordinates, Z-up)
        public float[] Normals;        // x,y,z per vertex
        public int[] Indices;          // 3 per triangle
        public bool IsClosed;
        public string Id;
        public string Name;
        public int LayerIndex;
        public Color ObjectColor;      // sRGB, the colour the viewer should show
        public double Alpha = 1.0;     // 1 = opaque
        public int ColorSource;        // Rhino ObjectColorSource enum value (0 = by layer)
        public int MaterialSource;     // Rhino ObjectMaterialSource enum value (0 = by layer)
        public string ObjectType;      // "Brep", "Mesh", ... (spelled like three.js 3DMLoader)
        public bool IsInstanceDefinitionObject;
        public Dictionary<string, string> UserStrings = new Dictionary<string, string>();
    }

    /// <summary>A text dot, written to the package as an annotation.</summary>
    internal sealed class ExportAnnotation
    {
        public string Text;
        public double X, Y, Z;
        public int LayerIndex;
        public Color ObjectColor;
    }
}

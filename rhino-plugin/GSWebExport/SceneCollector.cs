using System;
using System.Collections.Generic;
using System.Drawing;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace GSWebExport
{
    /// <summary>Walks the document and turns Rhino objects into meshes the viewer can show.</summary>
    internal sealed class SceneCollector
    {
        private readonly RhinoDoc _doc;
        public readonly List<ExportItem> Items = new List<ExportItem>();
        public readonly List<ExportAnnotation> Annotations = new List<ExportAnnotation>();
        public int SkippedCount;

        public SceneCollector(RhinoDoc doc) { _doc = doc; }

        public void Collect(bool selectedOnly)
        {
            var settings = new ObjectEnumeratorSettings
            {
                NormalObjects = true,
                LockedObjects = true,
                HiddenObjects = false,
                ActiveObjects = true,
                ReferenceObjects = true,
                IncludeLights = false,
                IncludeGrips = false,
                SelectedObjectsFilter = selectedOnly,
                VisibleFilter = true,
            };
            foreach (var obj in _doc.Objects.GetObjectList(settings))
                AddObject(obj, Transform.Identity, null, false);
            FlushCurves();
        }

        // Curves are merged per (layer, colour) so thousands of them stay one light object each.
        private sealed class CurveBucket
        {
            public int LayerIndex;
            public Color Color;
            public readonly List<float> Positions = new List<float>();
            public readonly List<int> Indices = new List<int>();
        }
        private readonly Dictionary<string, CurveBucket> _curveBuckets = new Dictionary<string, CurveBucket>();

        private void AddCurve(RhinoObject obj, Transform xform, RhinoObject parent)
        {
            var crv = obj.Geometry as Curve;
            if (crv == null) return;
            Polyline pl;
            if (!crv.TryGetPolyline(out pl))
            {
                var pc = crv.ToPolyline(0, 0, 0.1, 0.0, 0.0, _doc.ModelAbsoluteTolerance, 0.0, 0.0, true);
                if (pc == null || !pc.TryGetPolyline(out pl)) { SkippedCount++; return; }
            }
            if (pl.Count < 2) return;
            if (!xform.IsIdentity) pl.Transform(xform);

            int layerIndex = ResolveLayerIndex(obj, parent);
            Color color = ResolveCurveColor(obj, parent);
            string key = layerIndex + "|" + color.ToArgb();
            CurveBucket b;
            if (!_curveBuckets.TryGetValue(key, out b))
            {
                b = new CurveBucket { LayerIndex = layerIndex, Color = color };
                _curveBuckets[key] = b;
            }
            int baseIndex = b.Positions.Count / 3;
            for (int i = 0; i < pl.Count; i++)
            {
                var p = pl[i];
                b.Positions.Add((float)p.X); b.Positions.Add((float)p.Y); b.Positions.Add((float)p.Z);
            }
            for (int i = 0; i < pl.Count - 1; i++) { b.Indices.Add(baseIndex + i); b.Indices.Add(baseIndex + i + 1); }
        }

        private void FlushCurves()
        {
            foreach (var b in _curveBuckets.Values)
            {
                Items.Add(new ExportItem
                {
                    IsLine = true,
                    Positions = b.Positions.ToArray(),
                    Normals = new float[0],
                    Indices = b.Indices.ToArray(),
                    IsClosed = false,
                    Id = Guid.NewGuid().ToString(),
                    Name = "",
                    LayerIndex = b.LayerIndex,
                    ObjectColor = b.Color,
                    Alpha = 1.0,
                    ColorSource = (int)ObjectColorSource.ColorFromObject,
                    MaterialSource = (int)ObjectMaterialSource.MaterialFromObject,
                    ObjectType = "Curve",
                    IsInstanceDefinitionObject = false,
                });
            }
            _curveBuckets.Clear();
        }

        // Curves ignore render materials: Rhino draws them with the object or layer colour.
        private Color ResolveCurveColor(RhinoObject obj, RhinoObject parent)
        {
            switch (obj.Attributes.ColorSource)
            {
                case ObjectColorSource.ColorFromObject:
                    return obj.Attributes.ObjectColor;
                case ObjectColorSource.ColorFromParent:
                    if (parent != null) return ResolveCurveColor(parent, null);
                    break;
            }
            var li = obj.Attributes.LayerIndex;
            if (li >= 0 && li < _doc.Layers.Count) return _doc.Layers[li].Color;
            return Color.Black;
        }

        private void AddObject(RhinoObject obj, Transform xform, RhinoObject parent, bool inDefinition)
        {
            switch (obj.ObjectType)
            {
                case ObjectType.InstanceReference:
                    AddInstance((InstanceObject)obj, xform, parent ?? obj);
                    return;
                case ObjectType.TextDot:
                    AddTextDot(obj, xform, parent);
                    return;
                case ObjectType.Curve:
                    AddCurve(obj, xform, parent);
                    return;
                case ObjectType.Brep:
                case ObjectType.Extrusion:
                case ObjectType.Surface:
                case ObjectType.SubD:
                case ObjectType.Mesh:
                    AddMeshObject(obj, xform, parent, inDefinition);
                    return;
                default:
                    return; // points, dimensions etc. are not rendered in this version
            }
        }

        private void AddInstance(InstanceObject inst, Transform xform, RhinoObject topLevel)
        {
            var def = inst.InstanceDefinition;
            if (def == null) return;
            var combined = xform * inst.InstanceXform;
            foreach (var sub in def.GetObjects())
            {
                if (sub == null) continue;
                // Hidden sub-objects of a block are not drawn by Rhino either.
                if (sub.IsHidden) continue;
                AddObject(sub, combined, topLevel, false);
            }
        }

        private void AddTextDot(RhinoObject obj, Transform xform, RhinoObject parent)
        {
            var dot = obj.Geometry as TextDot;
            if (dot == null) return;
            var p = dot.Point;
            p.Transform(xform);
            Annotations.Add(new ExportAnnotation
            {
                Text = dot.Text ?? "",
                X = p.X, Y = p.Y, Z = p.Z,
                LayerIndex = ResolveLayerIndex(obj, parent),
                ObjectColor = ResolveColor(obj, parent, out _),
            });
        }

        private void AddMeshObject(RhinoObject obj, Transform xform, RhinoObject parent, bool inDefinition)
        {
            Mesh merged = BuildMesh(obj);
            if (merged == null || merged.Faces.Count == 0 || merged.Vertices.Count == 0) { SkippedCount++; return; }

            if (!xform.IsIdentity)
            {
                merged.Transform(xform);
                // A mirrored copy turns the triangles inside out; turn them back.
                if (xform.Determinant < 0) merged.Flip(true, true, true);
            }
            merged.Faces.ConvertQuadsToTriangles();
            if (merged.Normals.Count != merged.Vertices.Count) merged.Normals.ComputeNormals();

            var item = new ExportItem
            {
                Positions = ToPositions(merged),
                Normals = ToNormals(merged),
                Indices = ToIndices(merged),
                IsClosed = merged.IsClosed,
                Id = obj.Id.ToString(),
                Name = obj.Name ?? "",
                LayerIndex = ResolveLayerIndex(obj, parent),
                ColorSource = (int)obj.Attributes.ColorSource,
                MaterialSource = (int)obj.Attributes.MaterialSource,
                ObjectType = obj.ObjectType == ObjectType.Mesh ? "Mesh" : obj.ObjectType.ToString(),
                IsInstanceDefinitionObject = inDefinition,
            };
            double alpha;
            item.ObjectColor = ResolveColor(obj, parent, out alpha);
            item.Alpha = alpha;

            var strings = obj.Attributes.GetUserStrings();
            if (strings != null)
                foreach (string key in strings.AllKeys)
                    if (key != null) item.UserStrings[key] = strings[key];

            Items.Add(item);
        }

        private static float[] ToPositions(Mesh m)
        {
            var a = new float[m.Vertices.Count * 3];
            for (int i = 0; i < m.Vertices.Count; i++)
            {
                var p = m.Vertices[i];
                a[i * 3] = p.X; a[i * 3 + 1] = p.Y; a[i * 3 + 2] = p.Z;
            }
            return a;
        }

        private static float[] ToNormals(Mesh m)
        {
            var a = new float[m.Normals.Count * 3];
            for (int i = 0; i < m.Normals.Count; i++)
            {
                var n = m.Normals[i];
                a[i * 3] = n.X; a[i * 3 + 1] = n.Y; a[i * 3 + 2] = n.Z;
            }
            return a;
        }

        private static int[] ToIndices(Mesh m)
        {
            var a = new int[m.Faces.Count * 3];
            for (int i = 0; i < m.Faces.Count; i++)
            {
                var f = m.Faces[i];
                a[i * 3] = f.A; a[i * 3 + 1] = f.B; a[i * 3 + 2] = f.C;
            }
            return a;
        }

        /// <summary>Gets (or creates) the render mesh(es) of an object and merges them into one new mesh.</summary>
        private Mesh BuildMesh(RhinoObject obj)
        {
            var meshObj = obj as MeshObject;
            if (meshObj != null && meshObj.MeshGeometry != null)
                return meshObj.MeshGeometry.DuplicateMesh();

            Mesh[] parts = obj.GetMeshes(MeshType.Render);
            if (parts == null || parts.Length == 0)
            {
                obj.CreateMeshes(MeshType.Render, MeshingParameters.DocumentCurrentSetting(_doc), false);
                parts = obj.GetMeshes(MeshType.Render);
            }
            if (parts == null || parts.Length == 0) return null;

            var merged = new Mesh();
            foreach (var part in parts)
                if (part != null) merged.Append(part);
            return merged;
        }

        private int ResolveLayerIndex(RhinoObject obj, RhinoObject parent)
        {
            // Objects inside a block follow the layer of the block when theirs is the default layer.
            return obj.Attributes.LayerIndex;
        }

        /// <summary>Colour the object is displayed with, and its opacity.</summary>
        private Color ResolveColor(RhinoObject obj, RhinoObject parent, out double alpha)
        {
            alpha = 1.0;

            // An explicit render material wins, like Rhino's Rendered mode.
            var matIndex = ResolveMaterialIndex(obj, parent);
            if (matIndex >= 0 && matIndex < _doc.Materials.Count)
            {
                var m = _doc.Materials[matIndex];
                alpha = Math.Max(0.0, Math.Min(1.0, 1.0 - m.Transparency));
                return m.DiffuseColor;
            }

            switch (obj.Attributes.ColorSource)
            {
                case ObjectColorSource.ColorFromObject:
                    return obj.Attributes.ObjectColor;
                case ObjectColorSource.ColorFromParent:
                    if (parent != null) return ResolveColor(parent, null, out alpha);
                    break;
                case ObjectColorSource.ColorFromMaterial:
                    break;
            }
            var layerIndex = obj.Attributes.LayerIndex;
            if (layerIndex >= 0 && layerIndex < _doc.Layers.Count) return _doc.Layers[layerIndex].Color;
            return Color.FromArgb(204, 204, 204);
        }

        private int ResolveMaterialIndex(RhinoObject obj, RhinoObject parent)
        {
            switch (obj.Attributes.MaterialSource)
            {
                case ObjectMaterialSource.MaterialFromObject:
                    return obj.Attributes.MaterialIndex;
                case ObjectMaterialSource.MaterialFromParent:
                    return parent != null ? ResolveMaterialIndex(parent, null) : -1;
                default:
                    var li = obj.Attributes.LayerIndex;
                    return li >= 0 && li < _doc.Layers.Count ? _doc.Layers[li].RenderMaterialIndex : -1;
            }
        }
    }
}

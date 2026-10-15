using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace GSWebExport
{
    /// <summary>
    /// Writes the .rhv session container described in docs/rhv-format.md:
    /// gzip( "RV3D" | containerVersion u32 | jsonLength u32 | metadata JSON | GLB ).
    /// </summary>
    internal static class RhvWriter
    {
        public static byte[] Write(RhinoDoc doc, SceneCollector scene, byte[] glb)
            => RhvContainer.Pack(Encoding.UTF8.GetBytes(BuildMetadata(doc, scene)), glb);

        private static string BuildMetadata(RhinoDoc doc, SceneCollector scene)
        {
            var j = new JsonBuilder().BeginObject();
            j.Int("version", 4);
            j.Int("minViewerSchema", 3);
            j.BeginObject("producer")
                .Str("name", "GSWebExport")
                .Str("version", "0.1.0")
                .Str("host", "Rhino " + RhinoApp.Version.ToString())
                .EndObject();

            j.Str("displayMode", "shaded");
            j.BeginObject("settings").Str("modelUnit", doc.ModelUnitSystem.ToString()).EndObject();

            WriteCamera(j, doc);
            WriteLayers(j, doc);
            WriteAnnotations(j, scene);
            WriteNamedViews(j, doc);

            j.BeginArray("namedViews").EndArray();
            j.BeginObject("customMaterials").EndObject();
            j.BeginArray("hiddenKeys").EndArray();
            j.BeginArray("completedMeasurements").EndArray();
            j.BeginArray("notes").EndArray();
            j.EndObject();
            return j.ToString();
        }

        private static void WriteCamera(JsonBuilder j, RhinoDoc doc)
        {
            var view = doc.Views.ActiveView;
            if (view == null) return;
            var vp = view.ActiveViewport;
            var loc = vp.CameraLocation;
            var target = vp.CameraTarget;
            var up = vp.CameraUp;
            j.BeginObject("cameraState")
                .NumArray("position", loc.X, loc.Y, loc.Z)
                .NumArray("target", target.X, target.Y, target.Z)
                .NumArray("up", up.X, up.Y, up.Z)
                .Str("projection", vp.IsParallelProjection ? "parallel" : "perspective")
                .EndObject();
        }

        private static void WriteLayers(JsonBuilder j, RhinoDoc doc)
        {
            j.BeginArray("parsedLayers");
            foreach (var layer in doc.Layers)
            {
                if (layer.IsDeleted) continue;
                int parentIndex = -1;
                if (layer.ParentLayerId != Guid.Empty)
                {
                    var parent = doc.Layers.FindId(layer.ParentLayerId);
                    if (parent != null) parentIndex = parent.Index;
                }
                j.BeginObject()
                    .Int("index", layer.Index)
                    .Str("name", layer.FullPath)
                    .BeginObject("color").Int("r", layer.Color.R).Int("g", layer.Color.G).Int("b", layer.Color.B).Int("a", 255).EndObject()
                    .Bool("visible", layer.IsVisible)
                    .Int("parentLayerIndex", parentIndex)
                    .Null("customMaterial")
                    .Null("originalCustomMaterial")
                    .EndObject();
            }
            j.EndArray();
        }

        private static void WriteAnnotations(JsonBuilder j, SceneCollector scene)
        {
            j.BeginArray("parsedAnnotations");
            foreach (var a in scene.Annotations)
            {
                j.BeginObject()
                    .Str("type", "TextDot")
                    .Str("text", a.Text)
                    .NumArray("position", a.X, a.Y, a.Z)
                    .Int("layerIndex", a.LayerIndex)
                    .BeginObject("objectColor").Int("r", a.ObjectColor.R).Int("g", a.ObjectColor.G).Int("b", a.ObjectColor.B).Int("a", 255).EndObject()
                    .Bool("visible", true)
                    .EndObject();
            }
            j.EndArray();
        }

        private static void WriteNamedViews(JsonBuilder j, RhinoDoc doc)
        {
            j.BeginArray("rhinoNamedViews");
            for (int i = 0; i < doc.NamedViews.Count; i++)
            {
                try
                {
                    var nv = doc.NamedViews[i];
                    var vp = nv.Viewport;
                    var loc = vp.CameraLocation;
                    var up = vp.CameraUp;
                    var t = vp.TargetPoint;
                    j.BeginObject()
                        .Str("name", nv.Name)
                        .NumArray("position", loc.X, loc.Y, loc.Z)
                        .NumArray("up", up.X, up.Y, up.Z)
                        .NumArray("target", t.X, t.Y, t.Z)
                        .EndObject();
                }
                catch { /* a broken named view must not stop the export */ }
            }
            j.EndArray();
        }
    }
}

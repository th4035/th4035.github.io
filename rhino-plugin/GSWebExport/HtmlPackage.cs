using System;
using System.IO;
using System.Reflection;
using System.Text;
using Rhino;

namespace GSWebExport
{
    internal sealed class PackageResult
    {
        public byte[] Bytes;
        public int ObjectCount;
        public int SkippedCount;
    }

    /// <summary>Builds the final single-file .html: viewer page + embedded model.</summary>
    internal static class HtmlPackage
    {
        public static PackageResult Build(RhinoDoc doc, bool selectedOnly)
        {
            var scene = new SceneCollector(doc);
            scene.Collect(selectedOnly);
            if (scene.Items.Count == 0)
                throw new InvalidOperationException("There is nothing to export (no visible curves, surfaces, polysurfaces, meshes or blocks).");

            byte[] glb = GlbWriter.Write(scene.Items);
            byte[] rhv = RhvWriter.Write(doc, scene, glb);

            string name = (string.IsNullOrEmpty(doc.Name) ? "model" : Path.GetFileNameWithoutExtension(doc.Name)) + ".rhv";
            string html = HtmlInjector.Inject(LoadShell(), rhv, name);

            return new PackageResult
            {
                Bytes = new UTF8Encoding(false).GetBytes(html),
                ObjectCount = scene.Items.Count,
                SkippedCount = scene.SkippedCount,
            };
        }

        private static string LoadShell()
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var s = asm.GetManifestResourceStream("viewer-shell.html"))
            {
                if (s == null) throw new InvalidOperationException("viewer-shell.html is not embedded in the plug-in.");
                using (var r = new StreamReader(s, Encoding.UTF8)) return r.ReadToEnd();
            }
        }
    }
}

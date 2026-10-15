using System;
using System.Diagnostics;
using System.IO;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.UI;

namespace GSWebExport
{
    /// <summary>
    /// Command: GSWebExport — saves the model (or the selected objects) as a single
    /// .html file that opens in any browser with the GSWeb Viewer.
    /// </summary>
    [System.Runtime.InteropServices.Guid("b1c9d3a2-7e4f-4c61-8a0b-2d5e9f3c7a10")]
    public class GSWebExportCommand : Command
    {
        public override string EnglishName => "GSWebExport";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // 1) Choose what to export: the current selection if there is one, otherwise everything visible.
            var selected = doc.Objects.GetSelectedObjects(false, false);
            bool selectedOnly = false;
            foreach (var _ in selected) { selectedOnly = true; break; }

            // 2) Ask where to save.
            string path;
            if (mode == RunMode.Scripted)
            {
                var gs = new GetString();
                gs.SetCommandPrompt("Output .html file");
                if (gs.Get() != GetResult.String) return Result.Cancel;
                path = gs.StringResult().Trim('"');
            }
            else
            {
                var dlg = new SaveFileDialog
                {
                    Title = "GSWeb Viewer HTML export",
                    Filter = "GSWeb Viewer HTML (*.html)|*.html",
                    DefaultExt = "html",
                    FileName = string.IsNullOrEmpty(doc.Name)
                        ? "model.html"
                        : Path.GetFileNameWithoutExtension(doc.Name) + ".html",
                };
                if (!dlg.ShowSaveDialog()) return Result.Cancel;
                path = dlg.FileName;
            }
            if (string.IsNullOrWhiteSpace(path)) return Result.Cancel;
            if (!path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) path += ".html";

            // 3) Build and write the package.
            try
            {
                RhinoApp.WriteLine("GSWebExport: building package...");
                var result = HtmlPackage.Build(doc, selectedOnly);
                File.WriteAllBytes(path, result.Bytes);
                RhinoApp.WriteLine("GSWebExport: {0} object(s), {1:F1} MB -> {2}",
                    result.ObjectCount, result.Bytes.Length / 1048576.0, path);
                if (result.SkippedCount > 0)
                    RhinoApp.WriteLine("GSWebExport: {0} object(s) skipped (no mesh could be created).", result.SkippedCount);
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("GSWebExport failed: " + ex.Message);
                return Result.Failure;
            }

            // 4) Show the result right away.
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch { /* opening is a convenience only */ }
            return Result.Success;
        }
    }
}

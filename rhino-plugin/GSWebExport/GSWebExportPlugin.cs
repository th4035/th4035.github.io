using Rhino.PlugIns;

namespace GSWebExport
{
    /// <summary>Plug-in entry point. Rhino finds the commands in this assembly by itself.</summary>
    public class GSWebExportPlugin : PlugIn
    {
        public GSWebExportPlugin()
        {
            Instance = this;
        }

        public static GSWebExportPlugin Instance { get; private set; }

        // Load as soon as a command is used, not at Rhino start-up.
        public override PlugInLoadTime LoadTime => PlugInLoadTime.WhenNeeded;
    }
}

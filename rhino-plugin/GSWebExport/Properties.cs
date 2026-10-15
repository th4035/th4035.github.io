using System.Reflection;
using System.Runtime.InteropServices;
using Rhino.PlugIns;

// Rhino reads these to identify the plug-in. Do not reuse this Guid for another plug-in.
[assembly: PlugInDescription(DescriptionType.Address, "https://gswebplay.co.kr/")]
[assembly: PlugInDescription(DescriptionType.Organization, "GSWeb Recreation Equipment")]
[assembly: PlugInDescription(DescriptionType.UpdateUrl, "https://gswebplay.co.kr/")]
[assembly: PlugInDescription(DescriptionType.WebSite, "https://gswebplay.co.kr/")]

[assembly: AssemblyTitle("GSWebExport")]
[assembly: AssemblyDescription("Exports the Rhino model as a self-contained HTML package for the GSWeb Viewer")]
[assembly: AssemblyCompany("GSWeb Recreation Equipment")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: Guid("6f0d6a4e-3b1c-4a53-9d0e-5c2f7a8b1e42")]

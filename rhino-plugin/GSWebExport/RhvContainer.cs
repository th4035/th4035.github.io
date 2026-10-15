using System.IO;
using System.IO.Compression;
using System.Text;

namespace GSWebExport
{
    /// <summary>Byte layout of a .rhv file (docs/rhv-format.md §1). No Rhino types involved.</summary>
    internal static class RhvContainer
    {
        private const uint ContainerVersion = 1;

        /// <summary>Wraps metadata JSON + GLB in the gzip'd RV3D container. No Rhino types involved.</summary>
        public static byte[] Pack(byte[] json, byte[] glb)
        {

            using (var raw = new MemoryStream())
            {
                using (var w = new BinaryWriter(raw, new UTF8Encoding(false), true))
                {
                    w.Write(Encoding.ASCII.GetBytes("RV3D"));
                    w.Write(ContainerVersion);          // little-endian on every platform Rhino runs on
                    w.Write((uint)json.Length);
                    w.Write(json);
                    w.Write(glb);
                }

                using (var gz = new MemoryStream())
                {
                    using (var z = new GZipStream(gz, CompressionLevel.Optimal, true))
                        z.Write(raw.GetBuffer(), 0, (int)raw.Length);
                    return gz.ToArray();
                }
            }
        }
    }
}

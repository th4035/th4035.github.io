using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GSWebExport
{
    /// <summary>
    /// Writes a glTF 2.0 binary (.glb) in the form the viewer expects (see docs/rhv-format.md §5):
    /// Z-up with no axis conversion, linear base colours, per-node extras.attributes.
    /// </summary>
    internal static class GlbWriter
    {
        public static byte[] Write(IList<ExportItem> items)
        {
            var bin = new MemoryStream();
            var bufferViews = new List<string>();
            var accessors = new List<string>();
            var materials = new List<string>();
            var materialIndexByKey = new Dictionary<string, int>();
            var meshes = new List<string>();
            var nodes = new List<string>();

            foreach (var item in items)
            {
                int vCount = item.Positions.Length / 3;

                // --- POSITION ---
                Align4(bin);
                int posOffset = (int)bin.Position;
                double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
                var w = new BinaryWriter(bin);
                for (int i = 0; i < vCount; i++)
                {
                    float x = item.Positions[i * 3], y = item.Positions[i * 3 + 1], z = item.Positions[i * 3 + 2];
                    w.Write(x); w.Write(y); w.Write(z);
                    minX = Math.Min(minX, x); minY = Math.Min(minY, y); minZ = Math.Min(minZ, z);
                    maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); maxZ = Math.Max(maxZ, z);
                }
                int posLength = vCount * 12;

                // --- NORMAL (not needed for lines) ---
                int nrmOffset = 0, nrmLength = 0;
                if (!item.IsLine)
                {
                    Align4(bin);
                    nrmOffset = (int)bin.Position;
                    for (int i = 0; i < vCount * 3; i++) w.Write(item.Normals[i]);
                    nrmLength = vCount * 12;
                }

                // --- INDICES (triangles, or point pairs for lines) ---
                Align4(bin);
                int idxOffset = (int)bin.Position;
                for (int i = 0; i < item.Indices.Length; i++) w.Write((uint)item.Indices[i]);
                int idxLength = item.Indices.Length * 4;

                int bvPos = bufferViews.Count;
                bufferViews.Add(BufferView(posOffset, posLength, 34962));
                if (!item.IsLine) bufferViews.Add(BufferView(nrmOffset, nrmLength, 34962));
                bufferViews.Add(BufferView(idxOffset, idxLength, 34963));
                int bvIdx = item.IsLine ? bvPos + 1 : bvPos + 2;

                int accPos = accessors.Count;
                var jp = new JsonBuilder().BeginObject()
                    .Int("bufferView", bvPos).Int("componentType", 5126).Int("count", vCount).Str("type", "VEC3")
                    .NumArray("min", minX, minY, minZ).NumArray("max", maxX, maxY, maxZ).EndObject();
                accessors.Add(jp.ToString());
                if (!item.IsLine)
                    accessors.Add(new JsonBuilder().BeginObject()
                        .Int("bufferView", bvPos + 1).Int("componentType", 5126).Int("count", vCount).Str("type", "VEC3").EndObject().ToString());
                int accIdx = accessors.Count;
                accessors.Add(new JsonBuilder().BeginObject()
                    .Int("bufferView", bvIdx).Int("componentType", 5125).Int("count", item.Indices.Length).Str("type", "SCALAR").EndObject().ToString());

                int matIndex = MaterialIndex(item, materials, materialIndexByKey);

                var prim = new JsonBuilder().BeginObject()
                    .BeginArray("primitives").BeginObject();
                if (item.IsLine)
                    prim.BeginObject("attributes").Int("POSITION", accPos).EndObject();
                else
                    prim.BeginObject("attributes").Int("POSITION", accPos).Int("NORMAL", accPos + 1).EndObject();
                prim.Int("indices", accIdx).Int("material", matIndex).Int("mode", item.IsLine ? 1 : 4)
                    .EndObject().EndArray()
                    .EndObject();
                meshes.Add(prim.ToString());

                nodes.Add(NodeJson(item, meshes.Count - 1));
            }

            // Pad the binary chunk to 4 bytes.
            Align4(bin);

            var sb = new StringBuilder();
            sb.Append("{\"asset\":{\"version\":\"2.0\",\"generator\":\"GSWebExport\"},\"scene\":0,");
            sb.Append("\"scenes\":[{\"nodes\":[");
            for (int i = 0; i < nodes.Count; i++) { if (i > 0) sb.Append(','); sb.Append(i); }
            sb.Append("]}],");
            sb.Append("\"nodes\":[").Append(string.Join(",", nodes)).Append("],");
            sb.Append("\"meshes\":[").Append(string.Join(",", meshes)).Append("],");
            sb.Append("\"materials\":[").Append(string.Join(",", materials)).Append("],");
            sb.Append("\"accessors\":[").Append(string.Join(",", accessors)).Append("],");
            sb.Append("\"bufferViews\":[").Append(string.Join(",", bufferViews)).Append("],");
            sb.Append("\"buffers\":[{\"byteLength\":").Append(bin.Length).Append("}]}");

            byte[] jsonBytes = Encoding.UTF8.GetBytes(sb.ToString());
            int jsonPad = (4 - jsonBytes.Length % 4) % 4;
            int jsonChunkLen = jsonBytes.Length + jsonPad;
            int binLen = (int)bin.Length;
            int total = 12 + 8 + jsonChunkLen + 8 + binLen;

            using (var outMs = new MemoryStream(total))
            using (var ow = new BinaryWriter(outMs))
            {
                ow.Write(0x46546C67u);           // "glTF"
                ow.Write(2u);                    // version
                ow.Write((uint)total);
                ow.Write((uint)jsonChunkLen);
                ow.Write(0x4E4F534Au);           // "JSON"
                ow.Write(jsonBytes);
                for (int i = 0; i < jsonPad; i++) ow.Write((byte)0x20);
                ow.Write((uint)binLen);
                ow.Write(0x004E4942u);           // "BIN\0"
                ow.Write(bin.GetBuffer(), 0, binLen);
                return outMs.ToArray();
            }
        }

        private static string BufferView(int offset, int length, int target) =>
            new JsonBuilder().BeginObject().Int("buffer", 0).Int("byteOffset", offset).Int("byteLength", length).Int("target", target).EndObject().ToString();

        private static void Align4(MemoryStream ms)
        {
            while (ms.Position % 4 != 0) ms.WriteByte(0);
        }

        private static int MaterialIndex(ExportItem item, List<string> materials, Dictionary<string, int> cache)
        {
            double r = SrgbToLinear(item.ObjectColor.R / 255.0);
            double g = SrgbToLinear(item.ObjectColor.G / 255.0);
            double b = SrgbToLinear(item.ObjectColor.B / 255.0);
            double a = item.Alpha;
            string key = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F4}|{1:F4}|{2:F4}|{3:F3}", r, g, b, a);
            int index;
            if (cache.TryGetValue(key, out index)) return index;

            var j = new JsonBuilder().BeginObject();
            j.Str("name", "m" + materials.Count);
            j.BeginObject("pbrMetallicRoughness")
                .NumArray("baseColorFactor", r, g, b, a)
                .Num("metallicFactor", 0.0)
                .Num("roughnessFactor", 0.6)
                .EndObject();
            if (a < 0.999) j.Str("alphaMode", "BLEND");
            j.Bool("doubleSided", true);
            j.EndObject();

            index = materials.Count;
            materials.Add(j.ToString());
            cache[key] = index;
            return index;
        }

        private static string NodeJson(ExportItem item, int meshIndex)
        {
            var j = new JsonBuilder().BeginObject();
            j.Int("mesh", meshIndex);
            j.Str("name", string.IsNullOrEmpty(item.Name) ? item.ObjectType : item.Name);
            j.BeginObject("extras");
            if (item.IsClosed) j.Bool("solid", true);
            j.BeginObject("attributes")
                .Str("id", item.Id)
                .Str("name", item.Name)
                .Int("layerIndex", item.LayerIndex)
                .Bool("visible", true)
                .BeginObject("objectColor")
                    .Int("r", item.ObjectColor.R).Int("g", item.ObjectColor.G).Int("b", item.ObjectColor.B).Int("a", 255)
                .EndObject()
                .Int("colorSource", item.ColorSource)
                .Int("materialSource", item.MaterialSource)
                .Bool("isInstanceDefinitionObject", item.IsInstanceDefinitionObject)
                .Str("objectType", item.ObjectType);
            j.BeginObject("userStrings");
            foreach (var kv in item.UserStrings) j.Str(kv.Key, kv.Value);
            j.EndObject();
            j.EndObject();   // attributes
            j.EndObject();   // extras
            j.EndObject();
            return j.ToString();
        }

        private static double SrgbToLinear(double c) =>
            c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}

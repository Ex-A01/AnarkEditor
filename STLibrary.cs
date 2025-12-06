using System.IO;
using System.IO.Compression;

namespace EvershadeLibrary
{
    public static class LM2Helper
    {
        public static MemoryStream Decompress(Stream source)
        {
            using var ds = new DeflateStream(source, CompressionMode.Decompress);
            var ms = new MemoryStream((int)source.Length);

            ds.CopyTo(ms);
            ms.Position = 0;

            return ms;
        }

        public static bool GetBit(dynamic value, int position)
        {
            return ((value >> position) & 1) != 0;
        }

        public static dynamic GetBits(dynamic value, int position, int count)
        {
            var mask = (1u << count) - 1;
            return (value >> position) & mask;
        }

        public static dynamic SetBit(dynamic value, bool bit, int position)
        {
            var bitPos = 1u << position;

            return bit ?
                value | bitPos :
                value & ~bitPos;
        }

        public static dynamic SetBits(dynamic value, dynamic bits, int position, int count)
        {
            var mask = (1u << count) - 1;

            value &= ~(mask << position);
            value |= bits << position;

            return value;
        }
    }
}
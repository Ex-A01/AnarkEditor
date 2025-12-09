using EvershadeEditor.LM2;
using EvershadeLibrary;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Documents;

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

        //Trust me bro this works somehow
        public static void REPACK(string originalPath, string outputPath)
        {
            try
            {
                // Chemins
                string srcDict = Path.ChangeExtension(originalPath, ".dict");
                string srcData = Path.ChangeExtension(originalPath, ".data");
                string dstDict = Path.ChangeExtension(outputPath, ".REdict"); // Extension standard .dict pour que l'éditeur le reconnaisse
                string dstData = Path.ChangeExtension(outputPath, ".REdata");

                if (!File.Exists(srcDict) || !File.Exists(srcData))
                {
                    MessageBox.Show("Fichiers sources manquants.");
                    return;
                }

                // 1. Chargement du dictionnaire original
                var dict = new Dictionary();
                dict.Load(srcDict);

                //Stream sourceStream = null;
                FileStream fsRaw = File.OpenRead(srcData);
                FileStream fsDst = File.Open(dstData, FileMode.Append);

                if (dict.IsCompressed == 1)
                {
                    foreach (DataBlock b in dict.Blocks)
                    {
                        if (b.FileExtension == 0) //only decompress data blocks
                        {
                           byte[] compressedData = new byte[b.CompressedSize];
                           fsRaw.Seek(b.Offset, SeekOrigin.Begin);
                           fsRaw.Read(compressedData, 0, compressedData.Length);

                           byte[] decompData = new byte[b.DecompressedSize];
                           decompData = b.DecompressBlock(compressedData, b.DecompressedSize);

                           fsDst.Seek(0, SeekOrigin.End);
                           fsDst.Write(decompData, 0, decompData.Length);
                           b.Offset = (uint)fsDst.Position - (uint)decompData.Length; //trust me bro offset moment
                        }
                    }
                    fsDst.Close();
                    fsRaw.Close();

                    //Output same dict but without compression flag
                    dict.IsCompressed = 0;
                    dict.Save(dstDict);

                }
                else
                {
                    MessageBox.Show("File already decompressed !");
                    return;
                }

                MessageBox.Show($"Succès !\nArchive reconstruite dans :\n{dstData}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur critique lors de la reconstruction :\n{ex.Message}");
            }
        }
    }
}
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
        public static void DecompressDataArchive(string originalPath, string outputPath)
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

        public static void CompressDataArchive(string originalPath, string outputPath)
        {
            try
            {
                // Chemins (On part du principe que originalPath pointe vers la version décompressée .REdata/.REdict)
                // Mais pour garder la symétrie avec ton code, disons qu'on prend un dossier ou un nom de base.
                // Ici je suppose que "originalPath" est le fichier .REdata décompressé

                string srcData = Path.ChangeExtension(originalPath, ".data");
                string srcDict = Path.ChangeExtension(originalPath, ".dict");

                // Sortie : On remet les extensions originales .data et .dict
                string dstData = Path.ChangeExtension(outputPath, ".COdata");
                string dstDict = Path.ChangeExtension(outputPath, ".COdict");

                if (!File.Exists(srcDict) || !File.Exists(srcData))
                {
                    MessageBox.Show("Fichiers sources décompressés (.REdata/.REdict) manquants.");
                    return;
                }

                // 1. Chargement du dictionnaire (qui est actuellement en mode IsCompressed = 0)
                var dict = new Dictionary();
                dict.Load(srcDict);

                if (dict.IsCompressed != 0)
                {
                    MessageBox.Show("Le dictionnaire indique que les données sont déjà compressées !");
                    return;
                }

                FileStream fsRaw = File.OpenRead(srcData);      // Lecture des données brutes
                FileStream fsDst = File.Create(dstData);        // Écriture des données compressées

                // 2. Boucle de compression
                foreach (DataBlock b in dict.Blocks)
                {
                    if (b.FileExtension == 0) // On ne compresse que ce qui doit l'être
                    {
                        // Lecture de la donnée brute
                        // Note : Dans le fichier décompressé, la taille EST DecompressedSize
                        byte[] rawData = new byte[b.DecompressedSize];

                        fsRaw.Seek(b.Offset, SeekOrigin.Begin);
                        fsRaw.Read(rawData, 0, rawData.Length);

                        // Compression
                        byte[] compressedData = b.CompressBlock(rawData);

                        // Mise à jour du Block pour le nouveau fichier
                        // L'offset est la position actuelle dans le fichier de destination (avant écriture)
                        b.Offset = (uint)fsDst.Position;
                        b.CompressedSize = (uint)compressedData.Length;

                        // Écriture
                        fsDst.Write(compressedData, 0, compressedData.Length);
                    }
                }

                fsDst.Close();
                fsRaw.Close();

                // 3. Sauvegarde du dictionnaire avec le flag activé
                dict.IsCompressed = 1;
                dict.Save(dstDict);

                MessageBox.Show($"Succès !\nArchive compressée générée dans :\n{dstData}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur critique lors de la compression :\n{ex.Message}");
            }
        }
    }
}
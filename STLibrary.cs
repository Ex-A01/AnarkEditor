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
    }
}

namespace CompressionTools
{
    public class ZlibDecompressor
    {

            /// <summary>
            /// Extrait tous les blocs définis dans la liste à partir du fichier source.
            /// </summary>
            /// <param name="sourceFilePath">Le chemin du fichier .bin complet</param>
            /// <param name="outputFolder">Le dossier où sauvegarder les fichiers extraits</param>
            /// <param name="blocks">La liste des blocs (doit être déjà remplie)</param>
            public void ExtractBlocks(string sourceFilePath, string outputFolder, List<DataBlock> blocks)
            {
                if (!File.Exists(sourceFilePath)) return;
                Directory.CreateDirectory(outputFolder);

                // On ouvre le fichier UNE SEULE FOIS pour optimiser
                using (FileStream fs = File.OpenRead(sourceFilePath))
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    int counter = 0;

                    foreach (var block in blocks)
                    {
                        // 1. Validation basique
                        if (block.CompressedSize == 0 || block.Offset >= fs.Length)
                        {
                            Console.WriteLine($"Bloc {counter} ignoré (vide ou hors limites).");
                            counter++;
                            continue;
                        }

                        try
                        {
                            // 2. SE POSITIONNER (Seek)
                            // On va directement à l'adresse indiquée par l'Offset
                            fs.Seek(block.Offset, SeekOrigin.Begin);

                            // 3. LIRE LES BYTES COMPRESSÉS
                            // On lit exactement le nombre d'octets indiqué par CompressedSize
                            byte[] compressedBytes = reader.ReadBytes((int)block.CompressedSize);

                            // 4. DÉCOMPRESSER
                            // On appelle VOTRE méthode qui gère le byte[]
                            byte[] decompressedBytes = block.DecompressBlock(compressedBytes, block.DecompressedSize);

                            // 5. SAUVEGARDER
                            string fileName = $"File_{counter}_{block.Offset:X}.bin";
                            string fullPath = Path.Combine(outputFolder, fileName);

                            File.WriteAllBytes(fullPath, decompressedBytes);

                            Console.WriteLine($"Bloc {counter} extrait : {fileName} (Taille: {decompressedBytes.Length})");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erreur sur le bloc {counter} à l'offset {block.Offset}: {ex.Message}");
                        }

                        counter++;
                    }
                }
            }
    }
}

namespace LM2Tools
{
    public class LM2DataExtractor
    {
        // Signature du fichier .dict
        private const uint FILE_IDENTIFIER = 0xA9F32458;

        public class ExtractedBlock
        {
            public int Index { get; set; }
            public byte[] Data { get; set; }
            public bool IsCompressed { get; set; }
        }

        /// <summary>
        /// Extrait et décompresse tout le contenu d'une paire de fichiers LM2 (.dict + .data).
        /// </summary>
        /// <param name="basePath">Chemin du fichier (sans extension ou avec). Le code cherchera .dict et .data</param>
        /// <param name="outputDirectory">Dossier où sauvegarder les blocs extraits</param>
        public static void ExtractAll(string basePath, string outputDirectory)
        {
            string dictPath = Path.ChangeExtension(basePath, ".dict");
            string dataPath = Path.ChangeExtension(basePath, ".data");

            if (!File.Exists(dictPath) || !File.Exists(dataPath))
                throw new FileNotFoundException("Les fichiers .dict et .data sont requis.");

            Directory.CreateDirectory(outputDirectory);

            using var dictStream = File.OpenRead(dictPath);
            using var dataStream = File.OpenRead(dataPath);
            using var reader = new BinaryReader(dictStream);

            // --- 1. Lecture du Header .dict ---
            if (reader.ReadUInt32() != FILE_IDENTIFIER)
                throw new Exception("Signature de fichier .dict invalide.");

            ushort flags = reader.ReadUInt16();
            bool isGlobalCompressed = reader.ReadBoolean(); // Le flag global de compression
            dictStream.Seek(1, SeekOrigin.Current); // Skip align/padding

            int blockCount = reader.ReadInt32();
            dictStream.Seek(4, SeekOrigin.Current); // Skip Max Compressed Size

            // On saute les counts de tables/refs car on veut juste les données brutes des blocs
            byte tableInfoCount = reader.ReadByte();
            dictStream.Seek(1, SeekOrigin.Current);
            byte tableRefCount = reader.ReadByte();
            byte fileExtCount = reader.ReadByte();

            // Sauter les sections qu'on ne parse pas pour aller direct à la liste des blocs
            // La structure est : [Refs] -> [Infos] -> [Blocks]

            // Calcul de la taille des sections à sauter
            // FileTableReference = 4 (Hash) + 8 (Indices) = 12 bytes
            long sizeOfRefs = tableRefCount * 12;

            // FileTableInfo = 2 (Count) + 2 (Index) = 4 bytes
            long sizeOfInfos = (tableInfoCount * tableRefCount) * 4;

            dictStream.Seek(sizeOfRefs + sizeOfInfos, SeekOrigin.Current);

            // --- 2. Lecture des infos de Blocs ---
            var blocks = new List<BlockInfo>();
            for (int i = 0; i < blockCount; i++)
            {
                blocks.Add(new BlockInfo
                {
                    Offset = reader.ReadUInt32(),
                    DecompressedSize = reader.ReadUInt32(),
                    CompressedSize = reader.ReadUInt32(),
                    UsageType = reader.ReadByte(),
                    // Skip 1 (padding) + 1 (ext) + 1 (unknown) = 3 bytes
                });
                dictStream.Seek(3, SeekOrigin.Current);
            }

            // --- 3. Extraction et Décompression du .data ---
            MessageBox.Show($"Extraction de {blockCount} blocs...");

            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];

                // Se positionner dans le fichier .data
                dataStream.Seek(block.Offset, SeekOrigin.Begin);

                // Lire les données brutes
                uint readSize = isGlobalCompressed ? block.CompressedSize : block.DecompressedSize;
                byte[] rawData = new byte[readSize];
                dataStream.Read(rawData, 0, rawData.Length);

                byte[] finalData = rawData;

                // Tenter la décompression si nécessaire
                if (isGlobalCompressed)
                {
                    try
                    {
                        finalData = DecompressBlock(rawData, (int)block.DecompressedSize);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"[Erreur] Bloc {i} échec décompression: {ex.Message}. Sauvegarde brut.");
                    }
                }

                // Sauvegarde
                string fileName = Path.Combine(outputDirectory, $"block_{i}.bin");
                File.WriteAllBytes(fileName, finalData);
            }

            MessageBox.Show("Extraction terminée.");
        }

        private static byte[] DecompressBlock(byte[] data, int expectedSize)
        {
            if (data == null || data.Length < 2) return data;

            // Vérification simple du header Zlib (souvent 78 9C, 78 DA)
            ushort magic = BitConverter.ToUInt16(data, 0);
            // Note: BitConverter dépend de l'endianness du CPU, Zlib est BigEndian généralement pour le header, 
            // mais ici on vérifie juste la présence des octets.
            bool hasZlibHeader = (data[0] == 0x78 && (data[1] == 0x9C || data[1] == 0xDA));

            using var inputStream = new MemoryStream(data);

            // Si header Zlib détecté, on le saute car DeflateStream standard de .NET ne gère pas le header Zlib,
            // il gère seulement l'algo Deflate brut. (RFC 1951 vs 1950)
            if (hasZlibHeader)
            {
                inputStream.Seek(2, SeekOrigin.Begin);
            }

            using var decompressor = new DeflateStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();

            decompressor.CopyTo(outputStream);
            return outputStream.ToArray();
        }

        // Structure interne légère pour stocker les infos du .dict
        private struct BlockInfo
        {
            public uint Offset;
            public uint DecompressedSize;
            public uint CompressedSize;
            public byte UsageType;
        }




        /// <summary>
        /// Reconstruit un fichier DATA unique en fusionnant des blocs spécifiques (décompressés).
        /// Utile si le fichier original a été découpé en plusieurs chunks (ex: 0, 2, 3).
        /// </summary>
        /// <param name="basePath">Chemin du fichier source (.dict/.data)</param>
        /// <param name="outputFilePath">Chemin complet du nouveau fichier .data à créer</param>
        /// <param name="blockIndices">Liste des index à fusionner (ex: new int[] { 0, 2, 3 })</param>
        public static void RebuildCompositeData(string basePath, string outputFilePath, int[] blockIndices)
        {
            string dictPath = Path.ChangeExtension(basePath, ".dict");
            string dataPath = Path.ChangeExtension(basePath, ".data");

            if (!File.Exists(dictPath) || !File.Exists(dataPath))
                throw new FileNotFoundException("Fichiers source introuvables.");

            using var dictStream = File.OpenRead(dictPath);
            using var dataStream = File.OpenRead(dataPath);
            using var reader = new BinaryReader(dictStream);

            // --- 1. Lecture rapide du Header .dict (copié de la méthode précédente) ---
            if (reader.ReadUInt32() != FILE_IDENTIFIER) throw new Exception("Signature invalide.");
            ushort flags = reader.ReadUInt16();
            bool isGlobalCompressed = reader.ReadBoolean();
            dictStream.Seek(1, SeekOrigin.Current);
            int blockCount = reader.ReadInt32();
            dictStream.Seek(4, SeekOrigin.Current); // Max Compressed Size

            // Skip headers counts
            byte tableInfoCount = reader.ReadByte();
            dictStream.Seek(1, SeekOrigin.Current);
            byte tableRefCount = reader.ReadByte();

            // Skip sections (Refs + Infos)
            long sizeOfRefs = tableRefCount * 12;
            long sizeOfInfos = (tableInfoCount * tableRefCount) * 4;
            dictStream.Seek(sizeOfRefs + sizeOfInfos + 1, SeekOrigin.Current); // +1 pour fileExtCount lu avant mais pas stocké ici

            // --- 2. Récupération des infos de TOUS les blocs ---
            var blocks = new List<BlockInfo>();
            for (int i = 0; i < blockCount; i++)
            {
                blocks.Add(new BlockInfo
                {
                    Offset = reader.ReadUInt32(),
                    DecompressedSize = reader.ReadUInt32(),
                    CompressedSize = reader.ReadUInt32(),
                    UsageType = reader.ReadByte()
                });
                dictStream.Seek(3, SeekOrigin.Current); // Skip padding/ext/unknown
            }

            // --- 3. Création du nouveau fichier DATA unifié ---
            MessageBox.Show($"Création du fichier composite avec les blocs : {string.Join(", ", blockIndices)}");

            using var outputStream = File.Create(outputFilePath);

            foreach (int index in blockIndices)
            {
                if (index < 0 || index >= blocks.Count)
                {
                    MessageBox.Show($"[Attention] Index {index} hors limites, ignoré.");
                    continue;
                }

                var block = blocks[index];
                dataStream.Seek(block.Offset, SeekOrigin.Begin);

                // Lire les données source
                uint readSize = isGlobalCompressed ? block.CompressedSize : block.DecompressedSize;
                byte[] rawData = new byte[readSize];
                dataStream.Read(rawData, 0, rawData.Length);

                // Décompresser si nécessaire (CRUCIAL : pour recréer le flux continu, il faut décompresser les morceaux)
                byte[] dataToWrite = rawData;
                if (isGlobalCompressed)
                {
                    try
                    {
                        dataToWrite = DecompressBlock(rawData, (int)block.DecompressedSize);
                    }
                    catch
                    {
                        MessageBox.Show($"[Erreur] Impossible de décompresser le bloc {index}, écriture brute.");
                    }
                }

                // Écrire à la suite dans le nouveau fichier
                outputStream.Write(dataToWrite, 0, dataToWrite.Length);

                MessageBox.Show($"Bloc {index} ajouté. (Taille: {dataToWrite.Length} octets)");
            }

            MessageBox.Show("Nouveau DATA généré avec succès.");
        }
    }
}
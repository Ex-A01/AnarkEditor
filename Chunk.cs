using AnarkBrowser;
using ETC1Decoder;
using EvershadeEditor.Source.Interface;
using System;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;

namespace EvershadeEditor.LM2 {
    public class ChunkEntry {
        public ushort Type { get; set; }
        public ushort Flags { get; set; }
        public uint Offset { get; set; }
        public uint Size { get; set; }

        public string DisplayName { get; set; } = "";

        public ChunkEntry[]? Children { get; set; }
        public byte[]? Data { get; set; }

        public uint Index { get; set; }

        public ushort RawAlignment => (ushort)(Flags >> 1 & 10);
        public byte BlockIndex => (byte)(Flags >> 12 & 3);
        public bool HasChildren => (Flags >> 15 & 1) != 0;
        public ushort Alignment {
            get {
                if (RawAlignment == 512) {
                    return 16;
                }

                if ((byte)(Flags >> 8 & 1) != 0) {
                    return 8;
                }

                return 4;
            }
        }
    }

    public class ChunkFileEntry : ChunkEntry {
        public ChunkEntry DataChunk;

        public uint FileType;
        public uint FileHash;

        public void Read() {
            using (MemoryStream stream = new MemoryStream(Data))
            using (BinaryReader reader = new BinaryReader(stream)) {
                FileType = reader.ReadUInt32();
                FileHash = reader.ReadUInt32();
                DisplayName = " " + Helper.GetHashName(FileHash);
            }   
        }
    }

    public class TextureChunk : ChunkEntry, IChunkExtension {
        public uint TexSize;
        public uint TexHash;
        public uint TexFlags;
        public ushort TexWidth;
        public ushort TexHeight;
        public ushort TexDepth;
        public byte TexArrayCount;
        private byte RawTexMipmapLevel;
        public uint Compression;

        public byte TexMipmapLevel {
            get {
                return (byte)(RawTexMipmapLevel & 0xF);
            }
            set {
                if (value < 1 || value > 9) { throw new Exception("Mipmap Levels can only be set from 1 to 9"); }
                RawTexMipmapLevel = (byte)(value * 0x11);
            }
        }

        public void Read() {
            using (MemoryStream stream = new MemoryStream(Children[0].Data))
            using (BinaryReader reader = new BinaryReader(stream)) {
                TexSize = reader.ReadUInt32();
                TexHash = reader.ReadUInt32();

                reader.BaseStream.Seek(4, SeekOrigin.Current); // Skip Padding

                TexFlags = reader.ReadUInt32();
                TexWidth = reader.ReadUInt16();
                TexHeight = reader.ReadUInt16();
                TexDepth = reader.ReadUInt16();
                TexArrayCount = reader.ReadByte();
                RawTexMipmapLevel = reader.ReadByte();

                reader.BaseStream.Seek(4, SeekOrigin.Current); // Skip Padding
            }

            using (MemoryStream stream = new MemoryStream(Children[1].Data))
            using (BinaryReader reader = new BinaryReader(stream)) {
                reader.BaseStream.Seek(0x54, SeekOrigin.Current);
                Compression = reader.ReadUInt32();
            }
        }

        public void Write() {
            using (MemoryStream stream = new MemoryStream(Children[0].Data))
            using (BinaryWriter writer = new BinaryWriter(stream)) {
                writer.Write(TexSize);
                writer.Write(TexHash);

                writer.BaseStream.Seek(4, SeekOrigin.Current); // Skip Padding

                writer.Write(TexFlags);
                writer.Write(TexWidth);
                writer.Write(TexHeight);
                writer.Write(TexDepth);
                writer.Write(TexArrayCount);
                writer.Write(RawTexMipmapLevel);

                writer.BaseStream.Seek(4, SeekOrigin.Current); // Skip Padding
            }
        }

        public string GetCompression() {
            string compressText = "Unknown";

            switch (Compression) {
                case Helper.DXT1Identifier:
                    compressText = "DXT1";
                    break;
                case Helper.DXT5Identifier:
                    compressText = "DXT5";
                    break;
            }

            return compressText;
        }

        public BitmapImage MakeBitmap() {
            BitmapImage bitmap = new BitmapImage();

            using (MemoryStream stream = new MemoryStream(Children[1].Data)) {
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
            }

            return bitmap;
        }
    }

    public class TextureChunk3DS : ChunkEntry, IChunkExtension
    {
        //HeaderPattern
        uint TextureHeaderMAGIC;
        public uint TextureHash;
        public uint TextureFileSize;
        public uint TextureHash2;
        uint Padding;
        uint truc;
        public ushort Width;
        public ushort Height;
        ushort truc2;
        public byte MipLevel;
        byte flag;
        long truc3;
        long truc4;
        uint truc5;
        public uint CompressionFormat;

        //useless i guess
        public byte TexMipmapLevel
        {
            get
            {
                return (byte)(MipLevel & 0xF);
            }
            set
            {
                if (value < 1 || value > 9) { throw new Exception("Mipmap Levels can only be set from 1 to 9"); }
                MipLevel = (byte)(value * 0x11);
            }
        }

        //read from chunk
        public void Read()
        {
            using (MemoryStream stream = new MemoryStream(Children[0].Data))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                TextureHeaderMAGIC = reader.ReadUInt32();
                TextureHash = reader.ReadUInt32();
                reader.BaseStream.Seek(8, SeekOrigin.Current); // Skip Padding
                Width = reader.ReadUInt16();
                Height = reader.ReadUInt16();
                truc = reader.ReadUInt16();
                MipLevel = reader.ReadByte();
                flag = reader.ReadByte();
                truc3 = reader.ReadInt64();
                truc4 = reader.ReadInt64();
                truc5 = reader.ReadUInt32();
                CompressionFormat = reader.ReadUInt32();
            }

            using (MemoryStream stream = new MemoryStream(Children[1].Data))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                //reader.BaseStream.Seek(0x54, SeekOrigin.Current);
                //CompressionFormat = reader.ReadUInt32();
                
            }
        }

        //Patch a chunk data with the current values
        public void Write()
        {
            using (MemoryStream stream = new MemoryStream(Children[0].Data))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(TextureHeaderMAGIC);
                writer.Write(TextureHash);
                writer.Write(TextureFileSize);
                writer.Write(TextureHash2);
                writer.BaseStream.Seek(4, SeekOrigin.Current); // Skip Padding
                writer.Write(truc);
                writer.Write(Width); writer.Write(Height);
                writer.Write(MipLevel);
                writer.Write(flag);
                writer.Write(truc3);
                writer.Write(truc4);
                writer.Write(truc5);
                writer.Write(CompressionFormat);
                writer.BaseStream.Seek(4, SeekOrigin.Current); // Skip Padding
            }
        }

        //Gets the compression format as a string
        public string GetCompression()
        {
            string compressText = "Unknown";

            switch (CompressionFormat)
            {
                case Helper.ETC1_Identifier:
                    compressText = "ETC1";
                    break;
                case Helper.ETC1A_Identifier:
                    compressText = "ETC1A";
                    break;
            }

            return compressText;
        }

        //Creates a bitmap from the ETC1/ETC1A data
        public BitmapSource MakeBitmap()
        {
            bool isAlpha = (CompressionFormat == Helper.ETC1A_Identifier);
            BitmapSource bitmap;
            bitmap = ETC1ImageDecoder.KillzDecoder(Children[1].Data, Width, Height, isAlpha);

            return bitmap;
        }
    }

    public class MaterialChunk : ChunkEntry, IChunkExtension {
        public float Glow;
        public float Red;
        public float Green;
        public float Blue;
        public float Alpha;

        public void Read() {
            using (MemoryStream stream = new MemoryStream(Data))
            using (BinaryReader reader = new BinaryReader(stream)) {
                Glow = BitConverter.ToSingle(reader.ReadBytes(4));

                reader.BaseStream.Seek(12, SeekOrigin.Current);
                Red = BitConverter.ToSingle(reader.ReadBytes(4));
                Green = BitConverter.ToSingle(reader.ReadBytes(4));
                Blue = BitConverter.ToSingle(reader.ReadBytes(4));

                reader.BaseStream.Seek(0x28, SeekOrigin.Current);
                Alpha = BitConverter.ToSingle(reader.ReadBytes(4));
            }
        }

        public void Write() {
            using (MemoryStream stream = new MemoryStream(Data))
            using (BinaryWriter writer = new BinaryWriter(stream)) {
                writer.Write(BitConverter.GetBytes(Glow));

                writer.Seek(12, SeekOrigin.Current);
                writer.Write(BitConverter.GetBytes(Red));
                writer.Write(BitConverter.GetBytes(Green));
                writer.Write(BitConverter.GetBytes(Blue));

                writer.Seek(0x28, SeekOrigin.Current);
                writer.Write(BitConverter.GetBytes(Alpha));
            }
        }
    }

    public class FontChunk : ChunkEntry, IChunkExtension
    {
        // L'objet manipulable par l'éditeur
        public NlgFont FontObject { get; private set; }
        public List<uint> TexturesHash { get; private set; } //Liste des hash dans un FontTexture child chunk

        public void Read()
        {
            // La logique : Le FontChunk (Parent 0x7010) ne contient rien.
            // Il doit lire les données de son enfant FontData (0x7011).

            if (Children == null) return;

            // On cherche l'enfant qui contient le texte
            var dataChild = Array.Find(Children, c => c.Type == (ushort)ChunkType.FontData);

            if (dataChild != null && dataChild.Data != null)
            {
                try
                {
                    FontObject = NlgFont.FromBytes(dataChild.Data);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Erreur parsing Font: " + ex.Message);
                }
            }

            var textureChild = Array.Find(Children, c => c.Type == (ushort)ChunkType.FontTextures);
            if(textureChild != null && textureChild.Data != null)
            {
                TexturesHash = new List<uint>();
                using (MemoryStream stream = new MemoryStream(textureChild.Data))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    int textureCount = (int)(textureChild.Size / 4);
                    for (int i = 0; i < textureCount; i++)
                    {
                        uint texHash = reader.ReadUInt32();
                        TexturesHash.Add(texHash);
                    }
                }
            }
        }

        public void Write()
        {
            // On sérialise l'objet et on l'écrit dans l'enfant
            if (FontObject == null || Children == null) return;

            var dataChild = Array.Find(Children, c => c.Type == (ushort)ChunkType.FontData);

            if (dataChild != null)
            {
                byte[] newData = FontObject.ToBytes();

                // Mise à jour de l'enfant
                dataChild.Data = newData;
                dataChild.Size = (uint)newData.Length;

                // Note : On ne touche pas à l'offset ici, c'est LM2File.Save/Repack qui gère ça.
            }
        }
    }

    // Ajoutez ceci dans Chunk.cs, dans le namespace EvershadeEditor.LM2
    public class ScriptChunk : ChunkEntry, IChunkExtension
    {
        // Structures de données internes pour le Script
        public class Script
        {
            public uint Hash { get; set; }
            public uint StringBufferSize { get; set; }
            public List<Function> Functions { get; set; } = new List<Function>();
        }

        public class Function
        {
            public string Name { get; set; }
            public uint Hash { get; set; }
            public uint CodeStartIndex { get; set; }
            public uint Flag { get; set; }
            public List<Operation> Operations { get; set; } = new List<Operation>();
            public Dictionary<uint, CodeVariable> Variables { get; set; } = new Dictionary<uint, CodeVariable>();
            public string DecompiledCode { get; set; }
        }

        public class Operation
        {
            public ushort RawCode;
            public uint OpCode;
            public uint RegValue;
            public uint RegValueEx;
            public object DataRead;
        }

        public class CodeVariable
        {
            public uint Offset;
            public object Data;
            public string TypeName => Data?.GetType().Name ?? "Unknown";
        }

        // Propriétés du Chunk
        public List<Script> Scripts { get; private set; } = new List<Script>();
        public List<string> Strings { get; private set; } = new List<string>();
        public uint HashType { get; private set; }

        public void Read()
        {
            if (Children == null) return;

            // 1. Récupération des chunks enfants
            var headerChunk = Array.Find(Children, c => c.Type == (ushort)ChunkType.ScriptHeader);
            var funcTableChunks = Children.Where(c => c.Type == (ushort)ChunkType.ScriptFunctionTable).ToList();
            var dataChunk = Array.Find(Children, c => c.Type == (ushort)ChunkType.ScriptData);

            if (headerChunk == null || dataChunk == null) return;

            Scripts.Clear();
            Strings.Clear();

            // 2. Parsing du Header
            using (MemoryStream stream = new MemoryStream(headerChunk.Data))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                int numScripts = headerChunk.Data.Length / 8;
                for (int i = 0; i < numScripts; i++)
                {
                    Scripts.Add(new Script
                    {
                        Hash = reader.ReadUInt32(),
                        StringBufferSize = reader.ReadUInt32()
                    });
                }
            }

            // 3. Parsing des Tables de Fonctions
            for (int i = 0; i < Scripts.Count; i++)
            {
                if (i >= funcTableChunks.Count) break;

                using (MemoryStream stream = new MemoryStream(funcTableChunks[i].Data))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    int numFuncs = funcTableChunks[i].Data.Length / 12;
                    for (int j = 0; j < numFuncs; j++)
                    {
                        var func = new Function
                        {
                            Hash = reader.ReadUInt32(),
                            CodeStartIndex = reader.ReadUInt32(),
                            Flag = reader.ReadUInt32()
                        };
                        func.Name = Helper.GetHashName(func.Hash);
                        if (string.IsNullOrWhiteSpace(func.Name)) func.Name = $"Func_{func.Hash:X8}";
                        Scripts[i].Functions.Add(func);
                    }
                }
            }

            // 4. Parsing Data & Code
            ParseScriptData(dataChunk.Data);
        }

        public void Write()
        {
            // TODO: Implémenter la reconstruction du ScriptData (Repacking)
            // C'est complexe car il faut recalculer tous les offsets si la taille change.
            // Pour l'instant, on peut imaginer mettre à jour uniquement les valeurs des variables in-place.
        }

        private void ParseScriptData(byte[] data)
        {
            using (MemoryStream stream = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                HashType = reader.ReadUInt32();
                uint codeSize = reader.ReadUInt32();
                uint dataSize = reader.ReadUInt32();
                ushort stringTableSize = reader.ReadUInt16();
                ushort unk = reader.ReadUInt16();

                long dataStartPos = 16; // Header size
                long codeStartPos = dataStartPos + dataSize;
                long stringStartPos = codeStartPos + codeSize;

                // A. Charger les Strings
                if (stringStartPos < stream.Length)
                {
                    reader.BaseStream.Seek(stringStartPos, SeekOrigin.Begin);
                    while (reader.BaseStream.Position < stream.Length && reader.BaseStream.Position < stringStartPos + stringTableSize)
                    {
                        try { Strings.Add(reader.ReadZeroTerminatedString()); } catch { break; }
                    }
                }

                // B. Analyser les fonctions
                foreach (var script in Scripts)
                {
                    foreach (var func in script.Functions)
                    {
                        ParseFunction(reader, func, codeStartPos, dataStartPos, dataSize, stringStartPos);
                    }
                }
            }
        }

        private void ParseFunction(BinaryReader reader, Function func, long codeBase, long dataBase, uint maxDataSize, long strBase)
        {
            reader.BaseStream.Seek(codeBase + func.CodeStartIndex * 2, SeekOrigin.Begin);

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"// Function: {func.Name} (Flag: {func.Flag:X})");
            sb.AppendLine("{");

            List<dynamic> stack = new List<dynamic>();
            uint dataPointer = 0;

            while (true)
            {
                if (reader.BaseStream.Position >= reader.BaseStream.Length) break;

                ushort raw = reader.ReadUInt16();
                uint val = (uint)(raw & 0xffff03ff);
                uint opCode = (uint)(raw >> 10);

                var op = new Operation { RawCode = raw, OpCode = opCode, RegValue = val };
                func.Operations.Add(op);

                // -- Logique simplifiée de décompilation --
                switch (opCode)
                {
                    case 0: // READ
                        if (val < maxDataSize / 4)
                        {
                            long oldPos = reader.BaseStream.Position;
                            reader.BaseStream.Seek(dataBase + val * 4, SeekOrigin.Begin);
                            uint rawVal = reader.ReadUInt32();
                            float fVal = BitConverter.ToSingle(BitConverter.GetBytes(rawVal), 0);
                            reader.BaseStream.Seek(oldPos, SeekOrigin.Begin);

                            // Heuristique simple
                            object readVal = (fVal > 100000 || fVal < -100000 || Helper.Hashes.ContainsKey(rawVal)) ? rawVal : fVal;
                            op.DataRead = readVal;
                            stack.Add(readVal);
                        }
                        break;
                    case 2: // STRING
                        long sPos = reader.BaseStream.Position;
                        if (strBase + val < reader.BaseStream.Length)
                        {
                            reader.BaseStream.Seek(strBase + val, SeekOrigin.Begin);
                            string s = reader.ReadZeroTerminatedString();
                            stack.Add($"\"{s}\"");
                            reader.BaseStream.Seek(sPos, SeekOrigin.Begin);
                        }
                        break;
                    case 4: // SET
                        stack.Add(val);
                        break;
                    case 0xE: // PTR
                        dataPointer = val * 4 + 4;
                        break;
                    case 0x15: // SHIFT_PTR
                        dataPointer += val;
                        break;
                    case 0x10: // MOV_8 (Store Variable)
                        if (stack.Count > 0)
                        {
                            var v = stack[stack.Count - 1];
                            if (!func.Variables.ContainsKey(dataPointer))
                                func.Variables[dataPointer] = new CodeVariable { Offset = dataPointer, Data = v };
                            sb.AppendLine($"    var_{dataPointer:X} = {v};");
                            stack.Clear();
                        }
                        break;
                    case 0xC: // END
                        sb.AppendLine("}");
                        func.DecompiledCode = sb.ToString();
                        return;
                    case 7: // CMD
                        sb.AppendLine($"    CMD_{val}({string.Join(", ", stack)});");
                        stack.Clear();
                        break;
                    default:
                        // Extensions
                        if (new[] { 1, 3, 5, 6, 8, 0x16 }.Contains((int)opCode)) reader.ReadUInt16();
                        break;
                }
            }
        }
    }
}
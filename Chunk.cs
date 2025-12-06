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
        uint TextureHash;
        uint TextureFileSize;
        uint TextureHash2;
        uint Padding;
        uint truc;
        ushort Width;
        ushort Height;
        ushort truc2;
        byte MipLevel;
        byte flag;
        long truc3;
        long truc4;
        uint truc5;
        uint CompressionFormat;

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

            switch (CompressionFormat >> 24)
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
            bool isAlpha = (CompressionFormat >> 24 == Helper.ETC1A_Identifier);
            BitmapSource bitmap;
            bitmap = ETC1ImageDecoder.KillzDecoder(Children[1].Data, Width, Height, false);

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
}
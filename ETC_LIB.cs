using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace ETC1Decoder
{
    public class ETC1ImageDecoder
    {
        // Tables de modificateurs ETC1
        protected static readonly int[][] ModifierTable = new int[][]
        {
            new int[] { 2, 8, -2, -8 },
            new int[] { 5, 17, -5, -17 },
            new int[] { 9, 29, -9, -29 },
            new int[] { 13, 42, -13, -42 },
            new int[] { 18, 60, -18, -60 },
            new int[] { 24, 80, -24, -80 },
            new int[] { 33, 106, -33, -106 },
            new int[] { 47, 183, -47, -183 }
        };

        public static BitmapSource DecodeETC1File(string filePath, int width, int height)
        {
            byte[] etc1Data = File.ReadAllBytes(filePath);
            return DecodeETC1(etc1Data, width, height);
        }

        static byte[] Downscale2x(byte[] src, int width, int height)
        {
            int newWidth = width / 2;
            int newHeight = height / 2;
            byte[] dst = new byte[newWidth * newHeight * 4];

            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    int dstIndex = (y * newWidth + x) * 4;

                    int srcX = x * 2;
                    int srcY = y * 2;

                    // Moyenne des 4 pixels
                    for (int c = 0; c < 4; c++)
                    {
                        int sum = 0;
                        sum += src[((srcY * width + srcX) * 4) + c];
                        sum += src[((srcY * width + (srcX + 1)) * 4) + c];
                        sum += src[(((srcY + 1) * width + srcX) * 4) + c];
                        sum += src[(((srcY + 1) * width + (srcX + 1)) * 4) + c];

                        dst[dstIndex + c] = (byte)(sum / 4);
                    }
                }
            }

            return dst;
        }

        public static BitmapSource DecodeETC1(byte[] etc1Data, int width, int height)
        {
            // Calcul du nombre de blocs (chaque bloc = 4x4 pixels = 8 bytes)
            int blocksWide = (width + 3) / 4;
            int blocksHigh = (height + 3) / 4;

            // Buffer pour l'image décodée (BGRA32)
            byte[] pixels = new byte[width * height * 4];

            int blockIndex = 0;

            for (int by = 0; by < blocksHigh; by++)
            {
                for (int bx = 0; bx < blocksWide; bx++)
                {
                    if (blockIndex * 8 + 7 >= etc1Data.Length)
                        break;

                    DecodeBlock(etc1Data, blockIndex * 8, pixels, bx * 4, by * 4, width, height);
                    blockIndex++;
                }
            }

            // Créer un BitmapSource à partir des pixels décodés
            return BitmapSource.Create(
                width, height,
                96, 96,
                PixelFormats.Bgra32,
                null,
                pixels,
                width * 4);
        }

        private static void DecodeBlock(byte[] src, int srcOffset, byte[] dst, int x, int y, int width, int height)
        {
            // Lecture du bloc de 8 bytes
            ulong block = 0;
            for (int i = 0; i < 8; i++)
            {
                block |= (ulong)src[srcOffset + i] << (i * 8);
            }

            // Extraction des données du bloc
            bool diffBit = ((block >> 33) & 1) == 1;
            bool flipBit = ((block >> 32) & 1) == 1;

            int r1, g1, b1, r2, g2, b2;

            if (diffBit)
            {
                // Mode différentiel
                int r = (int)((block >> 59) & 0x1F);
                int g = (int)((block >> 51) & 0x1F);
                int b = (int)((block >> 43) & 0x1F);

                r1 = (r << 3) | (r >> 2);
                g1 = (g << 3) | (g >> 2);
                b1 = (b << 3) | (b >> 2);

                int rd = (int)((block >> 56) & 0x07);
                int gd = (int)((block >> 48) & 0x07);
                int bd = (int)((block >> 40) & 0x07);

                // Extension de signe
                if ((rd & 4) != 0) rd |= unchecked((int)0xFFFFFFF8);
                if ((gd & 4) != 0) gd |= unchecked((int)0xFFFFFFF8);
                if ((bd & 4) != 0) bd |= unchecked((int)0xFFFFFFF8);

                int r2_5bit = r + rd;
                int g2_5bit = g + gd;
                int b2_5bit = b + bd;

                r2 = (r2_5bit << 3) | (r2_5bit >> 2);
                g2 = (g2_5bit << 3) | (g2_5bit >> 2);
                b2 = (b2_5bit << 3) | (b2_5bit >> 2);
            }
            else
            {
                // Mode individuel
                r1 = (int)((block >> 60) & 0x0F);
                r1 = (r1 << 4) | r1;
                g1 = (int)((block >> 52) & 0x0F);
                g1 = (g1 << 4) | g1;
                b1 = (int)((block >> 44) & 0x0F);
                b1 = (b1 << 4) | b1;

                r2 = (int)((block >> 56) & 0x0F);
                r2 = (r2 << 4) | r2;
                g2 = (int)((block >> 48) & 0x0F);
                g2 = (g2 << 4) | g2;
                b2 = (int)((block >> 40) & 0x0F);
                b2 = (b2 << 4) | b2;
            }

            // Tables et indices de pixels
            int table1 = (int)((block >> 37) & 0x07);
            int table2 = (int)((block >> 34) & 0x07);
            uint pixelIndices = (uint)(block & 0xFFFFFFFF);

            // Décodage des 16 pixels du bloc
            for (int py = 0; py < 4; py++)
            {
                for (int px = 0; px < 4; px++)
                {
                    int pixelX = x + px;
                    int pixelY = y + py;

                    if (pixelX >= width || pixelY >= height)
                        continue;

                    int pixelIndex = py * 4 + px;
                    int bitIndex = pixelIndex;

                    int msb = (int)((pixelIndices >> (bitIndex + 16)) & 1);
                    int lsb = (int)((pixelIndices >> bitIndex) & 1);
                    int modifierIndex = (msb << 1) | lsb;

                    int r, g, b;
                    int modifier;

                    // Détermination de la couleur de base selon flip bit
                    bool useSubblock2 = flipBit ? (py >= 2) : (px >= 2);

                    if (useSubblock2)
                    {
                        r = r2;
                        g = g2;
                        b = b2;
                        modifier = ModifierTable[table2][modifierIndex];
                    }
                    else
                    {
                        r = r1;
                        g = g1;
                        b = b1;
                        modifier = ModifierTable[table1][modifierIndex];
                    }

                    // Application du modificateur avec clamping
                    r = Clamp(r + modifier, 0, 255);
                    g = Clamp(g + modifier, 0, 255);
                    b = Clamp(b + modifier, 0, 255);

                    // Écriture du pixel (BGRA32)
                    int dstIndex = (pixelY * width + pixelX) * 4;
                    dst[dstIndex + 0] = (byte)b; // B
                    dst[dstIndex + 1] = (byte)g; // G
                    dst[dstIndex + 2] = (byte)r; // R
                    dst[dstIndex + 3] = 255;     // A
                }
            }
        }

        protected static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static BitmapSource KillzDecoder(string filePath, int Width, int Height, bool Alpha)
        {
            byte[] etc1Data = File.ReadAllBytes(filePath);
            byte[] DecompData = Toolbox.Core.ETC1.ETC1Decompress(etc1Data,Width,Height,Alpha);

            byte[] bgraData = new byte[Width * Height * 4];

            for (int i = 0; i < Width * Height; i++)
            {
                int srcIndex = i * 4;
                byte r = DecompData[srcIndex + 0];
                byte g = DecompData[srcIndex + 1];
                byte b = DecompData[srcIndex + 2];
                byte a = DecompData[srcIndex + 3];

                int dstIndex = i * 4;
                bgraData[dstIndex + 0] = b; // B
                bgraData[dstIndex + 1] = g; // G
                bgraData[dstIndex + 2] = r; // R
                bgraData[dstIndex + 3] = a; // A
            }



            return BitmapSource.Create(
           Width, Height,
           96, 96,
           PixelFormats.Bgra32,
           null,
           bgraData,
           Width * 4);
        }

        public static BitmapSource KillzDecoder(byte[] etc1Data, int Width, int Height, bool Alpha)
        {
            byte[] DecompData = Toolbox.Core.ETC1.ETC1Decompress(etc1Data, Width, Height, Alpha);

            byte[] bgraData = new byte[Width * Height * 4];

            for (int i = 0; i < Width * Height; i++)
            {
                int srcIndex = i * 4;
                byte r = DecompData[srcIndex + 0];
                byte g = DecompData[srcIndex + 1];
                byte b = DecompData[srcIndex + 2];
                byte a = DecompData[srcIndex + 3];

                int dstIndex = i * 4;
                bgraData[dstIndex + 0] = b; // B
                bgraData[dstIndex + 1] = g; // G
                bgraData[dstIndex + 2] = r; // R
                bgraData[dstIndex + 3] = a; // A
            }



            return BitmapSource.Create(
           Width, Height,
           96, 96,
           PixelFormats.Bgra32,
           null,
           bgraData,
           Width * 4);
        }
    }

    public class ETC1AImageDecoder : ETC1ImageDecoder
    {
        public static BitmapSource DecodeETC1AFile(string filePath, int width, int height)
        {
            byte[] etc1aData = File.ReadAllBytes(filePath);
            return DecodeETC1A(etc1aData, width, height);
        }

        public static BitmapSource DecodeETC1A(byte[] etc1aData, int width, int height)
        {
            // ETC1A = RGB en ETC1 + canal Alpha en ETC1
            // Chaque bloc 4x4 = 8 bytes RGB + 8 bytes Alpha = 16 bytes
            int blocksWide = (width + 3) / 4;
            int blocksHigh = (height + 3) / 4;
            int totalBlocks = blocksWide * blocksHigh;

            // Buffer pour l'image décodée (BGRA32)
            byte[] pixels = new byte[width * height * 4];

            // Décodage RGB (première moitié des données)
            int blockIndex = 0;
            for (int by = 0; by < blocksHigh; by++)
            {
                for (int bx = 0; bx < blocksWide; bx++)
                {
                    if (blockIndex * 16 + 7 >= etc1aData.Length)
                        break;

                    // Bloc RGB (8 premiers bytes du bloc de 16)
                    DecodeBlock(etc1aData, blockIndex * 16, pixels, bx * 4, by * 4, width, height);
                    blockIndex++;
                }
            }

            // Décodage Alpha (deuxième moitié, stockée comme une texture ETC1 en grayscale)
            blockIndex = 0;
            for (int by = 0; by < blocksHigh; by++)
            {
                for (int bx = 0; bx < blocksWide; bx++)
                {
                    if (blockIndex * 16 + 15 >= etc1aData.Length)
                        break;

                    // Bloc Alpha (8 derniers bytes du bloc de 16)
                    DecodeAlphaBlock(etc1aData, blockIndex * 16 + 8, pixels, bx * 4, by * 4, width, height);
                    blockIndex++;
                }
            }

            // Créer un BitmapSource à partir des pixels décodés
            return BitmapSource.Create(
                width, height,
                96, 96,
                PixelFormats.Bgra32,
                null,
                pixels,
                width * 4);
        }

        private static void DecodeAlphaBlock(byte[] src, int srcOffset, byte[] dst, int x, int y, int width, int height)
        {
            // Lecture du bloc de 8 bytes pour l'alpha
            ulong block = 0;
            for (int i = 0; i < 8; i++)
            {
                block |= (ulong)src[srcOffset + i] << (i * 8);
            }

            // Extraction des données du bloc (même format que ETC1)
            bool diffBit = ((block >> 33) & 1) == 1;
            bool flipBit = ((block >> 32) & 1) == 1;

            int r1, g1, b1, r2, g2, b2;

            if (diffBit)
            {
                // Mode différentiel
                int r = (int)((block >> 59) & 0x1F);
                int g = (int)((block >> 51) & 0x1F);
                int b = (int)((block >> 43) & 0x1F);

                r1 = (r << 3) | (r >> 2);
                g1 = (g << 3) | (g >> 2);
                b1 = (b << 3) | (b >> 2);

                int rd = (int)((block >> 56) & 0x07);
                int gd = (int)((block >> 48) & 0x07);
                int bd = (int)((block >> 40) & 0x07);

                if ((rd & 4) != 0) rd |= unchecked((int)0xFFFFFFF8);
                if ((gd & 4) != 0) gd |= unchecked((int)0xFFFFFFF8);
                if ((bd & 4) != 0) bd |= unchecked((int)0xFFFFFFF8);

                int r2_5bit = r + rd;
                int g2_5bit = g + gd;
                int b2_5bit = b + bd;

                r2 = (r2_5bit << 3) | (r2_5bit >> 2);
                g2 = (g2_5bit << 3) | (g2_5bit >> 2);
                b2 = (b2_5bit << 3) | (b2_5bit >> 2);
            }
            else
            {
                // Mode individuel
                r1 = (int)((block >> 60) & 0x0F);
                r1 = (r1 << 4) | r1;
                g1 = (int)((block >> 52) & 0x0F);
                g1 = (g1 << 4) | g1;
                b1 = (int)((block >> 44) & 0x0F);
                b1 = (b1 << 4) | b1;

                r2 = (int)((block >> 56) & 0x0F);
                r2 = (r2 << 4) | r2;
                g2 = (int)((block >> 48) & 0x0F);
                g2 = (g2 << 4) | g2;
                b2 = (int)((block >> 40) & 0x0F);
                b2 = (b2 << 4) | b2;
            }

            int table1 = (int)((block >> 37) & 0x07);
            int table2 = (int)((block >> 34) & 0x07);
            uint pixelIndices = (uint)(block & 0xFFFFFFFF);

            // Décodage des 16 pixels du bloc (valeur alpha seulement)
            for (int py = 0; py < 4; py++)
            {
                for (int px = 0; px < 4; px++)
                {
                    int pixelX = x + px;
                    int pixelY = y + py;

                    if (pixelX >= width || pixelY >= height)
                        continue;

                    int pixelIndex = py * 4 + px;
                    int bitIndex = pixelIndex;

                    int msb = (int)((pixelIndices >> (bitIndex + 16)) & 1);
                    int lsb = (int)((pixelIndices >> bitIndex) & 1);
                    int modifierIndex = (msb << 1) | lsb;

                    int gray;
                    int modifier;

                    bool useSubblock2 = flipBit ? (py >= 2) : (px >= 2);

                    if (useSubblock2)
                    {
                        // On utilise R comme valeur de gris
                        gray = r2;
                        modifier = ModifierTable[table2][modifierIndex];
                    }
                    else
                    {
                        gray = r1;
                        modifier = ModifierTable[table1][modifierIndex];
                    }

                    // Application du modificateur
                    int alpha = Clamp(gray + modifier, 0, 255);

                    // Écriture de la valeur alpha dans le pixel existant
                    int dstIndex = (pixelY * width + pixelX) * 4;
                    dst[dstIndex + 3] = (byte)alpha; // A
                }
            }
        }

        private static void DecodeBlock(byte[] src, int srcOffset, byte[] dst, int x, int y, int width, int height)
        {
            // Lecture du bloc de 8 bytes
            ulong block = 0;
            for (int i = 0; i < 8; i++)
            {
                block |= (ulong)src[srcOffset + i] << (i * 8);
            }

            // Extraction des données du bloc
            bool diffBit = ((block >> 33) & 1) == 1;
            bool flipBit = ((block >> 32) & 1) == 1;

            int r1, g1, b1, r2, g2, b2;

            if (diffBit)
            {
                // Mode différentiel
                int r = (int)((block >> 59) & 0x1F);
                int g = (int)((block >> 51) & 0x1F);
                int b = (int)((block >> 43) & 0x1F);

                r1 = (r << 3) | (r >> 2);
                g1 = (g << 3) | (g >> 2);
                b1 = (b << 3) | (b >> 2);

                int rd = (int)((block >> 56) & 0x07);
                int gd = (int)((block >> 48) & 0x07);
                int bd = (int)((block >> 40) & 0x07);

                // Extension de signe
                if ((rd & 4) != 0) rd |= unchecked((int)0xFFFFFFF8);
                if ((gd & 4) != 0) gd |= unchecked((int)0xFFFFFFF8);
                if ((bd & 4) != 0) bd |= unchecked((int)0xFFFFFFF8);

                int r2_5bit = r + rd;
                int g2_5bit = g + gd;
                int b2_5bit = b + bd;

                r2 = (r2_5bit << 3) | (r2_5bit >> 2);
                g2 = (g2_5bit << 3) | (g2_5bit >> 2);
                b2 = (b2_5bit << 3) | (b2_5bit >> 2);
            }
            else
            {
                // Mode individuel
                r1 = (int)((block >> 60) & 0x0F);
                r1 = (r1 << 4) | r1;
                g1 = (int)((block >> 52) & 0x0F);
                g1 = (g1 << 4) | g1;
                b1 = (int)((block >> 44) & 0x0F);
                b1 = (b1 << 4) | b1;

                r2 = (int)((block >> 56) & 0x0F);
                r2 = (r2 << 4) | r2;
                g2 = (int)((block >> 48) & 0x0F);
                g2 = (g2 << 4) | g2;
                b2 = (int)((block >> 40) & 0x0F);
                b2 = (b2 << 4) | b2;
            }

            // Tables et indices de pixels
            int table1 = (int)((block >> 37) & 0x07);
            int table2 = (int)((block >> 34) & 0x07);
            uint pixelIndices = (uint)(block & 0xFFFFFFFF);

            // Décodage des 16 pixels du bloc
            for (int py = 0; py < 4; py++)
            {
                for (int px = 0; px < 4; px++)
                {
                    int pixelX = x + px;
                    int pixelY = y + py;

                    if (pixelX >= width || pixelY >= height)
                        continue;

                    int pixelIndex = py * 4 + px;
                    int bitIndex = pixelIndex;

                    int msb = (int)((pixelIndices >> (bitIndex + 16)) & 1);
                    int lsb = (int)((pixelIndices >> bitIndex) & 1);
                    int modifierIndex = (msb << 1) | lsb;

                    int r, g, b;
                    int modifier;

                    // Détermination de la couleur de base selon flip bit
                    bool useSubblock2 = flipBit ? (py >= 2) : (px >= 2);

                    if (useSubblock2)
                    {
                        r = r2;
                        g = g2;
                        b = b2;
                        modifier = ModifierTable[table2][modifierIndex];
                    }
                    else
                    {
                        r = r1;
                        g = g1;
                        b = b1;
                        modifier = ModifierTable[table1][modifierIndex];
                    }

                    // Application du modificateur avec clamping
                    r = Clamp(r + modifier, 0, 255);
                    g = Clamp(g + modifier, 0, 255);
                    b = Clamp(b + modifier, 0, 255);

                    // Écriture du pixel (BGRA32)
                    int dstIndex = (pixelY * width + pixelX) * 4;
                    dst[dstIndex + 0] = (byte)b; // B
                    dst[dstIndex + 1] = (byte)g; // G
                    dst[dstIndex + 2] = (byte)r; // R
                    dst[dstIndex + 3] = 255;     // A
                }
            }
        }

    }
}
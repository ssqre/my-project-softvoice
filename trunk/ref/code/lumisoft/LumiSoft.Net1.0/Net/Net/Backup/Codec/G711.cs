using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.Codec
{
    /// <summary>
    /// This class implements G.711 codec.
    /// </summary>
    public class G711
    {        
        #region byte[] ALawCompressTable

        private static readonly byte[] ALawCompressTable = new byte[]{ 
            1,1,2,2,3,3,3,3, 
            4,4,4,4,4,4,4,4, 
            5,5,5,5,5,5,5,5, 
            5,5,5,5,5,5,5,5, 
            6,6,6,6,6,6,6,6, 
            6,6,6,6,6,6,6,6, 
            6,6,6,6,6,6,6,6, 
            6,6,6,6,6,6,6,6, 
            7,7,7,7,7,7,7,7, 
            7,7,7,7,7,7,7,7, 
            7,7,7,7,7,7,7,7, 
            7,7,7,7,7,7,7,7, 
            7,7,7,7,7,7,7,7, 
            7,7,7,7,7,7,7,7, 
            7,7,7,7,7,7,7,7, 
            7,7,7,7,7,7,7,7 
        };

        #endregion

        #region short[] ALawDecompressTable

        private static readonly short[] ALawDecompressTable = new short[]{ 
            -5504, -5248, -6016, -5760, -4480, -4224, -4992, -4736, 
            -7552, -7296, -8064, -7808, -6528, -6272, -7040, -6784, 
            -2752, -2624, -3008, -2880, -2240, -2112, -2496, -2368, 
            -3776, -3648, -4032, -3904, -3264, -3136, -3520, -3392, 
            -22016,-20992,-24064,-23040,-17920,-16896,-19968,-18944, 
            -30208,-29184,-32256,-31232,-26112,-25088,-28160,-27136, 
            -11008,-10496,-12032,-11520,-8960, -8448, -9984, -9472, 
            -15104,-14592,-16128,-15616,-13056,-12544,-14080,-13568, 
            -344,  -328,  -376,  -360,  -280,  -264,  -312,  -296, 
            -472,  -456,  -504,  -488,  -408,  -392,  -440,  -424, 
            -88,   -72,   -120,  -104,  -24,   -8,    -56,   -40, 
            -216,  -200,  -248,  -232,  -152,  -136,  -184,  -168, 
            -1376, -1312, -1504, -1440, -1120, -1056, -1248, -1184, 
            -1888, -1824, -2016, -1952, -1632, -1568, -1760, -1696, 
            -688,  -656,  -752,  -720,  -560,  -528,  -624,  -592, 
            -944,  -912,  -1008, -976,  -816,  -784,  -880,  -848, 
            5504,  5248,  6016,  5760,  4480,  4224,  4992,  4736, 
            7552,  7296,  8064,  7808,  6528,  6272,  7040,  6784, 
            2752,  2624,  3008,  2880,  2240,  2112,  2496,  2368, 
            3776,  3648,  4032,  3904,  3264,  3136,  3520,  3392, 
            22016, 20992, 24064, 23040, 17920, 16896, 19968, 18944, 
            30208, 29184, 32256, 31232, 26112, 25088, 28160, 27136, 
            11008, 10496, 12032, 11520, 8960,  8448,  9984,  9472, 
            15104, 14592, 16128, 15616, 13056, 12544, 14080, 13568, 
            344,   328,   376,   360,   280,   264,   312,   296, 
            472,   456,   504,   488,   408,   392,   440,   424, 
            88,    72,   120,   104,    24,     8,    56,    40, 
            216,   200,   248,   232,   152,   136,   184,   168, 
            1376,  1312,  1504,  1440,  1120,  1056,  1248,  1184, 
            1888,  1824,  2016,  1952,  1632,  1568,  1760,  1696, 
            688,   656,   752,   720,   560,   528,   624,   592, 
            944,   912,  1008,   976,   816,   784,   880,   848 
        };

        #endregion

        #region byte[] MuLawCompressTable

        private static readonly byte[] MuLawCompressTable = new byte[]{ 
            0,0,1,1,2,2,2,2,3,3,3,3,3,3,3,3, 
            4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4, 
            5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5, 
            5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5, 
            6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6, 
            6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6, 
            6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6, 
            6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6, 
            7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7, 
            7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7, 
            7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7, 
            7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7, 
            7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7, 
            7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7, 
            7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7, 
            7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7 
        };

        #endregion

        #region short[] MuLawDecompressTable

        private static readonly short[] MuLawDecompressTable = new short[]{ 
            -32124,-31100,-30076,-29052,-28028,-27004,-25980,-24956, 
            -23932,-22908,-21884,-20860,-19836,-18812,-17788,-16764, 
            -15996,-15484,-14972,-14460,-13948,-13436,-12924,-12412, 
            -11900,-11388,-10876,-10364, -9852, -9340, -8828, -8316, 
            -7932, -7676, -7420, -7164, -6908, -6652, -6396, -6140, 
            -5884, -5628, -5372, -5116, -4860, -4604, -4348, -4092, 
            -3900, -3772, -3644, -3516, -3388, -3260, -3132, -3004, 
            -2876, -2748, -2620, -2492, -2364, -2236, -2108, -1980, 
            -1884, -1820, -1756, -1692, -1628, -1564, -1500, -1436, 
            -1372, -1308, -1244, -1180, -1116, -1052,  -988,  -924, 
            -876,  -844,  -812,  -780,  -748,  -716,  -684,  -652, 
            -620,  -588,  -556,  -524,  -492,  -460,  -428,  -396, 
            -372,  -356,  -340,  -324,  -308,  -292,  -276,  -260, 
            -244,  -228,  -212,  -196,  -180,  -164,  -148,  -132, 
            -120,  -112,  -104,   -96,   -88,   -80,   -72,   -64, 
            -56,   -48,   -40,   -32,   -24,   -16,    -8,     0, 
            32124, 31100, 30076, 29052, 28028, 27004, 25980, 24956, 
            23932, 22908, 21884, 20860, 19836, 18812, 17788, 16764, 
            15996, 15484, 14972, 14460, 13948, 13436, 12924, 12412, 
            11900, 11388, 10876, 10364,  9852,  9340,  8828,  8316, 
            7932,  7676,  7420,  7164,  6908,  6652,  6396,  6140, 
            5884,  5628,  5372,  5116,  4860,  4604,  4348,  4092, 
            3900,  3772,  3644,  3516,  3388,  3260,  3132,  3004, 
            2876,  2748,  2620,  2492,  2364,  2236,  2108,  1980, 
            1884,  1820,  1756,  1692,  1628,  1564,  1500,  1436, 
            1372,  1308,  1244,  1180,  1116,  1052,   988,   924, 
            876,   844,   812,   780,   748,   716,   684,   652, 
            620,   588,   556,   524,   492,   460,   428,   396, 
            372,   356,   340,   324,   308,   292,   276,   260, 
            244,   228,   212,   196,   180,   164,   148,   132, 
            120,   112,   104,    96,    88,    80,    72,    64, 
            56,    48,    40,    32,    24,    16,     8,     0 
        };

        #endregion
        

        /// <summary>
        /// Default constructor.
        /// </summary>
        public G711()
        {
        }

        #region static method Encode_aLaw

        /// <summary>
        /// Encodes linear 16-bit linear PCM to 8-bit a-law.
        /// </summary>
        /// <param name="buffer">Data which to convert. Data must be in Little-Endian format.</param>
        /// <param name="offset">Offset in the buffer.</param>
        /// <param name="count">Number of bytes to encode.</param>
        /// <returns>Returns encoded data.</returns>
        /// <exception cref="ArgumentNullException">Is raised when when <b>buffer</b> is null.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
        public static byte[] Encode_aLaw(byte[] buffer,int offset,int count)
        {
            if(buffer == null){
                throw new ArgumentNullException("buffer");
            }
            if(offset < 0 || offset > buffer.Length){
                throw new ArgumentException("Argument offset is out of range.");
            }
            if(count < 1 || (count + offset) > buffer.Length){
                throw new ArgumentException("Argument offset is out of range.");
            }
            if((buffer.Length % 2) != 0){
                throw new ArgumentException("Invalid buufer value, it doesn't contain 16-bit boundaries.");
            }

            int    offsetInRetVal = 0;
            byte[] retVal         = new byte[count / 2];
            while(offsetInRetVal < retVal.Length){
                // Little-Endian - lower byte,higer byte.
                short pcm = (short)(buffer[offset + 1] << 8 | buffer[offset]);
                offset += 2;
                
                retVal[offsetInRetVal++] = LinearToALawSample(pcm);
            }

            return retVal;
        }

        #endregion

        #region static method Decode_aLaw

        /// <summary>
        /// Decodes 8-bit a-law to 16-bit linear 16-bit PCM.
        /// </summary>
        /// <param name="buffer">Data to decode. Data must be in Little-Endian format.</param>
        /// <param name="offset">Offset in the buffer.</param>
        /// <param name="count">Number of bytes to decode.</param>
        /// <returns>Return decoded data.</returns>
        /// <exception cref="ArgumentNullException">Is raised when when <b>buffer</b> is null.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
        public static byte[] Decode_aLaw(byte[] buffer,int offset,int count)
        {
            if(buffer == null){
                throw new ArgumentNullException("buffer");
            }
            if(offset < 0 || offset > buffer.Length){
                throw new ArgumentException("Argument offset is out of range.");
            }
            if(count < 1 || (count + offset) > buffer.Length){
                throw new ArgumentException("Argument offset is out of range.");
            }

            int    offsetInRetVal = 0;
            byte[] retVal         = new byte[count * 2];
            for(int i=offset;i<buffer.Length;i++){
                short pcm = ALawDecompressTable[buffer[i]];                
                retVal[offsetInRetVal++] = (byte)(pcm      & 0xFF);
                retVal[offsetInRetVal++] = (byte)(pcm >> 8 & 0xFF);
            }

            return retVal;
        }

        #endregion

        #region static method Encode_uLaw

        /// <summary>
        /// Encodes linear 16-bit linear PCM to 8-bit u-law.
        /// </summary>
        /// <param name="buffer">Data which to convert. Data must be in Little-Endian format.</param>
        /// <param name="offset">Offset in the buffer.</param>
        /// <param name="count">Number of bytes to encode.</param>
        /// <returns>Returns encoded data.</returns>
        public static byte[] Encode_uLaw(byte[] buffer,int offset,int count)
        {
            if(buffer == null){
                throw new ArgumentNullException("buffer");
            }
            if(offset < 0 || offset > buffer.Length){
                throw new ArgumentException("Argument offset is out of range.");
            }
            if(count < 1 || (count + offset) > buffer.Length){
                throw new ArgumentException("Argument offset is out of range.");
            }
            if((buffer.Length % 2) != 0){
                throw new ArgumentException("Invalid buufer value, it doesn't contain 16-bit boundaries.");
            }

            int    offsetInRetVal = 0;
            byte[] retVal         = new byte[count / 2];
            while(offsetInRetVal < retVal.Length){
                // Little-Endian - lower byte,higer byte.
                short pcm = (short)(buffer[offset + 1] << 8 | buffer[offset]);
                offset += 2;
                
                retVal[offsetInRetVal++] = LinearToMuLawSample(pcm);
            }

            return retVal;
        }

        #endregion

        #region static method Decode_uLaw

        /// <summary>
        /// Decodes 8-bit u-law to 16-bit linear 16-bit PCM.
        /// </summary>
        /// <param name="buffer">Data to decode. Data must be in Little-Endian format.</param>
        /// <param name="offset">Offset in the buffer.</param>
        /// <param name="count">Number of bytes to decode.</param>
        /// <returns>Return decoded data.</returns>
        /// <exception cref="ArgumentNullException">Is raised when when <b>buffer</b> is null.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
        public static byte[] Decode_uLaw(byte[] buffer,int offset,int count)
        {
            if(buffer == null){
                throw new ArgumentNullException("buffer");
            }
            if(offset < 0 || offset > buffer.Length){
                throw new ArgumentException("Argument offset is out of range.");
            }
            if(count < 1 || (count + offset) > buffer.Length){
                throw new ArgumentException("Argument offset is out of range.");
            }

            int    offsetInRetVal = 0;
            byte[] retVal         = new byte[count * 2];
            for(int i=offset;i<buffer.Length;i++){
                short pcm = MuLawDecompressTable[buffer[i]];                
                retVal[offsetInRetVal++] = (byte)(pcm      & 0xFF);
                retVal[offsetInRetVal++] = (byte)(pcm >> 8 & 0xFF);
            }

            return retVal;
        }

        #endregion


        #region static method LinearToALawSample

        private static byte LinearToALawSample(short sample) 
        { 
            int  sign           = 0;
            int  exponent       = 0; 
            int  mantissa       = 0; 
            byte compressedByte = 0;

            sign = ((~sample) >> 8) & 0x80; 
            if(sign == 0){ 
                sample = (short)-sample; 
            }
            if(sample > 32635){
                sample = 32635;
            }
            if(sample >= 256){ 
                exponent = (int)ALawCompressTable[(sample >> 8) & 0x7F]; 
                mantissa = (sample >> (exponent + 3) ) & 0x0F; 
                compressedByte = (byte)((exponent << 4) | mantissa); 
            } 
            else{ 
                compressedByte = (byte)(sample >> 4); 
            } 

            compressedByte ^= (byte)(sign ^ 0x55); 

            return compressedByte;
        }

        #endregion

        #region static method LinearToMuLawSample

        private static byte LinearToMuLawSample(short sample) 
        { 
            int cBias = 0x84; 
            int cClip = 32635;

            int sign = (sample >> 8) & 0x80; 
            if(sign != 0){ 
                sample = (short)-sample; 
            }
            if(sample > cClip){
                sample = (short)cClip;
            }
            sample = (short)(sample + cBias); 
            int exponent = (int)MuLawCompressTable[(sample>>7) & 0xFF]; 
            int mantissa = (sample >> (exponent+3)) & 0x0F; 
            int compressedByte = ~(sign | (exponent << 4) | mantissa); 

            return (byte)compressedByte;
        }

        #endregion

    }
}

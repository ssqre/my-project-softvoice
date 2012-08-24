using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.RTP
{
    /// <summary>
    /// This class represents RR: Receiver Report RTCP Packet.
    /// </summary>
    public class RTCP_Packet_RR : RTCP_Packet
    {
        private int                              m_Version       = 2;
        private int                              m_SSRC          = 0;
        private List<RTCP_Packet_SR_ReportBlock> m_pReportBlocks = null;

        /// <summary>
        /// Default constructor.
        /// </summary>
        internal RTCP_Packet_RR()
        {
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="ssrc">SSRC of this packet sender.</param>
        internal RTCP_Packet_RR(uint ssrc)
        {
        }


        #region mehtod ParseInternal

        /// <summary>
        /// Parses receiver report(RR) from byte buffer.
        /// </summary>
        /// <param name="buffer">Buffer wihich contains receiver report.</param>
        /// <param name="offset">Offset in buufer.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>buffer</b> is null.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
        protected override void ParseInternal(byte[] buffer,ref int offset)
        {
            /* RFC 3550 6.4.2 RR: Receiver Report RTCP Packet.
                    0                   1                   2                   3
                    0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
                   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            header |V=2|P|    RC   |   PT=RR=201   |             length            |
                   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
                   |                     SSRC of packet sender                     |
                   +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
            report |                 SSRC_1 (SSRC of first source)                 |
            block  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
              1    | fraction lost |       cumulative number of packets lost       |
                   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
                   |           extended highest sequence number received           |
                   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
                   |                      interarrival jitter                      |
                   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
                   |                         last SR (LSR)                         |
                   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
                   |                   delay since last SR (DLSR)                  |
                   +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
            report |                 SSRC_2 (SSRC of second source)                |
            block  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
              2    :                               ...                             :
                   +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
                   |                  profile-specific extensions                  |
                   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            */

            if(buffer == null){
                throw new ArgumentNullException("buffer");
            }
            if(offset < 0){
                throw new ArgumentException("Argument 'offset' value must be >= 0.");
            }

                 m_Version        = buffer[offset++] >> 6;
            bool isPadded         = Convert.ToBoolean((buffer[offset] >> 5) & 0x1);
            int  reportBlockCount = buffer[offset++] & 0x1F;
            int  type             = buffer[offset++];
            int  length           = buffer[offset++] << 8 | buffer[offset++];
            if(isPadded){
                this.PaddBytesCount = buffer[offset + length];
            }

            m_SSRC = buffer[offset++] << 24 | buffer[offset++] << 16 | buffer[offset++] << 8 | buffer[offset++];
            for(int i=0;i<reportBlockCount;i++){
                RTCP_Packet_SR_ReportBlock reportBlock = new RTCP_Packet_SR_ReportBlock();
                reportBlock.Parse(buffer,offset);
                m_pReportBlocks.Add(reportBlock);
                offset += 24;
            }
            // TODO: profile-specific extensions
        }

        #endregion

        #region method ToByte

        /// <summary>
        /// Stores receiver report(RR) packet to the specified buffer.
        /// </summary>
        /// <param name="buffer">Buffer where to store RR packet.</param>
        /// <param name="offset">Offset in buffer.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>buffer</b> is null.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
        public override void ToByte(byte[] buffer,ref int offset)
        {
            /* RFC 3550 6.4.2 RR: Receiver Report RTCP Packet.
                    0                   1                   2                   3
                    0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
                   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            header |V=2|P|    RC   |   PT=RR=201   |             length            |
                   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
                   |                     SSRC of packet sender                     |
                   +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
            report |                 SSRC_1 (SSRC of first source)                 |
            block  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
              1    | fraction lost |       cumulative number of packets lost       |
                   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
                   |           extended highest sequence number received           |
                   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
                   |                      interarrival jitter                      |
                   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
                   |                         last SR (LSR)                         |
                   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
                   |                   delay since last SR (DLSR)                  |
                   +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
            report |                 SSRC_2 (SSRC of second source)                |
            block  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
              2    :                               ...                             :
                   +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
                   |                  profile-specific extensions                  |
                   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            */

            if(buffer == null){
                throw new ArgumentNullException("buffer");
            }
            if(offset < 0){
                throw new ArgumentException("Argument 'offset' value must be >= 0.");
            }

            int length = 4 + (m_pReportBlocks.Count * 24);

            // V P RC
            buffer[offset++] = (byte)(2 << 6 | 0 << 5 | (m_pReportBlocks.Count | 0x1F));
            // PT=RR=201
            buffer[offset++] = 201;
            // length
            buffer[offset++] = (byte)((length >> 8) | 0xFF);
            buffer[offset++] = (byte)((length)      | 0xFF);
            // SSRC
            buffer[offset++] = (byte)((m_SSRC >> 24) | 0xFF);
            buffer[offset++] = (byte)((m_SSRC >> 16) | 0xFF);
            buffer[offset++] = (byte)((m_SSRC >> 8)  | 0xFF);
            buffer[offset++] = (byte)((m_SSRC)       | 0xFF);
            // Report blocks
            foreach(RTCP_Packet_SR_ReportBlock block in m_pReportBlocks){
                block.ToByte(buffer,ref offset);
            }
        }

        #endregion


        #region Properties Implementation

        /// <summary>
        /// Gets RTCP version.
        /// </summary>
        public override int Version
        {
            get{ return m_Version; }
        }

        /// <summary>
        /// Gets RTCP packet type.
        /// </summary>
        public override int Type
        {
            get{ return RTCP_PacketType.RR; }
        }

        /// <summary>
        /// Gets sender synchronization source identifier.
        /// </summary>
        public int SSRC
        {
            get{ return m_SSRC; }
        }

        /// <summary>
        /// Gets reports blocks.
        /// </summary>
        public List<RTCP_Packet_SR_ReportBlock> ReportBlocks
        {
            get{ return m_pReportBlocks; }
        }

        /// <summary>
        /// Gets number of bytes needed for this packet.
        /// </summary>
        public override int Size
        {
            get{ return 8 + (24 * m_pReportBlocks.Count); }
        }

        #endregion

    }
}

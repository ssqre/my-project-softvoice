using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.RTP
{
    /// <summary>
    /// This class represents RTP data receiver.
    /// </summary>
    public class RTP_Receiver
    {
        private RTP_Session           m_pSession           = null;
        private string                m_CName              = "";
        private uint                  m_SSRC               = 0;
        private RTP_SourceDescription m_pSourceDescription = null;

        /// <summary>
        /// Default constructor.
        /// </summary>
        internal RTP_Receiver()
        {
            m_CName = RTP_Utils.GenerateCNAME();
            m_SSRC  = RTP_Utils.GenerateSSRC();

            m_pSourceDescription = new RTP_SourceDescription(m_CName);
        }


        #region method CreateRtcpCompoundPackets

        /// <summary>
        /// Creates all RTCP report packets what this source(receiver) must send to target.
        /// </summary>
        /// <returns>Returns compound packets.</returns>
        internal RTCP_CompoundPacket[] CreateRtcpCompoundPackets()
        {
            /* RFC 3550 6.4.
                Both the SR and RR forms include zero or more reception report
                blocks, one for each of the synchronization sources from which this
                receiver has received RTP data packets since the last report.
                Reports are not issued for contributing sources listed in the CSRC
                list.  Each reception report block provides statistics about the data
                received from the particular source indicated in that block.  Since a
                maximum of 31 reception report blocks will fit in an SR or RR packet,
                additional RR packets SHOULD be stacked after the initial SR or RR
                packet as needed to contain the reception reports for all sources
                heard during the interval since the last report.
            */

            // Create full and minimal SDES.
            RTCP_Packet_SDES sdes = new RTCP_Packet_SDES();
            RTCP_Packet_SDES_Chunk sdesChunk = new RTCP_Packet_SDES_Chunk(m_SSRC,m_CName);
            sdesChunk.Set(m_pSourceDescription);
            sdes.Chunks.Add(sdesChunk);
            RTCP_Packet_SDES sdesMin = new RTCP_Packet_SDES();
            sdesMin.Chunks.Add(new RTCP_Packet_SDES_Chunk(m_SSRC,m_CName));

            // Check SDES size, if its almost MTU, then use minimal SDES only.
            if(sdes.Size > (m_pSession.MTU * 0.75)){
                sdes = sdesMin;
            }

            // Get all sources for what we need to generate reception report.
            Queue<RTCP_Packet_SR_ReportBlock> reportBlocks = new Queue<RTCP_Packet_SR_ReportBlock>();
            foreach(RTP_ReceiveStream stream in m_pSession.ReceiveStreams){
                if(stream.LastRtpPacket > m_pSession.LastRtcpInterval){
                   reportBlocks.Enqueue(stream.ReceptionReport);
                }
            }

            // Create RR packets.
            Queue<RTCP_Packet_RR> rrPackets = new Queue<RTCP_Packet_RR>();
            while(reportBlocks.Count > 0){
                RTCP_Packet_RR rr = new RTCP_Packet_RR(m_SSRC);
                rrPackets.Enqueue(rr);
                // Add reception blocks up to 31, if more we need crate additional RR packets,
                // this is because RR can hold 31 report blocks only.
                while(rr.ReportBlocks.Count < 31 && reportBlocks.Count > 0){
                    rr.ReportBlocks.Add(reportBlocks.Dequeue());
                }
            }
            
            // Create compound packets. Each compound packet must start with RR and end with SDES.
            List<RTCP_CompoundPacket> compPackets = new List<RTCP_CompoundPacket>();
            while(rrPackets.Count > 0){                
                RTCP_CompoundPacket packet = new RTCP_CompoundPacket();
                compPackets.Add(packet);
                // Add RR packets up to MTU - SDES(because we need to add SDES for each comp packet).
                while(rrPackets.Count > 0 && ((packet.TotalSize + rrPackets.Peek().Size) < (m_pSession.MTU - sdes.Size))){
                    packet.Packets.Add(rrPackets.Dequeue());
                }
                                
                // Add full SDES for first packet and later add minimal sdes(CNAME only).
                if(compPackets.Count == 1){
                    packet.Packets.Add(sdes);
                }
                else{
                    packet.Packets.Add(sdesMin);
                    sdes = sdesMin;
                }                
            }

            return compPackets.ToArray();
        }

        #endregion

        

        #region method ResetSSRC

        /// <summary>
        /// Resets SSRC value. Normally this must be done only if SSRC collision.
        /// </summary>
        private void ResetSSRC()
        {
            m_SSRC = RTP_Utils.GenerateSSRC();
        }

        #endregion

        // TODO: BYE


        #region Properties Implementation
        
        /// <summary>
        /// Gets source description.
        /// </summary>
        public RTP_SourceDescription SourceDescription
        {
            get{ return m_pSourceDescription; }
        }

        /// <summary>
        /// Gets this sender CNAME.
        /// </summary>
        internal string CNAME
        {
            get{ return m_CName; }
        }

        /// <summary>
        /// Gets this sender SSRC value.
        /// </summary>
        internal uint SSRC
        {
            get{ return m_SSRC; }
        }

        #endregion

    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace LumiSoft.Net.RTP
{
    /// <summary>
    /// This class represents RTP receive stream.
    /// </summary>
    public class RTP_ReceiveStream
    {
        private RTP_Session           m_pSession           = null;
        private IPEndPoint            m_pRemoteEndPoint    = null;
        private uint                  m_SSRC               = 0;
        private RTP_SourceDescription m_pDescription       = null;
        private int                   m_PayloadType        = -1;
        private ushort                m_RtpMaxSeq          = 0;    /* highest seq. number seen */
        private uint                  m_RtpSeqCycles       = 0;    /* shifted count of seq. number cycles */
        private uint                  m_RtpBaseSeq         = 0;    /* base seq number */
        private uint                  m_RtpBadSeq          = 0;    /* last 'bad' seq number + 1 */
        private uint                  m_RtpProbation       = 0;    /* seq. packets till source is valid */
        private uint                  m_RtpPacketsReceived = 0;    /* packets received */
        private uint                  m_RtpExpectedPrior   = 0;    /* packet expected at last interval */
        private uint                  m_RtpReceivedPrior   = 0;    /* packet received at last interval */
        private uint                  m_RtpTransit         = 0;    /* relative trans time for prev pkt */
        private uint                  m_RtpJitter          = 0;    /* estimated jitter */
        private long                  m_PacketsReceived    = 0;
        private long                  m_BytesReceived      = 0;
        private DateTime              m_LastRtpPacket      = DateTime.MinValue;
        private DateTime              m_LastRtcpPacket     = DateTime.MinValue;
        private DateTime              m_LastSR             = DateTime.MinValue;
        private bool                  m_IsDisposed         = false;
        private uint                  RTP_SEQ_MOD          = (1 << 16);

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="session">Owner RTP session.</param>
        /// <param name="remoteEndPoint">Remote end point of this stream.</param>
        /// <param name="ssrc">Synchronization source ID.</param>
        internal RTP_ReceiveStream(RTP_Session session,IPEndPoint remoteEndPoint,uint ssrc)
        {
            m_pSession        = session;
            m_pRemoteEndPoint = remoteEndPoint;
            m_SSRC            = ssrc;

            // RFC 3550 A.1.
            //InitRtpSeq(seq);
            //max_seq = seq - 1;
            //probation = MIN_SEQUENTIAL;
        }

        #region method Dispose

        /// <summary>
        /// Cleans up any resources being used.
        /// </summary>
        public void Dispose()
        {
            m_IsDisposed = true;
        }

        #endregion


        #region method Process

        /// <summary>
        /// Processes specified RTP packet through this stream.
        /// </summary>
        /// <param name="packet">RTP packet.</param>
        /// <param name="remoteEP">Remote end point which sent this packet.</param>
        /// <param name="size">Packet size in bytes.</param>
        internal void Process(RTP_Packet packet,IPEndPoint remoteEP,int size)
        {
            // We need to check source end point, that ensures someone can't hack in.
            if(!m_pRemoteEndPoint.Equals(remoteEP)){
                return;
            }

            /* RFC 3550 6.2.1
                New entries MAY be considered not valid until multiple packets carrying the new SSRC
                have been received (see Appendix A.1), or until an SDES RTCP packet containing 
                a CNAME for that SSRC has been received.
            
                We handle this in UpdateRtpSeq.
            */
            if(!UpdateRtpSeq((ushort)packet.SequenceNumber)){
                return;
            }
            CalculateJitter(packet);

            m_BytesReceived += size;
            m_PayloadType    = packet.PayloadType;
            m_LastRtpPacket  = DateTime.Now;
            
            // Raise PacketReceived event.
            m_pSession.OnPacketReceived(this,packet);
        }

        #endregion

        #region method ProcessRtcpPacket

        /// <summary>
        /// Processes specified RTCP packet through this stream.
        /// </summary>
        /// <param name="packet">RTCP packet.</param>
        internal void ProcessRtcpPacket(RTCP_Packet packet)
        {
            if(packet.Type == RTCP_PacketType.SR){
                m_LastSR = DateTime.Now;
            }
            else if(packet.Type == RTCP_PacketType.BYE){
                // TODO: Begin dispose, just linger 2 sec, this ensures delayed RTP packets received by this time.
            }
        }

        #endregion


        #region method InitRtpSeq

        /// <summary>
        /// Initializes new RTP sequence number.
        /// </summary>
        /// <param name="seq">New RTP sequence number.</param>
        private void InitRtpSeq(ushort seq)
        {
            // RFC 3550 A.1.

            m_RtpBaseSeq         = seq;
            m_RtpMaxSeq          = seq;
            m_RtpBadSeq          = RTP_SEQ_MOD + 1;   /* so seq == bad_seq is false */
            m_RtpSeqCycles       = 0;
            m_RtpPacketsReceived = 0;
            m_RtpReceivedPrior   = 0;
            m_RtpReceivedPrior   = 0;
        }

        #endregion

        #region method UpdateRtpSeq

        /// <summary>
        /// Updates RTP sequence number and returns if sequence is valid.
        /// </summary>
        /// <param name="seq">RTP packet sequence number.</param>
        /// <returns>Returns true if sequence is valid.</returns>
        private bool UpdateRtpSeq(ushort seq)
        {
            // RFC 3550 A.1.

            ushort udelta = (ushort)(seq - m_RtpMaxSeq);
            int MAX_DROPOUT    = 3000;
            int MAX_MISORDER   = 100;
            int MIN_SEQUENTIAL = 2;
            
            /*
            * Source is not valid until MIN_SEQUENTIAL packets with
            * sequential sequence numbers have been received.
            */
            if(m_RtpProbation > 0){
                /* packet is in sequence */
                if(seq == m_RtpMaxSeq + 1){
                    m_RtpProbation--;
                    m_RtpMaxSeq = seq;
                    if(m_RtpProbation == 0){
                        InitRtpSeq(seq);
                        m_PacketsReceived++;

                        return true;
                    }
                }
                else{
                    m_RtpProbation = (uint)(MIN_SEQUENTIAL - 1);
                    m_RtpMaxSeq = seq;
                }

                return false;
            }
            else if(udelta < MAX_DROPOUT){
                /* in order, with permissible gap */
                if(seq < m_RtpMaxSeq){
                    /*
                    * Sequence number wrapped - count another 64K cycle.
                    */
                    m_RtpSeqCycles += RTP_SEQ_MOD;
                }
                m_RtpMaxSeq = seq;
            }
            else if(udelta <= RTP_SEQ_MOD - MAX_MISORDER){
                /* the sequence number made a very large jump */
                if(seq == m_RtpBadSeq){
                    /*
                     * Two sequential packets -- assume that the other side
                     * restarted without telling us so just re-sync
                     * (i.e., pretend this was the first packet).
                    */
                    InitRtpSeq(seq);
                }
                else{
                    m_RtpBadSeq = (uint)((seq + 1) & (RTP_SEQ_MOD - 1));

                    return false;
                }
            }
            else{
                /* duplicate or reordered packet */
            }
            m_PacketsReceived++;

            return true;
        }

        #endregion

        #region method CalculateJitter

        /// <summary>
        /// Calculates RTP Interarrival Jitter as specified in RFC 3550 6.4.1.
        /// </summary>
        /// <param name="packet">RTP packet.</param>
        private void CalculateJitter(RTP_Packet packet)
        {            
            // RFC 3550 A.8.
            /*
            int transit = arrival - packet.TimeStamp;
            int d = transit - m_RtpTransit;
            m_RtpTransit = transit;
            if(d < 0){
                d = -d;
            }
            m_RtpJitter += (uint)((1d/16d) * ((double)d - m_RtpJitter));*/
        }

        #endregion


        #region Properties Implementation

        /// <summary>
        /// Gets if this object is disposed.
        /// </summary>
        public bool IsDisposed
        {
            get{ return m_IsDisposed; }
        }

        /// <summary>
        /// Gets RTP stream owner session.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when object is disposed and this property is accessed.</exception>
        public RTP_Session Session
        {
            get{  
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_SendStream");
                }

                return m_pSession;
            }
        }

        /// <summary>
        /// Gets stream remote end point.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when object is disposed and this property is accessed.</exception>
        public IPEndPoint RemoteEndPoint
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_SendStream");
                }

                return m_pRemoteEndPoint;
            }
        }

        /// <summary>
        /// Gets synchronization source(SSRC) ID of this stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when object is disposed and this property is accessed.</exception>
        public uint SSRC
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_SendStream");
                }

                 return m_SSRC;
            }
        }

        /// <summary>
        /// Gets this stream info. Value null means that target hasn't send stream info.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when object is disposed and this property is accessed.</exception>
        public RTP_SourceDescription Description
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_SendStream");
                }

               return m_pDescription;
            } 
        }

        /// <summary>
        /// Gets stream payload type.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when object is disposed and this property is accessed.</exception>
        public int PayloadType
        {
            get{ return m_PayloadType; }
        }

        /// <summary>
        /// Gets how many RTP packets has received by this stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when object is disposed and this property is accessed.</exception>
        public long PacketsReceived
        {
            get{  
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_SendStream");
                }

                return m_PacketsReceived; 
            }
        }

        /// <summary>
        /// Gets number of bytes received by this stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when object is disposed and this property is accessed.</exception>
        public long BytesReceived
        {
            get{  
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_SendStream");
                }

                return m_BytesReceived;
            }
        }

        /// <summary>
        /// Gets the time when last RTP packet was received.
        /// </summary>
        public DateTime LastRtpPacket
        {
            get{ return m_LastRtpPacket; }
        }

        /// <summary>
        /// Gets the time when last RTCP packet was received.
        /// </summary>
        public DateTime LastRtcpPacket
        {
            get{ return m_LastRtcpPacket; }
        }

        /// <summary>
        /// Gets when last RTP or RTCP packet was received.
        /// </summary>
        public DateTime LastActivity
        {
            get{
                if(m_LastRtpPacket > m_LastRtcpPacket){
                    return m_LastRtpPacket;
                }
                else{
                    return m_LastRtcpPacket;
                } 
            }
        }

        /// <summary>
        /// Returns this stream reception report block.
        /// </summary>
        internal RTCP_Packet_SR_ReportBlock ReceptionReport
        {
            get{
                // RFC 3550 A.3 Determining Number of Packets Expected and Lost.
                int  fraction          = 0;
                uint extended_max      = (uint)(m_RtpSeqCycles + m_RtpMaxSeq);
                int  expected          = (int)(extended_max - m_RtpBaseSeq + 1);
                int  lost              = (int)(expected - m_RtpPacketsReceived);
                int  expected_interval = (int)(expected - m_RtpExpectedPrior);
                m_RtpExpectedPrior     = (uint)expected;
                int received_interval  = (int)(m_RtpPacketsReceived - m_RtpReceivedPrior);
                m_RtpReceivedPrior     = m_RtpPacketsReceived;
                int lost_interval = expected_interval - received_interval;
                if(expected_interval == 0 || lost_interval <= 0){
                    fraction = 0;
                }
                else{
                    fraction = (lost_interval << 8) / expected_interval;
                }

                // If no SR packet has been received yet from SSRC_n, the DLSR field is set to zero.
                uint delay = 0;
                if(m_LastSR != DateTime.MinValue){
                    delay = (uint)((((TimeSpan)(DateTime.Now - m_LastSR)).Milliseconds / 65536) * 1000);
                }

                RTCP_Packet_SR_ReportBlock rr = new RTCP_Packet_SR_ReportBlock(this.SSRC);                              
                rr.FractionLost           = (uint)fraction;
                rr.CumulativePacketsLost  = lost;
                rr.ExtendedSequenceNumber = extended_max;
                rr.Jitter                 = m_RtpJitter;
                rr.LastSR                 = RTP_Utils.DateTimeToNTP32(m_LastSR);
                rr.DelaySinceLastSR       = delay;
               
                return rr; 
            }
        }

        #endregion

    }
}

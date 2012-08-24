using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace LumiSoft.Net.RTP
{
    /// <summary>
    /// This class represents RTP send stream.
    /// </summary>
    public class RTP_SendStream
    {
        private RTP_Session           m_pSession           = null;
        private string                m_CName              = "";
        private uint                  m_SSRC               = 0;        
        private RTP_SourceDescription m_pSourceDescription = null;
        private short                 m_SeqNumber          = 1;
        private uint                  m_PacketsSent        = 0;
        private uint                  m_BytesSent          = 0;
        private DateTime              m_LastRtpPacket      = DateTime.MinValue;
        private bool                  m_IsDisposed         = false;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="session">Owner RTP session.</param>
        internal RTP_SendStream(RTP_Session session)
        {
            m_pSession = session;

            m_CName = RTP_Utils.GenerateCNAME();
            m_SSRC  = RTP_Utils.GenerateSSRC();
            
            // TODO: Add RFC reference
            // Initial sequnece number must be random because of security.
            m_SeqNumber = (short)new Random().Next(1,10000);
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


        #region method Start

        /// <summary>
        /// Starts sending stream.
        /// </summary>
        /// <exception cref="ArgumentNullException">Is raised when <b>data</b> is null.</exception>
        public void Start()
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException("RTP_SendStream");
            }
        }

        #endregion

        #region method Stop

        /// <summary>
        /// Stops sending stream.
        /// </summary>
        /// <exception cref="ArgumentNullException">Is raised when <b>data</b> is null.</exception>
        public void Stop()
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException("RTP_SendStream");
            }
        }

        #endregion


        #region method Send

        /// <summary>
        /// Sends specified data to the RTP session target.
        /// </summary>
        /// <param name="isMaker">Specifies if IsMake bit of RTP packet is set.</param>
        /// <param name="timestamp">RTP timestamp.</param>
        /// <param name="data">Data to send.</param>
        /// <exception cref="ObjectDisposedException">Is raised when object is disposed and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>data</b> is null.</exception>
        public void Send(bool isMaker,int timestamp,byte[] data)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException("RTP_SendStream");
            }
            if(data == null){
                throw new ArgumentNullException("data");
            }
            if(timestamp < 0){
                throw new ArgumentException("Argument 'timestamp' value must be >=0.");
            }

            // TODO: If MTU exceeded, generate multiple RTP packets.

            RTP_Packet packet = new RTP_Packet();
            packet.IsMaker        = isMaker;
            packet.PayloadType    = this.PayloadType;
            packet.SSRC           = m_SSRC;
            packet.SequenceNumber = m_SeqNumber;
            packet.Timestamp      = timestamp;
            packet.Data           = data;

            int    offset    = 0;
            byte[] rawPacket = new byte[1500];
            packet.ToByte(rawPacket,ref offset);

            // Send packet to target.
            m_pSession.SendRtpPacket(rawPacket,offset);
            m_BytesSent += (uint)rawPacket.Length;
            m_PacketsSent++;            
            m_SeqNumber++;
            m_LastRtpPacket = DateTime.Now;
        }

        #endregion

        #region method SendAppPacket

        /// <summary>
        /// Sends application specifiec data to the RTP session targets.
        /// </summary>
        public void SendAppPacket(byte[] data)
        {
            // TODO:
        }

        #endregion


        #region method ProcessRtcpPacket

        /// <summary>
        /// Processes specified RTCP packet through this stream.
        /// </summary>
        /// <param name="packet">RTCP packet.</param>
        internal void ProcessRtcpPacket(RTCP_Packet packet)
        {
        }

        #endregion

        #region method CreateRtcpCompoundPacket

        /// <summary>
        /// Creates all RTCP report packet what this source(sender) must send to target.
        /// </summary>
        /// <returns>Returns RTCP compound packet.</returns>
        internal RTCP_CompoundPacket CreateRtcpCompoundPacket()
        {
            RTCP_CompoundPacket packet = new RTCP_CompoundPacket();
            // Create SR
            RTCP_Packet_SR sr = new RTCP_Packet_SR(m_SSRC);            
            sr.NtpTimestamp      = RTP_Utils.DateTimeToNTP64(DateTime.Now);
            // TODO: FIXME:
            sr.RtpTimestamp      = 0;
            sr.SenderPacketCount = m_PacketsSent;
            sr.SenderOctetCount  = m_BytesSent;
            packet.Packets.Add(sr);
            //Create SDES
            RTCP_Packet_SDES sdes = new RTCP_Packet_SDES();
            RTCP_Packet_SDES_Chunk sdesChunk = new RTCP_Packet_SDES_Chunk(m_SSRC,m_CName);
            sdesChunk.Set(m_pSourceDescription);
            sdes.Chunks.Add(sdesChunk);
            packet.Packets.Add(sdes);

            return packet;
        }

        #endregion

        #region method ResetSSRC

        /// <summary>
        /// Resets this sender SSRC. Normally this must be done only if SSRC collision.
        /// </summary>
        private void ResetSSRC()
        {
            /* RFC 3550 6.4.1.
                If we chnage SSRC, we need to reset sender's packet and octets count.
            */

            m_SSRC        = RTP_Utils.GenerateSSRC();
            m_PacketsSent = 0;
            m_BytesSent   = 0;
        }

        #endregion


        // TODO: Allow to add CSRCs + their SDES


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
        /// Gets source description.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when object is disposed and this property is accessed.</exception>
        public RTP_SourceDescription SourceDescription
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_SendStream");
                }

                return m_pSourceDescription; 
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
        /// Gets stream payload type.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when object is disposed and this property is accessed.</exception>
        public int PayloadType
        {
            get{  
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_SendStream");
                }
                
                return m_pSession.PayloadType; 
            }
        }

        /// <summary>
        /// Gets how many RTP packets has sent through this stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when object is disposed and this property is accessed.</exception>
        public uint PacketsSent
        {
            get{  
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_SendStream");
                }
                
                return m_PacketsSent; 
            }
        }

        /// <summary>
        /// Gets number of bytes sent through this stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when object is disposed and this property is accessed.</exception>
        public uint BytesSent
        {
            get{  
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_SendStream");
                }
                
                return m_BytesSent; 
            }
        }

        /// <summary>
        /// Gets the time when last RTP packet was sent.
        /// </summary>
        public DateTime LastRtpPacket
        {
            get{ return m_LastRtpPacket; }
        }
        
        #endregion

    }
}

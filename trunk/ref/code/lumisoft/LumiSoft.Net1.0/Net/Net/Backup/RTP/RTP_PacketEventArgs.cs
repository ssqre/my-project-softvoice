using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.RTP
{
    /// <summary>
    /// This class provides data for the <b>RTP_Session.PacketReceived</b> event.
    /// </summary>
    public class RTP_PacketEventArgs
    {
        private RTP_Session       m_pSession = null;
        private RTP_ReceiveStream m_pStream  = null;
        private RTP_Packet        m_pPacket  = null;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="session">Owner RTP session.</param>
        /// <param name="stream">Stream which received packet.</param>
        /// <param name="packet">Received RTP packet.</param>
        internal RTP_PacketEventArgs(RTP_Session session,RTP_ReceiveStream stream,RTP_Packet packet)
        {
            m_pSession = session;
            m_pStream  = stream;
            m_pPacket  = packet;
        }


        #region Properties Implementation

        /// <summary>
        /// Gets owner RTP session.
        /// </summary>
        public RTP_Session Session
        {
            get{ return m_pSession; }
        }

        /// <summary>
        /// Gets stream which received packet.
        /// </summary>
        public RTP_ReceiveStream Stream
        {
            get{ return m_pStream; }
        }

        /// <summary>
        /// Gets received RTP packet.
        /// </summary>
        public RTP_Packet Packet
        {
            get{ return m_pPacket; }
        }

        #endregion

    }
}

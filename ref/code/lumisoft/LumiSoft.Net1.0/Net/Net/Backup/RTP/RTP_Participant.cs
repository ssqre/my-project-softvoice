using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Timers;

namespace LumiSoft.Net.RTP
{
    /// <summary>
    /// This class represents RTP session participant.
    /// </summary>
    public class RTP_Participant
    {
        #region class RTP_Participant_State

        /// <summary>
        /// This class holds RFC 3550 6.3 state data.
        /// </summary>
        private class RTP_Participant_State
        {
            /// <summary>
            /// The last time an RTCP packet was transmitted.
            /// </summary>
            public double tp = 0;
            /// <summary>
            /// The current time.
            /// </summary>
            public int tc = 0;
            /// <summary>
            /// The next scheduled transmission time of an RTCP packet.
            /// </summary>
            public double tn = 0;
            /// <summary>
            /// The estimated number of session members at the time tn was last recomputed.
            /// </summary>
            public int pmembers = 1;
            /// <summary>
            /// The most current estimate for the number of session members.
            /// </summary>
            public int members = 1;
            /// <summary>
            /// The most current estimate for the number of senders in the session.
            /// </summary>
            public int senders = 0;
            /// <summary>
            /// The target RTCP bandwidth, i.e., the total bandwidth that will be used for RTCP 
            /// packets by all members of this session, in octets per second.
            /// </summary>
            public int rtcp_bw = 1000;
            /// <summary>
            /// Flag that is true if the application has sent data since the 2nd previous RTCP report was transmitted.
            /// </summary>
            public bool we_sent = false;
            /// <summary>
            /// The average compound RTCP packet size, in octets, over all RTCP packets sent and 
            /// received by this participant. The size includes lower-layer transport and network 
            /// protocol headers (e.g., UDP and IP) as explained in Section 6.2.
            /// </summary>
            public int avg_rtcp_size = 200;
            /// <summary>
            /// Flag that is true if the application has not yet sent an RTCP packet.
            /// </summary>
            public bool initial = true;
        }

        #endregion

        private RTP_SourceDescription m_pEndPointInfo      = null;
        private IPEndPoint            m_pRtpEndPoint       = null;
        private IPEndPoint            m_pRtcpEndPoint      = null;
        private int                   m_SSRC               = 0;
        private bool                  m_IsIdentified       = false;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="endPoint">Participant RTP endpoint.</param>
        public RTP_Participant(IPEndPoint endPoint)
        {
            m_pRtpEndPoint = endPoint;
            m_pRtcpEndPoint = new IPEndPoint(endPoint.Address,endPoint.Port + 1);
        }
                       
        #region method ProcessPacket

        /// <summary>
        /// Processes specified RTCP packet through this participant.
        /// </summary>
        /// <param name="packet">RTCP packet to process.</param>
        /// <param name="packetSize">Packet size in bytes.</param>
        internal void ProcessPacket(RTCP_Packet packet,int packetSize)
        {
            /* RFC 3550 6.3.3. Receiving an Non-BYE RTCP Packet.
                For each compound RTCP packet received, the value of avg_rtcp_size is
                updated:
             
                    avg_rtcp_size = (1/16) * packet_size + (15/16) * avg_rtcp_size

                where packet_size is the size of the RTCP packet just received.
            */
            if(packet.Type != RTCP_PacketType.BYE){
                //m_pState.avg_rtcp_size = (1/16) * packetSize + (15/16) * m_pState.avg_rtcp_size;

                // TODO:
            }
            /* RFC 3550 6.3.4 Receiving an RTCP BYE Packet.
                Except as described in Section 6.3.7 for the case when an RTCP BYE is
                to be transmitted, if the received packet is an RTCP BYE packet, the
                SSRC is checked against the member table.  If present, the entry is
                removed from the table, and the value for members is updated.  The
                SSRC is then checked against the sender table.  If present, the entry
                is removed from the table, and the value for senders is updated.
              
                Furthermore, to make the transmission rate of RTCP packets more
                adaptive to changes in group membership, the following "reverse
                reconsideration" algorithm SHOULD be executed when a BYE packet is
                received that reduces members to a value less than pmembers:

                o  The value for tn is updated according to the following formula:
                    tn = tc + (members/pmembers) * (tn - tc)

                o  The value for tp is updated according the following formula:
                    tp = tc - (members/pmembers) * (tc - tp).  

                o  The next RTCP packet is rescheduled for transmission at time tn,
                   which is now earlier.

                o  The value of pmembers is set equal to members.

                This algorithm does not prevent the group size estimate from
                incorrectly dropping to zero for a short time due to premature
                timeouts when most participants of a large session leave at once but
                some remain.  The algorithm does make the estimate return to the
                correct value more rapidly.  This situation is unusual enough and the
                consequences are sufficiently harmless that this problem is deemed
                only a secondary concern.
            */
            else{
            }

            // TODO: Process
        }

        #endregion
               
        

        #region Properties Implementation

        /// <summary>
        /// Gets participant end point info. NOTE: Returns null if participant haven't sent it.
        /// </summary>
        public RTP_SourceDescription EndPointInfo
        {
            get{ return m_pEndPointInfo; }
        }

        /// <summary>
        /// Gets participant RTP end point, where participant waits for data.
        /// </summary>
        public IPEndPoint RtpEndPoint
        {
            get{ return m_pRtpEndPoint; }
        }

        /// <summary>
        /// Gets participant RTP end point, where participant waits for data.
        /// </summary>
        public IPEndPoint RtcpEndPoint
        {
            get{ return m_pRtcpEndPoint; }
        }

        /// <summary>
        /// Gets participant synchronization source identifier.
        /// </summary>
        public int SSRC
        {
            get{ return m_SSRC; }
        }

        /// <summary>
        /// Gets if participant has identified itself by sending session description (SDES) packet.
        /// </summary>
        public bool IsIdentified
        {
            get{ return m_IsIdentified; }
        }

        #endregion

    }
}

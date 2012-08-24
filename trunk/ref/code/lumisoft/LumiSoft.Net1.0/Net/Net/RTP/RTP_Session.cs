using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Timers;

namespace LumiSoft.Net.RTP
{
    #region Delegates

    /// <summary>
    /// Represents the method that will handle the <b>RTP_Session.ParticipantAdded</b> and <b>RTP_Session.ParticipantRemoved</b> event.
    /// </summary>
    /// <param name="e">Event data.</param>
    public delegate void RTP_ParticipantEventHandler(RTP_ParticipantEventArgs e);

    /// <summary>
    /// Represents the method that will handle the <b>RTP_Session.PacketReceived</b> event.
    /// </summary>
    /// <param name="e">Event data.</param>
    public delegate void PacketReceivedEventHandler(RTP_PacketEventArgs e);

    #endregion

    /// <summary>
    /// This class represesnts RTP session. Session can exchange 1 payload type at time.
    /// For example if application wants to send audio and video, it must create 2 RTP sessions.
    /// </summary>
    public class RTP_Session
    {
        private RTP_SourceDescription       m_pEndPointInfo    = null;
        private Socket                      m_pRtcpSocket      = null;
        private Socket                      m_pRtpSocket       = null;
        private IPEndPoint                  m_pTarget          = null;
        private bool                        m_CloseSockets     = true;
        private int                         m_MTU              = 1400;
        private RTP_Receiver                m_pRtpReceiver     = null;
        private RTP_ParticipantCollection   m_pParticipants    = null;
        private int                         m_PayloadType      = 0;
        private int                         m_Bandwidth        = 64000;
        private Dictionary<int,int>         m_pMembers         = null;
        private List<uint>                  m_pSenders         = null;
        private RTP_SenderStreamCollection  m_pSenderStreams   = null;
        private RTP_ReceiveStreamCollection m_pReceiveStreams  = null;
        private bool                        m_Initial          = true;
        private int                         m_Avg_rtcp_size    = 0;
        private int                         m_LastMembers      = 0;
        private DateTime                    m_LastRtcpInterval = DateTime.MinValue;
        private DateTime                    m_NextRtcpInterval = DateTime.MinValue;
        private bool                        m_WeSent           = false;
        private long                        m_RtpPacketsSent   = 0;
        private long                        m_RtpBytesSent     = 0;
        private long                        m_RtcpPacketsSent  = 0;
        private long                        m_RtcpBytesSent    = 0;
        private System.Timers.Timer         m_pRtcpTimer       = null;
        private bool                        m_IsRunning        = false;
        private bool                        m_IsDisposed      = false;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="endPointInfo">This session end point info, which is reported to participants.</param>
        /// <param name="rtcp">RTCP socket.</param>
        /// <param name="rtp">RTP socket.</param>
        /// <param name="target">RTP session target. This can be multicast or unicast end point.</param>
        /// <exception cref="ArgumentNullException">Is raised when any of the arguments is null.</exception>
        public RTP_Session(RTP_SourceDescription endPointInfo,Socket rtcp,Socket rtp,IPEndPoint target)
        {
            if(endPointInfo == null){
                throw new ArgumentNullException("endPointInfo");
            }
            if(rtcp == null){
                throw new ArgumentNullException("rtcp");
            }
            if(rtp == null){
                throw new ArgumentNullException("rtp");
            }
            if(target == null){
                throw new ArgumentNullException("target");
            }

            m_pEndPointInfo   = endPointInfo;
            m_pRtcpSocket     = rtcp;
            m_pRtpSocket      = rtp;
            m_pTarget         = target;
            m_pRtpReceiver    = new RTP_Receiver();
            m_pParticipants   = new RTP_ParticipantCollection();
            m_pMembers        = new Dictionary<int,int>();
            m_pSenders        = new List<uint>();
            m_pSenderStreams  = new RTP_SenderStreamCollection(this);
            m_pReceiveStreams = new RTP_ReceiveStreamCollection(this);
        }

        #region method Dispose

        /// <summary>
        /// Cleans up any resources being used. NOTE: BYE is sent to all active participants.
        /// </summary>
        public void Dispose()
        {
            if(m_IsDisposed){
                return;
            }
            m_IsDisposed = true;

            m_IsRunning = false;

            // TODO: Dispose SendStreams

            if(m_CloseSockets){
                m_pRtcpSocket.Close();
                m_pRtpSocket.Close();
            }
        }

        #endregion


        #region method Start

        /// <summary>
        /// Starts RTP session.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when RTP_Session is disposed and this method is accessed.</exception>
        public void Start()
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException("RTP_Session");
            }

            // We are running already.
            if(m_IsRunning){
                return;
            }
            m_IsRunning = true;

            // Start listening incoming RTCP and RTP
            Thread tr1 = new Thread(new ThreadStart(this.ProcessIncomingRtcp));
            tr1.Start();
            Thread tr2 = new Thread(new ThreadStart(this.ProcessIncomingRtp));
            tr2.Start();

            // Start RTCP transmission timer.
            m_pRtcpTimer = new System.Timers.Timer();
            m_pRtcpTimer.Elapsed += new ElapsedEventHandler(m_pRtcpTimer_Elapsed);
            m_pRtcpTimer.Enabled = true;
        }
                
        #endregion

        #region method Stop

        /// <summary>
        /// Stops RTP session.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when RTP_Session is disposed and this method is accessed.</exception>
        public void Stop()
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException("RTP_Session");
            }

            m_pRtcpTimer.Dispose();
            m_pRtcpTimer = null;

            m_IsRunning = false;
        }

        #endregion


        #region Events Handling

        #region method m_pRtcpTimer_Elapsed

        /// <summary>
        /// This method is called when RTCP transmission timer expires. We need to reschedule RTCP packet
        /// transmission or send RTCP report packet. For more info see section RFC 3550 6.3.6.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void m_pRtcpTimer_Elapsed(object sender,ElapsedEventArgs e)
        {
            // RFC 3550 6.3.5 Timing Out an SSRC.            
            CheckTimeout();

            /* RFC 3550 6.3.6 Expiration of Transmission Timer.
                When the packet transmission timer expires, the participant performs
                the following operations:

                    o  The transmission interval T is computed as described in Section
                       6.3.1, including the randomization factor.

                    o  If tp + T is less than or equal to tc, an RTCP packet is transmitted. 
                       tp is set to tc, then another value for T is calculated as in the previous 
                       step and tn is set to tc + T. The transmission timer is set to expire again 
                       at time tn. If tp + T is greater than tc, tn is set to tp + T. No RTCP packet 
                       is transmitted. The transmission timer is set to expire at time tn.

                    o  pmembers is set to members.

                    If an RTCP packet is transmitted, the value of initial is set to
                    FALSE.  Furthermore, the value of avg_rtcp_size is updated:
          
                        avg_rtcp_size = (1/16) * packet_size + (15/16) * avg_rtcp_size
         
                    where packet_size is the size of the RTCP packet just transmitted.
            */

            // Create RTCP report compound packets.
            List<RTCP_CompoundPacket> packets = new List<RTCP_CompoundPacket>();
            packets.AddRange(m_pRtpReceiver.CreateRtcpCompoundPackets());
            foreach(RTP_SendStream stream in m_pSenderStreams.ToArray()){
                packets.Add(stream.CreateRtcpCompoundPacket());
            }

            // Calculate total size of packets.
            int packetsSize = 0;
            foreach(RTCP_CompoundPacket packet in packets){
                packetsSize += packet.TotalSize;
            }

            DateTime currentTime = DateTime.Now;
            double   interval    = ComputeRtcpTransmissionInterval(this.MembersCount,m_pSenders.Count,this.RtcpBandwidth,m_WeSent,m_Avg_rtcp_size,m_Initial);            
            DateTime nextTime    = m_LastRtcpInterval.AddSeconds(interval);

            if(nextTime <= currentTime){
                // Send all RTCP compound packets.
                foreach(RTCP_CompoundPacket packet in packets){
                    SendRtcpPacket(packet);
                }

                m_Avg_rtcp_size = (int)((1.0/16.0) * packetsSize + (15.0/16.0) * m_Avg_rtcp_size);
                m_LastRtcpInterval = currentTime;

                /* We must redraw the interval. Don't reuse the one computed above, since its not actually
                   distributed the same, as we are conditioned on it being small enough to cause a packet to
                   be sent.
                */
                interval = ComputeRtcpTransmissionInterval(this.MembersCount,m_pSenders.Count,this.RtcpBandwidth,m_WeSent,m_Avg_rtcp_size,m_Initial);

                Schedule((int)(interval * 1000));
                m_Initial = false;
            }
            else{
                Schedule(((TimeSpan)(nextTime - currentTime)).Milliseconds);
            }            
            m_LastMembers = this.MembersCount;            
        }

        #endregion

        #endregion


        #region method ProcessIncomingRtcp

        /// <summary>
        /// Processes incoming RTCP data.
        /// </summary>
        private void ProcessIncomingRtcp()
        {
            try{
                byte[] buffer = new byte[m_MTU];
                while(m_IsRunning){
                    try{
                        if(m_pRtcpSocket.Poll(1,SelectMode.SelectRead)){                            
                            EndPoint remoteEP = new IPEndPoint(IPAddress.Any,0);
                            int size = m_pRtcpSocket.ReceiveFrom(buffer,ref remoteEP);

                            ProcessRtcpPacket(RTCP_CompoundPacket.Parse(buffer),size);
                        }
                        else{
                            Thread.Sleep(5);
                        }
                    }
                    catch{                        
                        //System.IO.File.WriteAllText("d:\\error.txt","");
                    }
                }
            }
            catch{
                // FIX ME:
            }
        }

        #endregion

        #region method ProcessIncomingRtp

        /// <summary>
        /// Processes incoming RTP data.
        /// </summary>
        private void ProcessIncomingRtp()
        {
            try{
                byte[] buffer = new byte[m_MTU];
                while(m_IsRunning){
                    try{
                        if(m_pRtpSocket.Poll(1,SelectMode.SelectRead)){
                    
                            EndPoint remoteEP = new IPEndPoint(IPAddress.Any,0);
                            int count = m_pRtpSocket.ReceiveFrom(buffer,ref remoteEP);

                            ProcessRtpPacket(RTP_Packet.Parse(buffer,count),(IPEndPoint)remoteEP,count);
                        }
                        else{
                            Thread.Sleep(1);
                        }
                    }
                    catch{
                        //System.IO.File.WriteAllText("d:\\error.txt","");
                    }
                }
            }
            catch{
                // FIX ME:
            }
        }

        #endregion

        #region method ProcessRtcpPacket

        /// <summary>
        /// Processes specified RTCP packet.
        /// </summary>
        /// <param name="packet">RTCP compound packet.</param>
        /// <param name="packetSize">Returns raw received packet size in bytes.</param>
        private void ProcessRtcpPacket(RTCP_CompoundPacket packet,int packetSize)
        {
            /* RFC 3550 6.3.3 Receiving an RTP or Non-BYE RTCP Packet.
                When an RTP or RTCP packet is received from a participant whose SSRC
                is not in the member table, the SSRC is added to the table, and the
                value for members is updated once the participant has been validated
                as described in Section 6.2.1. 

                For each compound RTCP packet received, the value of avg_rtcp_size is
                updated:

                avg_rtcp_size = (1/16) * packet_size + (15/16) * avg_rtcp_size

                where packet_size is the size of the RTCP packet just received.
            */
            /*
            if(!(packet is RTCP_Packet_BYE)){
                //m_pParticipants.Contains();
            }*/
            /* RFC 3550 6.3.4 Receiving an RTCP BYE Packet
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

            System.IO.File.WriteAllText("d:\\rtcp.txt",packet.ToString() + "\r\n");
            /*
            double avg_rtcp_size = 0;
            int    members       = 0;
            int    pmembers      = 0;
            int    senders       = 0;
            int    tn            = 0;
            int    tp            = 0;
            int    tc            = 0;

            //m_pParticipants.Contains();
           
            if(!(packet is RTCP_Packet_BYE)){
                if(NewMember(p) && (TypeOfEvent(e) == EVENT_REPORT)){
                    AddMember(p);
                    members += 1;
                }
                avg_rtcp_size = (1d/16d) * packetSize + (15d/16d) * avg_rtcp_size;
            }
            else if(packet is RTCP_Packet_BYE){
                avg_rtcp_size = (1d/16d) * packetSize + (15d/16d) * avg_rtcp_size;

                if(TypeOfEvent(e) == EVENT_REPORT){
                    if(NewSender(p) == false){
                        RemoveSender(p);
                        senders -= 1;
                    }

                    if(NewMember(p) == false){
                        RemoveMember(p);
                        members -= 1;
                    }

                    if(members < pmembers){
                        tn = tc + (((double)members)/(pmembers)) * (tn - tc);
                        tp = tc - (((double)members)/(pmembers)) * (tc - tp);

                        // Reschedule the next report for time tn 

                        Reschedule(tn,e);
                        pmembers = members;
                    }
                }
                else if(TypeOfEvent(e) == EVENT_BYE){
                    members += 1;
                }
            }*/
/*
            else if(packet is RTP_Packet){
                if(NewMember(p) && (TypeOfEvent(e) == EVENT_REPORT)){
                    AddMember(p);
                    *members += 1;
                }
                if(NewSender(p) && (TypeOfEvent(e) == EVENT_REPORT)){
                    AddSender(p);
                    *senders += 1;
                }
            }*/ 
        }

        #endregion

        #region method ProcessRtpPacket

        /// <summary>
        /// Processes specified RTP packet.
        /// </summary>
        /// <param name="packet">RTP packet.</param>
        /// <param name="remoteEP">Remote end pint from where packet was received.</param>
        /// <param name="size">Packet size in bytes.</param>
        private void ProcessRtpPacket(RTP_Packet packet,IPEndPoint remoteEP,int size)
        {
            /* RFC 3550 6.3.3 Receiving an RTP Packet.
                When an RTP packet is received from a participant whose SSRC is not in the member table, 
                the SSRC is added to the table, and the value for members is updated once the participant 
                has been validated as described in Section 6.2.1. The same processing occurs for each
                CSRC in a validated RTP packet.

                When an RTP packet is received from a participant whose SSRC is not in the sender table, 
                the SSRC is added to the table, and the value for senders is updated.
            */

            // TODO: Secured IP end points, allow user to specify from where we accept data only.
            
            foreach(int sourceID in packet.Sources){
                if(!m_pMembers.ContainsKey(sourceID)){
                    try{
                        m_pMembers.Add(sourceID,sourceID);
                    }
                    catch{
                        // We may get 'key already exists' here, because we don't lock m_pMembers,
                        // just skip that exception.
                    }
                }                
            }

            m_pReceiveStreams.Get(remoteEP,packet.SSRC,true).Process(packet,remoteEP,size);
        }

        #endregion

        #region method SendRtpPacket

        /// <summary>
        /// Sends raw RTP packet to the RTP session target.
        /// </summary>
        /// <param name="packet">Raw RTP packet.</param>
        /// <param name="offset">Offset in the buufer.</param>
        internal void SendRtpPacket(byte[] packet,int offset)
        {
            try{               
                m_pRtpSocket.SendTo(packet,offset,SocketFlags.None,m_pTarget);
            }
            catch{                
                // FIX ME: What todo here ?
            }
        }

        #endregion

        #region method SendRtcpPacket

        /// <summary>
        /// Sends RTCP compoun dpacket to the RTP session target.
        /// </summary>
        /// <param name="packet">RTCP compound packet.</param>
        private void SendRtcpPacket(RTCP_CompoundPacket packet)
        {
            try{
                m_pRtcpSocket.SendTo(packet.ToByte(),0,SocketFlags.None,new IPEndPoint(m_pTarget.Address,m_pTarget.Port + 1));
            }
            catch{
                // FIX ME: What todo ? Log 
            }
        }

        #endregion


        #region method ComputeRtcpTransmissionInterval

        /// <summary>
        /// Computes RTCP transmission interval. Defined in RFC 3550 6.3.1.
        /// </summary>
        /// <param name="members">Current mebers count.</param>
        /// <param name="senders">Current sender count.</param>
        /// <param name="rtcp_bw">RTCP bandwidth.</param>
        /// <param name="we_sent">Specifies if we have sent data after last 2 RTCP interval.</param>
        /// <param name="avg_rtcp_size">Average RTCP raw packet size, IP headers included.</param>
        /// <param name="initial">Specifies if we ever hae sent data to target.</param>
        /// <returns>Returns transmission interval in seconds.</returns>
        private double ComputeRtcpTransmissionInterval(int members,int senders,double rtcp_bw,bool we_sent,double avg_rtcp_size,bool initial)
        {
            // RFC 3550 A.7.

            /*
                Minimum average time between RTCP packets from this site (in
                seconds).  This time prevents the reports from `clumping' when
                sessions are small and the law of large numbers isn't helping
                to smooth out the traffic.  It also keeps the report interval
                from becoming ridiculously small during transient outages like
                a network partition.
            */
            double RTCP_MIN_TIME = 5;
            /*
                Fraction of the RTCP bandwidth to be shared among active
                senders.  (This fraction was chosen so that in a typical
                session with one or two active senders, the computed report
                time would be roughly equal to the minimum report time so that
                we don't unnecessarily slow down receiver reports.)  The
                receiver fraction must be 1 - the sender fraction.
            */
            double RTCP_SENDER_BW_FRACTION = 0.25;
            double RTCP_RCVR_BW_FRACTION = (1-RTCP_SENDER_BW_FRACTION);            
            /* 
                To compensate for "timer reconsideration" converging to a
                value below the intended average.
            */
            double COMPENSATION = 2.71828 - 1.5;

            double t;                   /* interval */
            double rtcp_min_time = RTCP_MIN_TIME;
            int n;                      /* no. of members for computation */

            /*
                Very first call at application start-up uses half the min
                delay for quicker notification while still allowing some time
                before reporting for randomization and to learn about other
                sources so the report interval will converge to the correct
                interval more quickly.
            */
            if(initial){
                rtcp_min_time /= 2;
            }
            /*
                Dedicate a fraction of the RTCP bandwidth to senders unless
                the number of senders is large enough that their share is
                more than that fraction.
            */
            n = members;
            if(senders <= (members * RTCP_SENDER_BW_FRACTION)){
                if(we_sent){
                    rtcp_bw = (int)(rtcp_bw * RTCP_SENDER_BW_FRACTION);
                    n = senders;
                }
                else{
                    rtcp_bw = (int)(rtcp_bw * RTCP_SENDER_BW_FRACTION);
                    n -= senders;
                }
            }

            /*
                The effective number of sites times the average packet size is
                the total number of octets sent when each site sends a report.
                Dividing this by the effective bandwidth gives the time
                interval over which those packets must be sent in order to
                meet the bandwidth target, with a minimum enforced.  In that
                time interval we send one report so this time is also our
                average time between reports.
            */
            t = avg_rtcp_size * n / rtcp_bw;
            if(t < rtcp_min_time){
                t = rtcp_min_time;
            }

            /*
                To avoid traffic bursts from unintended synchronization with
                other sites, we then pick our actual next report interval as a
                random number uniformly distributed between 0.5*t and 1.5*t.
            */
            t = t * (new Random().NextDouble() + 0.5);
            t = t / COMPENSATION;

            return t;
        }

        #endregion

        #region method CheckTimeout

        /// <summary>
        /// Check if some receive streams has timed out. For more info see RFC 3550 6.3.5.
        /// </summary>
        private void CheckTimeout()
        {
            /* RFC 3550 6.3.5 Timing Out an SSRC.
                At occasional intervals, the participant MUST check to see if any of
                the other participants time out.  To do this, the participant
                computes the deterministic (without the randomization factor)
                calculated interval Td for a receiver, that is, with we_sent false.
                Any other session member who has not sent an RTP or RTCP packet since
                time tc - MTd (M is the timeout multiplier, and defaults to 5) is
                timed out.  This means that its SSRC is removed from the member list,
                and members is updated.  A similar check is performed on the sender
                list.  Any member on the sender list who has not sent an RTP packet
                since time tc - 2T (within the last two RTCP report intervals) is
                removed from the sender list, and senders is updated.

                If any members time out, the reverse reconsideration algorithm
                described in Section 6.3.4 SHOULD be performed.

                The participant MUST perform this check at least once per RTCP
                transmission interval.
            */

            DateTime currentTime = DateTime.Now;
            double td = ComputeRtcpTransmissionInterval(this.MembersCount,m_pSenders.Count,this.RtcpBandwidth,false,m_Avg_rtcp_size,m_Initial);

            bool anyMemberTimedOut = false;            
            foreach(RTP_ReceiveStream stream in m_pReceiveStreams.ToArray()){
                // Members check
                if(currentTime > stream.LastActivity.AddSeconds(5 * td)){
                    m_pReceiveStreams.Remove(stream.SSRC);                    
                    m_pSenders.Remove(stream.SSRC);
                    anyMemberTimedOut = true;
                }
                // Sender flag check
                else if(currentTime > stream.LastActivity.AddSeconds(2 * td)){
                    m_pSenders.Remove(stream.SSRC);
                }
            }

            if(anyMemberTimedOut){
                DoReverseReconsideration();
            }
        }

        #endregion

        #region method Schedule

        /// <summary>
        /// Schedules transmission timer to exprire after specified milli seconds.
        /// If there is already active timer, then it's expire time is updated.
        /// </summary>
        /// <param name="interval">Interval in milliseconda.</param>
        private void Schedule(int interval)
        {
            m_NextRtcpInterval = DateTime.Now.AddMilliseconds(interval);

            m_pRtcpTimer.Stop();
            m_pRtcpTimer.Interval = interval;
            m_pRtcpTimer.Start();
        }

        #endregion

        #region method DoReverseReconsideration

        /// <summary>
        /// Does "reverse reconsideration" algorithm.
        /// </summary>
        private void DoReverseReconsideration()
        {
            /* RFC 3550 6.3.4. "reverse reconsideration"
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

            //m_pState.tn = m_pState.tc + ((m_pState.members / m_pState.pmembers) * (m_pState.tn - m_pState.tc));
            //m_pState.tp = m_pState.tc - ((m_pState.members / m_pState.pmembers) * (m_pState.tc - m_pState.tp));
            //Schedule(m_pState.tn);
            //m_pState.pmembers = m_pState.members;

        }

        #endregion

        #region method CheckAndHandleSSRCCollision

        /// <summary>
        /// Checks and handles local and remote source ID collision.
        /// </summary>
        /// <param name="source">Source ID.</param>
        private void CheckAndHandleSSRCCollision(int source)
        {
            // Send BYE and generate new local SSRC for that stream.
        }

        #endregion


        #region Properties Implementation

        /// <summary>
        /// Gets this session local end point info, which is reported to participants.
        /// </summary>
        public RTP_SourceDescription EndPointInfo
        {
            get{ return m_pEndPointInfo; }
        }

        /// <summary>
        /// Gets RTCP session local IP end point.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when RTP_Session is disposed and this property is accessed.</exception>
        public IPEndPoint RtcpLocalEndPoint
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_Session");
                }

                return (IPEndPoint)m_pRtcpSocket.LocalEndPoint; 
            }
        }

        /// <summary>
        /// Gets RTP session local IP end point.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when RTP_Session is disposed and this property is accessed.</exception>
        public IPEndPoint RtpLocalEndPoint
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_Session");
                }

                return (IPEndPoint)m_pRtpSocket.LocalEndPoint; 
            }
        }

        /// <summary>
        /// Gets this session participants.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when RTP_Session is disposed and this property is accessed.</exception>
        public RTP_Participant[] Participants
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_Session");
                }

                return m_pParticipants.ToArray(); 
            }
        }

        /// <summary>
        /// Gets or sest payload type.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when RTP_Session is disposed and this property is accessed.</exception>
        /// <exception cref="ArgumentException">Is raised when invalid value is passed.</exception>
        public int PayloadType
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_Session");
                }

                return m_PayloadType; 
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_Session");
                }
                if(value < 0 || value > 128){
                    throw new ArgumentException("Payload value must be >= 0 and <= 128.");
                }

                m_PayloadType = value;
            }
        }

        /// <summary>
        /// Gets or sets session bandwidth in bits per second.
        /// </summary>
        /// <exception cref="ArgumentException">Is raised when invalid value is passed.</exception>
        public int Bandwidth
        {
            get{ return m_Bandwidth; }

            set{
                if(value < 9600){
                    throw new ArgumentException("Property 'Bandwidth' value must be >= 9600.");
                }

                m_Bandwidth = value;
            }
        }

        /// <summary>
        /// Gets how many RTP packets this session has sent.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when RTP_Session is disposed and this property is accessed.</exception>
        public long RtpPacketsSent
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_Session");
                }

                return m_RtpPacketsSent;
            }
        }

        /// <summary>
        /// Gets how many RTP bytes this session has sent.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when RTP_Session is disposed and this property is accessed.</exception>
        public long RtpBytesSent
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_Session");
                }

                return m_RtpBytesSent; 
            }
        }

        /// <summary>
        /// Gets how many RTCP packets this session has sent.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when RTP_Session is disposed and this property is accessed.</exception>
        public long RtcpPacketsSent
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_Session");
                }

                return m_RtcpPacketsSent;
            }
        }

        /// <summary>
        /// Gets how many RTCP bytes this session has sent.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when RTP_Session is disposed and this property is accessed.</exception>
        public long RtcpBytesSent
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_Session");
                }

                return m_RtcpBytesSent;
            }
        }

        /// <summary>
        /// Gets RTP remote end point where RTP data is sent.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when RTP_Session is disposed and this property is accessed.</exception>
        public IPEndPoint Target
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_Session");
                }

                return m_pTarget;
            }
        }

        /// <summary>
        /// Gets network maximum transmission unit in bytes. This value must include IP headers too. 
        /// </summary>
        public int MTU
        {
            get{ return m_MTU; }
        }

        /// <summary>
        /// Gets collection of streams what we use to send data to targets.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when RTP_Session is disposed and this property is accessed.</exception>
        public RTP_SenderStreamCollection SendStreams
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_Session");
                }

                return m_pSenderStreams;
            }
        }

        /// <summary>
        /// Gets list of streams from targets where we receive data.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when RTP_Session is disposed and this property is accessed.</exception>
        public RTP_ReceiveStream[] ReceiveStreams
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_Session");
                }

                return m_pReceiveStreams.ToArray();
            }
        }

        /// <summary>
        /// Gets if this RTP session is intial and hasn't sent any RTCP packet.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when RTP_Session is disposed and this property is accessed.</exception>
        public bool Initial
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("RTP_Session");
                }

                return m_Initial;
            }
        }


        /// <summary>
        /// Gets the target RTCP bandwidth, i.e., the total bandwidth that will be used for RTCP 
        /// packets by all members of this session, in octets per second.
        /// </summary>
        internal int RtcpBandwidth
        {
            get{ return (int)(m_Bandwidth * 0.05); }
        }

        /// <summary>
        /// Gets last RTCP interval.
        /// </summary>
        internal DateTime LastRtcpInterval
        {
            get{ return m_LastRtcpInterval; }
        }

        /// <summary>
        /// Gets current RTP session mebers(local send + receive streams) count.
        /// </summary>
        private int MembersCount
        {
            get{ return m_pSenderStreams.Count + m_pReceiveStreams.Count; }
        }

        #endregion

        #region Events Implementation
                
        /// <summary>
        /// This event is raised when new participant has added.
        /// </summary>
        public event RTP_ParticipantEventHandler ParticipantAdded = null;

        #region method OnParticipantAdded

        /// <summary>
        /// Raises event <b>ParticipantAdded</b>.
        /// </summary>
        /// <param name="participant">RTP participant which was added.</param>
        private void OnParticipantAdded(RTP_Participant participant)
        {
            if(this.ParticipantAdded != null){
                this.ParticipantAdded(new RTP_ParticipantEventArgs(participant));
            }
        }

        #endregion

        /// <summary>
        /// This event is raised when new participant has removed.
        /// </summary>
        public event RTP_ParticipantEventHandler ParticipantRemoved = null;

        #region method OnParticipantRemoved

        /// <summary>
        /// Raises event <b>ParticipantRemoved</b>.
        /// </summary>
        /// <param name="participant">RTP participant which was removed.</param>
        private void OnParticipantRemoved(RTP_Participant participant)
        {
            if(this.ParticipantRemoved != null){
                this.ParticipantRemoved(new RTP_ParticipantEventArgs(participant));
            }
        }

        #endregion

        /// <summary>
        /// This event is raised when new RTP packet has received.
        /// </summary>
        public event PacketReceivedEventHandler PacketReceived = null;

        #region method OnPacketReceived

        /// <summary>
        /// Raises event <b>PacketReceived</b>.
        /// </summary>
        /// <param name="stream">Stream which go the packet.</param>
        /// <param name="packet">RTP packet what was received.</param>
        internal void OnPacketReceived(RTP_ReceiveStream stream,RTP_Packet packet)
        {
            if(this.PacketReceived != null){
                RTP_PacketEventArgs eArgs = new RTP_PacketEventArgs(this,stream,packet);
                this.PacketReceived(eArgs);
            }
        }

        #endregion

        internal void OnReceiveStreamPayloadChanged(RTP_ReceiveStream stream,int newPayload)
        {
        }

        /// <summary>
        /// This event is raised when APP packet has received from participant.
        /// </summary>
        public event EventHandler AppPacketReceived = null;

        #endregion

    }
}

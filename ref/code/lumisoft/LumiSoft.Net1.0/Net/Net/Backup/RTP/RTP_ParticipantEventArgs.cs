using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.RTP
{
    /// <summary>
    /// This class provides data to <b>ParticipantAdded</b> and <b>ParticipantRemoved</b> events.
    /// </summary>
    public class RTP_ParticipantEventArgs
    {
        private RTP_Participant m_pParticipant = null;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="participant">RTP participant.</param>
        /// <exception cref="ArgumentNullException">Is raised when argument <b>participant</b> is null.</exception>
        public RTP_ParticipantEventArgs(RTP_Participant participant)
        {
            if(participant == null){
                throw new ArgumentNullException("participant");
            }

            m_pParticipant = participant;
        }


        #region Properties Implementation

        /// <summary>
        /// Gets RTP participant.
        /// </summary>
        public RTP_Participant Participant
        {
            get{ return m_pParticipant; }
        }

        #endregion

    }
}

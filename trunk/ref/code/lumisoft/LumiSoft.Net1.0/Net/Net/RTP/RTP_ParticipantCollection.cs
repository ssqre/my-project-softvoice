using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.RTP
{
    /// <summary>
    /// This class impolementts RTP participant collection.
    /// </summary>
    internal class RTP_ParticipantCollection
    {
        private List<RTP_Participant> m_pParticipants = null;

        /// <summary>
        /// Default constructor.
        /// </summary>
        internal RTP_ParticipantCollection()
        {
            m_pParticipants = new List<RTP_Participant>();
        }


        public void Add()
        {
        }

        public void Remove(int ssrc)
        {
        }
                
        public void Remove(RTP_Participant participant)
        {
        }

        #region method Contains

        /// <summary>
        /// Gets if the collection contains partipant with specified ID.
        /// </summary>
        /// <param name="ssrc">Synchronization source identifier.</param>
        /// <returns>Returns true if contains, otherwise false.</returns>
        public bool Contains(int ssrc)
        {
            lock(this){
                foreach(RTP_Participant participant in m_pParticipants){
                    if(participant.SSRC == ssrc){
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region method ToArray

        /// <summary>
        /// Gets current participants.
        /// </summary>
        /// <returns>Current participants as RTP_Participant[] array.</returns>
        public RTP_Participant[] ToArray()
        {
            return m_pParticipants.ToArray();
        }

        #endregion


        #region Properties Implementation

        /// <summary>
        /// Gets number of participants in the collection.
        /// </summary>
        public int Count
        {
            get{ return 0; }
        }

        #endregion

    }
}

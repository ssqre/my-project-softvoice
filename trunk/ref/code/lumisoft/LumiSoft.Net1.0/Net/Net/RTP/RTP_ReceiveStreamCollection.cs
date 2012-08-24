using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace LumiSoft.Net.RTP
{
    /// <summary>
    /// This class represents RTP session receive streams collection.
    /// </summary>
    public class RTP_ReceiveStreamCollection
    {
        private RTP_Session                        m_pRTP     = null;
        private Dictionary<uint,RTP_ReceiveStream> m_pStreams = null;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="session">Owner RTP session.</param>
        internal RTP_ReceiveStreamCollection(RTP_Session session)
        {
            m_pRTP     = session;
            m_pStreams = new Dictionary<uint,RTP_ReceiveStream>();
        }


        #region method Remove

        /// <summary>
        /// Removes specified stream from the collection.
        /// </summary>
        /// <param name="sourceID">Stream ID.</param>
        internal void Remove(uint sourceID)
        {
            lock(this){
                if(m_pStreams.ContainsKey(sourceID)){
                    m_pStreams.Remove(sourceID);
                }
            }
        }

        #endregion

        #region method Get

        /// <summary>
        /// Gets specified stream. 
        /// </summary>
        /// <param name="remoteEP">Remote end point of the stream. This must be filled only if add = true.</param>
        /// <param name="sourceID">Stream ID.</param>
        /// <param name="add">Specifies if stream is created if it doesn't exist.</param>
        /// <returns>Returns stream or null if no such stream.</returns>
        internal RTP_ReceiveStream Get(IPEndPoint remoteEP,uint sourceID,bool add)
        {
            lock(this){
                RTP_ReceiveStream stream = null;
                if(m_pStreams.ContainsKey(sourceID)){
                    stream = m_pStreams[sourceID];
                }
                else if(add){
                    stream = new RTP_ReceiveStream(m_pRTP,remoteEP,sourceID);
                    m_pStreams.Add(sourceID,stream);
                }
                
                return stream;
            }
        }

        #endregion

        #region method ToArray

        /// <summary>
        /// Copies RTP_ReceiveStream's to new array. Note: This method is thread-safe.
        /// </summary>
        /// <returns>Returns RTP_ReceiveStream's array.</returns>
        public RTP_ReceiveStream[] ToArray()
        {
            lock(m_pStreams){
                RTP_ReceiveStream[] retVal = new RTP_ReceiveStream[m_pStreams.Values.Count];
                m_pStreams.Values.CopyTo(retVal,0);

                return retVal;
            }
        }

        #endregion


        #region Properties Collection

        /// <summary>
        /// Gets number of items in the collection.
        /// </summary>
        public int Count
        {
            get{ return m_pStreams.Count; }
        }

        #endregion

    }
}

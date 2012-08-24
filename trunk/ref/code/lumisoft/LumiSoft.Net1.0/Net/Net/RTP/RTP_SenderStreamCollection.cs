using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.RTP
{
    /// <summary>
    /// This class represents RTP session sender streams collection.
    /// </summary>
    public class RTP_SenderStreamCollection : IEnumerable
    {
        private RTP_Session          m_pRTP     = null;
        private List<RTP_SendStream> m_pStreams = null;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="session">Owner RTP session.</param>
        internal RTP_SenderStreamCollection(RTP_Session session)
        {
            m_pRTP     = session;
            m_pStreams = new List<RTP_SendStream>();
        }


        #region method Add

        /// <summary>
        /// Adds new sender stream.
        /// </summary>
        /// <returns>Returns new created stream.</returns>
        public RTP_SendStream Add()
        {
            RTP_SendStream stream = new RTP_SendStream(m_pRTP);
            m_pStreams.Add(stream);

            return stream;
        }

        #endregion

        #region method Remove

        /// <summary>
        /// Removes specified sender stream from the collection.
        /// </summary>
        /// <param name="stream">RTP sender stream to remove.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>stream</b> is null.</exception>
        public void Remove(RTP_SendStream stream)
        {
            if(stream == null){
                throw new ArgumentNullException("stream");
            }

            if(m_pStreams.Contains(stream)){
                m_pStreams.Remove(stream);
                stream.Dispose();
            }
        }

        #endregion

        #region method ToArray

        /// <summary>
        /// Copies all collection members to new array.
        /// </summary>
        /// <returns>Returns collection members as array.</returns>
        public RTP_SendStream[] ToArray()
        {
            return m_pStreams.ToArray();
        }

        #endregion


        #region interface IEnumerator

        /// <summary>
		/// Gets enumerator.
		/// </summary>
		/// <returns></returns>
		public IEnumerator GetEnumerator()
		{
			return m_pStreams.GetEnumerator();
		}

		#endregion

        #region Properties Implementation

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

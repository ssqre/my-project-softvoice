using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.POP3.Client
{
    /// <summary>
    /// This class represents POP3 client messages collection.
    /// </summary>
    public class POP3_MessageCollection : IEnumerable
    {
        private POP3_Client        m_pPop3Client = null;
        private List<POP3_Message> m_pMessages   = null;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="pop3">Owner POP3 client.</param>
        internal POP3_MessageCollection(POP3_Client pop3)
        {
            m_pPop3Client = pop3;

            m_pMessages = new List<POP3_Message>();
        }


        #region method Add

        /// <summary>
        /// Adds new message to messages collection.
        /// </summary>
        /// <param name="size">Message size in bytes.</param>
        internal void Add(int size)
        {
            m_pMessages.Add(new POP3_Message(m_pPop3Client,m_pMessages.Count + 1,size));
        }

        #endregion


        #region interface IEnumerator

		/// <summary>
		/// Gets enumerator.
		/// </summary>
		/// <returns></returns>
		public IEnumerator GetEnumerator()
		{
			return m_pMessages.GetEnumerator();
		}

		#endregion

        #region Properties Implementation

        /// <summary>
        /// Gets total size of messages, messages marked for deletion are included.
        /// </summary>
        public long TotalSize
        {
            get{ 
                long size = 0;
                foreach(POP3_Message message in m_pMessages){
                    size += message.Size;
                }

                return size; 
            }
        }

        /// <summary>
        /// Gets number of messages in the collection, messages marked for deletion are included.
        /// </summary>
        public int Count
        {
            get{ return m_pMessages.Count; }
        }

        /// <summary>
        /// Gets message from specified index.
        /// </summary>
        /// <param name="index">Message zero based index in the collection.</param>
        /// <exception cref="ArgumentOutOfRangeException">Is raised when index is out of range.</exception>
        public POP3_Message this[int index]
        {
            get{
                if(index < 0 || index > m_pMessages.Count){
                    throw new ArgumentOutOfRangeException();
                }

                return m_pMessages[index]; 
            }
        }

        /// <summary>
        /// Gets message with specified UID value.
        /// </summary>
        /// <param name="uid">Message UID value.</param>
        /// <returns>Returns message or null if message doesn't exist.</returns>
        /// <exception cref="NotSupportedException">Is raised when POP3 server doesn't support UIDL.</exception>
        public POP3_Message this[string uid]
        {
            get{
                if(!m_pPop3Client.IsUidlSupported){
                    throw new NotSupportedException();
                }

                foreach(POP3_Message message in m_pMessages){
                    if(message.UID == uid){
                        return message;
                    }
                }

                return null; 
            }
        }

        #endregion

    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.RTP
{
    /// <summary>
    /// This class holds RTP sender or receiver source description.
    /// </summary>
    public class RTP_SourceDescription
    {        
        private string m_CName    = "";
        private string m_Name     = "";
        private string m_Email    = "";
        private string m_Phone    = "";
        private string m_Location = "";
        private string m_Tool     = "";
        private string m_Note     = "";

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="cname">Canonical End-Point Identifier.</param>
        public RTP_SourceDescription(string cname)
        {
            m_CName= cname;
        }


        #region Properties Implementation

        /// <summary>
        /// Gets Canonical End-Point Identifier.
        /// </summary>
        public string CName
        {
            get{ return m_CName; }
        }

        /// <summary>
        /// Gets the real name, eg. "John Doe".
        /// </summary>
        public string Name
        {
            get{ return m_Name; }
        }

        /// <summary>
        /// Gets email address. For example "John.Doe@example.com".
        /// </summary>
        public string Email
        {
            get{ return m_Email; }
        }

        /// <summary>
        /// Gets phone number. For example "+1 908 555 1212".
        /// </summary>
        public string Phone
        {
            get{ return m_Phone; }
        }

        /// <summary>
        /// Gets location string. It may be geographic address or for example chat room name.
        /// </summary>
        public string Location
        {
            get{ return m_Location; }
        }

        /// <summary>
        /// Gets streaming application name/version.
        /// </summary>
        public string Tool
        {
            get{ return m_Tool; }
        }

        /// <summary>
        /// Gets note text. The NOTE item is intended for transient messages describing the current state
        /// of the source, e.g., "on the phone, can't talk".
        /// </summary>
        public string Note
        {
            get{ return m_Note; }
        }

        #endregion

    }
}

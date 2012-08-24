using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.SIP.Stack
{      
    /// <summary>
    /// This is base class for SIP user agent and SIP proxy core.
    /// </summary>
    public abstract class SIP_Core : IDisposable
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public SIP_Core()
        {
        }


        #region method Dispose

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        public virtual void Dispose()
        {
        }

        #endregion

        #region method OnRequestReceived

        /// <summary>
        /// This method is called when new request is received.
        /// </summary>
        /// <param name="e">Request event arguments.</param>
        public virtual void OnRequestReceived(SIP_RequestReceivedEventArgs e)
        {
        }

        #endregion

        #region method OnResponseReceived

        /// <summary>
        /// This method is called when new response is received.
        /// </summary>
        /// <param name="e">Response event arguments.</param>
        public virtual void OnResponseReceived(SIP_ResponseReceivedEventArgs e)
        {
        }

        #endregion

    }
}

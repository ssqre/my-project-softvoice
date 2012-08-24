using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.SIP.Stack
{
    /// <summary>
    /// This is base class for SIP client and server transaction.
    /// </summary>
    public abstract class SIP_Transaction : IDisposable
    {
        private DateTime             m_CreateTime;
        private string               m_Method          = "";
        private SIP_TransactionState m_TransactionState;
        private bool                 m_CanCreateDialog = false;
        private object               m_pTag            = null;
        private List<SIP_Dialog>     m_pDialogs        = null;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SIP_Transaction()
        {
            m_CreateTime = DateTime.Now;

            m_pDialogs = new List<SIP_Dialog>();
        }

        #region method Dispose

        /// <summary>
        /// Cleans up any resources being used.
        /// </summary>
        public virtual void Dispose()
        {
            this.Canceled       = null;
            this.Terminated     = null;
            this.TimedOut       = null;
            this.TransportError = null;
        }

        #endregion


        #region method Cancel

        /// <summary>
        /// Cancels current transaction.
        /// </summary>
        public abstract void Cancel();

        #endregion


        #region method GetLastProvisionalResponse

        /// <summary>
        /// Gets last provisional(1xx) response from responses collection. Returns null if no provisional responses.
        /// </summary>
        /// <returns>Returns last provisional response or null if none exists.</returns>
        protected SIP_Response GetLastProvisionalResponse()
        {
            for(int i=this.Responses.Length - 1;i>-1;i--){
                if(this.Responses[i].StatusCodeType == SIP_StatusCodeType.Provisional){
                    return this.Responses[i];
                }
            }

            return null;
        }

        #endregion

        #region method GetFinalResponse

        /// <summary>
        /// Gets final(2xx > 699) response from responses collection. Returns null if no final responses.
        /// </summary>
        /// <returns>Returns final response or null if none exists.</returns>
        internal protected SIP_Response GetFinalResponse()
        {
            foreach(SIP_Response response in this.Responses){
                if(response.StatusCodeType != SIP_StatusCodeType.Provisional){
                    return response;
                }
            }

            return null;
        }

        #endregion

        #region method SetRequestMethod

        /// <summary>
        /// Sets transaction request method.
        /// </summary>
        /// <param name="method">SIP request method.</param>
        protected void SetRequestMethod(string method)
        {
            m_Method = method;
        }

        #endregion

        #region method SetTransactionState

        /// <summary>
        /// Sets current transaction state.
        /// </summary>
        /// <param name="state">Transaction new state.</param>
        protected void SetTransactionState(SIP_TransactionState state)
        {
            m_TransactionState = state;
        }

        #endregion


        #region Properties Implementation

        /// <summary>
        /// Gets transaction ID (Via: branch parameter value).
        /// </summary>
        public abstract string ID
        {
            get;
        }

        /// <summary>
        /// Gets time when this transaction was created.
        /// </summary>
        public DateTime CreateTime
        {
            get{ return m_CreateTime; }
        }

        /// <summary>
        /// Gets request method that transaction handles.
        /// </summary>
        public string Method
        {
            get{ return m_Method; }
        }

        /// <summary>
        /// Gets current transaction state.
        /// </summary>
        public SIP_TransactionState TransactionState
        {
            get{ return m_TransactionState; }
        }

        /// <summary>
        /// Gets SIP request what caused to create this transaction.
        /// </summary>
        public abstract SIP_Request Request
        {
            get;
        }

        /// <summary>
        /// Gets transaction related responses.
        /// </summary>
        public abstract SIP_Response[] Responses
        {
            get;
        }

        /// <summary>
        /// Gets or sets if transaction can create SIP dialog.
        /// </summary>
        public bool CanCreateDialog
        {
            get{ return m_CanCreateDialog; }

            set{ m_CanCreateDialog = value; }
        }

        /// <summary>
        /// Gets transaction related SIP dialog. Returns null if no dialog available.
        /// </summary>
        public abstract SIP_Dialog Dialog
        {
            get;
        }

        /// <summary>
        /// Getsif transaction is disposed.
        /// </summary>
        public abstract bool IsDisposed
        {
            get;
        }

        /// <summary>
        /// Gets or sets user data.
        /// </summary>
        public object Tag
        {
            get{ return m_pTag; }

            set{ m_pTag = value; }
        }


        /// <summary>
        /// Gets SIP dialogs what this transaction has created.
        /// </summary>
        internal List<SIP_Dialog> Dialogs
        {
            get{ return m_pDialogs; }
        }

        #endregion

        #region Events Implementation

        /// <summary>
        /// Is raised if transaction is canceled.
        /// </summary>
        public event EventHandler Canceled = null;

        /// <summary>
        /// Raises Canceled event.
        /// </summary>
        protected void OnCanceled()
        {
            if(this.Canceled != null){
                this.Canceled(null,null);
            }
        }

        /// <summary>
        /// Is raised if transaction is terminated.
        /// </summary>
        public event EventHandler Terminated = null;

        /// <summary>
        /// Raises Terminated event.
        /// </summary>
        protected void OnTerminated()
        {
            if(this.Terminated != null){
                this.Terminated(this,new EventArgs());
            }
        }

        /// <summary>
        /// Is raised if transaction is timed out. 
        /// </summary>
        /// <remarks>
        /// TU(transaction user) MUST treat it as if a 408 (Request Timeout) status code 
        /// has been received (more info RFC 3261 8.1.3.).
        /// </remarks>
        public event EventHandler TimedOut = null;

        /// <summary>
        /// Raises TimedOut event.
        /// </summary>
        protected void OnTimedOut()
        {
            if(this.TimedOut != null){
                this.TimedOut(this,new EventArgs());
            }
        }

        /// <summary>
        /// Is raised when there is transport error. 
        /// </summary>
        /// <remarks>
        /// TU(transaction user) MUST treat it as if a 503 (Service Unavailable) status code 
        /// has been received (more info RFC 3261 8.1.3.).
        /// </remarks>
        public event EventHandler TransportError = null;

        /// <summary>
        /// Raises TimedOut event.
        /// </summary>
        protected void OnTransportError()
        {
            if(this.TransportError != null){
                this.TransportError(this,new EventArgs());
            }
        }

        /// <summary>
        /// Is raised when there is transaction error. For example this is raised when server transaction never
        /// gets ACK.
        /// </summary>
        public event EventHandler TransactionError = null;

        /// <summary>
        /// Raises TransactionError event.
        /// </summary>
        /// <param name="errorText">Text describing error.</param>
        protected void OnTransactionError(string errorText)
        {
            if(this.TransactionError != null){
                this.TransactionError(this,new EventArgs());
            }
        }

        #endregion

    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Timers;

using LumiSoft.Net.SIP.Message;

namespace LumiSoft.Net.SIP.Stack
{
    /// <summary>
    /// Implements SIP transaction layer. Defined in RFC 3261.
    /// Transaction layer manages client,server transactions and dialogs.
    /// </summary>
    public class SIP_TransactionLayer : IDisposable
    {        
        private SIP_Stack                   m_pSipStack           = null;
        private List<SIP_ClientTransaction> m_pClientTransactions = null;
        private List<SIP_ServerTransaction> m_pServerTransactions = null;
        private List<SIP_Dialog>            m_pDialogs            = null;
        private Timer                       m_pTimer              = null;
        
        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="sipStack">Reference to SIP stack.</param>
        public SIP_TransactionLayer(SIP_Stack sipStack)
        {
            m_pSipStack = sipStack;

            m_pClientTransactions = new List<SIP_ClientTransaction>();
            m_pServerTransactions = new List<SIP_ServerTransaction>();
            m_pDialogs            = new List<SIP_Dialog>();

            m_pTimer = new Timer(20000);
            m_pTimer.AutoReset = true;
            m_pTimer.Elapsed += new ElapsedEventHandler(m_pTimer_Elapsed);
        }

        #region method Dispose

        /// <summary>
        /// Cleans up any resources being used.
        /// </summary>
        public void Dispose()
        {
            if(m_pTimer != null){
                m_pTimer.Dispose();
                m_pTimer = null;
            }
        }

        #endregion


        #region Events Handling

        #region method m_pTimer_Elapsed

        private void m_pTimer_Elapsed(object sender,ElapsedEventArgs e)
        {
            foreach(SIP_Dialog dialog in m_pDialogs.ToArray()){
                // Terminate early dialog after 5 minutes, noramlly there must be any, but just in case ... .
                if(dialog.DialogState == SIP_DialogState.Early){
                    if(dialog.CreateTime.AddMinutes(5) < DateTime.Now){
                        dialog.Terminate();
                    }
                }
                // 
                else if(dialog.DialogState == SIP_DialogState.Confirmed){
                    // TODO:
                }
            }
        }

        #endregion

        #endregion



        #region method CreateClientTransaction

        /// <summary>
        /// Creates new SIP client transaction for specified request.
        /// </summary>
        /// <param name="request">SIP request.</param>
        /// <param name="target">Remote target info.</param>
        /// <param name="addVia">Specified if transaction adds new Via: header. If this value is false,
        /// then its user responsibility to add valid Via: header to <b>request</b> argument.</param>
        /// <returns>Returns ncreated SIP client transaction.</returns>
        public SIP_ClientTransaction CreateClientTransaction(SIP_Request request,SIP_Target target,bool addVia)
        {
            SIP_ClientTransaction transaction = new SIP_ClientTransaction(m_pSipStack,request,target,addVia);
            m_pClientTransactions.Add(transaction);

            return transaction;
        }

        #endregion

        #region method CreateServerTransaction

        /// <summary>
        /// Creates new SIP server transaction for specified request.
        /// </summary>
        /// <param name="request">SIP request.</param>
        /// <returns>Returns added server transaction.</returns>
        public SIP_ServerTransaction CreateServerTransaction(SIP_Request request)
        {
            SIP_ServerTransaction transaction = new SIP_ServerTransaction(m_pSipStack,request);
            m_pServerTransactions.Add(transaction);

            return transaction;
        }

        #endregion

        #region method RemoveClientTransaction

        /// <summary>
        /// Removes specified transaction from SIP clinet transactions collection.
        /// </summary>
        /// <param name="transaction">Transaction to remove.</param>
        internal void RemoveClientTransaction(SIP_ClientTransaction transaction)
        {
            m_pClientTransactions.Remove(transaction);
        }

        #endregion

        #region method RemoveServerTransaction

        /// <summary>
        /// Removes specified transaction from SIP server transactions collection.
        /// </summary>
        /// <param name="transaction">Transaction to remove.</param>
        internal void RemoveServerTransaction(SIP_ServerTransaction transaction)
        {
            m_pServerTransactions.Remove(transaction);
        }

        #endregion


        #region method MatchClientTransaction

        /// <summary>
        /// Matches SIP response to client transaction. If not matching transaction found, returns null.
        /// </summary>
        /// <param name="response">SIP response to match.</param>
        internal SIP_ClientTransaction MatchClientTransaction(SIP_Response response)
        {
            /* RFC 3261 17.1.3 Matching Responses to Client Transactions.
                1.  If the response has the same value of the branch parameter in
                    the top Via header field as the branch parameter in the top
                    Via header field of the request that created the transaction.

                2.  If the method parameter in the CSeq header field matches the
                    method of the request that created the transaction.  The
                    method is needed since a CANCEL request constitutes a
                    different transaction, but shares the same value of the branch
                    parameter.
            */

            string transactionID = response.Via.GetTopMostValue().Branch + "-" + response.CSeq.RequestMethod;
            foreach(SIP_ClientTransaction transaction in m_pClientTransactions.ToArray()){
                try{
                    if(transactionID == transaction.ID + "-" + transaction.Request.CSeq.RequestMethod){                        
                        return transaction;
                    }
                }
                catch(ObjectDisposedException x){
                    // Skip, transaction has Disposed(Terminated).
                    string dummy = x.Message;
                }
            }
            // m_pSipStack.Logger.AddDebug("No matching transaction for response branch='" + response.Via.GetTopMostValue().Branch + "': " + response.StatusCode_ReasonPhrase);

            return null;
        }

        #endregion

        #region method MatchServerTransaction

        /// <summary>
        /// Matches SIP request to server transaction. If not matching transaction found, returns null.
        /// </summary>
        /// <param name="request">SIP request to match.</param>
        internal SIP_ServerTransaction MatchServerTransaction(SIP_Request request)
        {
            /* RFC 3261 17.2.3 Matching Requests to Server Transactions.
                This matching rule applies to both INVITE and non-INVITE transactions.

                1. the branch parameter in the request is equal to the one in the top Via header 
                   field of the request that created the transaction, and

                2. the sent-by value in the top Via of the request is equal to the
                   one in the request that created the transaction, and

                3. the method of the request matches the one that created the transaction, except 
                   for ACK, where the method of the request that created the transaction is INVITE.
            */
                        
            string transactionID = request.Via.GetTopMostValue().Branch;
            string sentBy        = request.Via.GetTopMostValue().SentBy;
            foreach(SIP_ServerTransaction transaction in m_pServerTransactions.ToArray()){
                try{
                    if(transactionID == transaction.ID && transaction.Request.Via.GetTopMostValue().SentBy == sentBy){
                        // ACK may not used for method matching, otherwise it never matches.
                        if(request.Method == SIP_Methods.ACK){
                            // We linger INVITE server transaction in terminated state, so we may not
                            // forward ACK to terminated transaction, ACK must be handled by dialog or TU.
                            if(transaction.TransactionState == SIP_TransactionState.Terminated){
                                return null;
                            }
                            else{
                                return transaction;
                            }
                        }
                        // CANCEL also won't match method.
                        else if(request.Method == SIP_Methods.CANCEL){
                            return transaction;
                        }
                        else if(request.Method == transaction.Request.Method){
                            return transaction;
                        }
                    }
                }
                catch(ObjectDisposedException x){
                    // Skip, transaction has Disposed(Terminated).
                    string dummy = x.Message;
                }
            }
            return null;
        }

        #endregion


        #region method EnsureDialog

        /// <summary>
        /// Ensures that SIP dialog exists. If dialog doesnt exist, it will be created.
        /// </summary>
        /// <param name="transaction">Owner transaction what forces to create dialog.</param>
        /// <param name="response">Response what forces to create dialog.</param>
        /// <param name="dialog">Argument where to store SIP dialog.</param>
        /// <returns>Returns true if dialog created, false if existing dialog returned.</returns>
        internal bool EnsureDialog(SIP_Transaction transaction,SIP_Response response,out SIP_Dialog dialog)
        {            
            string dialogID = "";
            if(transaction is SIP_ServerTransaction){
                dialogID = response.CallID + "-" + response.To.ToTag + "-" + response.From.Tag;
            }
            else{
                dialogID = response.CallID + "-" + response.From.Tag + "-" + response.To.ToTag;                
            }

            lock(m_pDialogs){
                foreach(SIP_Dialog d in m_pDialogs){
                    if(d.DialogID == dialogID){
                        dialog = d;
                        return false;
                    }
                }

                dialog = new SIP_Dialog(m_pSipStack,transaction,response);
                m_pDialogs.Add(dialog);

                return true;
            }
        }

        #endregion


        #region method RemoveDialog

        /// <summary>
        /// Removes specified dialog from dialogs collection.
        /// </summary>
        /// <param name="dialog">SIP dialog to remove.</param>
        internal void RemoveDialog(SIP_Dialog dialog)
        {
            lock(m_pDialogs){
                m_pDialogs.Remove(dialog);
            }
        }

        #endregion

        #region method MatchDialog

        /// <summary>
        /// Matches specified SIP request to SIP dialog. If no matching dialog found, returns null.
        /// </summary>
        /// <param name="request">SIP request.</param>
        /// <returns>Returns matched SIP dialog or null in no match found.</returns>
        internal SIP_Dialog MatchDialog(SIP_Request request)
        {
            try{
                string callID  = request.CallID;
                string toTag   = request.To.ToTag;
                string fromTag = request.From.Tag;                                        
                if(callID != null && toTag != null && fromTag != null){
                    string dialogID = callID + "-" + toTag + "-" + fromTag;
                    lock(m_pDialogs){
                        foreach(SIP_Dialog dialog in m_pDialogs){
                            if(dialogID == dialog.DialogID){
                                return dialog;
                            }
                        }
                    }
                }
            }
            catch{
            }

            return null;
        }

        /// <summary>
        /// Matches specified SIP response to SIP dialog. If no matching dialog found, returns null.
        /// </summary>
        /// <param name="response">SIP response.</param>
        /// <returns>Returns matched SIP dialog or null in no match found.</returns>
        internal SIP_Dialog MatchDialog(SIP_Response response)
        {
            try{
                string callID  = response.CallID;
                string fromTag = response.From.Tag; 
                string toTag   = response.To.ToTag;                                       
                if(callID != null && fromTag != null && toTag != null){
                    string dialogID = callID + "-" + fromTag + "-" + toTag;
                    lock(m_pDialogs){
                        foreach(SIP_Dialog dialog in m_pDialogs){
                            if(dialogID == dialog.DialogID){
                                return dialog;
                            }
                        }
                    }
                }
            }
            catch{
            }

            return null;
        }

        #endregion



        #region Properties Implementation

        /// <summary>
        /// Gets all available client transactions.
        /// </summary>
        public List<SIP_ClientTransaction> ClientTransactions
        {
            get{ return m_pClientTransactions; }
        }

        /// <summary>
        /// Gets all available server transactions.
        /// </summary>
        public List<SIP_ServerTransaction> ServerTransactions
        {
            get{ return m_pServerTransactions; }
        }

        /// <summary>
        /// Gets active dialogs.
        /// </summary>
        public SIP_Dialog[] Dialogs
        {
            get{ return m_pDialogs.ToArray(); }
        }

        #endregion

    }
}

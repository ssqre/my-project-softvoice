using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;
using System.Net;
using System.Net.Sockets;

using LumiSoft.Net.SIP.Message;

namespace LumiSoft.Net.SIP.Stack
{
    #region Delegates

    /// <summary>
    /// Represents method what will handle ResponseReceived event.
    /// </summary>
    /// <param name="e">Event data.</param>
    public delegate void SIP_ResponseReceivedEventHandler(SIP_ResponseReceivedEventArgs e);

    #endregion

    /// <summary>
    /// A SIP_Transaction represents SIP client transaction. Defined in RFC 3261 17.1 Client Transaction.
    /// A transaction is a sequence of SIP messages exchanged between SIP network elements. 
    /// Client transaction is created by UAC,UAS or proxy core. Client transaction is responsible for
    /// sending request to remote UA and processing remote UA server transaction responses.
    /// Client transaction causes remote UA to create corresponding server transaction.
    /// </summary>
    /// <remarks>
    /// <img src="../images/SIP_ClientTransaction.gif" />
    /// </remarks>
    public class SIP_ClientTransaction : SIP_Transaction
    {           
        private SIP_Stack                  m_pSipStack                = null;
        private string                     m_ID                       = "";
        private SIP_Request                m_pRequest                 = null;
        private SIP_Target                 m_pTarget                  = null;
        private Timer                      m_pTimerA                  = null;
        private Timer                      m_pTimerB                  = null;
        private Timer                      m_pTimerD                  = null;
        private Timer                      m_pTimerE                  = null;
        private Timer                      m_pTimerF                  = null;
        private Timer                      m_pTimerK                  = null;
        private Timer                      m_pTransactionTimeoutTimer = null;
        private Timer                      m_pTimerDisposeLinger      = null;
        private int                        m_Timeout                  = 60;
        private bool                       m_Started                  = false;
        private bool                       m_CancelQueued             = false;
        private SIP_ServerTransaction      m_pServerTransaction       = null;
        private List<SIP_Response>         m_pResponses               = null;
        private SIP_Dialog                 m_pDialog                  = null;
        private bool                       m_IsDisposed               = false;
        private int                        m_RSeq                     = -1;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="sipStack">Reference to SIP stack.</param>
        /// <param name="request">SIP request to what caused to create client transaction.</param>
        /// <param name="target">Remote target info.</param>
        /// <param name="addVia">Specified if transaction adds new Via: header. If this value is false,
        /// then its user responsibility to add valid Via: header to <b>request</b> argument.</param>
        internal SIP_ClientTransaction(SIP_Stack sipStack,SIP_Request request,SIP_Target target,bool addVia)
        {
            m_pSipStack = sipStack;
            m_pRequest  = request;
            m_pTarget   = target;
            SetRequestMethod(request.Method);
                        
            m_pResponses = new List<SIP_Response>();

            // Add Via: header field.
            if(addVia){
                m_ID = SIP_t_ViaParm.CreateBranch();
                SIP_t_ViaParm via = new SIP_t_ViaParm();
                via.ProtocolName = "SIP";
                via.ProtocolVersion = "2.0";
                via.ProtocolTransport = m_pTarget.Transport;
                via.SentBy = "transport_layer_will_replace_it";
                via.Branch = m_ID;
                via.RPort = 0;
                request.Via.AddToTop(via.ToStringValue());
            }
            // User provided Via:.
            else{
                // Validate Via:
                SIP_t_ViaParm via = request.Via.GetTopMostValue();
                if(via == null){
                    throw new ArgumentException("Via: header is missing !");
                }
                if(via.Branch == null){
                    throw new ArgumentException("Via: header 'branch' prameter is missing !");
                }

                m_ID = via.Branch;
            }
        }

        #region method Dispose

        /// <summary>
        /// Disposes transaction and cleans up all resources.
        /// </summary>
        public override void Dispose()
        {
            if(m_IsDisposed){
                return;
            }
            m_IsDisposed = true;
                        
            try{
                SetTransactionState(SIP_TransactionState.Terminated);

                base.Dispose();

                this.ResponseReceived = null;

                if(m_pTimerA != null){
                    m_pTimerA.Dispose();
                    m_pTimerA = null;
                }

                if(m_pTimerB != null){
                    m_pTimerB.Dispose();
                    m_pTimerB = null;
                }

                if(m_pTimerD != null){
                    m_pTimerD.Dispose();
                    m_pTimerD = null;
                }

                if(m_pTimerE != null){
                    m_pTimerE.Dispose();
                    m_pTimerE = null;
                }

                if(m_pTimerF != null){
                    m_pTimerF.Dispose();
                    m_pTimerF = null;
                }

                if(m_pTimerK != null){
                    m_pTimerK.Dispose();
                    m_pTimerK = null;
                }

                if(m_pTransactionTimeoutTimer != null){
                    m_pTransactionTimeoutTimer.Dispose();
                    m_pTransactionTimeoutTimer = null;
                }

                if(m_pTimerDisposeLinger != null){
                    m_pTimerDisposeLinger.Dispose();
                    m_pTimerDisposeLinger = null;
                }

                m_pResponses = null;
                               
                // Log
                m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) disposed.");
            }
            finally{
                // Remove from transactions collection.
                m_pSipStack.TransactionLayer.RemoveClientTransaction(this);

                OnTerminated();
            }            
        }

        #endregion

        
        #region method Begin

        /// <summary>
        /// Call this method to start transaction.
        /// </summary>
        /// <exception cref="InvalidOperationException">Is called when Begin is called multiple times.</exception>
        public void Begin()
        {
            if(m_Started){
                throw new InvalidOperationException("Transaction already started !");
            }
            m_Started = true;

            // Log
            m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) created.");

            #region INVITE

            // INVITE transaction
            if(m_pRequest.Method == "INVITE"){
                /* RFC 3261 17.1.1.2.
                    If an unreliable transport is being used, the client transaction MUST start 
                    timer A with a value of T1. (Timer A controls request retransmissions).  
                */
                if(m_pRequest.Via.GetTopMostValue().ProtocolTransport.ToUpper() == "UDP"){
                    m_pTimerA = new Timer(SIP_TimerConstants.T1);
                    m_pTimerA.Elapsed += new ElapsedEventHandler(m_pTimerA_Elapsed);
                    m_pTimerA.Enabled = true;
                    // Log
                    m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer A(requst retransmit timer) started, will triger after " + SIP_TimerConstants.T1 + ".");
                }

                /* RFC 3261 17.1.1.2.
                    For any transport, the client transaction MUST start timer B with a value
                    of 64*T1 seconds (Timer B controls transaction timeouts).
                */
                m_pTimerB = new Timer(64 * SIP_TimerConstants.T1);
                m_pTimerB.AutoReset = false;
                m_pTimerB.Elapsed += new ElapsedEventHandler(m_pTimerB_Elapsed);
                m_pTimerB.Enabled = true;
                // Log
                m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer B(calling state timeout timer) started, will triger after " + 64 * SIP_TimerConstants.T1 + ".");
                
                // Set transaction state.
                SetTransactionState(SIP_TransactionState.Calling);
            }

            #endregion

            #region non-INVITE

            // Non-INVITE transaction
            else{
                /* RFC 3261 17.1.2.2.
                    If an unreliable transport is being used, the client transaction MUST start 
                    timer E with a value of T1. (Timer E controls request retransmissions).  
                */
                if(m_pRequest.Via.GetTopMostValue().ProtocolTransport.ToUpper() == "UDP"){
                    m_pTimerE = new Timer(SIP_TimerConstants.T1);
                    m_pTimerE.Elapsed += new ElapsedEventHandler(m_pTimerE_Elapsed);
                    m_pTimerE.Enabled = true;
                    // Log
                    m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer E(Non-INVITE request retransmission interval) started, will triger after " + SIP_TimerConstants.T1 + ".");
                }
                
                /* RFC 3261 17.1.2.2.
                    For any transport, the client transaction MUST start timer F with a value
                    of 64*T1 seconds (Timer F controls transaction timeouts).
                */
                m_pTimerF = new Timer(64 * SIP_TimerConstants.T1);
                m_pTimerF.AutoReset = false;
                m_pTimerF.Elapsed += new ElapsedEventHandler(m_pTimerF_Elapsed);
                m_pTimerF.Enabled = true;
                // Log
                m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer F(Non-INVITE trying state timeout timer) started, will triger after " + 64 * SIP_TimerConstants.T1 + ".");

                // Set transaction state.
                SetTransactionState(SIP_TransactionState.Trying);
            }

            #endregion

            //--- Common for all transactions
                        
            // This is just timout timer and ensures that transaction ends or will terminated.
            m_pTransactionTimeoutTimer = new Timer(m_Timeout * 1000);
            m_pTransactionTimeoutTimer.Elapsed += new ElapsedEventHandler(m_pTransactionTimeoutTimer_Elapsed);
            m_pTransactionTimeoutTimer.Enabled = true;
            // Log
            m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Transcation timeout timer started,timeout after " + m_pTransactionTimeoutTimer.Interval + " ms");

            try{
                // Transmits intial request to destination recipient.
                m_pSipStack.TransportLayer.SendRequest(m_pRequest,m_pTarget);
            }
            catch{
                // Log
                m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) transport error.");

                OnTransportError();
                Dispose();
            }
        }
                                                
        #endregion

        #region method Cancel

        /// <summary>
        /// Cancels transaction.
        /// </summary>
        /// <remarks>
        /// Cancel behaviour:
        ///     *) If non-INVITE method, skip cancel.
        ///     *) If has got final response, skip cancel because there is nothing to cancel.
        ///     *) If transaction has got provisional response, send cancel.
        ///     *) If transaction hasn't got provisional response, queue cancel request.
        ///          If provisional response received, send cancel.
        ///          If final response received, skip cancel because there is nothing to cancel.
        /// 
        /// NOTE: Canceled event is raised only if '478 Request Terminated' is received from calee !
        /// </remarks>
        public override void Cancel()
        {  
            /* RFC 3261 9.2.
                A CANCEL request SHOULD NOT be sent to cancel a request other than INVITE.
                If no provisional response has been received, the CANCEL request MUST NOT be sent; rather, 
                the client MUST wait for the arrival of a provisional response before sending the request. 
                If the original request has generated a final response, the CANCEL SHOULD NOT be sent, 
                as it is an effective no-op, since CANCEL has no effect on requests that have already 
                generated a final response.
            */

            lock(this){
                // Transcation disposed, nothing to cancel.
                if(m_IsDisposed){
                    return;
                }
                // We have already pending cancel.
                if(m_CancelQueued){
                    return;
                }

                if(m_pRequest.Method == SIP_Methods.INVITE){
                    DoCancel();
                }
            }
        }

        #endregion


        #region method m_pTimerA_Elapsed

        /// <summary>
        /// Is called when INVITE request retransission must be done.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_pTimerA_Elapsed(object sender,ElapsedEventArgs e)
        {
            /* RFC 3261 17.1.1.2.
                When timer A fires, the client transaction MUST retransmit the request by passing 
                it to the transport layer, and MUST reset the timer with a value of 2*T1.
            */
            
            try{
                // Retransmit intial request
                m_pSipStack.TransportLayer.SendRequest(m_pRequest,m_pTarget);

                // Update interval.
                m_pTimerA.Interval = m_pTimerA.Interval * 2;
            
                // Log
                m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer A(INVITE request retransmission) triggered,next transmit after " + m_pTimerA.Interval + " ms.");
            }
            catch{
                // Log
                m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) transport error.");

                OnTransportError();
                Dispose();
            }
        }

        #endregion

        #region method m_pTimerB_Elapsed

        /// <summary>
        /// This method is raised when INVITE transaction calling state didn't get any response.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_pTimerB_Elapsed(object sender,ElapsedEventArgs e)
        {
            /* RFC 3261 17.1.1.2.
                If the client transaction is still in the "Calling" state when timer
                B fires, the client transaction SHOULD inform the TU that a timeout
                has occurred.  The client transaction MUST NOT generate an ACK.
            */

            m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer B (INVITE transaction calling state timeout timer) triggered.");

            OnTimedOut();
            Dispose();
        }

        #endregion

        #region method m_pTimerD_Elapsed

        /// <summary>
        /// Is called when INVITE completed state linger time ended.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_pTimerD_Elapsed(object sender,ElapsedEventArgs e)
        {
            /* RFC 3261 17.1.1.2.
                If timer D fires while the client transaction is in the "Completed"
                state, the client transaction MUST move to the terminated state.
            */

            m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer D (INVITE completed state linger timer) triggered.");

            Dispose();
        }

        #endregion

        #region method m_pTimerE_Elapsed

        /// <summary>
        /// Is called when non-INVITE request retransission must be done.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_pTimerE_Elapsed(object sender,ElapsedEventArgs e)
        {                      
            /* RFC 3261 17.1.2.2.
                When the timer fires again, it is reset to a MIN(4*T1,T2).
                For the default values of T1 and T2, this results in
                intervals of 500 ms, 1 s, 2 s, 4 s, 4 s, 4 s, etc.
            */

            // Retransmit intial request
            m_pSipStack.TransportLayer.SendRequest(m_pRequest,m_pTarget);

            // Update interval
            m_pTimerE.Interval = Math.Min(m_pTimerE.Interval * 2,SIP_TimerConstants.T2);

            // Log
            m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer E (retransission timer) triggered,next transmit after " + m_pTimerE.Interval + " ms.");            
        }

        #endregion

        #region method m_pTimerF_Elapsed

        /// <summary>
        /// This method is raised when non-INVITE transaction trying or proceeding state didn't get any response.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_pTimerF_Elapsed(object sender,ElapsedEventArgs e)
        {
            /* RFC 3261 17.1.2.2.
                If Timer F fires while the client transaction is still in the
                "Trying" state, the client transaction SHOULD inform the TU about the
                timeout, and then it SHOULD enter the "Terminated" state.
            */

            m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer F (non-INVITE transaction trying or proceeding state timeout timer) triggered.");

            OnTimedOut();
            Dispose();
        }

        #endregion

        #region method m_pTimerK_Elapsed

        /// <summary>
        /// This method is called when non-INVITE completed state linger time ended.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_pTimerK_Elapsed(object sender,ElapsedEventArgs e)
        {
            /* RFC 3261 17.1.2.2.
                If Timer K fires while in completed state, the client transaction
                MUST transition to the "Terminated" state.
            */

            m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer K (non-INVITE completed state linger timer) triggered.");

            Dispose();
        }

        #endregion

        #region method m_pTransactionTimeoutTimer_Elapsed

        /// <summary>
        /// Is called when transaction has timed out.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_pTransactionTimeoutTimer_Elapsed(object sender,ElapsedEventArgs e)
        {
            // Log
            m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Transaction timeout timer triggered.");
                        
            OnTimedOut();
            
            /* We may not dispose at once, with normal INVITE cancel we must get '487 Request Terminated'.
               We linger 5 sec before dispose, in that time we must get '487 Request Terminated'.
               Don't know if thats best/clearest way but, that will do the job. */
            if(m_pRequest.Method == SIP_Methods.INVITE){
                Cancel();

                m_pTimerDisposeLinger = new Timer(5000);
                m_pTimerDisposeLinger.AutoReset = false;
                m_pTimerDisposeLinger.Elapsed += new ElapsedEventHandler(m_pTimerDisposeLinger_Elapsed);
                m_pTimerDisposeLinger.Enabled = true;
            }
            else{
                Dispose();
            }
        }
                
        #endregion

        #region method m_pTimerDisposeLinger_Elapsed

        /// <summary>
        /// This timer is called when Dispose linger timer ended, we now just need to dispose transaction.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_pTimerDisposeLinger_Elapsed(object sender,ElapsedEventArgs e)
        {
            Dispose();
        }

        #endregion


        #region method ProcessResponse

        /// <summary>
        /// Processes recipient returned response.
        /// </summary>
        /// <param name="response">SIP reponse what is returned from destination recipient.</param>
        internal void ProcessResponse(SIP_Response response)
        {   
            /* NOTES:
                We may not change transaction state before raising "ResponseRecievived" event,
                otherwise "proxy context" choose best final response will work wrong.
            */

            // Log
            m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) got response response='" + response.StatusCode + "'.");

            lock(this){
                /* RFC 3261 9.1.
                    If cancel queued, then we have Calling state, don't process non-final response.
                    Send cancel, we must get '478 Request terminated' or then timer B will timeout.
                    If final reponse process, skip cancel, because nothing to cancel.
                */
                if(m_CancelQueued){
                    m_CancelQueued = false;
                    if(response.StatusCodeType == SIP_StatusCodeType.Provisional){
                        DoCancel();
                        return;
                    }
                }

                // If '478 Request Terminated' Raise Canceled event, dispose.
                if(response.StatusCode == 478){
                    OnCanceled();
                    Dispose();
                    return;
                }

                #region INVITE

                // INVITE transaction
                if(m_pRequest.Method == "INVITE"){
                                        
                    #region Calling

                    // Calling state
                    if(this.TransactionState == SIP_TransactionState.Calling){
                        // If there is timer A (request retransmit timer), stop it,because we got response.
                        if(m_pTimerA != null){
                            m_pTimerA.Dispose();
                            m_pTimerA = null;
                            // Log
                            m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer A (retransmit timer) stopped.");
                        }
                        // If there is timer B (calling state timeout timer), stop it because we got response.
                        if(m_pTimerB != null){
                            m_pTimerB.Dispose();
                            m_pTimerB = null;
                            // Log
                            m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer B (calling state timeout timer) stopped.");
                        }

                        // 1xx response
                        if(response.StatusCodeType == SIP_StatusCodeType.Provisional){                            
                            m_pResponses.Add(response);
                            SetTransactionState(SIP_TransactionState.Proceeding);

                            // 101 - 199 create early dialog
                            if(response.StatusCode > 100){
                                EnsureDialog(response);
                            }

                            // Raise ResponseReceived event.
                            OnResponseReceived(response);                            
                        }
                        // 2xx response
                        else if(response.StatusCodeType == SIP_StatusCodeType.Success){                            
                            m_pResponses.Add(response);

                            EnsureDialog(response);

                            // Raise ResponseReceived event.
                            OnResponseReceived(response);
                            
                            SetTransactionState(SIP_TransactionState.Terminated);
                            Dispose();
                        }
                        // 300 - 699 response
                        else{                            
                            m_pResponses.Add(response);

                            // Raise ResponseReceived event.
                            OnResponseReceived(response);

                            // Send ACK
                            SendAck(response);

                            /* RFC 3261 17.1.1.2.
                                The client transaction SHOULD start timer D when it enters the "Completed" state, 
                                with a value of at least 32 seconds for unreliable transports.
                            */
                            if(response.Via.GetTopMostValue().ProtocolTransport.ToUpper() == "UDP"){
                                m_pTimerD = new Timer(64 * SIP_TimerConstants.T1);
                                m_pTimerD.AutoReset = false;
                                m_pTimerD.Elapsed += new ElapsedEventHandler(m_pTimerD_Elapsed);
                                m_pTimerD.Enabled = true;
                                // Log
                                m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer D(linger timer for retransimted negative final responses) started, will triger after " + m_pTimerD.Interval + ".");
                            }

                            SetTransactionState(SIP_TransactionState.Completed);
                        }
                    }

                    #endregion

                    #region Proceeding

                    // Proceeding state
                    else if(this.TransactionState == SIP_TransactionState.Proceeding){
                        // 1xx response
                        if(response.StatusCodeType == SIP_StatusCodeType.Provisional){
                            m_pResponses.Add(response);

                            // 101 - 199 create early dialog
                            if(response.StatusCode > 100){
                                EnsureDialog(response);
                            }

                            // Raise ResponseReceived event.
                            OnResponseReceived(response);
                        }
                        // 2xx reponse
                        else if(response.StatusCodeType == SIP_StatusCodeType.Success){                            
                            m_pResponses.Add(response);

                            EnsureDialog(response);

                            // Raise ResponseReceived event.
                            OnResponseReceived(response);
                            
                            SetTransactionState(SIP_TransactionState.Terminated);
                            Dispose();
                        }
                        // 300 - 699 response
                        else{                            
                            m_pResponses.Add(response);

                            // Raise ResponseReceived event.
                            OnResponseReceived(response);

                            // Send ACK
                            SendAck(response);

                            /* RFC 3261 17.1.1.2.
                                The client transaction SHOULD start timer D when it enters the "Completed" state, 
                                with a value of at least 32 seconds for unreliable transports.
                            */
                            if(response.Via.GetTopMostValue().ProtocolTransport.ToUpper() == "UDP"){
                                m_pTimerD = new Timer(64 * SIP_TimerConstants.T1);
                                m_pTimerD.AutoReset = false;
                                m_pTimerD.Elapsed += new ElapsedEventHandler(m_pTimerD_Elapsed);
                                m_pTimerD.Enabled = true;
                                // Log
                                m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer D(linger timer for retransimted negative final responses) started, will triger after " + m_pTimerD.Interval + ".");
                            }
                
                            SetTransactionState(SIP_TransactionState.Completed);
                        }
                    }

                    #endregion

                    #region Completed

                    // Completed state
                    else if(this.TransactionState == SIP_TransactionState.Completed){                    
                        // 1xx - 299 response
                        if(response.StatusCode >= 100 || response.StatusCode <= 299){
                            // Do nothing
                        }
                        // 300 - 699 response
                        else{
                            // Send ACK
                            SendAck(response);
                        }
                    }
 
                    #endregion

                    #region Terminated

                    // Terminated state
                    else if(this.TransactionState == SIP_TransactionState.Terminated){
                        // We should never reach here, but if so, do nothing.
                    }

                    #endregion
                }

                #endregion

                #region Non-INVITE

                // Non-INVITE transaction
                else{

                    #region Trying

                    // Trying state
                    if(this.TransactionState == SIP_TransactionState.Trying){
                        // 1xx response
                        if(response.StatusCodeType == SIP_StatusCodeType.Provisional){                            
                            m_pResponses.Add(response);

                            // Raise ResponseReceived event.
                            OnResponseReceived(response);

                            SetTransactionState(SIP_TransactionState.Proceeding);
                        }
                        // 200-699  response
                        else{                            
                            m_pResponses.Add(response);

                            // If there is timer E (retransmit timer), stop it,because we got response.
                            if(m_pTimerE != null){
                                m_pTimerE.Dispose();
                                m_pTimerE = null;

                                // Log
                                m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer E (retransmit timer) stopped.");
                            }
                            // If there is timer F (trying,proceeding state timeout timer), stop it because we got response.
                            if(m_pTimerF != null){
                                m_pTimerF.Dispose();
                                m_pTimerF = null;
                                // Log
                                m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer F (trying,proceeding state timeout timer) stopped.");
                            }

                            // Raise ResponseReceived event.
                            OnResponseReceived(response);

                            /* RFC 3261 17.1.1.2.
                                Once the client transaction enters the "Completed" state, it MUST set
                                Timer K to fire in T4 seconds for unreliable transports.
                            */
                            if(response.Via.GetTopMostValue().ProtocolTransport.ToUpper() == "UDP"){
                                m_pTimerK = new Timer(SIP_TimerConstants.T4);
                                m_pTimerK.AutoReset = false;
                                m_pTimerK.Elapsed += new ElapsedEventHandler(m_pTimerK_Elapsed);
                                m_pTimerK.Enabled = true;
                                // Log
                                m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer K(linger timer for retransimted final responses) started, will triger after " + m_pTimerK.Interval + ".");
                            }

                            SetTransactionState(SIP_TransactionState.Completed);
                        }
                    }

                    #endregion

                    #region Proceeding

                    // Proceeding state
                    else if(this.TransactionState == SIP_TransactionState.Proceeding){
                        // 1xx response
                        if(response.StatusCodeType == SIP_StatusCodeType.Provisional){
                            m_pResponses.Add(response);

                            // Raise ResponseReceived event.
                            OnResponseReceived(response);
                        }
                        // 200-699 response
                        else{                            
                            m_pResponses.Add(response);

                            // If there is timer E (retransmit timer), stop it,because we got response.
                            if(m_pTimerE != null){
                                m_pTimerE.Dispose();
                                m_pTimerE = null;

                                // Log
                                m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer E (retransmit timer) stopped.");
                            }
                            // If there is timer F (trying,proceeding state timeout timer), stop it because we got response.
                            if(m_pTimerF != null){
                                m_pTimerF.Dispose();
                                m_pTimerF = null;
                                // Log
                                m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer F (trying,proceeding state timeout timer) stopped.");
                            }

                            // Raise ResponseReceived event.
                            OnResponseReceived(response);

                            /* RFC 3261 17.1.1.2.
                                Once the client transaction enters the "Completed" state, it MUST set
                                Timer K to fire in T4 seconds for unreliable transports.
                            */
                            if(response.Via.GetTopMostValue().ProtocolTransport.ToUpper() == "UDP"){
                                m_pTimerK = new Timer(SIP_TimerConstants.T4);
                                m_pTimerK.AutoReset = false;
                                m_pTimerK.Elapsed += new ElapsedEventHandler(m_pTimerK_Elapsed);
                                m_pTimerK.Enabled = true;
                                // Log
                                m_pSipStack.Logger.AddDebug("Transaction(id='" + m_ID + "' method=" + m_pRequest.Method + " server=false) Timer K(linger timer for retransimted final responses) started, will triger after " + m_pTimerK.Interval + ".");
                            }

                            SetTransactionState(SIP_TransactionState.Completed);
                        }
                    }

                    #endregion

                    #region Completed

                    // Completed state
                    else if(this.TransactionState == SIP_TransactionState.Completed){
                        // Do nothing
                    }

                    #endregion

                    #region Terminated

                    // Terminated state
                    else if(this.TransactionState == SIP_TransactionState.Terminated){
                        // We should never reach here, but if so, do nothing.
                    }

                    #endregion
                }

                #endregion

            }
        }
                                                
        #endregion


        #region method SendAck

        /// <summary>
        /// Sends ACK to recipient.
        /// </summary>
        /// <param name="response">SIP response which to generate ACK.</param>
        private void SendAck(SIP_Response response)
        {
            /* RFC 3261 17.1.1.3 Construction of the ACK Request.
                The ACK request constructed by the client transaction MUST contain
                values for the Call-ID, From, and Request-URI that are equal to the
                values of those header fields in the request passed to the transport
                by the client transaction (call this the "original request").  The To
                header field in the ACK MUST equal the To header field in the
                response being acknowledged, and therefore will usually differ from
                the To header field in the original request by the addition of the
                tag parameter.  The ACK MUST contain a single Via header field, and
                this MUST be equal to the top Via header field of the original
                request.  The CSeq header field in the ACK MUST contain the same
                value for the sequence number as was present in the original request,
                but the method parameter MUST be equal to "ACK".
              
                If the INVITE request whose response is being acknowledged had Route
                header fields, those header fields MUST appear in the ACK.  This is
                to ensure that the ACK can be routed properly through any downstream
                stateless proxies.
            */

            SIP_Request ack = new SIP_Request();
            ack.Method = "ACK";
            ack.Uri = m_pRequest.Uri;
            ack.Via.AddToTop(m_pRequest.Via.GetTopMostValue().ToStringValue());
            ack.CallID = m_pRequest.CallID;
            ack.From = m_pRequest.From;
            ack.To = response.To;
            ack.CSeq = new SIP_t_CSeq(m_pRequest.CSeq.SequenceNumber,"ACK");
            foreach(SIP_HeaderField h in response.Header.Get("Route:")){
                ack.Header.Add("Route:",h.Value);
            }
            ack.MaxForwards = 70;

            // Send request to recipient.
            m_pSipStack.TransportLayer.SendRequest(ack,m_pTarget);
        }

        #endregion

        #region method DoCancel

        /// <summary>
        /// Cancels transaction as RFC 3261 specifies.
        /// </summary>
        private void DoCancel()
        {
            /*
                The following procedures are used to construct a CANCEL request.  The
                Request-URI, Call-ID, To, the numeric part of CSeq, and From header
                fields in the CANCEL request MUST be identical to those in the
                request being cancelled, including tags.  A CANCEL constructed by a
                client MUST have only a single Via header field value matching the
                top Via value in the request being cancelled.  Using the same values
                for these header fields allows the CANCEL to be matched with the
                request it cancels (Section 9.2 indicates how such matching occurs).
                However, the method part of the CSeq header field MUST have a value
                of CANCEL.  This allows it to be identified and processed as a
                transaction in its own right (See Section 17).

                If the request being cancelled contains a Route header field, the
                CANCEL request MUST include that Route header field's values.

                  This is needed so that stateless proxies are able to route CANCEL
                  requests properly.

                The CANCEL request MUST NOT contain any Require or Proxy-Require
                header fields.

                Once the CANCEL is constructed, the client SHOULD check whether it
                has received any response (provisional or final) for the request
                being cancelled (herein referred to as the "original request").
                If no provisional response has been received, the CANCEL request MUST
                NOT be sent; rather, the client MUST wait for the arrival of a
                provisional response before sending the request.  If the original
                request has generated a final response, the CANCEL SHOULD NOT be
                sent, as it is an effective no-op, since CANCEL has no effect on
                requests that have already generated a final response.  When the
                client decides to send the CANCEL, it creates a client transaction
                for the CANCEL and passes it the CANCEL request along with the
                destination address, port, and transport.  The destination address,
                port, and transport for the CANCEL MUST be identical to those used to
                send the original request.

                  If it was allowed to send the CANCEL before receiving a response
                  for the previous request, the server could receive the CANCEL
                  before the original request.

                Note that both the transaction corresponding to the original request
                and the CANCEL transaction will complete independently.  However, a
                UAC canceling a request cannot rely on receiving a 487 (Request
                Terminated) response for the original request, as an RFC 2543-
                compliant UAS will not generate such a response.  If there is no
                final response for the original request in 64*T1 seconds (T1 is
                defined in Section 17.1.1.1), the client SHOULD then consider the
                original transaction cancelled and SHOULD destroy the client
                transaction handling the original request.
            */
                                   
            // If has got final response, skip cancel because there is nothing to cancel.
            if(GetFinalResponse() != null){
                return;
            }
            else if(GetLastProvisionalResponse() != null){
                SIP_Request cancelRequest = new SIP_Request();
                cancelRequest.Method = SIP_Methods.CANCEL;
                cancelRequest.Uri = m_pRequest.Uri;
                cancelRequest.Via.Add(m_pRequest.Via.GetTopMostValue().ToStringValue());
                cancelRequest.CallID = m_pRequest.CallID;
                cancelRequest.From = m_pRequest.From;
                cancelRequest.To = m_pRequest.To;
                cancelRequest.CSeq = new SIP_t_CSeq(m_pRequest.CSeq.SequenceNumber,SIP_Methods.CANCEL);
                foreach(SIP_t_AddressParam route in m_pRequest.Route.GetAllValues()){
                    cancelRequest.Route.Add(route.ToStringValue());
                }
                cancelRequest.MaxForwards = 70;

                SIP_ClientTransaction transaction = m_pSipStack.TransactionLayer.CreateClientTransaction(cancelRequest,m_pTarget,false);
                transaction.Begin();
            }
            // If transaction hasn't got provisional response, queue cancel request.
            else{                
                m_CancelQueued = true;
            }           
        }

        #endregion

        #region method EnsureDialog

        /// <summary>
        /// Ensures that SIP dialog exists. If not, creates SIP dialog.
        /// </summary>
        /// <param name="response">SIP response what causes dialog creation.</param>
        private void EnsureDialog(SIP_Response response)
        {
            // NOTE: Provisional response is allowed not to have Contact header, that means we can't 
            //       create early dialog for that transaction.
            if(response.StatusCodeType == SIP_StatusCodeType.Provisional && response.Contact.Count == 0){
                return;
            }

            lock(this){
                if(this.CanCreateDialog){
                    SIP_Dialog dialog = null;
                    if(m_pSipStack.TransactionLayer.EnsureDialog(this,response,out dialog)){
                        this.Dialogs.Add(dialog);
                    }
                    
                    if(response.StatusCodeType == SIP_StatusCodeType.Success){
                        m_pDialog = dialog;
                    }
                }
            }
        }

        #endregion


        #region Properties Implementation

        /// <summary>
        /// Gets this client transaction request destination info.
        /// </summary>
        public SIP_Target Target
        {
            get{ return m_pTarget; }
        }

        /// <summary>
        /// Gets transaction ID (Via: branch parameter value).
        /// </summary>
        public override string ID
        {
            get{ return m_ID; }
        }

        /// <summary>
        /// SIP request what transaction handles.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this class is Disposed and this property is accessed.</exception>
        public override SIP_Request Request
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("SIP_ClientTransaction");
                }

                return m_pRequest; 
            }
        }
                
        /// <summary>
        /// Gets server transaction what child transaction this transaction is. Returns null if no owner server transaction.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this class is Disposed and this property is accessed.</exception>
        public SIP_ServerTransaction ServerTransaction
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("SIP_ClientTransaction");
                }

                return m_pServerTransaction; 
            }
        }

        /// <summary>
        /// Gets transaction related responses.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this class is Disposed and this property is accessed.</exception>
        public override SIP_Response[] Responses
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("SIP_ClientTransaction");
                }

                return m_pResponses.ToArray(); 
            }
        }

        /// <summary>
        /// Gets transaction dialog. Returns null if no dialog available.
        /// </summary>
        public override SIP_Dialog Dialog
        {
            get{ return m_pDialog; }
        }

        /// <summary>
        /// Gets or sets after how many seconds this client transaction times out.
        /// NOTE: This property takes effect only if transaction hasn't started yet.
        /// </summary>
        /// <exception cref="ArgumentException">Is raised when ivalid value is passed.</exception>
        public int Timeout
        {
            get{ return m_Timeout; }

            set{
                if(value < 1){
                    throw new ArgumentException("Property Timeout value must be >= 1 !");
                }

                m_Timeout = value;
            }
        }

        /// <summary>
        /// Gets if transaction is disposed.
        /// </summary>
        public override bool IsDisposed
        {
            get{ return m_IsDisposed; }
        }


        /// <summary>
        /// Gets or sets RSeq value. Value -1 means no reliable provisional response received.
        /// </summary>
        internal int RSeq
        {
            get{ return m_RSeq; }

            set{ m_RSeq = value; }
        }

        #endregion

        #region Events Implementation
                
        /// <summary>
        /// Is called when this transaction has got response destination end point.
        /// </summary>
        public event SIP_ResponseReceivedEventHandler ResponseReceived = null;

        /// <summary>
        /// Raises ResponseReceived event.
        /// </summary>
        /// <param name="response">SIP response received.</param>
        protected void OnResponseReceived(SIP_Response response)
        {
            if(this.ResponseReceived != null){
                this.ResponseReceived(new SIP_ResponseReceivedEventArgs(m_pSipStack,this,response));
            }
        }

        #endregion

    }
}

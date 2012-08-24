using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;
using System.Net;

using LumiSoft.Net.AUTH;
using LumiSoft.Net.SIP.Message;
using LumiSoft.Net.SIP.Stack;

namespace LumiSoft.Net.SIP.Proxy
{
    /// <summary>
    /// Implements SIP 'proxy context'. Defined in RFC 3261.
    /// </summary>
    /// <remarks>Proxy context is bridge between caller and calee. 
    /// Proxy context job is to forward request to contact(s) and send received responses back to caller.</remarks>
    public class SIP_ProxyContext : IDisposable
    {
        private SIP_ProxyCore               m_pProxy                    = null;
        private SIP_ServerTransaction       m_pServerTransaction        = null;
        private SIP_Request                 m_pServerTransactionRequest = null;
        private SIP_Request                 m_pRequest                  = null;
        private SIP_ForkingMode             m_ForkingMode               = SIP_ForkingMode.Parallel;
        private bool                        m_IsB2BUA                   = true;
        private bool                        m_NoCancel                  = false;
        private bool                        m_NoRecurse                 = true;
        private DateTime                    m_CreateTime;
        private int                         m_SequentialTimeout         = 15;
        private List<SIP_ClientTransaction> m_pClientTransactions       = null;
        private List<SIP_Response>          m_pResponses                = null;
        private Queue<SIP_Target>           m_pRemainingDestinations    = null;
        private List<NetworkCredential>     m_pCredentials              = null;
        private bool                        m_Started                   = false;
        private bool                        m_FinalResponseSent         = false;
        private bool                        m_Disposed                  = false;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="proxy">Owner proxy.</param>
        /// <param name="transaction">Server transaction what is used to send SIP responses back to caller.</param>
        /// <param name="request">Request to forward.</param>
        /// <param name="forkingMode">Specifies how proxy context must handle forking.</param>
        /// <param name="isB2BUA">Specifies if prxy context is in B2BUA or just transaction satefull mode.</param>
        /// <param name="noCancel">Specifies if proxy should not send Cancel to forked requests.</param>
        /// <param name="noRecurse">Specifies what proxy server does when it gets 3xx response. If true proxy will forward
        /// request to new specified address if false, proxy will return 3xx response to caller.</param>
        /// <param name="targets">Possible remote targets. NOTE: These values must be in priority order !</param>
        /// <param name="credentials">Target set credentials.</param>
        /// <exception cref="ArgumentNullException">Is raised when any of the reference type prameters is null.</exception>
        public SIP_ProxyContext(SIP_ProxyCore proxy,SIP_ServerTransaction transaction,SIP_Request request,SIP_ForkingMode forkingMode,bool isB2BUA,bool noCancel,bool noRecurse,SIP_Target[] targets,NetworkCredential[] credentials)
        {
            if(proxy == null){
                throw new ArgumentNullException("proxy");
            }
            if(transaction == null){
                throw new ArgumentNullException("transaction");
            }
            if(request == null){
                throw new ArgumentNullException("request");
            }
            if(targets == null){
                throw new ArgumentNullException("targets");
            }

            m_pProxy = proxy;

            m_pServerTransaction = transaction;
            m_pServerTransaction.CanCreateDialog = isB2BUA;
            m_pServerTransaction.Canceled += new EventHandler(m_pServerTransaction_Canceled);
            m_pServerTransaction.Terminated += new EventHandler(m_pServerTransaction_Terminated);

            m_pServerTransactionRequest = m_pServerTransaction.Request;
            m_pRequest    = request;
            m_ForkingMode = forkingMode;
            m_IsB2BUA     = isB2BUA;
            m_NoCancel    = noCancel;
            m_NoRecurse   = noRecurse;

            m_pClientTransactions = new List<SIP_ClientTransaction>();
            m_pResponses          = new List<SIP_Response>();
            m_CreateTime          = DateTime.Now;

            // Queue targets up, higest to lowest.
            m_pRemainingDestinations = new Queue<SIP_Target>();
            foreach(SIP_Target target in targets){
                m_pRemainingDestinations.Enqueue(target);
            }

            m_pCredentials = new List<NetworkCredential>();
            m_pCredentials.AddRange(credentials);
        }
                                
        #region method Dispose

        /// <summary>
        /// Cleans up any resources being used.
        /// </summary>
        public void Dispose()
        {
            if(m_Disposed){
                return;
            }
            m_Disposed = true;

            m_pProxy                 = null;
            m_pServerTransaction     = null;
            m_pClientTransactions    = null;
            m_pResponses             = null;
            m_pRemainingDestinations = null;
        }

        #endregion


        #region Events Handling

        #region method m_pServerTransaction_Canceled

        /// <summary>
        /// Is called when server transaction has canceled.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_pServerTransaction_Canceled(object sender,EventArgs e)
        {
            // Cancel all pending client transactions.
            if(!m_NoCancel){
                foreach(SIP_ClientTransaction transaction in m_pClientTransactions.ToArray()){
                    transaction.Cancel();
                }
            }

            // We dont need to Dispose proxy context, server transaction will call Terminated event
            // after cancel, there we dispose it.
        }

        #endregion

        #region method m_pServerTransaction_Terminated

        /// <summary>
        /// Is called when server transaction has completed and terminated.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void m_pServerTransaction_Terminated(object sender,EventArgs e)
        {
            // All done, just dispose proxy context.
            Dispose();
        }

        #endregion

        
        #region method ClientTransaction_ResponseReceived

        /// <summary>
        /// Is called when client transactions receives response.
        /// </summary>
        /// <param name="e">Event data.</param>
        private void ClientTransaction_ResponseReceived(SIP_ResponseReceivedEventArgs e)
        {
            // If 401 or 407 (Authentication required), see i we have specified realm(s) credentials, 
            // if so try to authenticate.
            if(e.Response.StatusCode == 401 || e.Response.StatusCode == 407){
                SIP_t_Challenge[] challanges = null;
                if(e.Response.StatusCode == 401){
                    challanges = e.Response.WWWAuthenticate.GetAllValues();
                }
                else{
                    challanges = e.Response.ProxyAuthenticate.GetAllValues();
                }

                // TODO: Porbably we need to auth only if we can provide authentication data to all realms ?

                SIP_Request request = m_pServerTransaction.Request.Copy();
                request.CSeq.SequenceNumber++;
                bool hasAny = false;
                foreach(SIP_t_Challenge challange in challanges){
                    Auth_HttpDigest authDigest = new Auth_HttpDigest(challange.AuthData,m_pServerTransaction.Request.Method);
                    NetworkCredential credential = GetCredential(authDigest.Realm);
                    if(credential != null){
                        // Don't authenticate again, if we tried already once and failed.
                        // FIX ME: if user passed authorization, then works wrong.
                        if(e.ClientTransaction.Request.Authorization.Count == 0 && e.ClientTransaction.Request.ProxyAuthorization.Count == 0){
                            authDigest.RequestMethod = m_pServerTransaction.Request.Method;
                            authDigest.Uri           = e.ClientTransaction.Request.Uri;
                            authDigest.Realm         = credential.Domain;
                            authDigest.UserName      = credential.UserName;
                            authDigest.Password      = credential.Password;
                            authDigest.CNonce        = Auth_HttpDigest.CreateNonce();
                            authDigest.Qop           = authDigest.Qop;
                            authDigest.Opaque        = authDigest.Opaque;
                            authDigest.Algorithm     = authDigest.Algorithm;
                            if(e.Response.StatusCode == 401){
                                request.Authorization.Add(authDigest.ToAuthorization());
                            }
                            else{
                                request.ProxyAuthorization.Add(authDigest.ToAuthorization());
                            }
                            hasAny = true;
                        }
                    }
                }
                if(hasAny){
                    CreateClientTransaction((SIP_Target)e.ClientTransaction.Tag,request);
                    return;
                }
            }

            ProcessResponse(e.ClientTransaction,e.Response);
        }

        #endregion

        #region method ClientTransaction_TimedOut

        /// <summary>
        /// Is called when client transaction has timed out.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClientTransaction_TimedOut(object sender,EventArgs e)
        {
            /* RFC 3261 16.8 Processing Timer C.
                If the client transaction has received a provisional response, the proxy
                MUST generate a CANCEL request matching that transaction.  If the client 
                transaction has not received a provisional response, the proxy MUST behave 
                as if the transaction received a 408 (Request Timeout) response.
            */

            SIP_ClientTransaction transaction = (SIP_ClientTransaction)sender;

            // Cancel is done automatically by Timeout, so we don't need todo it.

            // Remove ResponseReceived event listener, we don't care about new responses any more.
            transaction.ResponseReceived -= new SIP_ResponseReceivedEventHandler(ClientTransaction_ResponseReceived);

            // Remove that transaction from proxy context, otherwise choose best final response may work wrong.
            m_pClientTransactions.Remove(transaction);

            if(transaction.Responses.Length == 0){
                ProcessResponse(transaction,transaction.Request.CreateResponse(SIP_ResponseCodes.x408_Request_Timeout));
            }

            // If Sequential forking, try next destination.
            if(m_ForkingMode == SIP_ForkingMode.Sequential && m_pRemainingDestinations.Count > 0){
                CreateClientTransaction(m_pRemainingDestinations.Dequeue());
            }
        }

        #endregion

        #region method ClientTransaction_TransportError

        /// <summary>
        /// Is called when client transaction encountered transport error.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void ClientTransaction_TransportError(object sender,EventArgs e)
        {
            /* RFC 3261 16.9 Handling Transport Errors
                If the transport layer notifies a proxy of an error when it tries to
                forward a request (see Section 18.4), the proxy MUST behave as if the
                forwarded request received a 503 (Service Unavailable) response.
            */

            ProcessResponse((SIP_ClientTransaction)sender,((SIP_Transaction)sender).Request.CreateResponse(SIP_ResponseCodes.x503_Service_Unavailable));

            // If Sequential forking, try next destination.
            if(m_ForkingMode == SIP_ForkingMode.Sequential && m_pRemainingDestinations.Count > 0){
                CreateClientTransaction(m_pRemainingDestinations.Dequeue());
            }
        }

        #endregion

        #region method ClientTransaction_Terminated

        /// <summary>
        /// Is called when client transaction has terminated.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClientTransaction_Terminated(object sender,EventArgs e)
        {
            m_pClientTransactions.Remove((SIP_ClientTransaction)sender);
        }

        #endregion

        #endregion


        #region method Start

        /// <summary>
        /// Starts processing.
        /// </summary>
        /// <exception cref="InvalidOperationException">Is raised when this method is called more than once.</exception>
        public void Start()
        {
            if(m_Started){
                throw new InvalidOperationException();
            }
            m_Started = true;

            // Only use destination with the highest q value.
            // We already have ordered highest to lowest, so just get first destination.
            if(m_ForkingMode == SIP_ForkingMode.None){
                CreateClientTransaction(m_pRemainingDestinations.Dequeue());
            }
            // Use all destinations at same time.
            else if(m_ForkingMode == SIP_ForkingMode.Parallel){
                while(m_pRemainingDestinations.Count > 0){
                    CreateClientTransaction(m_pRemainingDestinations.Dequeue());
                }
            }
            // Start processing destinations with highest q value to lowest.
            else if(m_ForkingMode == SIP_ForkingMode.Sequential){
                // Start processing highest destination.
                CreateClientTransaction(m_pRemainingDestinations.Dequeue());
            }
        }

        #endregion

        #region method Cancel

        /// <summary>
        /// Cancels proxy context processing. All client transactions and owner server transaction will be canceled,
        /// proxy context will be disposed. 
        /// </summary>
        public void Cancel()
        {
            /* RFC 3261 16.10 CANCEL Processing.
                Furthermore, the element MUST generate CANCEL requests for all pending client 
                transactions in the context as described in Section 16.7 step 10.
            */
            
            if(!m_NoCancel){
                foreach(SIP_ClientTransaction transaction in m_pClientTransactions.ToArray()){
                    transaction.Cancel();
                }
            }
            m_pServerTransaction.Cancel();
        }

        #endregion


        #region method CreateClientTransaction

        /// <summary>
        /// Creates client transaction and starts processing it.
        /// </summary>
        /// <param name="target">SIP target.</param>
        private void CreateClientTransaction(SIP_Target target)
        {
            CreateClientTransaction(target,m_pRequest.Copy());
        }

        /// <summary>
        /// Creates client transaction and starts processing it.
        /// </summary>
        /// <param name="target">SIP target.</param>
        /// <param name="request">SIP request that client transaction will handle.</param>
        private void CreateClientTransaction(SIP_Target target,SIP_Request request)
        {
            /* Sequential handling:
                If there are more that 1 destination, then m_SequentialTimeout interval
                is used to to try next destination. Otherwise 3 minute is used.               
            */

            SIP_ClientTransaction transaction = m_pProxy.Stack.TransactionLayer.CreateClientTransaction(request,target,true);
            transaction.Tag = target;
            if(m_ForkingMode == SIP_ForkingMode.Sequential && m_pRemainingDestinations.Count > 0){
                transaction.Timeout = 15;
            }
            else{
                transaction.Timeout = 180;
            }
            transaction.CanCreateDialog = m_IsB2BUA;
            transaction.ResponseReceived += new SIP_ResponseReceivedEventHandler(ClientTransaction_ResponseReceived);
            transaction.TimedOut += new EventHandler(ClientTransaction_TimedOut);
            transaction.TransportError += new EventHandler(ClientTransaction_TransportError);
            transaction.Terminated += new EventHandler(ClientTransaction_Terminated);
            m_pClientTransactions.Add(transaction);
            transaction.Begin();
        }
                                                                
        #endregion

        #region method ProcessResponse

        /// <summary>
        /// Processes client transaction received response.
        /// </summary>
        /// <param name="transaction">Client transaction what response it is.</param>
        /// <param name="response">Response received.</param>
        private void ProcessResponse(SIP_ClientTransaction transaction,SIP_Response response)
        {
            /* RFC 3261 16.7 Response Processing.
                1.  Find the appropriate response context
                2.  Update timer C for provisional responses
                3.  Remove the topmost Via
                4.  Add the response to the response context
                5.  Check to see if this response should be forwarded immediately
                6.  When necessary, choose the best final response from the response context

                If no final response has been forwarded after every client
                transaction associated with the response context has been terminated,
                the proxy must choose and forward the "best" response from those it
                has seen so far.

                The following processing MUST be performed on each response that is
                forwarded.  It is likely that more than one response to each request
                will be forwarded: at least each provisional and one final response.

                7.  Aggregate authorization header field values if necessary
                8.  Optionally rewrite Record-Route header field values
                9.  Forward the response
                10. Generate any necessary CANCEL requests
            */

            lock(this){
                // 1.  Find the appropriate response context.
                // Done, "this" is it.

                // 2.  Update timer C for provisional responses.
                //     Our client transaction will handle it.

                // 3.  Remove the topmost Via. If no Via header field values remain in the response, 
                //     the response was meant for this element and MUST NOT be forwarded.
                //
                //     NOTE: We MAY NOT do it for B2BUA, skip it for B2BUA
                if(!m_IsB2BUA){
                    response.Via.RemoveTopMostValue();
                    if(response.Via.GetAllValues().Length == 0){
                        return;
                    }
                }

                // 4.  Add the response to the response context.
                if(!m_NoRecurse && response.StatusCodeType == SIP_StatusCodeType.Redirection){
                    /*  If the proxy chooses to recurse on any contacts in a 3xx response by adding them to 
                        the target set, it MUST remove them from the response before adding the response to 
                        the response context. However, a proxy SHOULD NOT recurse to a non-SIPS URI if the 
                        Request-URI of the original request was a SIPS URI. If the proxy recurses on all of 
                        the contacts in a 3xx response, the proxy SHOULD NOT add the resulting contactless 
                        response to the response context.
                    */

                    // Get SIP contacts and remove them from response.
                    SIP_t_ContactParam[] contacts = response.Contact.GetAllValues();
                    // Remove all contacts from response, we add no-SIP URIs back.
                    response.Contact.RemoveAll();
                    foreach(SIP_t_ContactParam contact in contacts){
                        // SIP URI add it to fork list.
                        if(contact.Address.IsSipOrSipsUri){
                            m_pRemainingDestinations.Enqueue(new SIP_Target(SIP_Uri.Parse(contact.Address.Uri)));
                        }
                        // Add specified URI back to response.
                        else{
                            response.Contact.Add(contact.ToStringValue());
                        }
                    }

                    // There are remaining non-SIP contacts, so we need to add the response to reponses collection.
                    if(response.Contact.GetAllValues().Length > 0){
                        m_pResponses.Add(response);
                    }

                    // Handle forking
                    if(m_pRemainingDestinations.Count > 0){
                        if(m_ForkingMode == SIP_ForkingMode.Parallel){
                            while(m_pRemainingDestinations.Count > 0){
                                CreateClientTransaction(m_pRemainingDestinations.Dequeue());
                            }
                        }
                        // Just fork next.
                        else{
                            CreateClientTransaction(m_pRemainingDestinations.Dequeue());
                        }

                        // Because we forked request to new target(s), we don't need to do steps 5 - 10.
                        return;
                    }
                }
                // Not 3xx response or recursing disabled.
                else{
                    m_pResponses.Add(response);
                }
                        
                // 5.  Check to see if this response should be forwarded immediately.
                bool forwardResponse = false;
                if(m_FinalResponseSent){
                    // -  Any 2xx response to an INVITE request
                    if(response.StatusCodeType == SIP_StatusCodeType.Success && m_pServerTransaction.Request.Method == SIP_Methods.INVITE){
                        forwardResponse = true;
                    }
                }
                else{
                    // -  Any provisional response other than 100 (Trying)
                    if(response.StatusCodeType == SIP_StatusCodeType.Provisional && response.StatusCode != 101){
                        forwardResponse = true;
                    }
                    // -  Any 2xx response
                    else if(response.StatusCodeType == SIP_StatusCodeType.Success){
                        forwardResponse = true;
                    }
                }

                /* 6. When necessary, choose the best final response from the response context.
                      A stateful proxy MUST send a final response to a response context's server transaction 
                      if no final responses have been immediately forwarded by the above rules and all client
                      transactions in this response context have been terminated.
                */
                bool mustChooseBestFinalResponse = false;
                if(!forwardResponse && m_pRemainingDestinations.Count == 0){
                    mustChooseBestFinalResponse = true;
                    foreach(SIP_ClientTransaction t in m_pClientTransactions.ToArray()){
                        // Acutally we can't relay on terminated state, thats not accurate, just see
                        // any transaction haven't also got final response (the we can expect final response).
                        if(t.GetFinalResponse() == null){
                            mustChooseBestFinalResponse = false;
                            break;
                        }
                    }
                }
                if(mustChooseBestFinalResponse){
                    response = GetBestFinalResponse();
                    if(response == null){
                        /*  If there are no final responses in the context, the proxy MUST send a 
                            408 (Request Timeout) response to the server transaction.
                        */
                        response = m_pServerTransaction.Request.CreateResponse(SIP_ResponseCodes.x408_Request_Timeout);
                    }
                    forwardResponse = true;
                }

                if(forwardResponse){
                    /* 7.  Aggregate authorization header field values if necessary.
                           If the selected response is a 401 (Unauthorized) or 407 (Proxy Authentication Required), 
                           the proxy MUST collect any WWW-Authenticate and Proxy-Authenticate header field values 
                           from all other 401 (Unauthorized) and 407 (Proxy Authentication Required) responses 
                           received so far in this response context and add them to this response without 
                           modification before forwarding. The resulting 401 (Unauthorized) or 407 (Proxy
                           Authentication Required) response could have several WWW-Authenticate AND 
                           Proxy-Authenticate header field values.

                           This is necessary because any or all of the destinations the request was forwarded to 
                           may have requested credentials.  The client needs to receive all of those challenges and 
                           supply credentials for each of them when it retries the request.
                    */
                    if(response.StatusCode == 401 || response.StatusCode == 407){
                        foreach(SIP_Response resp in m_pResponses.ToArray()){
                            if(response != resp && (resp.StatusCode == 401 || resp.StatusCode == 407)){
                                // WWW-Authenticate
                                foreach(SIP_HeaderField hf in resp.WWWAuthenticate.HeaderFields){
                                    resp.WWWAuthenticate.Add(hf.Value);
                                }
                                // Proxy-Authenticate
                                foreach(SIP_HeaderField hf in resp.ProxyAuthenticate.HeaderFields){
                                    resp.ProxyAuthenticate.Add(hf.Value);
                                }
                            }
                        }
                    }
            
                    // 8.  Optionally rewrite Record-Route header field values.
                    //     This is optional so we currently won't do that.
            
                    // 9.  Forward the response.
                    SendResponse(transaction,response);
                    if(response.StatusCodeType != SIP_StatusCodeType.Provisional){
                        m_FinalResponseSent = true;
                    }
            
                    /* 10. Generate any necessary CANCEL requests.
                           If the forwarded response was a final response, the proxy MUST
                           generate a CANCEL request for all pending client transactions
                           associated with this response context.
                    */                
                    if(response.StatusCodeType != SIP_StatusCodeType.Provisional){
                        if(!m_NoCancel){
                            foreach(SIP_ClientTransaction t in m_pClientTransactions.ToArray()){
                                t.Cancel();
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region method SendResponse

        /// <summary>
        /// Sends SIP response to caller. If proxy context is in B2BUA mode, new response is generated 
        /// as needed.
        /// </summary>
        /// <param name="transaction">Client transaction what response it is.</param>
        /// <param name="response">Response to send.</param>
        private void SendResponse(SIP_ClientTransaction transaction,SIP_Response response)
        { 
            if(m_IsB2BUA){
                /* draft-marjou-sipping-b2bua-00 4.1.3.
                    When the UAC side of the B2BUA receives the downstream SIP response
                    of a forwarded request, its associated UAS creates an upstream
                    response (except for 100 responses).  The creation of the Via, Max-
                    Forwards, Call-Id, CSeq, Record-Route and Contact header fields
                    follows the rules of [2].  The Record-Route header fields of the
                    downstream response are not copied in the new upstream response, as
                    Record-Route is meaningful for the downstream dialog.  The UAS SHOULD
                    copy other header fields and body from the downstream response into
                    this upstream response before sending it.
                */
                
                SIP_Request originalRequest = m_pServerTransaction.Request;

                // We need to use caller original request to construct response from proxied response.
                SIP_Response b2buaResponse = response.Copy();
                b2buaResponse.Via.RemoveAll();
                b2buaResponse.Via.AddToTop(originalRequest.Via.GetTopMostValue().ToStringValue());
                b2buaResponse.CallID = originalRequest.CallID;
                b2buaResponse.CSeq = originalRequest.CSeq;
                b2buaResponse.Contact.RemoveAll();
                b2buaResponse.Contact.Add(m_pProxy.CreateContact(originalRequest.From.Address).ToStringValue());
                b2buaResponse.RecordRoute.RemoveAll();
                               
                b2buaResponse.Allow.RemoveAll();
                b2buaResponse.Supported.RemoveAll();
                // Accept to non ACK,BYE request.
                if(originalRequest.Method != SIP_Methods.ACK && originalRequest.Method != SIP_Methods.BYE){
                    b2buaResponse.Allow.Add("INVITE,ACK,OPTIONS,CANCEL,BYE,PRACK");
                }
                // Supported to non ACK request. 
                if(originalRequest.Method != SIP_Methods.ACK){
                    b2buaResponse.Supported.Add("100rel,timer");
                }
                // Remove Require: header.
                b2buaResponse.Require.RemoveAll();

                m_pServerTransaction.SendResponse(b2buaResponse);
                
                // If INVITE 2xx response do call here.
                if(response.CSeq.RequestMethod.ToUpper() == SIP_Methods.INVITE && response.StatusCodeType == SIP_StatusCodeType.Success){
                    m_pProxy.B2BUA.AddCall(m_pServerTransaction.Dialog,transaction.Dialog);
                }
            }
            else{
                m_pServerTransaction.SendResponse(response);
            }
        }

        #endregion

        #region method GetBestFinalResponse

        /// <summary>
        /// Gets best final response. If no final response in responses collection, null is returned.
        /// </summary>
        /// <returns>Resturns best final response or  null if no final response.</returns>
        private SIP_Response GetBestFinalResponse()
        {
            // 6xx -> 2xx -> 3xx -> 4xx -> 5xx

            // 6xx
            foreach(SIP_Response resp in m_pResponses.ToArray()){
                if(resp.StatusCodeType == SIP_StatusCodeType.GlobalFailure){
                    return resp;
                }
            }
            // 2xx
            foreach(SIP_Response resp in m_pResponses.ToArray()){
                if(resp.StatusCodeType == SIP_StatusCodeType.Success){
                    return resp;
                }
            }
            // 3xx
            foreach(SIP_Response resp in m_pResponses.ToArray()){
                if(resp.StatusCodeType == SIP_StatusCodeType.Redirection){
                    return resp;
                }
            }                
            // 4xx
            foreach(SIP_Response resp in m_pResponses.ToArray()){
                if(resp.StatusCodeType == SIP_StatusCodeType.RequestFailure){
                    return resp;
                }
            }
            // 5xx
            foreach(SIP_Response resp in m_pResponses.ToArray()){
                if(resp.StatusCodeType == SIP_StatusCodeType.ServerFailure){
                    return resp;
                }
            }

            return null;
        }

        #endregion

        #region method GetCredential

        /// <summary>
        /// Gets credentials for specified realm. Returns null if none such credentials.
        /// </summary>
        /// <param name="realm">Realm which credentials to get.</param>
        /// <returns>Returns specified realm credentials or null in none.</returns>
        private NetworkCredential GetCredential(string realm)
        {
            foreach(NetworkCredential c in m_pCredentials){
                if(c.Domain.ToLower() == realm.ToLower()){
                    return c;
                }
            }
            return null;
        }

        #endregion


        #region Properties Implementation

        /// <summary>
        /// Gets time when proxy context was created.
        /// </summary>
        public DateTime CreateTime
        {
            get{ return m_CreateTime; }
        }

        /// <summary>
        /// Gets forking mode used by this 'proxy context'.
        /// </summary>
        public SIP_ForkingMode ForkingMode
        {
            get{ return m_ForkingMode; }
        }

        /// <summary>
        /// Gets if proxy cancels forked requests what are not needed any more. If true, 
        /// requests not canceled, otherwise canceled.
        /// </summary>
        public bool NoCancel
        {
            get{ return m_NoCancel; }
        }

        /// <summary>
        /// Gets what proxy server does when it gets 3xx response. If true proxy will forward
        /// request to new specified address if false, proxy will return 3xx response to caller.
        /// </summary>
        public bool Recurse
        {
            get{ return !m_NoRecurse; }
        }

        /// <summary>
        /// Gets server transaction what is responsible for sending responses to caller.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this class is Disposed and this property is accessed.</exception>
        public SIP_ServerTransaction ServerTransaction
        {
            get{ 
                if(m_Disposed){
                    throw new ObjectDisposedException("SIP_ProxyContext");
                }

                return m_pServerTransaction; 
            }
        }

        /// <summary>
        /// Gets request what is forwarded by proxy context.
        /// </summary>
        public SIP_Request Request
        {
            get{ 
                if(m_Disposed){
                    throw new ObjectDisposedException("SIP_ProxyContext");
                }

                return m_pRequest; 
            }
        }

        /// <summary>
        /// Gets active client transactions that will handle forward request. 
        /// There may be more than 1 active client transaction if parallel forking.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this class is Disposed and this property is accessed.</exception>
        public SIP_ClientTransaction[] ClientTransactions
        {
            get{
                if(m_Disposed){
                    throw new ObjectDisposedException("SIP_ProxyContext");
                }

                return m_pClientTransactions.ToArray(); 
            }
        } 
       
        /// <summary>
        /// Gets all responses what proxy context has received.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this class is Disposed and this property is accessed.</exception>
        public SIP_Response[] Responses
        {
            get{ 
                if(m_Disposed){
                    throw new ObjectDisposedException("SIP_ProxyContext");
                }

                return m_pResponses.ToArray(); 
            }
        }

        /// <summary>
        /// Gets number of seconds that proxy waits before trying the next contact. 
        /// NOTE: That value applies to sequential forking only.  
        /// </summary>
        public int SequentialTimeout
        {
            get{ return m_SequentialTimeout; }
        }

        #endregion

    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

using LumiSoft.Net.AUTH;
using LumiSoft.Net.SIP.Message;
using LumiSoft.Net.SIP.Stack;

namespace LumiSoft.Net.SIP
{
    /// <summary>
    /// SIP helper methods.
    /// </summary>
    public class SIP_Utils
    {
        #region method ParseAddress

        /// <summary>
        /// Parses address from SIP To: header field.
        /// </summary>
        /// <param name="to">SIP header To: value.</param>
        /// <returns></returns>
        public static string ParseAddress(string to)
        {
            try{
                string retVal = to;
                if(to.IndexOf('<') > -1 && to.IndexOf('<') < to.IndexOf('>')){
                    retVal = to.Substring(to.IndexOf('<') + 1,to.IndexOf('>') - to.IndexOf('<') - 1);
                }
                // Remove sip:
                if(retVal.IndexOf(':') > -1){
                    retVal = retVal.Substring(retVal.IndexOf(':') + 1).Split(':')[0];
                }
                return retVal;
            }
            catch{
                throw new ArgumentException("Invalid SIP header To: '" + to + "' value !");
            }
        }

        #endregion

        #region method UriToRequestUri

        /// <summary>
        /// Converts URI to Request-URI by removing all not allowed Request-URI parameters from URI.
        /// </summary>
        /// <param name="uri">URI value.</param>
        /// <returns>Returns valid Request-URI value.</returns>
        public static string UriToRequestUri(string uri)
        {            
            // RFC 3261 19.1.2.(Table)
            // We need to strip off "method-param" and "header" URI parameters".
            // Currently we do it for sip or sips uri, do we need todo it for others too ?
            try{
                SIP_Uri sUri = SIP_Uri.Parse(uri);
                sUri.Parameters.Remove("method");
                sUri.Header = null;
                return sUri.ToStringValue();
            }
            catch{
                return uri;
            }            
        }

        #endregion

        #region method ToEndPointInfo

        /// <summary>
        /// Converts socket local or remote end point to SIP_EndPointInfo.
        /// </summary>
        /// <param name="socket">Socket to use.</param>
        /// <param name="local_remote">Specifies if loacl or remote end point of socket is used.</param>
        /// <returns>Returns SIP end point info.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>socket</b> is null.</exception>
        public static SIP_EndPointInfo ToEndPointInfo(SocketEx socket,bool local_remote)
        {
            if(socket == null){
                throw new ArgumentNullException("socket");
            }

            IPEndPoint ep = null;
            if(local_remote){
                ep = (IPEndPoint)socket.LocalEndPoint;
            }
            else{
                ep = (IPEndPoint)socket.RemoteEndPoint;
            }

            if(socket.RawSocket.ProtocolType == System.Net.Sockets.ProtocolType.Udp){
                return new SIP_EndPointInfo(SIP_Transport.UDP,ep);
            }
            else{
                return new SIP_EndPointInfo(SIP_Transport.TCP,ep);
            }
        }

        #endregion

        #region method IsSipUri

        /// <summary>
        /// Gets if specified value is SIP or SIPS URI.
        /// </summary>
        /// <param name="value">Value to check.</param>
        /// <returns>Returns true if specified value is SIP or SIPS URI, otherwise false.</returns>
        public static bool IsSipUri(string value)
        {
            try{
                SIP_Uri.Parse(value);
                return true;
            }
            catch{
            }
            return false;
        }

        #endregion

        #region method IsTelUri

        /// <summary>
        /// Gets if specified URI is tel: or sip tel URI. There is special case when SIP URI can be tel:, 
        /// sip:+xxxx and sip:xxx;user=phone.
        /// </summary>
        /// <param name="uri">URI to check.</param>
        /// <returns>Returns true if specified URI is tel: URI.</returns>
        public static bool IsTelUri(string uri)
        {
            uri = uri.ToLower();

            try{
                if(uri.StartsWith("tel:")){
                    return true;
                }
                else if(IsSipUri(uri)){
                    SIP_Uri sipUri = SIP_Uri.Parse(uri);
                    // RFC 3398 12. If user part starts with +, it's tel: URI.
                    if(sipUri.User.StartsWith("+")){
                        return true;
                    }
                    // RFC 3398 12.
                    else if(sipUri.Param_User != null && sipUri.Param_User.ToLower() == "phone"){
                        return true;
                    }
                }
            }
            catch{
            }

            return false;
        }

        #endregion

        #region method GetCredentials

        /// <summary>
        /// Gets specified realm SIP proxy credentials. Returns null if none exists for specified realm.
        /// </summary>
        /// <param name="request">SIP reques.</param>
        /// <param name="realm">Realm(domain).</param>
        /// <returns>Returns specified realm credentials or null if none.</returns>
        public static SIP_t_Credentials GetCredentials(SIP_Request request,string realm)
        {
            foreach(SIP_SingleValueHF<SIP_t_Credentials> authorization in request.ProxyAuthorization.HeaderFields){
                if(authorization.ValueX.Method.ToLower() == "digest"){
                    Auth_HttpDigest authDigest = new Auth_HttpDigest(authorization.ValueX.AuthData,request.Method);
                    if(authDigest.Realm.ToLower() == realm.ToLower()){
                        return authorization.ValueX;
                    }
                }
            }

            return null;
        }

        #endregion

        #region method ContainsOptionTag

        /// <summary>
        /// Gets is specified option tags constains specified option tag.
        /// </summary>
        /// <param name="tags">Option tags.</param>
        /// <param name="tag">Option tag to check.</param>
        /// <returns>Returns true if specified option tag exists.</returns>
        public static bool ContainsOptionTag(SIP_t_OptionTag[] tags,string tag)
        {
            foreach(SIP_t_OptionTag t in tags){
                if(t.OptionTag.ToLower() == tag){
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region method GetUriScheme

        /// <summary>
        /// Gets URI scheme from specified value.
        /// </summary>
        /// <param name="value">String value.</param>
        /// <returns>Returns uri scheme.</returns>
        public static string GetUriScheme(string value)
        {
            if(value.IndexOf(':') > -1){
                return value.Substring(0,value.IndexOf(':'));
            }
            else{
                return value;
            }
        }

        #endregion

        #region method MethodCanEstablishDialog

        /// <summary>
        /// Gets if specified method can establish dialog.
        /// </summary>
        /// <param name="method">SIP method.</param>
        /// <returns>Returns true if specified SIP method can establish dialog, otherwise false.</returns>
        /// <exception cref="ArgumentException">Is raised when invalid value is passed.</exception>
        public static bool MethodCanEstablishDialog(string method)
        {
            if(string.IsNullOrEmpty(method)){
                throw new ArgumentException("Argument 'method' value can't be null or empty !");
            }
            method = method.ToUpper();

            if(method == SIP_Methods.INVITE){
                return true;
            }

            return false;
        }

        #endregion

        #region method CreateTag

        /// <summary>
        /// Creates tag for tag header filed. For example From:/To: tag value.
        /// </summary>
        /// <returns>Returns tag string.</returns>
        public static string CreateTag()
        {
            return Guid.NewGuid().ToString().Replace("-","").Substring(8);
        }

        #endregion
    }
}

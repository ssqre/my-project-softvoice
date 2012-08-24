using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.AUTH
{
    /// <summary>
    /// Implements http digest access authentication. Defined in RFC 2617.
    /// </summary>
    public class Auth_HttpDigest
    {
        private string m_Method     = "";
        private string m_Realm      = "";
        private string m_Nonce      = "";
        private string m_Opaque     = "";
        private string m_Algorithm  = "";
        private string m_Response   = "";
        private string m_UserName   = "";
        private string m_Password   = "";
        private string m_Uri        = "";
        private string m_Qop        = "";
        private string m_Cnonce     = "";
        private int    m_NonceCount = 1;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="digestResponse">Client returned digest response.</param>
        /// <param name="requestMethod">Request method.</param>
        public Auth_HttpDigest(string digestResponse,string requestMethod)
        {
            m_Method = requestMethod;

            Parse(digestResponse);
        }
                
        /// <summary>
        /// Client constructor. This is used to build valid Authorization response to server.
        /// </summary>
        /// <param name="userName">User name.</param>
        /// <param name="password">Password.</param>
        /// <param name="cnonce">Client nonce value.</param>
        /// <param name="uri">Request URI.</param>
        /// <param name="digestResponse">Server authenticate resposne.</param>
        /// <param name="requestMethod">Request method.</param>
        public Auth_HttpDigest(string userName,string password,string cnonce,string uri,string digestResponse,string requestMethod)
        {            
            Parse(digestResponse);

            m_UserName   = userName;
            m_Password   = password;
            m_Method     = requestMethod;
            m_Cnonce     = cnonce;
            m_Uri        = uri;            
            m_Qop        = "auth";
            m_NonceCount = 1;
            m_Response   = CalculateResponse(m_UserName,m_Password);
        }

        /// <summary>
        /// Server constructor. This is used to build valid Authenticate response to client.
        /// </summary>
        /// <param name="realm">Realm(domain).</param>
        /// <param name="nonce">Nonce value.</param>
        /// <param name="opaque">Opaque value.</param>
        public Auth_HttpDigest(string realm,string nonce,string opaque)
        {
            m_Realm  = realm;
            m_Nonce  = nonce;
            m_Opaque = opaque;
        }

                
        #region method Authenticate

        /// <summary>
        /// Authenticates specified user and password using this class parameters.
        /// </summary>
        /// <param name="userName">User name.</param>
        /// <param name="password">Password.</param>
        /// <returns>Returns true if authenticated, otherwise false.</returns>
        public bool Authenticate(string userName,string password)
        {                        
            // Check that our computed digest is same as client provided.
            if(this.Response == CalculateResponse(userName,password)){
                return true;
            }
            else{
                return false;
            }
        }

        #endregion


        #region method Parse

        /// <summary>
        /// Parses authetication info from client digest response.
        /// </summary>
        /// <param name="digestResponse">Client returned digest response.</param>
        private void Parse(string digestResponse)
        {
            string[] parameters = TextUtils.SplitQuotedString(digestResponse,',');
            foreach(string parameter in parameters){
                string[] name_value = parameter.Split(new char[]{'='},2);
                string   name       = name_value[0].Trim();

                if(name_value.Length == 2){
                    if(name.ToLower() == "realm"){
                        m_Realm = TextUtils.UnQuoteString(name_value[1]);
                    }
                    else if(name.ToLower() == "nonce"){
                        m_Nonce = TextUtils.UnQuoteString(name_value[1]);
                    }
                    else if(name.ToLower() == "uri"){
                        m_Uri = TextUtils.UnQuoteString(name_value[1]);
                    }
                    else if(name.ToLower() == "qop"){
                        m_Qop = TextUtils.UnQuoteString(name_value[1]);
                    }
                    else if(name.ToLower() == "nc"){
                        m_NonceCount = Convert.ToInt32(TextUtils.UnQuoteString(name_value[1]));
                    }
                    else if(name.ToLower() == "cnonce"){
                        m_Cnonce = TextUtils.UnQuoteString(name_value[1]);
                    }
                    else if(name.ToLower() == "response"){
                        m_Response = TextUtils.UnQuoteString(name_value[1]);
                    }
                    else if(name.ToLower() == "opaque"){
                        m_Opaque = TextUtils.UnQuoteString(name_value[1]);
                    }                    
                    else if(name.ToLower() == "username"){
                        m_UserName = TextUtils.UnQuoteString(name_value[1]);
                    }                   
                    else if(name.ToLower() == "algorithm"){
                        m_Algorithm = TextUtils.UnQuoteString(name_value[1]);
                    }
                }
            }
        }

        #endregion


        #region method CalculateResponse

        /// <summary>
        /// Calculates response value.
        /// </summary>
        /// <param name="userName">User name.</param>
        /// <param name="password">User password.</param>
        /// <returns>Returns calculated rsponse value.</returns>
        private string CalculateResponse(string userName,string password)
        {
            /*
                MD5
                    A1 = username-value ":" realm-value ":" passwd
            
                MD5-sess
                    A1 = md5(username-value ":" realm-value ":" passwd) ":" nonce-value ":" cnonce-value
                                         
                qop not peresent or auth
                    A2 = Method ":" digest-uri-value
             
                qop auth-int
                    A2 = Method ":" digest-uri-value ":" md5(entity-body)
              
                qop present
                    response = md5(md5(A1) ":" nonce-value ":" nc-value ":" cnonce-value ":" qop-value ":" md5(A2))
                          
                qop not present
                    response = md5(md5(A1) ":" nonce-value ":" md5(A2))
                        
            */

            string a1 = "";
            string a2 = "";
            // Create A1
            if(this.Algorithm == "" || this.Algorithm.ToLower() == "md5"){
                a1 = userName + ":" + this.Realm + ":" + password;
            }
            else if(this.Algorithm.ToLower() == "md5-sess"){
                a1 = Core.ComputeMd5(userName + ":" + this.Realm + ":" + password) + ":" + this.Nonce + ":" + this.CNonce;
            }
            else{
                throw new ArgumentException("Invalid Algorithm value '" + this.Algorithm + "' !");
            }
            // Create A2            
            if(this.Qop == "" || this.Qop.ToLower() == "auth"){
                a2 = m_Method.ToUpper() + ":" + this.Uri;
            }
            else{
                throw new ArgumentException("Invalid qop value '" + this.Qop + "' !");
            }

            // Calculate response value.
            // qop
            if(this.Qop != ""){
                return Core.ComputeMd5(Core.ComputeMd5(a1) + ":" + this.Nonce + ":" + this.NonceCount.ToString("x8") + ":" + this.CNonce + ":" + this.Qop + ":" + Core.ComputeMd5(a2));
            }
            // qop not present
            else{                
                return Core.ComputeMd5(Core.ComputeMd5(a1) + ":" + this.Nonce + ":" + Core.ComputeMd5(a2));
            }
        }

        #endregion


        #region method ToString

        /// <summary>
        /// Converts this to valid digest string.
        /// </summary>
        /// <returns>Returns digest string.</returns>
        public override string ToString()
        {
            StringBuilder retVal = new StringBuilder();
            retVal.Append("realm=\"" + m_Realm + "\",");
            retVal.Append("username=\"" + m_UserName + "\",");
            if(!string.IsNullOrEmpty(m_Qop)){
                retVal.Append("qop=\"" + m_Qop + "\",");
            }
            retVal.Append("nonce=\"" + m_Nonce + "\",");
            retVal.Append("nc=\"" + m_NonceCount + "\",");
            retVal.Append("cnonce=\"" + m_Cnonce + "\",");
            retVal.Append("response=\"" + m_Response + "\",");
            retVal.Append("opaque=\"" + m_Opaque + "\",");
            retVal.Append("uri=\"" + m_Uri + "\"");            

            return retVal.ToString();
        }

        #endregion

        #region method ToChallange

        /// <summary>
        /// Creates 'Challange' data using this class info. 
        /// </summary>
        /// <returns>Returns Challange data.</returns>
        public string ToChallange()
        {
            // digest realm="",qop="",nonce="",opaque=""

            return "digest " + "realm=\"" + m_Realm + "\",nonce=\"" + m_Nonce + "\",opaque=\"" + m_Opaque + "\"";
        }

        #endregion

        #region method ToAuthorization

        /// <summary>
        /// Creates 'Authorization' data using this class info.
        /// </summary>
        /// <returns>Return Authorization data.</returns>
        public string ToAuthorization()
        {
            // digest realm="",username="",nonce="",qop=,nc=,cnonce="",response="",opaque=""

            string response = "";
            if(string.IsNullOrEmpty(m_Password)){
                response = m_Response;
            }
            else{
                response = CalculateResponse(m_UserName,m_Password);
            }

            StringBuilder authData = new StringBuilder();
            authData.Append("Digest ");
            authData.Append("realm=\"" + m_Realm + "\",");
            authData.Append("username=\"" + m_UserName + "\",");
            authData.Append("nonce=\"" + m_Nonce + "\",");
            if(!string.IsNullOrEmpty(m_Uri)){
                authData.Append("uri=\"" + m_Uri + "\",");
            }
            if(!string.IsNullOrEmpty(m_Qop)){
                authData.Append("qop=\"" + m_Qop + "\",");
            }
            // nc value must be specified only if qop is present.
            if(!string.IsNullOrEmpty(m_Qop)){
                authData.Append("nc=" + m_NonceCount.ToString("x8") + ",");
            }
            if(!string.IsNullOrEmpty(m_Qop)){
                authData.Append("cnonce=\"" + m_Cnonce + "\",");
            }
            authData.Append("response=\"" + response + "\",");
            if(!string.IsNullOrEmpty(m_Opaque)){
                authData.Append("opaque=\"" + m_Opaque + "\",");
            }
            if(!string.IsNullOrEmpty(m_Algorithm)){
                authData.Append("algorithm=" + m_Algorithm + ",");
            }

            string retVal = authData.ToString().Trim();
            if(retVal.EndsWith(",")){
                retVal = retVal.Substring(0,retVal.Length - 1);
            }

            return retVal;
        }

        #endregion


        #region static method CreateNonce

        /// <summary>
        /// Creates valid nonce value.
        /// </summary>
        /// <returns>Returns nonce value.</returns>
        public static string CreateNonce()
        {
            return Guid.NewGuid().ToString().Replace("-","");
        }

        #endregion

        #region static method CreateOpaque

        /// <summary>
        /// Creates valid opaque value.
        /// </summary>
        /// <returns>Renturn opaque value.</returns>
        public static string CreateOpaque()
        {
            return Guid.NewGuid().ToString().Replace("-","");
        }

        #endregion


        #region Properties Implementation

        /// <summary>
        /// Gets or sets request method.
        /// </summary>
        public string RequestMethod
        {
            get{ return m_Method; }

            set{
                if(value == null){
                    value = "";
                }
                m_Method = value;
            }
        }

        /// <summary>
        /// Gets or sets a string to be displayed to users so they know which username and password 
        /// to use. This string should contain at least the name of the host performing the 
        /// authentication and might additionally indicate the collection of users who might have access.
        /// An example might be "registered_users@gotham.news.com".
        /// </summary>
        public string Realm
        {
            get{ return m_Realm; }

            set{
                if(value == null){
                    value = "";
                }
                m_Realm = value;
            }
        }

        /// <summary>
        /// Gets or sets a server-specified unique data string. It is recommended that this 
        /// string be base64 or hexadecimal data. 
        /// Suggested value: base64(time-stamp hex(time-stamp ":" ETag ":" private-key)).
        /// </summary>
        /// <exception cref="ArgumentException">Is raised when invalid value is specified.</exception>
        public string Nonce
        {
            get{ return m_Nonce; }

            set{
                if(string.IsNullOrEmpty(value)){
                    throw new ArgumentException("Nonce value can't be null or empty !");
                }

                m_Nonce = value;
            }
        }

        /// <summary>
        /// Gets or sets string of data, specified by the server, which should be returned by the client unchanged.
        /// It is recommended that this string be base64 or hexadecimal data.
        /// </summary>
        /// <exception cref="ArgumentException">Is raised when invalid value is specified.</exception>
        public string Opaque
        {
            get{ return m_Opaque; }

            set{ m_Opaque = value; }
        }

        /*
        public bool Stale
        {
            get{ return false; }
        }
        */
        
        /// <summary>
        /// Gets or sets algorithm to use to produce the digest and a checksum.
        /// This is normally MD5 or MD5-sess.
        /// </summary>
        public string Algorithm
        {
            get{ return m_Algorithm; }

            set{ m_Algorithm = value; }
        }
        

        /// <summary>
        /// Gets a string of 32 hex digits computed by HTTP digest algorithm, 
        /// which proves that the user knows a password.
        /// </summary>
        public string Response
        {
            get{ return m_Response; }
        }

        /// <summary>
        /// Gets or sets user name.
        /// </summary>
        public string UserName
        {
            get{ return m_UserName; }

            set{
                if(value == null){
                    value = "";
                }
                m_UserName = value;
            }
        }

        /// <summary>
        /// Gets or sets password.
        /// </summary>
        public string Password
        {
            get{ return m_Password; }

            set{
                if(value == null){
                    value = "";
                }
                m_Password = value;
            }
        }

        /// <summary>
        /// Gets the URI from Request-URI.
        /// </summary>
        public string Uri
        {
            get{ return m_Uri; }

            set{ m_Uri = value; }
        }

        /// <summary>
        /// Gets or sets value what indicates "quality of protection" the client has applied to
        /// the message. If present, its value MUST be one of the alternatives the server indicated
        /// it supports in the WWW-Authenticate header. This directive is optional in order to preserve 
        /// backward compatibility.
        /// </summary>
        public string Qop
        {
            get{ return m_Qop; }

            set{ m_Qop = value; }
        }

        /// <summary>
        /// Gets or sets Client nonce value. This MUST be specified if a qop directive is sent (see above), and
        /// MUST NOT be specified if the server did not send a qop directive in the WWW-Authenticate header field.
        /// </summary>
        public string CNonce
        {
            get{ return m_Cnonce; }

            set{
                if(value == null){
                    value = "";
                }
                m_Cnonce = value;
            }
        }

        /// <summary>
        /// Gets nonce count. This MUST be specified if a qop directive is sent (see above), and
        /// MUST NOT be specified if the server did not send a qop directive in the WWW-Authenticate 
        /// header field.  The nc-value is the hexadecimal count of the number of requests.
        /// </summary>
        public int NonceCount
        {
            get{ return m_NonceCount;}
        }
                
        #endregion

    }
}

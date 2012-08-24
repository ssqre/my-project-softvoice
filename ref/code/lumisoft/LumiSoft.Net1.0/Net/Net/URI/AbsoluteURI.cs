using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net
{
    /// <summary>
    /// Implements absolute-URI. Defined in RFC 3986.4.3.
    /// </summary>
    public class AbsoluteURI
    {
        private string m_Scheme = "";
        private string m_Value  = "";

        /// <summary>
        /// Default constructor,
        /// </summary>
        public AbsoluteURI()
        {
        }

        /*
        /// <summary>
        /// Parse URI from string value.
        /// </summary>
        /// <param name="value">String URI value.</param>
        protected virtual void Parse(string value)
        {
        }*/


        #region Properties Implementation

        /// <summary>
        /// Gets URI scheme.
        /// </summary>
        public string Scheme
        {
            get{ return m_Scheme; }
        }

        /// <summary>
        /// Gets or sets URI value after scheme.
        /// </summary>
        public string Value
        {
            get{ return m_Value; }

            set{
                if(value == null){
                    throw new ArgumentNullException();
                }

                m_Value = value;
            }
        }

        #endregion

    }
}

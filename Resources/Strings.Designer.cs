﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.34209
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Erp.BO.CRTI_MiscAction.Resources
{
    using System;
    
    
    /// <summary>
    /// A strongly-typed resource class for looking up localized (formatted) strings.
    ///This is a Server Side Strings Resource File. It may contain only string entries.
    /// </summary>
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Ice.Explorer.SingleFileGenerators.StringsDesignerGenerator", "3.0.1.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    internal class Strings : Ice.Resources.StringsBase<Erp.BO.CRTI_MiscAction.Resources.Strings>
    {
        
        protected Strings()
        {
        }
        
        /// <summary>
        /// String: 'There is at least one job in Industria ({0}) that use the order line {1}. Are you sure you want to modify this line?'.
        /// </summary>
        internal static string ConfirmUpdateIndustriaJob(object arg0, object arg1)
        {
            return GetString("ConfirmUpdateIndustriaJob", arg0, arg1);
        }
        
        /// <summary>
        /// String: 'There is at least one job in Industria ({0}) that use the component order line {1}. Are you sure you want to modify this line?'.
        /// </summary>
        internal static string ConfirmUpdateIndustriaJobKit(object arg0, object arg1)
        {
            return GetString("ConfirmUpdateIndustriaJobKit", arg0, arg1);
        }
        
        /// <summary>
        /// String: 'The Industria ship quantity ({0}) is different than Epicor ship quantity ({1}) for the line {2}.'.
        /// </summary>
        internal static string ErrorIndustriaShipQty(object arg0, object arg1, object arg2)
        {
            return GetString("ErrorIndustriaShipQty", arg0, arg1, arg2);
        }
        
        /// <summary>
        /// String: 'This date is not included in the production calendar.'.
        /// </summary>
        internal static string InvalidDateProdCalendar
        {
            get
            {
                return GetString("InvalidDateProdCalendar");
            }
        }
        
        /// <summary>
        /// String: 'This date is not included in the carrier calendar.'.
        /// </summary>
        internal static string InvalidDateShipViaCalendar
        {
            get
            {
                return GetString("InvalidDateShipViaCalendar");
            }
        }
        
        /// <summary>
        /// String: 'No production calendar found for the company'.
        /// </summary>
        internal static string NoCalendar
        {
            get
            {
                return GetString("NoCalendar");
            }
        }
    }
}
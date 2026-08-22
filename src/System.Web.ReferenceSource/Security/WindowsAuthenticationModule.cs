//------------------------------------------------------------------------------
// <copyright file="WindowsAuthenticationModule.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

/*
 * WindowsAuthenticationModule class
 * 
 * Copyright (c) 1999 Microsoft Corporation
 */

namespace System.Web.Security {
    using System;
    using System.Security.Permissions;
    using System.Security.Principal;
    using System.Web;
    using System.Web.Configuration;
    using System.Web.Util;

    /// <devdoc>
    ///    <para>
    ///       Allows ASP.NET applications to use Windows/IIS authentication.
    ///    </para>
    /// </devdoc>
    public sealed class WindowsAuthenticationModule : IHttpModule {

        private WindowsAuthenticationEventHandler _eventHandler;

#if NETFRAMEWORK
        private static bool             _fAuthChecked;
        private static bool             _fAuthRequired;
#endif

        // anonymous identity + principal are static for easy referencing + reuse
#if NETFRAMEWORK
        private static readonly WindowsIdentity AnonymousIdentity = WindowsIdentity.GetAnonymous();
        internal static readonly WindowsPrincipal AnonymousPrincipal = new WindowsPrincipal(AnonymousIdentity);
#else
        // WindowsIdentity.GetAnonymous throws off Windows. As fields of this type they would refuse
        // the module itself, and the module is registered in every application's collection; a
        // nested holder defers the throw to the Windows-only paths that read it, none of which run
        // unless <authentication mode="Windows">, which activation refuses.
        private static class Anonymous {
            internal static readonly WindowsIdentity Identity = WindowsIdentity.GetAnonymous();
            internal static readonly WindowsPrincipal Principal = new WindowsPrincipal(Identity);
        }

        private static WindowsIdentity AnonymousIdentity {
            get { return Anonymous.Identity; }
        }

        internal static WindowsPrincipal AnonymousPrincipal {
            get { return Anonymous.Principal; }
        }
#endif


        /// <devdoc>
        ///    <para>
        ///       Initializes a new instance of the <see cref='System.Web.Security.WindowsAuthenticationModule'/>
        ///       class.
        ///     </para>
        /// </devdoc>
        [SecurityPermission(SecurityAction.Demand, Unrestricted=true)]
        public WindowsAuthenticationModule() {
        }


        /// <devdoc>
        ///    This is a global.asax event that must be
        ///    named WindowsAuthenticate_OnAuthenticate event. It's used primarily to attach a
        ///    custom IPrincipal object to the context.
        /// </devdoc>
        public event WindowsAuthenticationEventHandler Authenticate {
            add {
                _eventHandler += value;
            }
            remove {
                _eventHandler -= value;
            }
        }


        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public void Dispose() {
        }


        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public void Init(HttpApplication app) {
            app.AuthenticateRequest += new EventHandler(this.OnEnter);
        }

        ////////////////////////////////////////////////////////////
        // OnAuthenticate: Custom Authentication modules can override
        //             this method to create a custom IPrincipal object from
        //             a WindowsIdentity

        /// <devdoc>
        ///    Calls the
        ///    WindowsAuthentication_OnAuthenticate handler if one exists.
        /// </devdoc>
        void OnAuthenticate(WindowsAuthenticationEventArgs e) {
            ////////////////////////////////////////////////////////////
            // If there are event handlers, invoke the handlers
            if (_eventHandler != null)
                 _eventHandler(this, e);

            if (e.Context.User == null)
            {
                if (e.User != null)
                    e.Context.User = e.User;
                else  if (e.Identity == AnonymousIdentity)
                    e.Context.SetPrincipalNoDemand(AnonymousPrincipal, false /*needToSetNativePrincipal*/);
                else
                    e.Context.SetPrincipalNoDemand(new WindowsPrincipal(e.Identity), false /*needToSetNativePrincipal*/);
            }
        }



        ////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////
        // Methods for internal implementation

        /// <internalonly/>
        /// <devdoc>
        /// </devdoc>
        [SecurityPermission(SecurityAction.Assert, UnmanagedCode = true, ControlPrincipal = true)]
        void OnEnter(Object source, EventArgs eventArgs) {
            if (!IsEnabled)
                return;

            HttpApplication         app = (HttpApplication)source;
            HttpContext             context = app.Context;;
            WindowsIdentity         identity = null;
            
            //////////////////////////////////////////////////////////////////
            // Step 2: Create a Windows Identity from the credentials from IIS
            if (HttpRuntime.UseIntegratedPipeline) {

                // The native WindowsAuthenticationModule sets the user principal in IIS7WorkerRequest.SynchronizeVariables.
                // The managed WindowsAuthenticationModule provides backward compatibility by rasing the OnAuthenticate event.
                WindowsPrincipal user = context.User as WindowsPrincipal;
                if (user != null) {
                    // identity will be null if this is not a WindowsIdentity
                    identity = user.Identity as WindowsIdentity;
                    // clear Context.User for backward compatibility (it will be set in OnAuthenticate)
                    context.SetPrincipalNoDemand(null, false /*needToSetNativePrincipal*/);
                }
            }
            else {
                String  strLogonUser  = context.WorkerRequest.GetServerVariable("LOGON_USER");
                String  strAuthType   = context.WorkerRequest.GetServerVariable("AUTH_TYPE");
                if (strLogonUser == null) {
                    strLogonUser = String.Empty;
                }
                if (strAuthType == null) {
                    strAuthType = String.Empty;
                }

                if (strLogonUser.Length == 0 && (strAuthType.Length == 0 || 
                                                 StringUtil.EqualsIgnoreCase(strAuthType, "basic"))) 
                {
                    ////////////////////////////////////////////////////////
                    // Step 2a: Use the anonymous identity
                    identity = AnonymousIdentity;
                }
                else
                {
                    identity = new WindowsIdentity(
                        context.WorkerRequest.GetUserToken(), 
                        strAuthType,
                        WindowsAccountType.Normal,
                        true);
                }
            }

            ///////////////////////////////////////////////////////////////////////////////////
            // Step 3: Call OnAuthenticate to create IPrincipal for this request.
            if (identity != null) {
                OnAuthenticate( new WindowsAuthenticationEventArgs(identity, context) );
            }
        }

        internal static bool IsEnabled {
            get {
#if NETFRAMEWORK
                if (!_fAuthChecked) {
                    _fAuthRequired = (AuthenticationConfig.Mode == AuthenticationMode.Windows);
                    _fAuthChecked = true;
                }
                return _fAuthRequired;
#else
                // The section default is Windows, so answering the mode would enable this for every
                // application that declares no <authentication> element — and off Windows the
                // anonymous identity it then builds does not exist. No worker request here carries
                // a Windows login and a declared mode="Windows" refuses activation, so the module
                // registers per the IIS golden and never acts.
                return false;
#endif
            }
        }
    }
}

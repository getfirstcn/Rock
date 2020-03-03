// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using Rock;
using Rock.Attribute;
using Rock.Blocks;
using Rock.Communication;
using Rock.Data;
using Rock.Model;
using Rock.Net;
using Rock.Security;
using Rock.Web.Cache;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace RockWeb.Blocks.Security
{
    /// <summary>
    /// Prompts user for login credentials.
    /// </summary>
    [DisplayName( "Login" )]
    [Category( "Security" )]
    [Description( "Prompts user for login credentials." )]

    [LinkedPage( "New Account Page", "Page to navigate to when user selects 'Create New Account' (if blank will use 'NewAccountPage' page route)", false, "", "", 0 )]
    [LinkedPage( "Help Page", "Page to navigate to when user selects 'Help' option (if blank will use 'ForgotUserName' page route)", false, "", "", 1 )]
    [CodeEditorField( "Confirm Caption", "The text (HTML) to display when a user's account needs to be confirmed.", CodeEditorMode.Html, CodeEditorTheme.Rock, 100, false, @"
Thank you for logging in, however, we need to confirm the email associated with this account belongs to you. We’ve sent you an email that contains a link for confirming.  Please click the link in your email to continue.
", "", 2 )]
    [LinkedPage( "Confirmation Page", "Page for user to confirm their account (if blank will use 'ConfirmAccount' page route)", false, "", "", 3 )]
    [SystemCommunicationField( "Confirm Account Template", "Confirm Account Email Template", false, Rock.SystemGuid.SystemCommunication.SECURITY_CONFIRM_ACCOUNT, "", 4 )]
    [CodeEditorField( "Locked Out Caption", "The text (HTML) to display when a user's account has been locked.", CodeEditorMode.Html, CodeEditorTheme.Rock, 100, false, @"
{%- assign phone = Global' | Attribute:'OrganizationPhone' | Trim -%} Sorry, your account has been locked.  Please {% if phone != '' %}contact our office at {{ 'Global' | Attribute:'OrganizationPhone' }} or email{% else %}email us at{% endif %} <a href='mailto:{{ 'Global' | Attribute:'OrganizationEmail' }}'>{{ 'Global' | Attribute:'OrganizationEmail' }}</a> for help. Thank you.
", "", 5 )]
    [BooleanField( "Hide New Account Option", "Should 'New Account' option be hidden?  For sites that require user to be in a role (Internal Rock Site for example), users shouldn't be able to create their own account.", false, "", 6, "HideNewAccount" )]
    [TextField( "New Account Text", "The text to show on the New Account button.", false, "Register", "", 7, "NewAccountButtonText" )]
    [CodeEditorField( "No Account Text", "The text to show when no account exists. <span class='tip tip-lava'></span>.", CodeEditorMode.Html, CodeEditorTheme.Rock, 100, false, @"We couldn’t find an account with that username and password combination. Can we help you recover your <a href='{{HelpPage}}'>account information</a>?", "", 8, "NoAccountText" )]

    [CodeEditorField( "Remote Authorization Prompt Message", "Optional text (HTML) to display above remote authorization options.", CodeEditorMode.Html, CodeEditorTheme.Rock, 100, false, defaultValue: "Login with social account", order: 9 )]
    [RemoteAuthsField( "Remote Authorization Types", "Which of the active remote authorization types should be displayed as an option for user to use for authentication.", false, "", "", 10 )]
    [BooleanField( "Show Internal Login", "Show the default (non-remote) login", defaultValue: true, order: 11 )]
    [BooleanField( "Redirect to Single External Auth Provider", "Redirect straight to the external authentication provider if only one is configured and the internal login is disabled.", defaultValue: false, order: 12 )]

    [CodeEditorField( "Prompt Message", "Optional text (HTML) to display above username and password fields.", CodeEditorMode.Html, CodeEditorTheme.Rock, 100, false, @"", "", 13 )]
    [LinkedPage( "Redirect Page", "Page to redirect user to upon successful login. The 'returnurl' query string will always override this setting for database authenticated logins. Redirect Page Setting will override third-party authentication 'returnurl'.", false, "", "", 14 )]

    [CodeEditorField( "Invalid PersonToken Text", "The text to show when a person is logged out due to an invalid persontoken. <span class='tip tip-lava'></span>.", CodeEditorMode.Html, CodeEditorTheme.Rock, 100, false, @"<div class='alert alert-warning'>The login token you provided is no longer valid. Please login below.</div>", "", 15 )]
    public partial class Login : RockRestBlock
    {
        /// <summary>
        /// Gets the root element identifier. Should be something like upnlMyBlock.ClientId
        /// </summary>
        /// <returns></returns>
        protected override string GetRootElementId()
        {
            return string.Format( "login_{0}", _instanceGuid );
        }

        private readonly Guid _instanceGuid = Guid.NewGuid();

        #region Base Control Methods

        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );
        }

        #endregion

        #region Events

        /// <summary>
        /// Handles the Click event of the btnLogin control.
        /// NOTE: This is the btnLogin for Internal Auth
        /// </summary>
        /// <param name="password"></param>
        /// <param name="rememberMe"></param>
        /// <param name="username"></param>
        [BlockAction( "login" )]
        public LoginResult BlockActionLogin( string username, string password, bool rememberMe )
        {
            var rockContext = new RockContext();
            var userLoginService = new UserLoginService( rockContext );
            var userLogin = userLoginService.GetByUserName( username );
            if ( userLogin != null && userLogin.EntityType != null )
            {
                var component = AuthenticationContainer.GetComponent( userLogin.EntityType.Name );
                if ( component != null && component.IsActive && !component.RequiresRemoteAuthentication )
                {
                    var isSuccess = component.AuthenticateAndTrack( userLogin, password );
                    rockContext.SaveChanges();

                    if ( isSuccess )
                    {
                        var result = new LoginResult();

                        if ( ( userLogin.IsConfirmed ?? true ) && !IsLockedOut( userLogin ) )
                        {
                            UserLoginService.UpdateLastLogin( userLogin.UserName );
                            result.AuthCookie = Authorization.GetAuthCookie( userLogin.UserName, rememberMe, false );

                            if ( result.AuthCookie != null )
                            {
                                result.DomainCookie = Authorization.GetDomainCookie( result.AuthCookie );
                                result.IsSuccess = true;
                            }
                        }
                        else if (IsLockedOut(userLogin))
                        {
                            result.ErrorMessage = GetLockoutMessage();
                        }

                        return result;
                    }
                }
            }

            var helpUrl = GetHelpPageUrl();
            var mergeFieldsNoAccount = Rock.Lava.LavaHelper.GetCommonMergeFields( this.RockPage, this.CurrentPerson );
            mergeFieldsNoAccount.Add( "HelpPage", helpUrl );

            return new LoginResult {
                ErrorMessage = GetAttributeValue( "NoAccountText" ).ResolveMergeFields( mergeFieldsNoAccount )
            };
        }

        /// <summary>
        /// Determines whether the user login is locked out.
        /// </summary>
        /// <param name="userLogin">The user login.</param>
        /// <returns>
        ///   <c>true</c> if [is locked out] [the specified user login]; otherwise, <c>false</c>.
        /// </returns>
        private bool IsLockedOut(UserLogin userLogin)
        {
            return userLogin.IsLockedOut ?? false;
        }

        /// <summary>
        /// Gets the message for a lockout
        /// </summary>
        /// <param name="userLogin">The user login.</param>
        /// <param name="mergeFields">The merge fields.</param>
        /// <returns>True if the user is locked out.</returns>
        private string GetLockoutMessage( Dictionary<string, object> mergeFields = null )
        {
            return GetAttributeValue( "LockedOutCaption" ).ResolveMergeFields( mergeFields );
        }

        /// <summary>
        /// Handles the Click event of the btnHelp control.
        /// </summary>
        [BlockAction( "help" )]
        public string BlockActionHelp()
        {
            return GetHelpPageUrl();
        }

        private string GetHelpPageUrl()
        {
            if ( !string.IsNullOrWhiteSpace( GetAttributeValue( "HelpPage" ) ) )
            {
                return LinkedPageUrl( "HelpPage" );
            }
            else
            {
                return "~/ForgotUserName";
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Sends the confirmation.
        /// </summary>
        /// <param name="userLogin">The user login.</param>
        private void SendConfirmation( UserLogin userLogin )
        {
            string url = LinkedPageUrl( "ConfirmationPage" );
            if ( string.IsNullOrWhiteSpace( url ) )
            {
                url = ResolveRockUrl( "~/ConfirmAccount" );
            }

            var mergeFields = Rock.Lava.LavaHelper.GetCommonMergeFields( RockPage, CurrentPerson );
            mergeFields.Add( "ConfirmAccountUrl", RootPath + url.TrimStart( '/' ) );
            mergeFields.Add( "Person", userLogin.Person );
            mergeFields.Add( "User", userLogin );

            var recipients = new List<RockEmailMessageRecipient>();
            recipients.Add( new RockEmailMessageRecipient( userLogin.Person, mergeFields ) );

            var message = new RockEmailMessage( GetAttributeValue( "ConfirmAccountTemplate" ).AsGuid() );
            message.SetRecipients( recipients );
            message.AppRoot = ResolveRockUrl( "~/" );
            message.ThemeRoot = ResolveRockUrl( "~~/" );
            message.CreateCommunicationRecord = false;
            message.Send();
        }

        #endregion
    }

    /// <summary>
    /// A login result object
    /// </summary>
    public class LoginResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether this instance is success.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is success; otherwise, <c>false</c>.
        /// </value>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the cookie.
        /// </summary>
        /// <value>
        /// The cookie.
        /// </value>
        public HttpCookie AuthCookie { get; set; }

        /// <summary>
        /// Gets or sets the domain cookie.
        /// </summary>
        /// <value>
        /// The domain cookie.
        /// </value>
        public HttpCookie DomainCookie { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>
        /// The error message.
        /// </value>
        public string ErrorMessage { get; set; }
    }

    // helpful links
    //  http://blog.prabir.me/post/Facebook-CSharp-SDK-Writing-your-first-Facebook-Application.aspx
}

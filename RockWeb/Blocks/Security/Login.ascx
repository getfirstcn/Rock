<%@ Control Language="C#" AutoEventWireup="true" CodeFile="Login.ascx.cs" Inherits="RockWeb.Blocks.Security.Login" %>
<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>

        <asp:Panel ID="pnlLogin" runat="server" CssClass="login-block">

            <fieldset>
                <legend>Login</legend>

                <div class="row">
                    <div class="alert alert-danger" v-if="errorMessage" v-html="errorMessage"></div>

                    <Rock:NotificationBox ID="nbAdminRedirectPrompt" runat="server" NotificationBoxType="Danger" Visible="false" />
                    <asp:Panel ID="pnlRemoteAuthLogins" runat="server" CssClass="col-md-6 margin-b-lg remote-logins">
                        <p>
                            <asp:Literal ID="lRemoteAuthLoginsHeadingText" runat="server" Text="Login with social account" /></p>
                        <asp:PlaceHolder ID="phExternalLogins" runat="server"></asp:PlaceHolder>
                    </asp:Panel>
                    <asp:Panel ID="pnlInternalAuthLogin" runat="server" CssClass="col-md-6">

                        <asp:ValidationSummary ID="valSummary" runat="server" HeaderText="Please correct the following:" CssClass="alert alert-validation" />

                        <asp:Literal ID="lPromptMessage" runat="server" />
                        <asp:Literal ID="lInvalidPersonTokenText" runat="server" />

                        <rock-text-box label="Username" v-model="username"></rock-text-box>
                        <rock-text-box label="Password" v-model="password" type="password"></rock-text-box>
                        <rock-checkbox label="Keep me logged in" v-model="rememberMe"></rock-checkbox>
                        
                        <rock-button :is-loading="isLoading" loading-text="Logging In..." label="Log In" class="btn btn-primary" @click="submitLogin"></rock-button>
                        <asp:Button ID="btnNewAccount" runat="server" Text="Register" CssClass="btn btn-action" OnClick="btnNewAccount_Click" CausesValidation="false" />
                        <asp:Button ID="btnHelp" runat="server" Text="Forgot Account" CssClass="btn btn-link" OnClick="btnHelp_Click" CausesValidation="false" />

                        <asp:Panel ID="pnlMessage" runat="server" Visible="false" CssClass="alert alert-warning block-message margin-t-md" />

                    </asp:Panel>
                </div>
            </fieldset>
        </asp:Panel>


        <asp:Panel ID="pnlLockedOut" runat="server" Visible="false">

            <div class="alert alert-danger">
                <asp:Literal ID="lLockedOutCaption" runat="server" />
            </div>

        </asp:Panel>

        <asp:Panel ID="pnlConfirmation" runat="server" Visible="false">

            <div class="alert alert-warning">
                <asp:Literal ID="lConfirmCaption" runat="server" />
            </div>

        </asp:Panel>

    </ContentTemplate>
</asp:UpdatePanel>



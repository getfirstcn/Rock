<%@ Control Language="C#" AutoEventWireup="true" CodeFile="Login.ascx.cs" Inherits="RockWeb.Blocks.Security.Login" %>

<div id="<%= GetRootElementId() %>">
    <div class="login-block">
        <fieldset>
            <legend>Login</legend>

            <div class="alert alert-danger" v-if="errorMessage" v-html="errorMessage"></div>

            <form @submit.prevent="submitLogin">
                <rock-text-box label="Username" v-model="username"></rock-text-box>
                <rock-text-box label="Password" v-model="password" type="password"></rock-text-box>
                <rock-checkbox label="Keep me logged in" v-model="rememberMe"></rock-checkbox>
                <rock-button :is-loading="isLoading" loading-text="Logging In..." label="Log In" class="btn btn-primary" @click="submitLogin" type="submit"></rock-button>
            </form>

            <rock-button :is-loading="isLoading" label="Forgot Account" class="btn btn-link" @click="onHelpClick"></rock-button>

        </fieldset>
    </div>
</div>

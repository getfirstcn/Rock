﻿window.Rock.RestBlocks['Blocks/Security/Login'] = function ({ rootElement, pageId, blockId }) {
    const setCookie = function (cookie) {
        let expires = '';

        if (cookie.Expires) {
            const date = new Date(cookie.Expires);

            if (date < new Date()) {
                expires = '';
            }
            else {
                expires = "; expires=" + date.toGMTString();
            }
        }
        else {
            expires = "";
        }

        document.cookie = cookie.Name + "=" + cookie.Value + expires + "; path=/";
    };

    const redirectAfterLogin = function () {
        const urlParams = new URLSearchParams(window.location.search);
        const returnUrl = urlParams.get('returnurl');

        // TODO make this force relative URLs (no absolute URLs)
        window.location.href = decodeURIComponent(returnUrl);
    };

    new Vue({
        el: rootElement,
        data: function () {
            return {
                username: '',
                password: '',
                rememberMe: false,
                isLoading: false,
                errorMessage: ''
            };
        },
        methods: {
            submitLogin: async function() {
                const url = '/api/blocks/action/' + pageId + '/' + blockId + '/action_login';
                this.isLoading = true;
                this.errorMessage = '';

                try {
                    const result = await this.$http.post(url, {
                        username: this.username,
                        password: this.password,
                        rememberMe: this.rememberMe
                    });

                    const loginResult = result.data;

                    if (!loginResult || !loginResult.IsSuccess || loginResult.ErrorMessage) {
                        this.errorMessage = loginResult.ErrorMessage || 'An unknown error occurred communicating with the server';
                    }

                    if (loginResult && loginResult.AuthCookie) {
                        setCookie(loginResult.AuthCookie);

                        if (loginResult.DomainCookie) {
                            setCookie(loginResult.DomainCookie);
                        }

                        redirectAfterLogin();
                    }
                }
                catch (e) {
                    this.errorMessage = `An exception occurred: ${e}`;
                }
                finally {
                    this.isLoading = false;
                }
            }
        }
    });
};

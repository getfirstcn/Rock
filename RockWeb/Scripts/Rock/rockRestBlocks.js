(function () {
    window.Rock = window.Rock || {};
    window.Rock.RestBlocks = {
        getGuid: function() {
            const S4 = () => (((1 + Math.random()) * 0x10000) | 0).toString(16).substring(1);
            return S4() + S4() + "-" + S4() + "-" + S4() + "-" + S4() + "-" + S4() + S4() + S4();
        }
    };

    Vue.prototype.$http = axios;
})();

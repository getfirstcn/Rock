(function () {
    window.Rock = window.Rock || {};
    window.Rock.RestBlocks = {
        getGuid: function() {
            const S4 = () => (((1 + Math.random()) * 0x10000) | 0).toString(16).substring(1);
            return S4() + S4() + "-" + S4() + "-" + S4() + "-" + S4() + "-" + S4() + S4() + S4();
        },
        blockActionFactory: function ({ blockId, pageId }) {
            return async function (actionName, data) {
                return await axios.post(`/api/blocks/action/${pageId}/${blockId}/${actionName}`, data);
            };
        }
    };

    Vue.prototype.$http = axios;
})();

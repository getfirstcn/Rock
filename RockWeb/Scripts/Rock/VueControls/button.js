Vue.component('RockButton', {
    props: {
        label: {
            type: String,
            required: true
        },
        isLoading: {
            type: Boolean,
            default: false
        },
        loadingText: {
            type: String,
            default: 'Loading...'
        }
    },
    methods: {
        handleClick: function() {
            this.$emit('click');
        }
    },
    template:
`<a href="javascript:void(0);" class="btn" :disabled="isLoading" @click="handleClick">
    {{isLoading ? loadingText : label}}
</a>`
});

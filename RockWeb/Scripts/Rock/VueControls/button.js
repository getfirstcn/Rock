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
`<button class="btn" :disabled="isLoading" @click="handleClick">
    {{isLoading ? loadingText : label}}
</button>`
});

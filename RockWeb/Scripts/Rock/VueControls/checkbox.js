Vue.component('RockCheckbox', {
    props: {
        value: {
            type: Boolean,
            required: true
        },
        label: {
            type: String,
            required: true
        }
    },
    data: function() {
        return {
            uniqueId: `rock-checkbox-${Rock.RestBlocks.getGuid()}`,
            internalValue: this.value
        };
    },
    methods: {
        handleInput: function() {
            this.$emit('input', this.internalValue);
        },
        handleChange: function() {
            this.$emit('change', this.internalValue);
        }
    },
    watch: {
        value: function() {
            this.internalValue = this.value;
        }
    },
    template:
`<div class="checkbox">
    <label title="">
        <input type="checkbox" v-model="internalValue" />
        <span class="label-text ">{{label}}</span>
    </label>
</div>`
});

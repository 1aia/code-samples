import Ember from 'ember';

export default Ember.TextField.extend({
  classNames: ['form-control'],
  becomeFocused: function() {
    this.$().focus();
  }.on('didInsertElement')
});

import Ember from 'ember';
import EmberValidations from 'ember-validations';

export default Ember.Component.extend(EmberValidations, {
  isFromComp: true,
  model: null,
  libSelection: null,
  modelCopy: null,
  dataService: Ember.inject.service('data'),
  apply: 'update',

  init(){
    this.set('modelCopy', Ember.copy(this.get('model')) || {});
    this.set('libSelection', Ember.copy(this.get('model')) || {});
    return this._super();
  },

  didInsertElement: function() {
    var me = this;
    var modal = this.$('.modal');

    modal.on('hidden.bs.modal', function () {
      me.set('editing', false);
    });
    modal.modal();
  },

  actions: {
    apply(mediaobject){
      let me = this;
      me.sendAction('apply', mediaobject);
      me.$('.modal').modal('hide');
    },

    fromComp(){
      this.set('isFromComp', true);
    },
    fromLib(){
      this.set('isFromComp', false);
    }
  }
});

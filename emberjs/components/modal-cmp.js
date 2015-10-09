import Ember from 'ember';

export default Ember.Component.extend({
  confirmAction: 'confirm',
  title: 'Подтвердите действие',
  okBtnText: 'Подтверждаю',

  init() {
    this._super(arguments);
    this.classNames = ['modal', 'fade'];
  },

  didInsertElement: function() {
    var me = this;
    var modal = this.$();

    modal.on('hidden.bs.modal', function () {
      if(!(me.isDestroying || me.isDestroyed)){
        me.set('editing', false);
      }
    });
    modal.modal();
  },

  actions: {
    confirm(){
      this.$().modal('hide');
      this.sendAction('confirmAction');
    },
    cancel(){
      this.$().modal('hide');
    }
  }
});

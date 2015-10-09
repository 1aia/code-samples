import Ember from 'ember';

export default Ember.Component.extend({
  classNames: ['confirm-button-inline'],
  btnclass: 'btn btn-default',
  title: 'Подтвердите действие',
  okBtnText: 'Подтверждаю',
  icon: false,
  confirmAction: null,

  showModalInner(){
    this.set('modal', true);
  },

  actions: {
    showModal(){
      var me = this;
      var confirmAction = this.get('confirmAction');
      if(confirmAction){
        var promise = confirmAction.apply(this);
        if(promise && promise.then){
          promise.then(function(){
            me.showModalInner();
          });
        } else {
          this.showModalInner();
        }
      }
      else {
        this.showModalInner();
      }
    },
    confirm(){
      this.sendAction('ok', this.get('param'));
    },
  }
});

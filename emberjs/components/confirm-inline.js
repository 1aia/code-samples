import Ember from 'ember';

export default Ember.Component.extend({
  tagName: 'span',
  title: 'Подтвердите действие',
  okBtnText: 'Подтверждаю',

  showModalInner(){
    this.set('modal', true);
  },

  actions: {
    showModal(){
      this.showModalInner();
    },
    confirm(){
      this.sendAction('ok', this.get('param'));
    }
  }
});

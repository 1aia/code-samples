import Ember from 'ember';
import EmberValidations from 'ember-validations';

export default Ember.Component.extend(EmberValidations, {
  tagName: 'tr',
  modelObserver: function(){
    Ember.set(this.get('model'), 'isValid', this.get('isValid'));
  }.observes('isValid').on('init'),
  validations: {
    'model.minLevel': {
      numericality: {
        onlyInteger: true,
        greaterThanOrEqualTo: 0,
        messages:{
          numericality: 'Введите число',
          greaterThanOrEqualTo: 'Введите положительное число',
          onlyInteger: 'Введите целое число'
        }
      }
    },
    'model.maxLevel': {
      numericality: {
        onlyInteger: true,
        greaterThanOrEqualTo: 0,
        messages:{
          numericality: 'Введите число',
          greaterThanOrEqualTo: 'Введите положительное число',
          onlyInteger: 'Введите целое число'
        }
      }
    }
  },
});

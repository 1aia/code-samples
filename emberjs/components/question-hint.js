import Ember from 'ember';
import EmberValidations from 'ember-validations';

export default Ember.Component.extend(EmberValidations, {
  classNames: ['panel no-margin'],
  isSelfPersistanceMode: null,
  dictService: Ember.inject.service(),
  dataService: Ember.inject.service('data'),

  modelObserver: function(){
    Ember.run.scheduleOnce('afterRender', this, ()=>{
      Ember.set(this.get('model'), 'isValid', this.get('isValid'));
    });
  }.observes('isValid').on('init'),

  deleteHintAction: 'deleteHint',

  validations: {
    'model.penalty': {
      numericality: {
        greaterThan: 0,
        messages:{
          numericality: 'Введите число',
          greaterThan: 'Штраф должен быть положительным числом',
        }
      },
    },
    'model.content': {
      presence: {
        message: 'Заполните поле'
      }
    }
  },

  init(){
    this.set('modelCopy', Ember.copy(this.get('model')));
    this.set('isEditing', this.get('model').isNew);
    this.set('persister', this.get('isSelfPersistanceMode') ? this.selfPersister : this.flowPersister );
    return this._super();
  },

  selfPersister(){
    var me = this;
    var model = me.get('model');
    return {
      delete(){
        if(model.isNew){
          me.sendAction('deleteHintAction', model);
          return;
        }

        me.dataService.delete('questions/deleteQuestionHint', model.id).then(function() {
          me.sendAction('deleteHintAction', model);
          me.notifyService.success("Подсказка успешно удалена.");
        });
      },
      apply(){
        me.dataService.post('questions/applyQuestionHint', model).then(function(data) {
          Ember.set(model, 'isNew', false);
          Ember.set(model, 'id', data.id);
          me.set('modelCopy', Ember.copy(model));
          me.notifyService.success("Подсказка успешно cохранена.");
        });
      },
    };
  },

  flowPersister(){
    var me = this;
    var model = me.get('model');

    return {
      delete(){
        me.sendAction('deleteHintAction', model);
      },
      apply(){
        Ember.set(model, 'isNew', false);
        me.set('modelCopy', Ember.copy(model));
      },
    };
  },

  actions:{
    apply(){
      var me = this;
      if(!me.get('isValid')){
        me.notifyService.alert("Не все поля заполнены верно.");
        return;
      }

      me.set('isEditing', false);
      me.persister().apply();
    },

    edit(){
      this.set('isEditing', true);
      this.$('.panel-collapse').collapse('show');
    },

    cancel(){
      var model = this.get('model');

      if(model.isNew){
        this.persister().delete();
      }

      var modelCopy = this.get('modelCopy');

      for(var prop in modelCopy){
        Ember.set(model, prop, modelCopy[prop]);
      }

      this.set('isEditing', false);
    }
  }
});

import Ember from 'ember';
import EmberValidations from 'ember-validations';

export default Ember.Component.extend(EmberValidations, {
  tagName: 'tr',
  removeAction: 'remove',
  exams: null,
  isSelfPersistanceMode: null,

  modelObserver: function(){
    Ember.set(this.get('model'), 'isValid', this.get('isValid'));
  }.observes('isValid').on('init'),

  validations: {
    'model.examId': {
      presence: {
        message: 'Выберите экзамен'
      }
    },
    'model.difficultyLevel': {
      presence: {
        message: 'Заполните поле'
      }
    },
    'model.laborInput': {
      presence: {
        message: 'Заполните поле'
      }
    }
  },

  init(){
    this.set('modelCopy', Ember.copy(this.get('model')));
    this.set('persister', this.get('isSelfPersistanceMode') ? this.selfPersister : this.flowPersister );
    return this._super();
  },

  selfPersister: function(){
    var me = this;
    var model = me.get('model');

    return{
      delete(){
        me.dataService.delete('questions/deleteQuestionExam', model.id).then(function() {
          me.sendAction('removeAction', model);
          me.notifyService.success("Связь с экзаменом успешно удалена.");
        });
      },
      apply(){
        me.dataService.post('questions/applyQuestionExam', model).then(function(data) {
          Ember.set(model, 'isNew', false);
          Ember.set(model, 'isEditing', false);
          Ember.set(model, 'id', data.id);
          me.set('modelCopy', Ember.copy(model));
          me.notifyService.success("Связь с экзаменом успешно cохранена.");
        });
      }
    };
  },

  flowPersister: function(){
    var me = this;
    var model = me.get('model');

    return{
      delete(){
        me.sendAction('removeAction', model);
      },
      apply(){
        Ember.set(model, 'isNew', false);
        Ember.set(model, 'isEditing', false);
        me.set('modelCopy', Ember.copy(model));
      }
    };
  },

  examSections: function(){
    var examId = this.get('model.examId');

    if(!examId){
      this.set('model.examSectionId', null);
      return null;
    }

    var selectedExam = this.get('exams').findBy('id', examId);

    if(!selectedExam || !selectedExam.sections){
      this.set('model.examSectionId', null);
      return null;
    }

    if(!selectedExam.sections.findBy('id', this.get('model.examSectionId'))){
      this.set('model.examSectionId', null);
    }

    return selectedExam.sections;
  }.property('model.examId'),

  actions:{
    remove(){
      this.persister().delete();
    },
    save(){
      if (!this.get('isValid')) {
        this.notifyService.warning('Проверьте корректность заполнения полей.');
        return;
      }

      this.persister().apply();
    },
    edit(){
      this.set('model.isEditing', true);
    },
    cancel(){
      var model = this.get('model');

      if(model.isNew){
        this.sendAction('removeAction', model);
        return;
      }

      var modelCopy = this.get('modelCopy');

      for(var prop in modelCopy){
        Ember.set(model, prop, modelCopy[prop]);
      }

      Ember.set(model, 'isEditing', false);
    }
  }
});

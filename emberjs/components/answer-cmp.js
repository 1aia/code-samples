import Ember from 'ember';
import EmberValidations from 'ember-validations';

export default Ember.Component.extend(EmberValidations, {
  dictService: Ember.inject.service(),
  areExplanationsValid: true,
  isSelfPersistanceMode: null,

  hasConcepts: function(){
    return this.get('model.concepts').length > 0;
  }.property('model.concepts', 'model.concepts.length'),

  explanationsObserver: function(){
    var explanations = this.get('model.explanations');
    var res = true;
    if(explanations.length > 0){
      res = explanations.reduce(function(reduced, item) {
          return reduced && ((item.isValid === undefined) || !!(item.isValid));
      }, true);
    }

    var areExplanationsValid = !!this.get('areExplanationsValid');

    if(areExplanationsValid !== res){
      this.set('areExplanationsValid', res);
    }
  }.observes('model.explanations.length', 'model.explanations.@each.isValid'),

  modelObserver: function(){
    Ember.set(this.get('model'), 'isValid', this.get('isValid'));
  }.observes('isValid').on('init'),
  deleteVariantAction: 'deleteVariant',
  validations: {
    'model.weight': {
      numericality: {
        greaterThanOrEqualTo: 0,
        lessThanOrEqualTo: 1,
        messages:{
          numericality: 'Введите число',
          greaterThanOrEqualTo: 'Вес должен входить в промежуток 0..1 включительно',
          lessThanOrEqualTo: 'Вес должен входить в промежуток 0..1 включительно'
        }
      },
    },
    'model.content': {
      presence: {
        message: 'Заполните поле'
      }
    },
    'areExplanationsValid': {
      acceptance: {
        accept: true
      }
    }
  },

  getPanel(){
    return this.$('#variant-' + this.get('model').index);
  },

  init(){
    this.set('isEditing', this.get('model').isNew || this.get('model').isEditing);
    this.set('modelCopy', Ember.copy(this.get('model')));
    this.set('conceptsCopy', Ember.copy(this.get('model.concepts')));
    this.set('explanationsCopy', Ember.copy(this.get('model.explanations'), true));
    this.set('persister', this.get('isSelfPersistanceMode') ? this.selfPersister : this.flowPersister );
    return this._super();
  },

  selfPersister(){
    var me = this;
    var model = me.get('model');
    return {
      delete(){
        if(model.isNew){
          me.sendAction('deleteVariantAction', model);
          return;
        }

        me.dataService.delete('questions/deleteAnswerVariant', model.id).then(function() {
          me.sendAction('deleteVariantAction', model);
          me.notifyService.success("Вариант ответа успешно удален.");
        });
      },
      apply(){
        me.dataService.post('questions/applyAnswerVariant', model).then(function(data) {
          Ember.set(model, 'isNew', false);
          Ember.set(model, 'id', data.id);
          Ember.set(model, 'explanations', data.explanations);
          me.set('modelCopy', Ember.copy(model));
          me.set('conceptsCopy', Ember.copy(model.concepts));
          me.set('explanationsCopy', Ember.copy(model.explanations, true));
          me.notifyService.success("Вариант ответа успешно cохранен.");
        });
      },
    };
  },

  flowPersister(){
    var me = this;
    var model = me.get('model');
    return {
      delete(){
        me.sendAction('deleteVariantAction', model);
      },
      apply(){
        Ember.set(model, 'isNew', false);
        me.set('modelCopy', Ember.copy(model));
        me.set('conceptsCopy', Ember.copy(model.concepts));
        me.set('explanationsCopy', Ember.copy(model.explanations, true));
      },
    };
  },

  actions:{
    deleteVariant(){
      this.persister().delete();
    },
    apply(){
      var me = this;
      if(!this.get('areExplanationsValid')){
        return;
      }

      if(!this.get('isValid')){
        me.notifyService.warning("Проверьте корректность заполнения полей.");
        return;
      }

      var explanations = this.get('model').explanations;
      explanations.removeObjects(explanations.filterBy('isNew'));

      this.set('isEditing', false);
      this.persister().apply();
      //this.getPanel().collapse('hide');
    },

    deleteExplanation(item){
      this.get('model').explanations.removeObject(item);
    },
    createExplanation(){
      var me = this;
      var defaultLanguage = this.get('dictService').languages.findBy('isDefault', true);

      var newItem = {
        languageId: defaultLanguage && defaultLanguage.id,
        name: '',
        content: '',
        answerVariantId: me.get('model').id,
        isValid: false,
        isNew: true
      };

      me.get('model').explanations.pushObject(newItem);
    },

    edit(){
      this.set('isEditing', true);
      this.getPanel().collapse('show');
    },

    cancel(){
      var model = this.get('model');

      if(model.isNew){
        this.persister().delete();
      }

      var modelCopy = this.get('modelCopy');

      Ember.set(model, 'content', modelCopy.content);
      Ember.set(model, 'weight', modelCopy.weight);
      Ember.set(model, 'comment', modelCopy.comment);

      model.concepts.clear();
      this.get('conceptsCopy').map(function(x) {
        model.concepts.pushObject(x);
      });

      model.explanations.clear();
      this.get('explanationsCopy').map(function(x) {
        model.explanations.pushObject(x);
      });

      this.set('isEditing', false);
      //this.getPanel().collapse('hide');
    }
  }
});

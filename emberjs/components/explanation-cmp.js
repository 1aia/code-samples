import Ember from 'ember';
import EmberValidations from 'ember-validations';

export default Ember.Component.extend(EmberValidations, {
  classNames: ['panel no-margin'],
  isParentEditing: null,
  dictService: Ember.inject.service(),
  dataService: Ember.inject.service('data'),

  modelObserver: function(){
    Ember.set(this.get('model'), 'isValid', this.get('isValid'));
  }.observes('isValid').on('init'),

  showActionButtons: function(){
    return this.get('isParentEditing') && !this.get('isEditing');
  }.property('isEditing', 'isParentEditing'),

  parentObserver: function(){
    var isParentEditing = this.get('isParentEditing');
    if(!isParentEditing){
      var model = this.get('model');
      var modelCopy = this.get('modelCopy');

      for(var prop in modelCopy){
        Ember.set(model, prop, modelCopy[prop]);
      }
      this.set('isEditing', false);
    }
  }.observes('isParentEditing'),

  deleteExplanationAction: 'deleteExplanation',

  validations: {
    'model.name': {
      presence: {
        message: "Заполните поле"
      }
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
    this.set('persister', this.flowPersister );
    return this._super();
  },

  actions:{
    deleteExplanation(){
      var me = this;
      var model = me.get('model');
      me.sendAction('deleteExplanationAction', model);
    },

    apply(){
      var me = this;

      if(!me.get("isValid")){
        me.notifyService.warning("Проверьте корректность заполнения полей.");
        return;
      }

      var model = me.get('model');

      me.set('isEditing', false);
      Ember.set(model, 'isNew', false);
      me.set('modelCopy', Ember.copy(model));
    },

    edit(){
      this.set('isEditing', true);
      this.$('.panel-collapse').collapse('show');
    },

    cancel(){
      var model = this.get('model');
      var modelCopy = this.get('modelCopy');

      for(var prop in modelCopy){
        Ember.set(model, prop, modelCopy[prop]);
      }

      this.set('isEditing', false);
    }
  }
});

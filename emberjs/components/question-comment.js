import Ember from 'ember';
import EmberValidations from 'ember-validations';

export default Ember.Component.extend(EmberValidations, {
  isSelfPersistanceMode: null,
  comments: [],
  dataService: Ember.inject.service('data'),
  dictService: Ember.inject.service(),
  content: '',
  questionId: null,
  isEditing: null,

  init() {
    this.set('persister', this.get('isSelfPersistanceMode') ? this.selfPersister : this.flowPersister );
    return this._super();
  },

  selfPersister: function(){
    var me = this;

    return{
      apply(newComment){
        me.dataService.post('questions/applyQuestionComment', newComment).then(function(data) {
          me.get('comments').pushObject(data);
          me.notifyService.success("Комментарий успешно cохранен.");
          me.set('isEditing', false);
        });
      }
    };
  },

  flowPersister: function(){
    var me = this;

    return{
      apply(newComment){
        newComment.creatorUserName = me.get('dictService').currentUserName;
        me.get('comments').pushObject(newComment);

        me.set('isEditing', false);
      }
    };
  },

  actions: {
    create(){
      this.set('isEditing', true);
    },

    cancel(){
      this.set('isEditing', false);
    },

    sendComment() {
      var me = this;
      var content = me.get('content');

      if(!content){
        me.notifyService.alert("Комментарий не может быть пустым.");
        return;
      }

      var newComment = {
        questionId: me.get('questionId'),
        content: content
      };

      this.persister().apply(newComment);
    }
  }
});

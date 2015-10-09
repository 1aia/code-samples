import Ember from 'ember';

export default Ember.Component.extend({
  exams: null,
  examDict: null,
  examsAreValid: null,
  questionId: null,
  isSelfPersistanceMode: null,

  hasAny: function(){
    var exams = this.get('exams');
    return exams && exams.length > 0;
  }.property('exams', 'exams.length'),

  isValid: function(){
    var exams = this.get('exams');
    var result = false;
    if(exams.length > 0){
      result = exams.reduce(function(reduced, item) {
          return reduced && item.isValid;
      }, true);
    }
    this.set('examsAreValid', result);
    return result;
  }.property('exams.@each.isValid'),

  actions:{
    create(){
      var newObject = {
        questionId: this.get('questionId'),
        isNew: true,
        isValid: false,
        isEditing: true
      };

      if(this.examDict && this.examDict.length){
        newObject.examId = this.examDict[0].id;
      }

      this.exams.pushObject(newObject);
    },
    remove(item){
      this.exams.removeObject(item);
    }
  }
});

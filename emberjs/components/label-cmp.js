import Ember from 'ember';

export default Ember.Component.extend({
  tagName: 'span',
  classNames: ['label-cmp'],
  dictService: Ember.inject.service(),
  style: function(){
    var color = this.get('model').color || this.getConceptColor(this.get('model'));
    return 'background-color:' + color;
  }.property('model'),
  getConceptColor(data){
    if(!(data && data.group)){
      return null;
    }

    var conceptTypesWithColors = this.get('dictService').conceptTypesWithColors;

    var color = conceptTypesWithColors && conceptTypesWithColors[data.group] ?
      conceptTypesWithColors[data.group].highlight : null;

    return color;
  },

  didInsertElement(){
    this.$().attr('style', this.get('style'));
  }
});

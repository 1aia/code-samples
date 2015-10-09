import Ember from 'ember';
import ConceptGraphFilter from '../mixins/concept-graph-filter';

export default Ember.Component.extend(ConceptGraphFilter, {
  classNames: ['confirm-button-inline'],
  dataService: Ember.inject.service('data'),
  dictService: Ember.inject.service(),
  concepts: null,
  selected: null,
  itemsWithWeights: false,

  conceptsObserver: function(){
    var me = this;
    me.get('concepts').map(function(x){
      x.color = x.color || me.getConceptColor(x);
    });
  }.observes('concepts').on('init'),

  applyDisabled:function() {
    var selected = this.get('selected');
    return selected == null || selected.length === 0;
  }.property('selected', 'selected.@each'),

  getConceptColor(data){
    if(!(data && data.group)){
      return null;
    }

    var conceptTypesWithColors = this.get('dictService').conceptTypesWithColors;

    var color = conceptTypesWithColors && conceptTypesWithColors[data.group] ?
      conceptTypesWithColors[data.group].highlight : null;

    return color;
  },

  actions: {
    showModal(){
      var me = this;

      me.set('selected', []);
      me.$('.modal').modal();
      me.get('dataService').post('concepts/filter', this.getFilter()).then(function(data){
        me.trigger('reloadOnFilter', data);
      });
    },
    confirm(){
      var me = this;
      var concepts = this.get('concepts');
      var selected = this.get('selected');

      selected.forEach(function(x){
        var item = concepts.findBy('id', x.id);

        if(me.itemsWithWeights){
          if(item){
            concepts.removeObject(item);
          }

          concepts.pushObject({id: x.id, name: x.name, color: x.color, weight: x.weight});
        }
        else {
          if(!item){
            concepts.pushObject({id: x.id, name: x.name, color: x.color});
          }
        }
      });

      this.set('currentSelection', null);

      this.$('.modal').modal('hide');
    },
    selectNode(data){
      if(!data){
        return;
      }

      var selected = this.get('selected');

      if(this.itemsWithWeights){
        this.set('currentSelection', data);
        this.set('currentSelectionWeight', 1);
        return;
      }

      var item = selected.findBy('id', data.id);
      if(!item){
        selected.pushObject({id: data.id, name: data.name, color: this.getConceptColor(data)});
      }
    },
    deselectNode(){
      this.set('currentSelection', null);
    },
    addToSelected(){
      if((+this.get('currentSelectionWeight') > 0)===false){
        this.notifyService.warning("Введите корректный вес концепции");
        return;
      }

      var data = this.get('currentSelection');

      if(!data){
        this.set('currentSelection', null);
        return;
      }

      var selected = this.get('selected');

      var item = selected.findBy('id', data.id);
      if(item){
        selected.popObject(item);
      }
      selected.pushObject({
        id: data.id,
        name: data.name,
        color: this.getConceptColor(data),
        weight: +this.get('currentSelectionWeight')});

      this.set('currentSelection', null);
    }
  }
});

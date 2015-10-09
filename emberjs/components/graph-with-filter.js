import Ember from 'ember';
import ConceptGraphFilter from '../mixins/concept-graph-filter';

export default Ember.Component.extend(ConceptGraphFilter, {
  dataService: Ember.inject.service('data'),
  dictService: Ember.inject.service(),
  selectNodeAction: null,
  selectEdgeAction: null,
  deselectNodeAction: null,
  startStabilizingAction: null,

  createEdgeAction(data){ this.trigger('createEdge', data);  },
  removeEdgeAction(data){ this.trigger('removeEdge', data);  },
  updateEdgeAction(data){ this.trigger('updateEdge', data);  },
  highlightAction(data) { this.trigger('highlight', data);   },

  didInsertElement: function(){
    var hoster = this.get('hosterParent');

    if(hoster){
       hoster.on('createEdge', this, this.createEdgeAction);
       hoster.on('removeEdge', this, this.removeEdgeAction);
       hoster.on('updateEdge', this, this.updateEdgeAction);

       if(this.startStabilizingAction){
          hoster.on('highlight', this, this.highlightAction);
       }
    }
  },

  willDestroyElement: function(){
    var hoster = this.get('hosterParent');

    if(hoster){
       hoster.off('createEdge', this, this.createEdgeAction);
       hoster.off('removeEdge', this, this.removeEdgeAction);
       hoster.off('updateEdge', this, this.updateEdgeAction);

       if(this.startStabilizingAction){
          hoster.off('highlight', this, this.highlightAction);
       }
    }
  },

  actions: {
    selectNode(data){
      if(this.selectNodeAction){
        this.sendAction('selectNodeAction', data);
      }
    },
    deselectNode(data){
      if(this.deselectNodeAction){
        this.sendAction('deselectNodeAction', data);
      }
    },
    selectEdge(data, from, to){
      if(this.selectEdgeAction){
        this.sendAction('selectEdgeAction', data, from, to);
      }
    },
    startStabilizing(data){
      if(this.startStabilizingAction){
        this.sendAction('startStabilizingAction', data);
      }
    },
  }
});

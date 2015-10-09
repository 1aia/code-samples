import Ember from 'ember';

export default Ember.Component.extend({
  classNames: ['graph-viewer-component'],
  dictService: Ember.inject.service(),
  nodes: null,
  edges: null,
  network: null,
  // selectNodeAction: "selectNode",
  // deselectNodeAction: "deselectNode",
  selectNodeAction: null,
  selectEdgeAction: null,
  deselectNodeAction: null,
  startStabilizingAction: null,
  reloadEvent: null,
  highlightEvent: null,
  unselectAllEvent: null,
  createEdgeEvent: null,
  removeEdgeEvent: null,
  updateEdgeEvent: null,
  deleteSelectedEvent: 'deleteSelected',
  hoster: null,
  showLegend: false,
  options: {
    interaction: {
      navigationButtons: true,
      keyboard: {
        bindToWindow: false
      },
      hover: true,
    },
    nodes: {
      font: {
          face: 'Tahoma',
          //size: 20
      }
    },
    edges: {
      arrows: {
          to: true
      },
      color: {
        //highlight: 'orange',
        //hover: 'black',
        inherit: true
      },

      // color: {
      //   hover: '#848484',
      //   color: '#666666'
      // },
      font: {
        face: 'Tahoma',
        align: 'top',
        size: 10,
      },
      smooth:{
        type: 'continuous'
      }
    },
    physics: {
      barnesHut: {
        //centralGravity: 0.2,
        springLength: 130
      }
    }
  },

  destroyNetwork() {
    if (this.network !== null) {
      this.network.destroy();
      this.network = null;
    }
  },

  draw(container) {
    var me = this;
    this.destroyNetwork();

    // create a network
    var data = {
      nodes: this.nodes,
      edges: this.edges
    };

    me.options.groups = {};

    var conceptTypesWithColors = this.get('dictService').conceptTypesWithColors;

    for(var property in conceptTypesWithColors){
      var x = conceptTypesWithColors[property];
      me.options.groups[property] = {
        name: x.name,
        shape: 'dot',
        color: {
          background: x.color,
          border: x.color,
          highlight: {
            background: x.highlight,
            border: x.color
          },
          hover: {
            background: x.highlight,
            border: x.color
          }
        },
      };
    }

    var network = new vis.Network(container, data, this.options);

    var selectNodeAction = function (params) {
      var data;

      if (params.nodes.length > 0) {
        data = me.network.body.data.nodes.get(params.nodes[0]);
        me.sendAction('selectNodeAction', data);
      } else if(params.edges.length > 0){
        data = me.network.body.data.edges.get(params.edges[0]);
        let from = me.network.body.data.nodes.get(data.from);
        let to = me.network.body.data.nodes.get(data.to);
        me.sendAction('selectEdgeAction', data, from, to);
      }
      else {
        me.sendAction('deselectNodeAction');
      }
    };

    network.on('click', selectNodeAction);
    network.on('dragStart', selectNodeAction);

    network.on('stabilized', me.onStabilized);

    if(me.startStabilizingAction){
      network.on('startStabilizing', () => me.sendAction('startStabilizingAction'));
    }

    if(this.get('showLegend')){
      Ember.$(network.body.container).prepend(this.getLegendCanvas(this.options.groups));
    }

    this.set('network', network);
  },

  onStabilized(data){
    if(data.iterations > 0 ){
       this.physics.options.enabled = false;
    }
  },

  getLegendCanvas(groups){
    var legendCanvas = Ember.$('<canvas/>').prop({ width: 70, height: 320 });
    legendCanvas.attr('style', 'position: absolute');

    var ctx = legendCanvas[0].getContext("2d");

    var radius = 25;
    var gap = 80;
    var start = radius + 10;
    var xstart = start;

    for(var property in groups){
      var group = groups[property];

      ctx.beginPath();
      ctx.arc(xstart,start,radius,0,2*Math.PI);
      ctx.fillStyle = group.color.background;
      ctx.fill();
      ctx.strokeStyle = group.color.border;
      ctx.stroke();

      ctx.fillStyle = "#343434";
      ctx.font = "14px Tahoma";
      ctx.strokeStyle = "#2b7ce9";
      ctx.textAlign =  "center";
      ctx.textBaseline = "hanging";
      ctx.fillText(group.name, xstart, start + 27);

      start += gap;
    }

    return legendCanvas;
  },

  didInsertElement: function(){
    var container = this.get('element');
    this.draw(container);

    if(this.hoster){
      if(this.deleteSelectedEvent){
        this.hoster.on(this.deleteSelectedEvent, this, this.deleteSelected);
      }
      if(this.highlightEvent){
        this.hoster.on(this.highlightEvent, this, this.highlightNode);
      }
      if(this.reloadEvent){
        this.hoster.on(this.reloadEvent, this, this.reload);
      }
      if(this.unselectAllEvent){
        this.hoster.on(this.unselectAllEvent, this, this.unselectAll);
      }
      if(this.createEdgeEvent){
        this.hoster.on(this.createEdgeEvent, this, this.createEdge);
      }
      if(this.removeEdgeEvent){
        this.hoster.on(this.removeEdgeEvent, this, this.removeEdge);
      }
      if(this.updateEdgeEvent){
        this.hoster.on(this.updateEdgeEvent, this, this.updateEdge);
      }
    }
  },

  willDestroyElement: function(){
    if(this.hoster){
      if(this.deleteSelectedEvent){
        this.hoster.off(this.deleteSelectedEvent, this, this.deleteSelected);
      }
      if(this.highlightEvent){
        this.hoster.off(this.highlightEvent, this, this.highlightNode);
      }
      if(this.reloadEvent){
        this.hoster.off(this.reloadEvent, this, this.reload);
      }
      if(this.unselectAllEvent){
        this.hoster.off(this.unselectAllEvent, this, this.unselectAll);
      }
      if(this.createEdgeEvent){
        this.hoster.off(this.createEdgeEvent, this, this.createEdge);
      }
      if(this.removeEdgeEvent){
        this.hoster.off(this.removeEdgeEvent, this, this.removeEdge);
      }
      if(this.updateEdgeEvent){
        this.hoster.off(this.updateEdgeEvent, this, this.updateEdge);
      }
    }
    this.destroyNetwork();
  },

  deleteSelected: function(){
    if(this.network){
      this.network.deleteSelected();
    }
  },

  createNode: function(newNode){
    if(this.network){
      this.network.body.data.nodes.add(newNode);
      this.network.selectNodes([newNode.id]);
      this.sendAction('selectNodeAction', newNode);
    }
  },

  unselectAll: function(){
    if(this.network){
      this.network.unselectAll();
      this.network.redraw();
    }
  },

  reload: function(data){
    if(this.network){
      this.network.physics.options.enabled = true;
      this.network.setData(data);
    }
  },

  highlightNode: function(nodeGuid){
    var guid = nodeGuid.toLowerCase();
    var data = this.network.body.data.nodes.get(guid); // get the data from selected node

    if(!data){
      return;
    }

    this.network.fit({nodes : [guid]});
    this.network.selectNodes([guid], true);
    this.sendAction('selectNodeAction', data);
  },

  createEdge(edge){
    if(this.network){
      this.network.body.data.edges.add(edge);
    }
  },

  removeEdge(edgeId){
    if(this.network){
      this.network.body.data.edges.remove(edgeId);
    }
  },

  updateEdge(edge){
    if(this.network){
      this.network.body.data.edges.remove(edge.id);
      this.network.body.data.edges.add(edge);
      this.network.selectEdges([edge.id]);
    }
  },
});

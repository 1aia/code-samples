import Ember from 'ember';

export default Ember.Component.extend({
  model: null,
  readOnly: null,

  actions:{
    remove(){
      this.set('model', null);
      this.$('img').attr('src', null);
    },

    edit(){
      this.set('editing', true);
    },

    update(data){
      if(!data){
        return;
      }

      var me = this;

      Ember.set(me, 'model', {
        id: me.model && me.model.id,
        mediaObject: data,
        mediaObjectId: data.id
      });

      me._updatePreview(data.url);
    }
  },

  didInsertElement: function() {
    let me = this;

    if(me.model && me.model.mediaObject && me.model.mediaObject.url){
      me._updatePreview(me.model.mediaObject.url);
    }
  },

  _updatePreview(url){
    let me = this;
    let img = me.$('img');

    img.attr('src', url);
  }
});

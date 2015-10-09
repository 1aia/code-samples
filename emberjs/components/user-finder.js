import Ember from 'ember';

export default Ember.Component.extend({
  value: null,

  valueChange: function(){
    if(this.isInternalUpdate){
      return;
    }

    this._selectize.setValue(this.get('value'));
  }.observes('value'),

  didInsertElement: function(){
    var me = this;
    var element = this.$('select');
    element.selectize({
      valueField: 'id',
      labelField: 'name',
      searchField: 'name',
      load: function(query, callback) {
          me._selectize.clearOptions();

          if (!query.length){
            return callback();
          }

          var queryData = {
            findText: query
          };

          Ember.$.ajax({
              url: '/api/users/find',
              type: 'POST',
              data: JSON.stringify(queryData),
              contentType: 'application/json; charset=utf-8',
              dataType: "json",
              error: function() {
                  callback();
              },
              success: function(res) {
                  callback(res);
              }
          });
      },
      onChange: function(value) {
        me.isInternalUpdate = true;
        me.set('value', value);
        me.isInternalUpdate = false;
      },
		});

    this._selectize = element[0].selectize;
  },

  willDestroyElement() {
    if(this._selectize){
      this._selectize.destroy();
      this._selectize = null;
    }
  },
});

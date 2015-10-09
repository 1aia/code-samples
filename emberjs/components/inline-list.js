import Ember from 'ember';

export default Ember.Component.extend({
  list: null,
  optionValuePath: 'id',
  optionLabelPath: 'name',

  updateList(){
    if(this.isInternalUpdate){
      return;
    }

    var me = this;
    var options = me.get('list');
    me._selectize.addOption(options);

    var value = options && options.map(function(x){
      if(me._selectize.options[x.id]){
        me._selectize.options[x.id] = x;
      }

      return x.id;
    });

    if(value){
      me._selectize.setValue(value);
    }
  },

  listChange: function(){
    this.updateList();
  }.observes('list.@each'),

  listChangeFull: function(){
    this.updateList();
  }.observes('list'),

  didInsertElement: function(){
    var me = this;
    var element = this.$('input');
    var options = this.get('list');

    element.selectize({
        plugins: ['remove_button', 'item_color', 'item_badge'],
        valueField: this.get('optionValuePath'),
        labelField: this.get('optionLabelPath'),
        options: options,
        openOnFocus: false,
        readonly: true,
        onDelete: function(values) {
          me.isInternalUpdate = true;
					delete this.options[values[0]];

          var options = me.get('list');
          options.removeObject(options.findBy('id', values[0]));
          me.isInternalUpdate = false;
					return true;
				}
		});

    this._selectize = element[0].selectize;
    this._selectize.$control_input[0].readOnly = true;

    var value = options && options.map(function(x){ return x.id;});

    if(value){
      this._selectize.setValue(value);
    }
  },

  willDestroyElement() {
    if(this._selectize){
      this._selectize.destroy();
      this._selectize = null;
    }
  },
});

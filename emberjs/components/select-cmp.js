import Ember from 'ember';

export default Ember.Component.extend({
  create: false,
  multiple: false,
  content: null,
  optionValuePath: 'id',
  optionLabelPath: 'name',
  value: null,
  placeHolder: null,

  contentChange: function(){
    var me = this;
    //me._selectize.clearOptions();
    me._selectize.addOption(me.get('content'));

    // for(var property in me._selectize.options){
    //   var option = me._selectize.options[property];
    //   if(option.isDefault === undefined){
    //     me._selectize.removeOption(property);
    //   }
    // }
    //me._selectize.setValue(me.get('value'));
  }.observes('content'),

  valueChange: function(){
    if(this.isInternalUpdate){
      return;
    }

    this._selectize.setValue(this.get('value'));
  }.observes('value', 'value.@each'),

  didInsertElement: function(){
    var me = this;

    var isMultiple = this.get('multiple');
    var tag = isMultiple ? '<input/>' : '<select/>';

    var element = this.$(tag).appendTo(this.$());

    var placeHolder = this.get('placeHolder') ||
      ( this.get('multiple') ?
          'Выберите значения из списка и/или задайте свои варианты' :
          'Выберите значение из списка, либо задайте свой вариант');

    element.attr('placeHolder', placeHolder);

    element.selectize({
				create: this.get('create'),
        plugins: ['remove_button'],
        valueField: this.get('optionValuePath'),
        labelField: this.get('optionLabelPath'),
        searchField: this.get('optionLabelPath'),
        options: this.get('content'),
        onChange: function(value) {
          me.isInternalUpdate = true;
          //me.set('value', isMultiple ? this.items : value);
          if (isMultiple) {
            if (me.value) {
              me.value.clear();
              me.value.pushObjects(this.items);
            } else {
              me.set('value', Ember.copy(this.items, true));
            }
          } else {
            me.set('value', value);
          }
          me.isInternalUpdate = false;
        },
        render: {
          option_create: function(data, escape) {
              return '<div class="create">Создать <strong>' + escape(data.input) + '</strong>&hellip;</div>';
          }
        },
		});

    this._selectize = element[0].selectize;

    var value = this.get('value');

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

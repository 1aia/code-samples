import Ember from 'ember';

export default Ember.Component.extend({
  tagName: 'select',
  create: false,
  multiple: false,
  content: null,
  optionValuePath: 'id',
  optionLabelPath: 'name',
  value: null,
  placeHolder: null,
  prompt: null,
  disabled: null,

  contentChange: function(){
    var me = this;
    me._selectize.clearOptions();
    me._selectize.addOption(me.getContent());
  }.observes('content'),

  valueChange: function(){
    if(this.isInternalUpdate){
      return;
    }

    this._selectize.setValue(this.get('value'));

  }.observes('value'),

  getContent(element){
    var content = this.get('content');
    var prompt = this.get('prompt');

    if(prompt){
      content = (content && Ember.copy(content)) || [];

      var promptItem = {};
      promptItem[this.get('optionValuePath')] = 0;
      promptItem[this.get('optionLabelPath')] = prompt;

      content.insertAt(0, promptItem);

      if(element){
        element.attr('placeHolder', prompt);
      }
    }

    return content;
  },

  didInsertElement: function(){
    Ember.run.scheduleOnce('afterRender', this, this.initSelectize);
  },

  initSelectize(){
    var me = this;
    var element = this.$();

    var content = this.getContent(element);
    var prompt = this.get('prompt');

    element.selectize({
        create: this.get('create'),
        allowEmptyOption: !!prompt,
        valueField: this.get('optionValuePath'),
        labelField: this.get('optionLabelPath'),
        searchField: this.get('optionLabelPath'),
        options: content,
        //onChange: this.onChangeFactory()
    });

    this._selectize = element[0].selectize;
    this._selectize.$control_input[0].readOnly = true;

    var value = this.get('value');
    if(value){
      this._selectize.setValue(value);
    } else if(content && content.length > 0) {
      value = content[0][this.get('optionValuePath')];
      this._selectize.setValue(value);

      if(content[0][this.get('optionLabelPath')] !== prompt){
        me.set('value', value);
      }
    }

    if(this.get('disabled')){
      this._selectize.lock();
    }

    this._selectize.on('change', this.onChangeFactory());
  },

  onChangeFactory(){
    var me = this;
    return function(value) {
      me.isInternalUpdate = true;
      var val = me.get('value');

      if(val !== value){
        me.set('value', value);
      }

      me.isInternalUpdate = false;
    };
  },

  willDestroyElement() {
    if(this._selectize){
      this._selectize.destroy();
      this._selectize = null;
    }
  },
});

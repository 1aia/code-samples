import Ember from 'ember';

export default Ember.Component.extend({
   classNames: ['wysiwyg-editor'],
   height: 120,
   _editor: null,

   willDestroyElement: function() {
     if(this._editor && this._editor.destroy){
       this._editor.destroy();
     }
   },

   didInsertElement: function() {
     var height = this.get('height');
     var editorElement = this.$()[0];

     this._editor = CKEDITOR.replace(editorElement.id, {
       height: height
     });

     this._editor.on('change', this.onChangeFactory());
   },

   onChangeFactory(){
     var me = this;

     return function(){
       me.isDoUpdate = true;
       me.set('content', me.unProcessMathML(this.getData()));
       me.isDoUpdate = false;
     };
   },

  init(){
    this.set('inner_content', this.processMathML(this.get('content')));
    return this._super();
  },

  processMathML(content){
    if(content){
      return content.replace(/<span class="math-tex"><math(.*?)<\/math><\/span>/g, this.processMathMLReplacer);
    }
    return content;
  },

  processMathMLReplacer(str, p1){
    return '<span class="math-tex">' + CKEDITOR.tools.htmlEncode('<math' + p1 + '</math>') + '</span>';
  },

  unProcessMathML(content){
    if(content){
      return content.replace(/<span class="math-tex">&lt;math(.*?)&lt;\/math&gt;<\/span>/g, this.unProcessMathMLReplacer);
    }
    return content;
  },

  unProcessMathMLReplacer(str, p1){
    return '<span class="math-tex"><math' + CKEDITOR.tools.htmlDecode(p1) + '</math></span>';
  }
});

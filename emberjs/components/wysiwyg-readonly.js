import Ember from 'ember';

export default Ember.Component.extend({
   classNames: ['wysiwyg-readonly'],
   height: 60,
   oldValue: null,

   queueTypeset(){
     var content = this.get('content');
     if(this.get('oldValue') === content){
       return;
     }

     var element = this.$();
     var el = element && element[0];

     if(el){
       this.set('oldValue', content);
       Ember.run.scheduleOnce('afterRender', this, function(){
         MathJax.Hub.Queue(["Typeset", MathJax.Hub, el]);
       });
     }
   },

   contentChanged: function() {
     this.queueTypeset();
   }.observes('content').on('init'),

   didInsertElement: function() {
     this.queueTypeset();
   }
});

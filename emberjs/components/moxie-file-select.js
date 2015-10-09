import Ember from 'ember';
import InboundActions from 'ember-component-inbound-actions/inbound-actions';

export default Ember.Component.extend(InboundActions, {
  actionReceiver: null,
  _fileInput: null,
  fileName: null,
  extensions: '*',
  receiveAction: 'onReceive',

  actions: {
    getFileContent(){
      let me = this;
      var files = me._fileInput.files;

      if(files){
        me.loadAttachment(files[0]).then(function(data){
          me.sendAction('receiveAction', data);
        });
      } else {
        me.sendAction('receiveAction', null);
      }
    }
  },

  willDestroyElement: function(){
    this._fileInput.destroy();
  },

  didInsertElement: function() {
    let me = this;

    me._fileInput = new mOxie.FileInput({
      browse_button: this.$('button').get(0),
      accept: [{
        extensions: me.get('extensions')
      }]
    });

    me._fileInput.onchange = function() {
      var files = me._fileInput.files;

      if(files){
        me.set('fileName', files[0].name);
      }
    };

    me._fileInput.init();
  },

  loadAttachment(file) {
    return new Ember.RSVP.Promise(function(resolve, reject) {
      var reader = new mOxie.FileReader();

      reader.onloadend = function() {
        var res = {
          fileName: file.name,
          data: mOxie.btoa(reader.result)
        };

        resolve(res);
      };

      reader.onerror = function() {
        reject(reader.error);
      };

      reader.readAsBinaryString(file);
    });
  }
});

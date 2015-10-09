import Ember from 'ember';
import EmberValidations from 'ember-validations';

export default Ember.Component.extend(EmberValidations, {
  _fileInput: null,
  model: null,
  dictService: Ember.inject.service(),
  apply: 'apply',

  validations: {
    'model.name': {
      presence: {
        message: "Заполните поле"
      }
    },
    'model.description': {
      presence: {
        message: 'Заполните поле'
      }
    }
  },

  isLocalValid: function(){
    var model = this.model;
    return !!(this.get('isValid') && model && (model.url || model.data));
  }.property('isValid', 'model.url', 'model.data'),

  hasImage: function(){
    var model = this.model;
    return model && (model.url || model.data);
  }.property('model.url', 'model.data'),

  willDestroyElement: function(){
    this._fileInput.destroy();
  },

  didInsertElement: function() {
    let me = this;

    me._fileInput = new mOxie.FileInput({
      browse_button: this.$('button').get(0),
      accept: [{
        title: "Images",
        extensions: "jpg,png,gif"
      }]
    });

    me._fileInput.onchange = function() {
      me._updatePreviewImage();
    };

    me._fileInput.init();

    if(me.model){
      let src = me.model.url || (me.model.getBase64Data && me.model.getBase64Data());
      if(src){
        me._updatePreview(src);
      }
    }
  },

  _updatePreviewImage(){
    let me = this;
    let files = me._fileInput.files;

    if(!files){
      return;
    }

    let file = files[0];

    Ember.set(me.model, 'url', null);
    Ember.set(me.model, 'type', 10);
    Ember.set(me.model, 'fileType', file.type);
    Ember.set(me.model, 'fileSize', file.size);

    me.loadAttachment(file, true)
      .then(function(data) {
        var src = data.base64prefix + data.data;

        me._updatePreview(src);

        Ember.set(me.model, 'data', data.data);
        Ember.set(me.model, 'base64prefix', data.base64prefix);
        Ember.set(me.model, 'getBase64Data', function(){
          return this.base64prefix + this.data;
        });
      });
  },

  _updatePreview(url){
    let me = this;
    let img = me.$('img');

    img.attr('src', url);
  },

  loadAttachment(file) {
    return new Ember.RSVP.Promise(function(resolve, reject) {
      var reader = new mOxie.FileReader();

      reader.onloadend = function() {
        var res = {
          base64prefix: 'data:' + file.type + ';base64,',
          data: mOxie.btoa(reader.result)
        };

        resolve(res);
      };

      reader.onerror = function() {
        reject(reader.error);
      };

      reader.readAsBinaryString(file);
    });
  },

  actions: {
    apply(){
      let me = this;

      if(!me.get('isLocalValid')) {
        me.notifyService.alert("Не все данные введены верно, либо не выбрано изображение.");
        return;
      }

      me.get('dataService').post('mediaobjects/createOrUpdate',me.get('model'))
        .then(function(mediaobject){
          me.sendAction('apply', mediaobject);
        });
    },
  }
});

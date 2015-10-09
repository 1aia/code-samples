import Ember from 'ember';

export default Ember.Component.extend({
  tagName: 'span',
  ok: 'filter',

  actions: {
    showModal(){
      if(!window.FormData){
        this.notifyService.warning("Ваш браузер не позволяет загружать файлы.");
        return;
      }
      this.$('.modal').modal();
    },
    confirm(){
      let me = this;

      if(!window.FormData){
          me.notifyService.warning("Ваш браузер не позволяет загружать файлы.");
      }

      var files = this.$("#mediaObjectsBatchUploadInput").get(0).files;;
      if(!(files && files.length)){
        me.notifyService.warning("Выберите файл.");
        return;
      }

      me.set('uploading', true);

      let url = "/api/mediaobjects/batchupload/10";

      var data = new FormData();
      for (var i = 0; i < files.length; i++) {
          data.append("file" + i, files[i]);
      }

      Ember.$.ajax({
          type: "POST",
          url: url,
          contentType: false,
          processData: false,
          data: data,
          success: function () {
              me.notifyService.success("Файлы успешно загружены.");
              me.$('.modal').modal('hide');
              me.sendAction('ok');
          },
          error: function () {
              me.notifyService.alert("Ошибка загрузки фыайлов.");
          },
          complete: function(){
              me.set('uploading', false);
          }
      });
    }
  }
});

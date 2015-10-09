import Ember from 'ember';

export default Ember.Service.extend({
  isBuzy: false,
  notifyService: Ember.inject.service('notifywrapper'),
  usingLoadIndicator(scope, action, argsArray, success, failure){
    var me = this;
    me.set('isBuzy', true);

    var promise = action.apply(scope, argsArray);

    return promise.then(
      function(data){
        if(success){
          success.apply(scope, [data]);
        }
        me.set('isBuzy', false);
        return data;
      },
      function(data){
        if(failure){
          failure.apply(scope, [data]);
        } else {
          me.get('notifyService').alert("Возникла ошибка.");
        }

        me.set('isBuzy', false);
        return data;
      }
    );
  }
});

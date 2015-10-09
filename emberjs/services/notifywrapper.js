import Ember from 'ember';

export default Ember.Service.extend({
  notify: Ember.inject.service(),
  info(){
    var Notify = this.get('notify');
    return Notify.info.apply(Notify, arguments);
  },
  alert(){
    var Notify = this.get('notify');
    var args = arguments;

    if(args.length === 1){
      args[1] = { closeAfter: 5000 };
      args.length = 2;
    }

    return Notify.alert.apply(Notify, args);
  },
  success(){
    var Notify = this.get('notify');
    return Notify.success.apply(Notify, arguments);
  },
  warning(){
    var Notify = this.get('notify');
    var args = arguments;

    if(args.length === 1){
      args[1] = { closeAfter: 5000 };
      args.length = 2;
    }

    return Notify.warning.apply(Notify, args);
  }
});

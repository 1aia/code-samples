import Ember from 'ember';
import PagingMixin from '../mixins/paging';

export default Ember.Component.extend(PagingMixin, {
  dataService: Ember.inject.service('data'),
  items: null,
  apply: 'apply',

  getList(){
    var me = this;
    var filter = me.getFilter();
    var url = 'mediaobjects/filter';

    me.get('dataService').post(url, filter)
      .then(function(data){
        var items = data.data;
  
        items = items.map((x)=>{
          x.style = new Ember.Handlebars.SafeString("background-image: url(" + x.url +")");
        });

        me.set('items', data.data);
        me.set('page', data.page);
        me.set('total', Math.ceil( data.count / filter.pageSize));
      });
  },

  getFilter(){
    var filter = this.getPagingFilter();

    return {
      page: filter.page,
      pageSize: filter.pageSize
    };
  },

  didInsertElement(){
    Ember.run.scheduleOnce('afterRender', this, this.getList);
  },

  loadData() {
    this.getList();
  },

  actions: {
    edit(item){
      this.set('model', item);
    },

    apply(){
      var me = this;

      if(!me.get('model')) {
        me.notifyService.alert("Не выбран объект из библиотеки.");
        return;
      }

      me.sendAction('apply', me.get('model'));
    }
  }
});

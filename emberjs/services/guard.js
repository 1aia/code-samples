import Ember from 'ember';

export default Ember.Service.extend({
  dictService: Ember.inject.service(),
  navigation: Ember.inject.service(),
  personalMenu: null,
  userName: null,
  guardRoute(transition){
    var routeDetails = this.get('navigation').navigation[transition.targetName];

    if(!routeDetails){
      return false;
    }

    return this.hasAccess(routeDetails.access);
  },

  hasAccess(accessRoles){
    var currentUserRoles = this.get('dictService').currentUserRoles;

    if(currentUserRoles.length === 0){
      return false;
    }

    if(accessRoles.length === 0){
      return true;
    }

    return this.hasIntersection(accessRoles, currentUserRoles);
  },

  hasIntersection(array1, array2){
    var intersection = array1.filter(function(n) {
        return array2.indexOf(n) !== -1;
    });
    return intersection.length > 0;
  },

  doInit(){
    var navigation = this.get('navigation').navigation;

    var menu = [];

    for (var key in navigation) {
      if (navigation.hasOwnProperty(key)) {
        var nav = navigation[key];

        if(this.hasAccess(nav.access) && nav.isMenuItem){
          menu.push({ route: key, name: nav.name, order: nav.order, icon: nav.icon });
        }
      }
    }

    this.set('personalMenu', menu.sortBy('order'));
    this.set('userName', this.get('dictService').currentUserName);
  }
});

import Ember from 'ember';

export default Ember.Service.extend({
  namespace: '/api/',
  activityService: Ember.inject.service('activity'),

  getJSON(url, useLoadIndicator){
    if(useLoadIndicator){
      return this.get('activityService').usingLoadIndicator(this, Ember.$.getJSON, [this.namespace + url]);
    }    

    return Ember.$.getJSON(this.namespace + url);
  },

  post(url, data, useLoadIndicator){
    return this._requestHandler('POST', url, data, useLoadIndicator);
  },

  put(url, data, useLoadIndicator){
    return this._requestHandler('PUT', url, data, useLoadIndicator);
  },

  delete(url, id, data, useLoadIndicator){
    var _url = this.namespace + url;

    if(id){
      _url = _url.replace(/\/$/, "") + '/' + id;
    }

    var params = [{
        type: 'DELETE',
        url: _url,
        data: JSON.stringify(data),
        contentType: 'application/json; charset=utf-8',
        dataType: "json",
    }];

    if(useLoadIndicator === false){
      return Ember.$.ajax.apply(this, params);
    }

    return this.get('activityService').usingLoadIndicator(this, Ember.$.ajax, params);
  },

  _requestHandler: function(type, url, data, useLoadIndicator){
    var params = [{
        type: type,
        url: this.namespace + url,
        data: JSON.stringify(data),
        contentType: 'application/json; charset=utf-8',
        dataType: "json",
    }];

    if(useLoadIndicator === false){
      return Ember.$.ajax.apply(this, params);
    }

    return this.get('activityService').usingLoadIndicator(this, Ember.$.ajax, params);
  }
});

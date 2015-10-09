import Ember from 'ember';
import DatepickerSupport from '../mixins/datepicker-support';

export default Ember.Component.extend(DatepickerSupport, {
  instrumentDisplay: '{{input type="text"}}',

  classNames: ['ember-text-field', 'form-control'],

  tagName: 'input',
  language: 'ru',

  attributeBindings: [
    'accesskey',
    'autocomplete',
    'autofocus',
    'contenteditable',
    'contextmenu',
    'dir',
    'disabled',
    'draggable',
    'dropzone',
    'form',
    'hidden',
    'id',
    'lang',
    'list',
    'max',
    'min',
    'name',
    'placeholder',
    'readonly',
    'required',
    'spellcheck',
    'step',
    //'style',
    'tabindex',
    'title',
    'translate',
    'type'
  ],

  type: 'text'
});

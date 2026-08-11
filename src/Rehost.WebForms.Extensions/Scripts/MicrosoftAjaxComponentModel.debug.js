//!-----------------------------------------------------------------------
//! Copyright (C) Microsoft Corporation. All rights reserved.
//!-----------------------------------------------------------------------
//! MicrosoftAjaxComponentModel.js
//! Microsoft AJAX Framework Component Model.

//!/ <reference name="MicrosoftAjaxCore.js" />

Type._registerScript("MicrosoftAjaxComponentModel.js", ["MicrosoftAjaxCore.js"]);

Type.registerNamespace('Sys.UI');

Sys.CommandEventArgs = function(commandName, commandArgument, commandSource) {
    /// <param name="commandName" type="String">The command name.</param>
    /// <param name="commandArgument" mayBeNull="true">The command arguments.</param>
    /// <param name="commandSource" mayBeNull="true">The command source.</param>
    Sys.CommandEventArgs.initializeBase(this);
    this._commandName = commandName;
    this._commandArgument = commandArgument;
    this._commandSource = commandSource;
}
Sys.CommandEventArgs.prototype = {
    _commandName: null,
    _commandArgument: null,
    _commandSource: null,
    get_commandName: function() {
        /// <value type="String">The command name.</value>
        return this._commandName;
    },
    get_commandArgument: function() {
        /// <value mayBeNull="true">The command arguments.</value>
        return this._commandArgument;
    },
    get_commandSource: function() {
        /// <value mayBeNull="true">The command source.</value>
        return this._commandSource;
    }
}
Sys.CommandEventArgs.registerClass("Sys.CommandEventArgs", Sys.CancelEventArgs);
 
Sys.INotifyDisposing = function() {
    /// <summary>Implement this interface if the class exposes an event to notify when it's disposing.</summary>
    throw Error.notImplemented();
}
Sys.INotifyDisposing.prototype = {
    add_disposing: function(handler) {
        throw Error.notImplemented();
    },
    remove_disposing: function(handler) {
        throw Error.notImplemented();
    }
}
Sys.INotifyDisposing.registerInterface("Sys.INotifyDisposing");
 
Sys.Component = function() {
    /// <summary>Base class for Control, Behavior and any object that wants its lifetime to be managed.</summary>
    if (Sys.Application) Sys.Application.registerDisposableObject(this);
}
Sys.Component.prototype = {
    _id: null,
    _idSet: false,
    _initialized: false,
    _updating: false,
    get_events: function() {
        /// <value type="Sys.EventHandlerList">
        ///   The collection of event handlers for this behavior.
        ///   This property should only be used by derived behaviors
        ///   and should not be publicly called by other code.
        /// </value>
        if (!this._events) {
            this._events = new Sys.EventHandlerList();
        }
        return this._events;
    },
    get_id: function() {
        /// <value type="String"/>
        return this._id;
    },
    set_id: function(value) {
        if (this._idSet) throw Error.invalidOperation(Sys.Res.componentCantSetIdTwice);
        this._idSet = true;
        var oldId = this.get_id();
        if (oldId && Sys.Application.findComponent(oldId)) throw Error.invalidOperation(Sys.Res.componentCantSetIdAfterAddedToApp);
        this._id = value;
    },
    get_isInitialized: function() {
        /// <value type="Boolean"/>
        return this._initialized;
    },
    get_isUpdating: function() {
        /// <value type="Boolean"/>
        return this._updating;
    },
    add_disposing: function(handler) {
        this.get_events().addHandler("disposing", handler);
    },
    remove_disposing: function(handler) {
        this.get_events().removeHandler("disposing", handler);
    },
    add_propertyChanged: function(handler) {
        this.get_events().addHandler("propertyChanged", handler);
    },
    remove_propertyChanged: function(handler) {
        this.get_events().removeHandler("propertyChanged", handler);
    },
    beginUpdate: function() {
        this._updating = true;
    },
    dispose: function() {
        if (this._events) {
            var handler = this._events.getHandler("disposing");
            if (handler) {
                handler(this, Sys.EventArgs.Empty);
            }
        }
        delete this._events;
        Sys.Application.unregisterDisposableObject(this);
        Sys.Application.removeComponent(this);
    },
    endUpdate: function() {
        this._updating = false;
        if (!this._initialized) this.initialize();
        this.updated();
    },
    initialize: function() {
        this._initialized = true;
    },
    raisePropertyChanged: function(propertyName) {
        /// <summary>Raises a change notification event.</summary>
        /// <param name="propertyName" type="String">The name of the property that changed.</param>
        if (!this._events) return;
        var handler = this._events.getHandler("propertyChanged");
        if (handler) {
            handler(this, new Sys.PropertyChangedEventArgs(propertyName));
        }
    },
    updated: function() {
    }
}
Sys.Component.registerClass('Sys.Component', null, Sys.IDisposable, Sys.INotifyPropertyChange, Sys.INotifyDisposing);

function Sys$Component$_setProperties(target, properties) {
    /// <summary>Recursively sets properties on an object.</summary>
    /// <param name="target">The object on which to set the property values.</param>
    /// <param name="properties">A JSON object containing the property values.</param>
    var current;
    var targetType = Object.getType(target);
    var isObject = (targetType === Object) || (targetType === Sys.UI.DomElement);
    var isComponent = Sys.Component.isInstanceOfType(target) && !target.get_isUpdating();
    if (isComponent) target.beginUpdate();
    for (var name in properties) {
        var val = properties[name];
        var getter = isObject ? null : target["get_" + name];
        if (isObject || typeof(getter) !== 'function') {
            // No getter, looking for an existing field.
            var targetVal = target[name];
            if (!isObject && typeof(targetVal) === 'undefined') throw Error.invalidOperation(String.format(Sys.Res.propertyUndefined, name));
            if (!val || (typeof(val) !== 'object') || (isObject && !targetVal)) {
                target[name] = val;
            }
            else {
                Sys$Component$_setProperties(targetVal, val);
            }
        }
        else {
            var setter = target["set_" + name];
            if (typeof(setter) === 'function') {
                // The setter exists, using it in all cases.
                setter.apply(target, [val]);
            }
            else if (val instanceof Array) {
                // There is a getter but no setter and the value to set is an array. Adding to the existing array.
                current = getter.apply(target);
                if (!(current instanceof Array)) throw new Error.invalidOperation(String.format(Sys.Res.propertyNotAnArray, name));
                for (var i = 0, j = current.length, l= val.length; i < l; i++, j++) {
                    current[j] = val[i];
                }
            }
            else if ((typeof(val) === 'object') && (Object.getType(val) === Object)) {
                // There is a getter but no setter and the value to set is a plain object. Adding to the existing object.
                current = getter.apply(target);
                if ((typeof(current) === 'undefined') || (current === null)) throw new Error.invalidOperation(String.format(Sys.Res.propertyNullOrUndefined, name));
                Sys$Component$_setProperties(current, val);
            }
            else {
                // No setter, and the value is not an array or object, throwing.
                throw new Error.invalidOperation(String.format(Sys.Res.propertyNotWritable, name));
            }
        }
    }
    if (isComponent) target.endUpdate();
}

function Sys$Component$_setReferences(component, references) {
    for (var name in references) {
        var setter = component["set_" + name];
        var reference = $find(references[name]);
        if (typeof(setter) !== 'function') throw new Error.invalidOperation(String.format(Sys.Res.propertyNotWritable, name));
        if (!reference) throw Error.invalidOperation(String.format(Sys.Res.referenceNotFound, references[name]));
        setter.apply(component, [reference]);
    }
}

var $create = Sys.Component.create = function(type, properties, events, references, element) {
    /// <summary>
    ///   Instantiates a component of the specified type, attaches it to the specified element if it's
    ///   a Control or Behavior, sets the properties as described by the specified JSON object,
    ///   then calls initialize.
    /// </summary>
    /// <param name="type" type="Type">The type of the component to create.</param>
    /// <param name="properties" optional="true" mayBeNull="true">
    ///   A JSON object that describes the properties and their values.
    /// </param>
    /// <param name="events" optional="true" mayBeNull="true">
    ///   A JSON object that describes the events and their handlers.
    /// </param>
    /// <param name="references" optional="true" mayBeNull="true">
    ///   A JSON object that describes the properties that are references to other components.
    ///   The contents of this object consists of name/id pairs.
    ///   If in a two-pass creation, the setting of these properties will be delayed until the second pass.
    /// </param>
    /// <param name="element" domElement="true" optional="true" mayBeNull="true">
    ///   The DOM element the component must be attached to.
    /// </param>
    /// <returns type="Sys.UI.Component">The component instance.</returns>
    if (!type.inheritsFrom(Sys.Component)) {
        throw Error.argument('type', String.format(Sys.Res.createNotComponent, type.getName()));
    }
    if (type.inheritsFrom(Sys.UI.Behavior) || type.inheritsFrom(Sys.UI.Control)) {
        if (!element) throw Error.argument('element', Sys.Res.createNoDom);
    }
    else if (element) throw Error.argument('element', Sys.Res.createComponentOnDom);
    var component = (element ? new type(element): new type());
    var app = Sys.Application;
    var creatingComponents = app.get_isCreatingComponents();

    component.beginUpdate();
    if (properties) {
        Sys$Component$_setProperties(component, properties);
    }
    if (events) {
        for (var name in events) {
            if (!(component["add_" + name] instanceof Function)) throw new Error.invalidOperation(String.format(Sys.Res.undefinedEvent, name));
            if (!(events[name] instanceof Function)) throw new Error.invalidOperation(Sys.Res.eventHandlerNotFunction);
            component["add_" + name](events[name]);
        }
    }

    if (component.get_id()) {
        app.addComponent(component);
    }
    if (creatingComponents) {
        // DevDiv 81690: Do not add to createdComponent list unless we are in 2 pass mode,
        // which is during the first GET and on partial updates. 
        app._createdComponents[app._createdComponents.length] = component;
        if (references) {
            app._addComponentToSecondPass(component, references);
        }
        else {
            component.endUpdate();
        }
    }
    else {
        if (references) {
            Sys$Component$_setReferences(component, references);
        }
        component.endUpdate();
    }

    return component;
}
 
Sys.UI.MouseButton = function() {
    /// <summary>
    ///   Describes mouse buttons. The values are those from the DOM standard, which are different from the IE values.
    /// </summary>
    /// <field name="leftButton" type="Number" integer="true" static="true"/>
    /// <field name="middleButton" type="Number" integer="true" static="true"/>
    /// <field name="rightButton" type="Number" integer="true" static="true"/>
    throw Error.notImplemented();
}
Sys.UI.MouseButton.prototype = {
    leftButton: 0,
    middleButton: 1,
    rightButton: 2
}
Sys.UI.MouseButton.registerEnum("Sys.UI.MouseButton");
 
Sys.UI.Key = function() {
    /// <summary>
    ///   Describes key codes.
    /// </summary>
    /// <field name="backspace" type="Number" integer="true" static="true"/>
    /// <field name="tab" type="Number" integer="true" static="true"/>
    /// <field name="enter" type="Number" integer="true" static="true"/>
    /// <field name="esc" type="Number" integer="true" static="true"/>
    /// <field name="space" type="Number" integer="true" static="true"/>
    /// <field name="pageUp" type="Number" integer="true" static="true"/>
    /// <field name="pageDown" type="Number" integer="true" static="true"/>
    /// <field name="end" type="Number" integer="true" static="true"/>
    /// <field name="home" type="Number" integer="true" static="true"/>
    /// <field name="left" type="Number" integer="true" static="true"/>
    /// <field name="up" type="Number" integer="true" static="true"/>
    /// <field name="right" type="Number" integer="true" static="true"/>
    /// <field name="down" type="Number" integer="true" static="true"/>
    /// <field name="del" type="Number" integer="true" static="true"/>
    throw Error.notImplemented();
}
Sys.UI.Key.prototype = {
    backspace: 8,
    tab: 9,
    enter: 13,
    esc: 27,
    space: 32,
    pageUp: 33,
    pageDown: 34,
    end: 35,
    home: 36,
    left: 37,
    up: 38,
    right: 39,
    down: 40,
    del: 127
}
Sys.UI.Key.registerEnum("Sys.UI.Key");
 
Sys.UI.Point = function(x, y) {
    /// <param name="x" type="Number"/>
    /// <param name="y" type="Number"/>
    /// <field name="x" type="Number" integer="true"/>
    /// <field name="y" type="Number" integer="true"/>
    /// <field name="rawX" type="Number" />
    /// <field name="rawY" type="Number" />
    // DevDiv 398683: IE10 started returning floating points
    // Just round the inputs to protect against getting non integers
    this.rawX = x;
    this.rawY = y;
    this.x = Math.round(x);
    this.y = Math.round(y);
}
Sys.UI.Point.registerClass('Sys.UI.Point');
 
Sys.UI.Bounds = function(x, y, width, height) {
    /// <param name="x" type="Number" integer="true"/>
    /// <param name="y" type="Number" integer="true"/>
    /// <param name="width" type="Number" integer="true"/>
    /// <param name="height" type="Number" integer="true"/>
    /// <field name="x" type="Number" integer="true"/>
    /// <field name="y" type="Number" integer="true"/>
    /// <field name="width" type="Number" integer="true"/>
    /// <field name="height" type="Number" integer="true"/>
    this.x = x;
    this.y = y;
    this.height = height;
    this.width = width;
}
Sys.UI.Bounds.registerClass('Sys.UI.Bounds');
 
Sys.UI.DomEvent = function(eventObject) {
    /// <summary>A cross-browser object that represents event properties.</summary>
    /// <param name="eventObject">The browser-specific event object (window.event for IE).</param>
    /// <field name="altKey" type="Boolean"/>
    /// <field name="button" type="Sys.UI.MouseButton"/>
    /// <field name="charCode" type="Number" integer="true">The character code for the pressed key.</field>
    /// <field name="clientX" type="Number" integer="true"/>
    /// <field name="clientY" type="Number" integer="true"/>
    /// <field name="ctrlKey" type="Boolean"/>
    /// <field name="keyCode" type="Number" integer="true">The key code for the pressed key.</field>
    /// <field name="offsetX" type="Number" integer="true"/>
    /// <field name="offsetY" type="Number" integer="true"/>
    /// <field name="screenX" type="Number" integer="true"/>
    /// <field name="screenY" type="Number" integer="true"/>
    /// <field name="shiftKey" type="Boolean"/>
    /// <field name="target"/>
    /// <field name="type" type="String"/>
    var ev = eventObject;
    var etype = this.type = ev.type.toLowerCase();
    this.rawEvent = ev;
    this.altKey = ev.altKey;
    if (typeof(ev.button) !== 'undefined') {
        this.button = (typeof(ev.which) !== 'undefined') ? ev.button :
            (ev.button === 4) ? Sys.UI.MouseButton.middleButton :
            (ev.button === 2) ? Sys.UI.MouseButton.rightButton :
            Sys.UI.MouseButton.leftButton;
    }
    if (etype === 'keypress') {
        this.charCode = ev.charCode || ev.keyCode;
    }
    else if (ev.keyCode && (ev.keyCode === 46)) {
        this.keyCode = 127;
    }
    else {
        this.keyCode = ev.keyCode;
    }
    this.clientX = ev.clientX;
    this.clientY = ev.clientY;
    this.ctrlKey = ev.ctrlKey;
    this.target = ev.target ? ev.target : ev.srcElement;
    if (!etype.startsWith('key')) {
        if ((typeof(ev.offsetX) !== 'undefined') && (typeof(ev.offsetY) !== 'undefined')) {
            this.offsetX = ev.offsetX;
            this.offsetY = ev.offsetY;
        }
        else if (this.target && (this.target.nodeType !== 3) && (typeof(ev.clientX) === 'number')) {
            var loc = Sys.UI.DomElement.getLocation(this.target);
            var w = Sys.UI.DomElement._getWindow(this.target);
            this.offsetX = (w.pageXOffset || 0) + ev.clientX - loc.x;
            this.offsetY = (w.pageYOffset || 0) + ev.clientY - loc.y;
        }
    }
    this.screenX = ev.screenX;
    this.screenY = ev.screenY;
    this.shiftKey = ev.shiftKey;
}
Sys.UI.DomEvent.prototype = {
    preventDefault: function() {
        /// <summary>
        ///   Prevents the default event action from happening. For example, a textbox keydown event,
        ///   if suppressed, will prevent the character from being appended to the textbox.
        /// </summary>
        if (this.rawEvent.preventDefault) {
            this.rawEvent.preventDefault();
        }
        else if (window.event) {
            this.rawEvent.returnValue = false;
        }
    },
    stopPropagation: function() {
        /// <summary>Prevents the event from being propagated to parent elements.</summary>
        if (this.rawEvent.stopPropagation) {
            this.rawEvent.stopPropagation();
        }
        else if (window.event) {
            this.rawEvent.cancelBubble = true;
        }
    }
}
Sys.UI.DomEvent.registerClass('Sys.UI.DomEvent');

var $addHandler = Sys.UI.DomEvent.addHandler = function(element, eventName, handler, autoRemove) {
    /// <summary>A cross-browser way to add a DOM event handler to an element.</summary>
    /// <param name="element">The element or text node that exposes the event.</param>
    /// <param name="eventName" type="String">
    ///   The name of the event. Do not include the 'on' prefix, for example, 'click' instead of 'onclick'.
    /// </param>
    /// <param name="handler" type="Function">The event handler to add.</param>
    /// <param name="autoRemove" type="Boolean" optional="true">
    /// Whether the handler should be removed automatically when the element is disposed of,
    /// such as when an UpdatePanel refreshes, or Sys.Application.disposeElement is called.
    /// </param>
    Sys.UI.DomEvent._ensureDomNode(element);
    if (eventName === "error") throw Error.invalidOperation(Sys.Res.addHandlerCantBeUsedForError);
    if (!element._events) {
        element._events = {};
    }
    var eventCache = element._events[eventName];
    if (!eventCache) {
        element._events[eventName] = eventCache = [];
    }
    var browserHandler;
    if (element.addEventListener) {
        browserHandler = function(e) {
            return handler.call(element, new Sys.UI.DomEvent(e));
        }
        element.addEventListener(eventName, browserHandler, false);
    }
    else if (element.attachEvent) {
        browserHandler = function() {
            // window.event can be denied access in some rare circumstances (DevDiv 68929)
            var e = {};
            // We want to use the window for the event element, not the window for this script (DevDiv 63167)
            try {e = Sys.UI.DomElement._getWindow(element).event} catch(ex) {}
            return handler.call(element, new Sys.UI.DomEvent(e));
        }
        element.attachEvent('on' + eventName, browserHandler);
    }
    eventCache[eventCache.length] = {handler: handler, browserHandler: browserHandler, autoRemove: autoRemove };
    if (autoRemove) {
        var d = element.dispose;
        if (d !== Sys.UI.DomEvent._disposeHandlers) {
            // element.dispose called when an updatepanel refreshes or disposeElement called.
            element.dispose = Sys.UI.DomEvent._disposeHandlers;
            if (typeof(d) !== "undefined") {
                element._chainDispose = d;
            }
        }
    }
}

var $addHandlers = Sys.UI.DomEvent.addHandlers = function(element, events, handlerOwner, autoRemove) {
    /// <summary>
    ///   Adds a list of event handlers to an element.
    ///   If a handlerOwner is specified, delegates are created with each of the handlers.
    /// </summary>
    /// <param name="element">The element or text node that exposes the event.</param>
    /// <param name="events" type="Object">A dictionary of event handlers.</param>
    /// <param name="handlerOwner" optional="true">
    ///   The owner of the event handlers that will be the this pointer
    ///   for the delegates that will be created from the handlers.
    /// </param>
    /// <param name="autoRemove" type="Boolean" optional="true">
    /// Whether the handler should be removed automatically when the element is disposed of,
    /// such as when an UpdatePanel refreshes, or when Sys.Application.disposeElement is called.
    /// </param>
    Sys.UI.DomEvent._ensureDomNode(element);
    for (var name in events) {
        var handler = events[name];
        if (typeof(handler) !== 'function') throw Error.invalidOperation(Sys.Res.cantAddNonFunctionhandler);
        if (handlerOwner) {
            handler = Function.createDelegate(handlerOwner, handler);
        }
        $addHandler(element, name, handler, autoRemove || false);
    }
}

var $clearHandlers = Sys.UI.DomEvent.clearHandlers = function(element) {
    /// <summary>
    ///   Clears all the event handlers that were added to the element.
    /// </summary>
    /// <param name="element">The element or text node.</param>
    Sys.UI.DomEvent._ensureDomNode(element);
    Sys.UI.DomEvent._clearHandlers(element, false);
}

Sys.UI.DomEvent._clearHandlers = function(element, autoRemoving) {
    if (element._events) {
        var cache = element._events;
        for (var name in cache) {
            var handlers = cache[name];
            for (var i = handlers.length - 1; i >= 0; i--) {
                var entry = handlers[i];
                if (!autoRemoving || entry.autoRemove) {
                    $removeHandler(element, name, entry.handler);
                }
            }
        }
        element._events = null;
    }
}

Sys.UI.DomEvent._disposeHandlers = function() {
    Sys.UI.DomEvent._clearHandlers(this, true);
    var d = this._chainDispose, type = typeof(d);
    if (type !== "undefined") {
        this.dispose = d;
        this._chainDispose = null;
        if (type === "function") {
            this.dispose();
        }
    }
}

var $removeHandler = Sys.UI.DomEvent.removeHandler = function(element, eventName, handler) {
    /// <summary>A cross-browser way to remove a DOM event handler from an element.</summary>
    /// <param name="element">The element or text node that exposes the event.</param>
    /// <param name="eventName" type="String">
    ///   The name of the event. Do not include the 'on' prefix, for example, 'click' instead of 'onclick'.
    /// </param>
    /// <param name="handler" type="Function">The event handler to remove.</param>
    Sys.UI.DomEvent._removeHandler(element, eventName, handler);
}
Sys.UI.DomEvent._removeHandler = function(element, eventName, handler) {
    Sys.UI.DomEvent._ensureDomNode(element);
    var browserHandler = null;
    if ((typeof(element._events) !== 'object') || !element._events) throw Error.invalidOperation(Sys.Res.eventHandlerInvalid);
    var cache = element._events[eventName];
    if (!(cache instanceof Array)) throw Error.invalidOperation(Sys.Res.eventHandlerInvalid);
    for (var i = 0, l = cache.length; i < l; i++) {
        if (cache[i].handler === handler) {
            browserHandler = cache[i].browserHandler;
            break;
        }
    }
    if (typeof(browserHandler) !== 'function') throw Error.invalidOperation(Sys.Res.eventHandlerInvalid);
    if (element.removeEventListener) {
        element.removeEventListener(eventName, browserHandler, false);
    }
    else if (element.detachEvent) {
        element.detachEvent('on' + eventName, browserHandler);
    }
    cache.splice(i, 1);
}

Sys.UI.DomEvent._ensureDomNode = function(element) {
    // DevDiv Bugs 100697: Accessing element.document causes dynamic script nodes to load prematurely.
    // DevDiv Bugs 124696: Firefox warns on undefined property element.tagName, added first part of IF
    // DevDiv Bugs 146697: tagName needs to be case insensitive to work with xhtml content type
    if (element.tagName && (element.tagName.toUpperCase() === "SCRIPT")) return;
    
    var doc = element.ownerDocument || element.document || element;
    // Can't use _getWindow here and compare to the element to check if it's a window
    // because the object Safari exposes as document.defaultView is not the window (DevDiv 100229)
    // Looking at the document property instead to include window in DOM nodes, then comparing to the
    // document for this element and finally look for the nodeType property.
    if ((typeof(element.document) !== 'object') && (element != doc) && (typeof(element.nodeType) !== 'number')) {
        throw Error.argument("element", Sys.Res.argumentDomNode);
    }
}
 
Sys.UI.DomElement = function() {
    /// <summary>This static class provides helpers to work with DOM elements.</summary>
    throw Error.notImplemented();
}
Sys.UI.DomElement.registerClass('Sys.UI.DomElement');

Sys.UI.DomElement.addCssClass = function(element, className) {
    /// <summary>Adds a CSS class to an element if it doesn't already have it.</summary>
    /// <param name="element" domElement="true"/>
    /// <param name="className" type="String">The name of the CSS class to add.</param>
    if (!Sys.UI.DomElement.containsCssClass(element, className)) {
        if (element.className === '') {
            element.className = className;
        }
        else {
            element.className += ' ' + className;
        }
    }
}

Sys.UI.DomElement.containsCssClass = function(element, className) {
    /// <summary>Determines if an element has the specified CSS class.</summary>
    /// <param name="element" domElement="true"/>
    /// <param name="className" type="String">The name of the CSS class to test.</param>
    /// <returns type="Boolean">True if the CSS class was found on the element.</returns>
    return Array.contains(element.className.split(' '), className);
}

Sys.UI.DomElement.getBounds = function(element) {
    /// <summary>Gets the coordinates, width and height of an element.</summary>
    /// <param name="element" domElement="true"/>
    /// <returns type="Sys.UI.Bounds">
    ///   A Bounds object with four fields, x, y, width and height, which contain the pixel coordinates,
    ///   width and height of the element.
    /// </returns>
    var offset = Sys.UI.DomElement.getLocation(element);

    return new Sys.UI.Bounds(offset.x, offset.y, element.offsetWidth || 0, element.offsetHeight || 0);
}

var $get = Sys.UI.DomElement.getElementById = function(id, element) {
    /// <summary>Finds an element by id.</summary>
    /// <param name="id" type="String">The id of the element to find.</param>
    /// <param name="element" domElement="true" optional="true" mayBeNull="true"/>
    /// <returns domElement="true" mayBeNull="true">The element, or null if it was not found.</returns>
    if (!element) return document.getElementById(id);
    if (element.getElementById) return element.getElementById(id);

    // Implementation for browsers that don't have getElementById on elements:
    var nodeQueue = [];
    var childNodes = element.childNodes;
    for (var i = 0; i < childNodes.length; i++) {
        var node = childNodes[i];
        if (node.nodeType == 1) {
            nodeQueue[nodeQueue.length] = node;
        }
    }

    while (nodeQueue.length) {
        node = nodeQueue.shift();
        if (node.id == id) {
            return node;
        }
        childNodes = node.childNodes;
        for (i = 0; i < childNodes.length; i++) {
            node = childNodes[i];
            if (node.nodeType == 1) {
                nodeQueue[nodeQueue.length] = node;
            }
        }
    }

    return null;
}


if (document.documentElement.getBoundingClientRect) {
    Sys.UI.DomElement.getLocation = function(element) {
        /// <summary>Gets the coordinates of a DOM element.</summary>
        /// <param name="element" domElement="true"/>
        /// <returns type="Sys.UI.Point">
        ///   A Point object with two fields, x and y, which contain the pixel coordinates of the element.
        /// </returns>
        // For a document element, body, or window, return zero.
        // In IE8, the boundingClientRect for body is influenced by the bounding rect of its content, and so may not be 0,0.
        // But for positioning purposes, elements positioned at 0,0 will be at the top even if the content has margins, etc, so
        // getlocation should return 0,0 for body.
        // In all browsers, detecting the body works by seeing if the element's parent ndoe is the element's own document's documentElement node.
        if (element.self || element.nodeType === 9 || // window?
            (element === document.documentElement) || // documentElement?
            (element.parentNode === element.ownerDocument.documentElement)) { // body?
            return new Sys.UI.Point(0, 0);
        }        
        
        // Here there is a small inconsistency with what other browsers would give for wrapping elements:
        // the bounding rect can be different from the first rectangle. getBoundingRect is used here
        // because it's more consistent and because clientRects need to be offset by the coordinates
        // of the frame in the parent window, which is not always accessible to script (if it's in a different
        // domain in particular).
        var clientRect = element.getBoundingClientRect();
        if (!clientRect) {
            return new Sys.UI.Point(0,0);
        }
        // Dev11 360629 - Different browsers can fill in the scroll position on either the documentElement or the body
        // element.  Safari and quirks-mode IE use the body element from what I can tell, and standards-mode IE along
        // with most other browsers use documentElement.  The other value will always exist, but be 0.  Choose the
        // correct one carefully, giving preference to the more standard documentElement.
        var documentElement = element.ownerDocument.documentElement;
        var bodyElement = element.ownerDocument.body;
        // Firefox 3 can return decimals here, so round them.
        // This appears to be consistent with how the display engine actually places the element when there is a decimal.
        var ex,
            offsetX = Math.round(clientRect.left) + (documentElement.scrollLeft || bodyElement.scrollLeft),
            offsetY = Math.round(clientRect.top) + (documentElement.scrollTop || bodyElement.scrollTop);
        if (Sys.Browser.agent === Sys.Browser.InternetExplorer) {
            // When the window is an iframe, the frameborder needs to be added. This is only available from
            // script when the parent window is in the same domain as the frame, hence the try/catch.
            try {
                var f = element.ownerDocument.parentWindow.frameElement || null;
                if (f) {
                    // frameBorder has a default of "1" so undefined must map to 0, and "0" and "no" to 2.
                    var offset = (f.frameBorder === "0" || f.frameBorder === "no") ? 2 : 0;
                    offsetX += offset;
                    offsetY += offset;
                }
            }
            catch(ex) {
            }
            if (Sys.Browser.version === 7 && !document.documentMode) {
                // IE7 reapplies the page zoom level when using the returned coordinates.
                // therefore we must divide by the zoom level to compensate. This is not perfect, but close.
                // NOTE: IE8 with document.documentMode === 7 does NOT emulate IE7 behavior, by design.
                // Also, this zoom detection does not work perfectly in IE8 compat mode, where we would want
                // it to be 100% always, so it is necessary that we ensure this only happens in ACTUAL IE7.
                // IE6 does not support zoom.
                var body = document.body,
                    rect = body.getBoundingClientRect(),
                    zoom = (rect.right-rect.left) / body.clientWidth;
                // zoom is not completely accurate, so snap to the previous 5% by multiplying by 100, rounding,
                // then subtracting zoom % 5, then dividing by 100 to get back to a multiplier.
                // It's not likely someone is zooming at 154%, for example, so that probably means it is actually 150%, whereas
                // 156% probably means 155% (the estimate tends to over-estimate).
                zoom = Math.round(zoom * 100);
                zoom = (zoom - zoom % 5) / 100;
                if (!isNaN(zoom) && (zoom !== 1)) {
                    offsetX = Math.round(offsetX / zoom);
                    offsetY = Math.round(offsetY / zoom);
                }
            }        
            if ((document.documentMode || 0) < 8) {
                offsetX -= documentElement.clientLeft;
                offsetY -= documentElement.clientTop;
            }
        }
        return new Sys.UI.Point(offsetX, offsetY);
    }
}
else if (Sys.Browser.agent === Sys.Browser.Safari) {
    Sys.UI.DomElement.getLocation = function(element) {
        /// <summary>Gets the coordinates of a DOM element.</summary>
        /// <param name="element" domElement="true"/>
        /// <returns type="Sys.UI.Point">
        ///   A Point object with two fields, x and y, which contain the pixel coordinates of the element.
        /// </returns>
        // For a document element, return zero.
        if ((element.window && (element.window === element)) || element.nodeType === 9) return new Sys.UI.Point(0,0);

        var offsetX = 0, offsetY = 0,
            parent,
            previous = null,
            previousStyle = null,
            currentStyle;
        for (parent = element; parent; previous = parent, previousStyle = currentStyle, parent = parent.offsetParent) {
            currentStyle = Sys.UI.DomElement._getCurrentStyle(parent);
            // DevDiv Bugs 146697: tagName needs to be case insensitive to work with xhtml content type
            var tagName = parent.tagName ? parent.tagName.toUpperCase() : null;

            // Safari has a bug that double-counts the body offset for absolutely positioned elements
            // that are direct children of body.
            // Firefox has its own quirk, which is that non-absolutely positioned elements that are
            // direct children of body get the body offset counted twice.
            if ((parent.offsetLeft || parent.offsetTop) &&
                ((tagName !== "BODY") || (!previousStyle || previousStyle.position !== "absolute"))) {
                offsetX += parent.offsetLeft;
                offsetY += parent.offsetTop;
            }

            // safari 3 does not count border width. But don't count the element's own border.
            // previous would be set if parent is not the target element.
            if (previous && Sys.Browser.version >= 3) {
                offsetX += parseInt(currentStyle.borderLeftWidth);
                offsetY += parseInt(currentStyle.borderTopWidth);
            }
        }

        currentStyle = Sys.UI.DomElement._getCurrentStyle(element);
        var elementPosition = currentStyle ? currentStyle.position : null;
        // If an element is absolutely positioned, its parent's scroll should not be subtracted
        if (!elementPosition || (elementPosition !== "absolute")) {
            // In Firefox and Safari, all parent's scroll values must be taken into account.
            for (parent = element.parentNode; parent; parent = parent.parentNode) {
                // DevDiv Bugs 146697: tagName needs to be case insensitive to work with xhtml content type
                tagName = parent.tagName ? parent.tagName.toUpperCase() : null;

                if ((tagName !== "BODY") && (tagName !== "HTML") && (parent.scrollLeft || parent.scrollTop)) {
                    offsetX -= (parent.scrollLeft || 0);
                    offsetY -= (parent.scrollTop || 0);
                }
                currentStyle = Sys.UI.DomElement._getCurrentStyle(parent);
                var parentPosition = currentStyle ? currentStyle.position : null;

                // If an element is absolutely positioned, its parent's scroll should not be subtracted
                if (parentPosition && (parentPosition === "absolute")) break;
            }
        }
        return new Sys.UI.Point(offsetX, offsetY);
    }
}
else {
    Sys.UI.DomElement.getLocation = function(element) {
        /// <summary>Gets the coordinates of a DOM element.</summary>
        /// <param name="element" domElement="true"/>
        /// <returns type="Sys.UI.Point">
        ///   A Point object with two fields, x and y, which contain the pixel coordinates of the element.
        /// </returns>
        // For a document element, return zero.
        if ((element.window && (element.window === element)) || element.nodeType === 9) return new Sys.UI.Point(0,0);

        var offsetX = 0, offsetY = 0,
            parent,
            previous = null,
            previousStyle = null,
            currentStyle = null;
        for (parent = element; parent; previous = parent, previousStyle = currentStyle, parent = parent.offsetParent) {
            // DevDiv Bugs 146697: tagName needs to be case insensitive to work with xhtml content type
            var tagName = parent.tagName ? parent.tagName.toUpperCase() : null;
            currentStyle = Sys.UI.DomElement._getCurrentStyle(parent);

            // Firefox has its own quirk, which is that non-absolutely positioned elements that are
            // direct children of body get the body offset counted twice.
            if ((parent.offsetLeft || parent.offsetTop) &&
                !((tagName === "BODY") &&
                (!previousStyle || previousStyle.position !== "absolute"))) {

                offsetX += parent.offsetLeft;
                offsetY += parent.offsetTop;
            }

            // This code works around a difference in behavior in Opera and Safari which includes
            // clientLeft and clientTop in the computedstyle offset.
            if (previous !== null && currentStyle) {
                // This is to workaround a known bug in IE and Firefox:
                // <table> and <td> have strange behavior with offsetLeft/offsetTop and clientLeft/clientTop.
                // Say you have the following html: <table style="border-width:25px"><tr><td></table>
                // The offsetLeft and offsetTop for the <td> will be 25, but the client/borderLeft and
                // client/borderTop for the <table> will also be 25.  So if you count the client/borderLeft and
                // client/borderTop for the <table>, you will be double-counting the table border.
                if ((tagName !== "TABLE") && (tagName !== "TD") && (tagName !== "HTML")) {
                    offsetX += parseInt(currentStyle.borderLeftWidth) || 0;
                    offsetY += parseInt(currentStyle.borderTopWidth) || 0;
                }
                if (tagName === "TABLE" &&
                    (currentStyle.position === "relative" || currentStyle.position === "absolute")) {
                    offsetX += parseInt(currentStyle.marginLeft) || 0;
                    offsetY += parseInt(currentStyle.marginTop) || 0;
                }
            }
        }

        currentStyle = Sys.UI.DomElement._getCurrentStyle(element);
        var elementPosition = currentStyle ? currentStyle.position : null;
        // If an element is absolutely positioned, its parent's scroll should not be subtracted, except on Opera.
        if (!elementPosition || (elementPosition !== "absolute")) {
            // In Firefox and Safari, all parent's scroll values must be taken into account.
            // In IE, only the offset parent's because positioned elements are offset-parented to BODY and
            // don't need scroll substraction. Non-positioned elements are offset-parented to their parent,
            // which may be scrolled.
            for (parent = element.parentNode; parent; parent = parent.parentNode) {
                // In IE quirks mode, the <body> element has bogus values for scrollLeft and scrollTop.
                // So we do not use the scrollLeft and scrollTop for the <body> element.  This does not
                // break the standards mode behavior. (VSWhidbey 426176)
                // DevDiv Bugs 146697: tagName needs to be case insensitive to work with xhtml content type
                tagName = parent.tagName ? parent.tagName.toUpperCase() : null;

                if ((tagName !== "BODY") && (tagName !== "HTML") && (parent.scrollLeft || parent.scrollTop)) {

                    offsetX -= (parent.scrollLeft || 0);
                    offsetY -= (parent.scrollTop || 0);

                    currentStyle = Sys.UI.DomElement._getCurrentStyle(parent);
                    if (currentStyle) {
                        offsetX += parseInt(currentStyle.borderLeftWidth) || 0;
                        offsetY += parseInt(currentStyle.borderTopWidth) || 0;
                    }
                }
            }
        }
        return new Sys.UI.Point(offsetX, offsetY);
    }
}

Sys.UI.DomElement.isDomElement = function(obj) {
    /// <summary>Determines if the given argument is a DOM element.</summary>
    /// <param name="obj"></param>
    /// <returns type="Boolean">True if the object is a DOM element, otherwise false.</returns>
    return Sys._isDomElement(obj);
}

Sys.UI.DomElement.removeCssClass = function(element, className) {
    /// <summary>Removes a CSS class from an element.</summary>
    /// <param name="element" domElement="true"/>
    /// <param name="className" type="String">The name of the CSS class to remove.</param>
    var currentClassName = ' ' + element.className + ' ';
    var index = currentClassName.indexOf(' ' + className + ' ');
    if (index >= 0) {
        element.className = (currentClassName.substr(0, index) + ' ' +
            currentClassName.substring(index + className.length + 1, currentClassName.length)).trim();
    }
}

Sys.UI.DomElement.resolveElement = function(elementOrElementId, containerElement) {
    /// <summary>Returns the element with the specified Id in the specified container, or the element if it is already an element.</summary>
    /// <param name="elementOrElementId" mayBeNull="true" />
    /// <param name="containerElement" domElement="true" optional="true" mayBeNull="true"/>
    /// <returns domElement="true"/>
    var el = elementOrElementId;
    if (!el) return null;
    if (typeof(el) === "string") {
        el = Sys.UI.DomElement.getElementById(el, containerElement);
        if (!el) {
            throw Error.argument("elementOrElementId", String.format(Sys.Res.elementNotFound, elementOrElementId));
        }
    }
    else if(!Sys.UI.DomElement.isDomElement(el)) {
        throw Error.argument("elementOrElementId", Sys.Res.expectedElementOrId);
    }
    return el;
}

Sys.UI.DomElement.raiseBubbleEvent = function(source, args) {
    /// <summary>Raises a bubble event.</summary>
    /// <param name="source" domElement="true">The DOM element that triggers the event.</param>
    /// <param name="args" type="Sys.EventArgs">The event arguments.</param>
    var target = source;
    while (target) {
        var control = target.control;
        if (control && control.onBubbleEvent && control.raiseBubbleEvent) {
            Sys.UI.DomElement._raiseBubbleEventFromControl(control, source, args);
            return;
        }
        target = target.parentNode;
    }
}

Sys.UI.DomElement._raiseBubbleEventFromControl = function(control, source, args) {
    if (!control.onBubbleEvent(source, args)) {
        control._raiseBubbleEvent(source, args);
    }
}

Sys.UI.DomElement.setLocation = function(element, x, y) {
    /// <summary>Sets the position of an element.</summary>
    /// <param name="element" domElement="true"/>
    /// <param name="x" type="Number" integer="true"/>
    /// <param name="y" type="Number" integer="true"/>
    var style = element.style;
    style.position = 'absolute';
    style.left = x + "px";
    style.top = y + "px";
}

Sys.UI.DomElement.toggleCssClass = function(element, className) {
    /// <summary>Toggles a CSS class on and off o an element.</summary>
    /// <param name="element" domElement="true"/>
    /// <param name="className" type="String">The name of the CSS class to toggle.</param>
    if (Sys.UI.DomElement.containsCssClass(element, className)) {
        Sys.UI.DomElement.removeCssClass(element, className);
    }
    else {
        Sys.UI.DomElement.addCssClass(element, className);
    }
}

Sys.UI.DomElement.getVisibilityMode = function(element) {
    /// <param name="element" domElement="true"/>
    /// <returns type="Sys.UI.VisibilityMode"/>
    return (element._visibilityMode === Sys.UI.VisibilityMode.hide) ?
        Sys.UI.VisibilityMode.hide :
        Sys.UI.VisibilityMode.collapse;
}
Sys.UI.DomElement.setVisibilityMode = function(element, value) {
    /// <param name="element" domElement="true"/>
    /// <param name="value" type="Sys.UI.VisibilityMode"/>
    Sys.UI.DomElement._ensureOldDisplayMode(element);
    if (element._visibilityMode !== value) {
        element._visibilityMode = value;
        if (Sys.UI.DomElement.getVisible(element) === false) {
            if (element._visibilityMode === Sys.UI.VisibilityMode.hide) {
                element.style.display = element._oldDisplayMode;
            }
            else {
                element.style.display = 'none';
            }
        }
        element._visibilityMode = value;
    }
}

Sys.UI.DomElement.getVisible = function(element) {
    /// <param name="element" domElement="true"/>
    /// <returns type="Boolean"/>
    var style = element.currentStyle || Sys.UI.DomElement._getCurrentStyle(element);
    if (!style) return true;
    return (style.visibility !== 'hidden') && (style.display !== 'none');
}
Sys.UI.DomElement.setVisible = function(element, value) {
    /// <param name="element" domElement="true"/>
    /// <param name="value" type="Boolean"/>
    if (value !== Sys.UI.DomElement.getVisible(element)) {
        Sys.UI.DomElement._ensureOldDisplayMode(element);
        element.style.visibility = value ? 'visible' : 'hidden';
        if (value || (element._visibilityMode === Sys.UI.VisibilityMode.hide)) {
            element.style.display = element._oldDisplayMode;
        }
        else {
            element.style.display = 'none';
        }
    }
}

Sys.UI.DomElement._ensureOldDisplayMode = function(element) {
    if (!element._oldDisplayMode) {
        var style = element.currentStyle || Sys.UI.DomElement._getCurrentStyle(element);
        element._oldDisplayMode = style ? style.display : null;
        if (!element._oldDisplayMode || element._oldDisplayMode === 'none') {
            // Default is different depending on the tag name (omitting deprecated and non-standard tags)
            switch(element.tagName.toUpperCase()) {
                case 'DIV': case 'P': case 'ADDRESS': case 'BLOCKQUOTE': case 'BODY': case 'COL':
                case 'COLGROUP': case 'DD': case 'DL': case 'DT': case 'FIELDSET': case 'FORM':
                case 'H1': case 'H2': case 'H3': case 'H4': case 'H5': case 'H6': case 'HR':
                case 'IFRAME': case 'LEGEND': case 'OL': case 'PRE': case 'TABLE': case 'TD':
                case 'TH': case 'TR': case 'UL':
                    element._oldDisplayMode = 'block';
                    break;
                case 'LI':
                    element._oldDisplayMode = 'list-item';
                    break;
                default:
                    element._oldDisplayMode = 'inline';
            }
        }
    }
}

Sys.UI.DomElement._getWindow = function(element) {
    var doc = element.ownerDocument || element.document || element;
    return doc.defaultView || doc.parentWindow;
}

Sys.UI.DomElement._getCurrentStyle = function(element) {
    if (element.nodeType === 3) return null;
    var w = Sys.UI.DomElement._getWindow(element);
    if (element.documentElement) element = element.documentElement;
    var computedStyle = (w && (element !== w) && w.getComputedStyle) ?
        w.getComputedStyle(element, null) :
        element.currentStyle || element.style;
    if (!computedStyle && (Sys.Browser.agent === Sys.Browser.Safari) && element.style) {
        // Safari has an interesting bug (fixed in WebKit) where an element with display:none will have a null computed style.
        var oldDisplay = element.style.display;
        var oldPosition = element.style.position;
        element.style.position = 'absolute';
        element.style.display = 'block';
        var style = w.getComputedStyle(element, null);
        element.style.display = oldDisplay;
        element.style.position = oldPosition;
        // Need a clone as the display property may be wrong and can't be fixed on the original object.
        computedStyle = {};
        for (var n in style) {
            computedStyle[n] = style[n];
        }
        computedStyle.display = 'none';
    }
    return computedStyle;
}
 
Sys.IContainer = function() {
    throw Error.notImplemented();
}
Sys.IContainer.prototype = {
    addComponent: function(component) {
        /// <param name="component" type="Sys.Component"/>
        throw Error.notImplemented();
    },
    removeComponent: function(component) {
        /// <param name="component" type="Sys.Component"/>
        throw Error.notImplemented();
    },
    findComponent: function(id) {
        /// <param name="id" type="String"/>
        /// <returns type="Sys.Component"/>
        throw Error.notImplemented();
    },
    getComponents: function() {
        /// <returns type="Array" elementType="Sys.Component"/>
        throw Error.notImplemented();
    }
}
Sys.IContainer.registerInterface("Sys.IContainer");

 
Sys.ApplicationLoadEventArgs = function(components, isPartialLoad) {
    /// <param name="components" type="Array" elementType="Sys.Component">
    ///   The list of components that were created since the last time the load event was raised.
    /// </param>
    /// <param name="isPartialLoad" type="Boolean">True if the page is partially loading.</param>
    Sys.ApplicationLoadEventArgs.initializeBase(this);
    this._components = components;
    this._isPartialLoad = isPartialLoad;
}
 Sys.ApplicationLoadEventArgs.prototype = {
    get_components: function() {
        /// <value type="Array" elementType="Sys.Component">
        ///   The list of components that were created since the last time the load event was raised.
        /// </value>
        return this._components;
    },
    get_isPartialLoad: function() {
        /// <value type="Boolean">True if the page is partially loading.</value>
        return this._isPartialLoad;
    }
}
Sys.ApplicationLoadEventArgs.registerClass('Sys.ApplicationLoadEventArgs', Sys.EventArgs);
 
Sys._Application = function() {
    /// <summary locid="M:J#Sys.Application.#ctor"/>
    Sys._Application.initializeBase(this);

    this._disposableObjects = [];
    this._components = {};
    this._createdComponents = [];
    this._secondPassComponents = [];

    // dispose the app in window.unload
    this._unloadHandlerDelegate = Function.createDelegate(this, this._unloadHandler);
    Sys.UI.DomEvent.addHandler(window, "unload", this._unloadHandlerDelegate);
    // automatically initialize when the dom is ready
    this._domReady();
}
Sys._Application.prototype = {
    _creatingComponents: false,
    _disposing: false,
    _deleteCount: 0,

    get_isCreatingComponents: function() {
        /// <value type="Boolean" locid="P:J#Sys.Application.isCreatingComponents"/>
        return this._creatingComponents;
    },
    get_isDisposing: function() {
        /// <value type="Boolean" locid="P:J#Sys.Application.isDisposing"/>
        return this._disposing;
    },
    add_init: function(handler) {
        /// <summary locid="E:J#Sys.Application.init"/>
        if (this._initialized) {
            handler(this, Sys.EventArgs.Empty);
        }
        else {
            this.get_events().addHandler("init", handler);
        }
    },
    remove_init: function(handler) {
        this.get_events().removeHandler("init", handler);
    },
    add_load: function(handler) {
        /// <summary locid="E:J#Sys.Application.load"/>
        this.get_events().addHandler("load", handler);
    },
    remove_load: function(handler) {
        this.get_events().removeHandler("load", handler);
    },
    add_unload: function(handler) {
        /// <summary locid="E:J#Sys.Application.unload"/>
        this.get_events().addHandler("unload", handler);
    },
    remove_unload: function(handler) {
        this.get_events().removeHandler("unload", handler);
    },
    addComponent: function(component) {
        /// <summary locid="M:J#Sys.Application.addComponent">Adds a top-level component to the application.</summary>
        /// <param name="component" type="Sys.Component">The component to add.</param>
        var id = component.get_id();
        if (!id) throw Error.invalidOperation(Sys.Res.cantAddWithoutId);
        if (typeof(this._components[id]) !== 'undefined') throw Error.invalidOperation(String.format(Sys.Res.appDuplicateComponent, id));
        this._components[id] = component;
    },
    beginCreateComponents: function() {
        /// <summary locid="M:J#Sys.Application.beginCreateComponents"/>
        this._creatingComponents = true;
    },
    dispose: function() {
        /// <summary locid="M:J#Sys.Application.dispose"/>
        if (!this._disposing) {
            this._disposing = true;
            if (this._timerCookie) {
                window.clearTimeout(this._timerCookie);
                delete this._timerCookie;
            }
            if (this._endRequestHandler) {
                Sys.WebForms.PageRequestManager.getInstance().remove_endRequest(this._endRequestHandler);
                delete this._endRequestHandler;
            }
            if (this._beginRequestHandler) {
                Sys.WebForms.PageRequestManager.getInstance().remove_beginRequest(this._beginRequestHandler);
                delete this._beginRequestHandler;
            }
            if (window.pageUnload) {
                window.pageUnload(this, Sys.EventArgs.Empty);
            }
            var unloadHandler = this.get_events().getHandler("unload");
            if (unloadHandler) {
                unloadHandler(this, Sys.EventArgs.Empty);
            }
            var disposableObjects = Array.clone(this._disposableObjects);
            for (var i = 0, l = disposableObjects.length; i < l; i++) {
                var object = disposableObjects[i];
                // some entries are undefined, but we keep the density such that no more than 1000 are.
                if (typeof(object) !== "undefined") {
                    object.dispose();
                }
            }
            Array.clear(this._disposableObjects);

            Sys.UI.DomEvent.removeHandler(window, "unload", this._unloadHandlerDelegate);

            if (Sys._ScriptLoader) {
                var sl = Sys._ScriptLoader.getInstance();
                if(sl) {
                    sl.dispose();
                }
            }

            Sys._Application.callBaseMethod(this, 'dispose');
        }
    },
    disposeElement: function(element, childNodesOnly) {
        /// <summary>Disposes of control and behavior resources associated with an element and its child nodes.</summary>
        /// <param name="element">The element to dispose.</param>
        /// <param name="childNodesOnly" type="Boolean">Whether to dispose of the element and its child nodes or only its child nodes.</param>
        // note: cannot use domElement="true" for parameter because it fails for text nodes which we want to
        // allow here.
        if (element.nodeType === 1) {
            var i, allElements = element.getElementsByTagName("*"),
                length = allElements.length,
                children = new Array(length);
            // we must clone the array first as it is a reference to the actual live tree
            // and will change if disposed components modify the DOM
            // Also we cannot use Array.clone() because it is not actually a javascript array
            for (i = 0; i < length; i++) {
                children[i] = allElements[i];
            }
            for (i = length - 1; i >= 0; i--) {
                var child = children[i];
                // disposes of controls and behaviors attached to an element
                // logic adapted from PageRequestManager._destroyTree
                // This logic inlined because with a large number of DOM elements, the
                // overhead of calling a different method for each element adds up
                var d = child.dispose;
                if (d && typeof(d) === "function") {
                    child.dispose();
                }
                else {
                    var c = child.control;
                    if (c && typeof(c.dispose) === "function") {
                        c.dispose();
                    }
                }
                var list = child._behaviors;
                if (list) {
                    this._disposeComponents(list);
                }
                list = child._components;
                if (list) {
                    this._disposeComponents(list);
                    child._components = null;
                }
            }
            if (!childNodesOnly) {
                var d = element.dispose;
                if (d && typeof(d) === "function") {
                    element.dispose();
                }
                else {
                    var c = element.control;
                    if (c && typeof(c.dispose) === "function") {
                        c.dispose();
                    }
                }
                var list = element._behaviors;
                if (list) {
                    this._disposeComponents(list);
                }
                list = element._components;
                if (list) {
                    this._disposeComponents(list);
                    element._components = null;
                }
            }
        }
    },
    endCreateComponents: function() {
        /// <summary locid="M:J#Sys.Application.endCreateComponents"/>
        var components = this._secondPassComponents;
        for (var i = 0, l = components.length; i < l; i++) {
            var component = components[i].component;
            Sys$Component$_setReferences(component, components[i].references);
            component.endUpdate();
        }
        this._secondPassComponents = [];
        this._creatingComponents = false;
    },
    findComponent: function(id, parent) {
        /// <summary locid="M:J#Sys.Application.findComponent">
        ///   Finds top-level components that were added through addComponent if no parent is specified
        ///   or children of the specified parent. If parent is a component
        /// </summary>
        /// <param name="id" type="String">The id of the component to find.</param>
        /// <param name="parent" optional="true" mayBeNull="true">
        ///   The component or element that contains the component to find.
        ///   If not specified or null, the search is made on Application.
        /// </param>
        /// <returns type="Sys.Component" mayBeNull="true">The component, or null if it wasn't found.</returns>
        // Need to reference the application singleton directly beause the $find alias
        // points to the instance function without context. The 'this' pointer won't work here.
        return (parent ?
            ((Sys.IContainer.isInstanceOfType(parent)) ?
                parent.findComponent(id) :
                parent[id] || null) :
            Sys.Application._components[id] || null);
    },
    getComponents: function() {
        /// <summary locid="M:J#Sys.Application.getComponents"/>
        /// <returns type="Array" elementType="Sys.Component"/>
        var res = [];
        var components = this._components;
        for (var name in components) {
            res[res.length] = components[name];
        }
        return res;
    },
    initialize: function() {
        /// <summary locid="M:J#Sys.Application.initialize"/>
        if(!this.get_isInitialized() && !this._disposing) {
            Sys._Application.callBaseMethod(this, 'initialize');
            this._raiseInit();
            if (this.get_stateString) {
                // only execute if history has been imported
                if (Sys.WebForms && Sys.WebForms.PageRequestManager) {
                    // Subscribe to begin and end request events
                    this._beginRequestHandler = Function.createDelegate(this, this._onPageRequestManagerBeginRequest);
                    Sys.WebForms.PageRequestManager.getInstance().add_beginRequest(this._beginRequestHandler);
                    this._endRequestHandler = Function.createDelegate(this, this._onPageRequestManagerEndRequest);
                    Sys.WebForms.PageRequestManager.getInstance().add_endRequest(this._endRequestHandler);
                }
                var loadedEntry = this.get_stateString();
                if (loadedEntry !== this._currentEntry) {
                    this._navigate(loadedEntry);
                }
                else {
                    // Dev10 Bug: 599356
                    // necessary to ensure history is initialized, or there wont be any timer running to check for changes in the
                    // hash. For example, if the user navigates a few times, then goes back to the first state and refreshes the
                    // page, we will be loading the page with no state (this else case), yet they can click forward -- but we
                    // never initialized history so it goes unnoticed.
                    this._ensureHistory();
                }
            }
            this.raiseLoad();
        }
    },
    notifyScriptLoaded: function() {
        /// <summary locid="M:J#Sys.Application.notifyScriptLoaded">Called by referenced scripts to indicate that they have completed loading. [Obsolete]</summary>
    },
    registerDisposableObject: function(object) {
        /// <summary locid="M:J#Sys.Application.registerDisposableObject">Registers a disposable object with the application.</summary>
        /// <param name="object" type="Sys.IDisposable">The object to register.</param>
        if (!this._disposing) {
            var objects = this._disposableObjects,
                i = objects.length;
            objects[i] = object;
            object.__msdisposeindex = i;
        }
    },
    raiseLoad: function() {
        /// <summary locid="M:J#Sys.Application.raiseLoad"/>
        var h = this.get_events().getHandler("load");
        var args = new Sys.ApplicationLoadEventArgs(Array.clone(this._createdComponents), !!this._loaded);
        this._loaded = true;
        if (h) {
            h(this, args);
        }

        if (window.pageLoad) {
            window.pageLoad(this, args);
        }
        this._createdComponents = [];
    },
    removeComponent: function(component) {
        /// <summary locid="M:J#Sys.Application.removeComponent">Removes a top-level component from the application.</summary>
        /// <param name="component" type="Sys.Component">The component to remove.</param>
        var id = component.get_id();
        if (id) delete this._components[id];
    },
    unregisterDisposableObject: function(object) {
        /// <summary locid="M:J#Sys.Application.unregisterDisposableObject">Unregisters a disposable object from the application.</summary>
        /// <param name="object" type="Sys.IDisposable">The object to unregister.</param>
        if (!this._disposing) {
            var i = object.__msdisposeindex;
            if (typeof(i) === "number") {
                // delete it from the array instead of removing it, so the msdisposeindex
                // remains correct on the other existing objects
                // When the array is enumerated we use for/in to skip over the deleted entries.
                var disposableObjects = this._disposableObjects;
                delete disposableObjects[i];
                delete object.__msdisposeindex;
                if (++this._deleteCount > 1000) {
                    // periodically rebuild the array to remove the sparse elements
                    // to put a cap on the amount of memory it can consume
                    var newArray = [];
                    for (var j = 0, l = disposableObjects.length; j < l; j++) {
                        object = disposableObjects[j];
                        if (typeof(object) !== "undefined") {
                            object.__msdisposeindex = newArray.length;
                            newArray.push(object);
                        }
                    }
                    this._disposableObjects = newArray;
                    this._deleteCount = 0;
                }
            }
        }
    },
    _addComponentToSecondPass: function(component, references) {
        this._secondPassComponents[this._secondPassComponents.length] = {component: component, references: references};
    },
    _disposeComponents: function(list) {
        if (list) {
            for (var i = list.length - 1; i >= 0; i--) {
                var item = list[i];
                if (typeof(item.dispose) === "function") {
                    item.dispose();
                }
            }
        }
    },
    _domReady: function() {
        // note that the DOM might be ready immediately, in which case the application fires its
        // init and load events immediately during the constructor: new Sys._Application().
        // Since the instance of Sys.Application is set to that value, Sys.Application does not yet
        // exist when _domReady is called. No script here or called by any of this scripts should use
        // Sys.Application. Use 'app' or 'this' instead.
        var check, er, app = this;
        function init() { app.initialize(); }

        // window.onload is the safe fallback. The rest is to try and initialize sooner, since onload
        // only fires once all images and other binary content is downloaded.
        var onload = function() {
            Sys.UI.DomEvent.removeHandler(window, "load", onload);
            init();
        }
        Sys.UI.DomEvent.addHandler(window, "load", onload);
        
        if (document.addEventListener) {
            try {
                // try/catch in case the browser does not support DOMContentLoaded
                document.addEventListener("DOMContentLoaded", check = function() {
                    document.removeEventListener("DOMContentLoaded", check, false);
                    init();
                }, false);
            }
            catch (er) { }
        }
        else if (document.attachEvent) {
            if ((window == window.top) && document.documentElement.doScroll) {
                // timer/doscroll trick works only when not in a frame
                var timeout, el = document.createElement("div");
                check = function() {
                    try {
                        el.doScroll("left");
                    }
                    catch (er) {
                        timeout = window.setTimeout(check, 0);
                        return;
                    }
                    el = null;
                    init();
                }
                check();
            }
            else {
                // in a frame this is the only reliable way to fire before onload, however
                // testing has shown it is not much better than onload if at all better.
                // using a <script> element with defer="true" is much better, but you have to
                // document.write it for the 'defer' to work, and that wouldnt work if this
                // script is being loaded dynamically, a reasonable possibility.
                // There is no known way of detecting whether the script is loaded dynamically or not.
		document.attachEvent("onreadystatechange", check = function() {
                    if (document.readyState === "complete") {
                        document.detachEvent("onreadystatechange", check);
                        init();
                    }
                });


            }
        }

    },
    _raiseInit: function() {
        var handler = this.get_events().getHandler("init");
        if (handler) {
            this.beginCreateComponents();
            handler(this, Sys.EventArgs.Empty);
            this.endCreateComponents();
        }
    },
    _unloadHandler: function(event) {
        this.dispose();
    }
}
Sys._Application.registerClass('Sys._Application', Sys.Component, Sys.IContainer);

Sys.Application = new Sys._Application();

var $find = Sys.Application.findComponent;

 
Sys.UI.Behavior = function(element) {
    /// <param name="element" domElement="true">The DOM element the behavior is associated with.</param>
    Sys.UI.Behavior.initializeBase(this);

    this._element = element;

    var behaviors = element._behaviors;
    if (!behaviors) {
        element._behaviors = [this];
    }
    else {
        behaviors[behaviors.length] = this;
    }
}
Sys.UI.Behavior.prototype = {
    _name: null,
    get_element: function() {
        /// <value domElement="true">The DOM element this behavior is associated with</value>
        return this._element;
    },
    get_id: function() {
        /// <value type="String"/>
        var baseId = Sys.UI.Behavior.callBaseMethod(this, 'get_id');
        if (baseId) return baseId;
        if (!this._element || !this._element.id) return '';
        return this._element.id + '$' + this.get_name();
    },
    get_name: function() {
        /// <value type="String"/>
        if (this._name) return this._name;
        var name = Object.getTypeName(this);
        var i = name.lastIndexOf('.');
        if (i !== -1) name = name.substr(i + 1);
        if (!this.get_isInitialized()) this._name = name;
        return name;
    },
    set_name: function(value) {
        if ((value === '') || (value.charAt(0) === ' ') || (value.charAt(value.length - 1) === ' '))
            throw Error.argument('value', Sys.Res.invalidId);
        if (typeof(this._element[value]) !== 'undefined')
            throw Error.invalidOperation(String.format(Sys.Res.behaviorDuplicateName, value));
        if (this.get_isInitialized()) throw Error.invalidOperation(Sys.Res.cantSetNameAfterInit);
        this._name = value;
    },
    initialize: function() {
        Sys.UI.Behavior.callBaseMethod(this, 'initialize');
        var name = this.get_name();
        if (name) this._element[name] = this;
    },
    dispose: function() {
        Sys.UI.Behavior.callBaseMethod(this, 'dispose');
        var e = this._element;
        if (e) {
            var name = this.get_name();
            if (name) {
                e[name] = null;
            }
            var behaviors = e._behaviors;
            Array.remove(behaviors, this);
            if (behaviors.length === 0) {
                e._behaviors = null;
            }
            delete this._element;
        }
    }
}
Sys.UI.Behavior.registerClass('Sys.UI.Behavior', Sys.Component);

Sys.UI.Behavior.getBehaviorByName = function(element, name) {
    /// <summary>Gets a behavior with the specified name from the dom element.</summary>
    /// <param name="element" domElement="true">The DOM element to inspect.</param>
    /// <param name="name" type="String">The name of the behavior to look for.</param>
    /// <returns type="Sys.UI.Behavior" mayBeNull="true">
    ///   The behaviors or null if it was not found.
    /// </returns>
    var b = element[name];
    return (b && Sys.UI.Behavior.isInstanceOfType(b)) ? b : null;
}

Sys.UI.Behavior.getBehaviors = function(element) {
    /// <summary>Gets a collection containing the behaviors associated with an element.</summary>
    /// <param name="element" domElement="true">The DOM element.</param>
    /// <returns type="Array" elementType="Sys.UI.Behavior">
    ///   An array containing the behaviors associated with the DOM element.
    /// </returns>
    if (!element._behaviors) return [];
    return Array.clone(element._behaviors);
}

Sys.UI.Behavior.getBehaviorsByType = function(element, type) {
    /// <summary>Gets an array of behaviors with the specified type from the dom element.</summary>
    /// <param name="element" domElement="true">The DOM element to inspect.</param>
    /// <param name="type" type="Type">The type of behavior to look for.</param>
    /// <returns type="Array" elementType="Sys.UI.Behavior">
    ///   An array containing the behaviors of the specified type found on the element.
    ///   The array is empty if no behavior of this type was found.
    /// </returns>
    var behaviors = element._behaviors;
    var results = [];
    if (behaviors) {
        for (var i = 0, l = behaviors.length; i < l; i++) {
            if (type.isInstanceOfType(behaviors[i])) {
                results[results.length] = behaviors[i];
            }
        }
    }
    return results;
}
 
Sys.UI.VisibilityMode = function() {
    /// <summary>
    ///   Describes how a DOM element should disappear when its visible property is set to false.
    /// </summary>
    /// <field name="hide" type="Number" integer="true" static="true">
    ///   The element disappears but its space remains
    /// </field>
    /// <field name="collapse" type="Number" integer="true" static="true">
    ///   The element disappears and the space it occupied is collapsed.
    /// </field>
    throw Error.notImplemented();
}
Sys.UI.VisibilityMode.prototype = {
    hide: 0,
    collapse: 1
}
Sys.UI.VisibilityMode.registerEnum("Sys.UI.VisibilityMode");

 
Sys.UI.Control = function(element) {
    /// <param name="element" domElement="true">The DOM element the behavior is associated with.</param>
    if (element.control !== null && typeof(element.control) !== 'undefined') throw Error.invalidOperation(Sys.Res.controlAlreadyDefined);
    Sys.UI.Control.initializeBase(this);

    this._element = element;
    element.control = this;
    // Add support for WAI-ARIA role property.
    var role = this.get_role();
    if (role) {
        element.setAttribute("role", role);
    }
}
Sys.UI.Control.prototype = {
    _parent: null,
    _visibilityMode: Sys.UI.VisibilityMode.hide,

    get_element: function() {
        /// <value domElement="true">The DOM element this behavior is associated with</value>
        return this._element;
    },
    get_id: function() {
        /// <value type="String"/>
        if (!this._element) return '';
        return this._element.id;
    },
    set_id: function(value) {
        throw Error.invalidOperation(Sys.Res.cantSetId);
    },
    get_parent: function() {
        /// <summary>
        ///   Returns the parent control for this control.
        ///   If it has never been set, it looks up the DOM to find the first parent element
        ///   that has a control associated with it.
        /// </summary>
        /// <value type="Sys.UI.Control"/>
        if (this._parent) return this._parent;
        if (!this._element) return null;
        
        var parentElement = this._element.parentNode;
        while (parentElement) {
            if (parentElement.control) {
                return parentElement.control;
            }
            parentElement = parentElement.parentNode;
        }
        return null;
    },
    set_parent: function(value) {
        if (!this._element) throw Error.invalidOperation(Sys.Res.cantBeCalledAfterDispose);
        var parents = [this];
        var current = value;
        while (current) {
            if (Array.contains(parents, current)) throw Error.invalidOperation(Sys.Res.circularParentChain);
            parents[parents.length] = current;
            current = current.get_parent();
        }
        this._parent = value;
    },
    get_role: function() {
        /// <value type="String"></value>
        return null;
    },
    get_visibilityMode: function() {
        /// <value type="Sys.UI.VisibilityMode"/>
        if (!this._element) throw Error.invalidOperation(Sys.Res.cantBeCalledAfterDispose);
        return Sys.UI.DomElement.getVisibilityMode(this._element);
    },
    set_visibilityMode: function(value) {
        if (!this._element) throw Error.invalidOperation(Sys.Res.cantBeCalledAfterDispose);
        Sys.UI.DomElement.setVisibilityMode(this._element, value);
    },
    get_visible: function() {
        /// <value type="Boolean"/>
        if (!this._element) throw Error.invalidOperation(Sys.Res.cantBeCalledAfterDispose);
        return Sys.UI.DomElement.getVisible(this._element);
    },
    set_visible: function(value) {
        if (!this._element) throw Error.invalidOperation(Sys.Res.cantBeCalledAfterDispose);
        Sys.UI.DomElement.setVisible(this._element, value)
    },
    addCssClass: function(className) {
        /// <summary>Adds a CSS class to the control if it doesn't already have it.</summary>
        /// <param name="className" type="String">The name of the CSS class to add.</param>
        if (!this._element) throw Error.invalidOperation(Sys.Res.cantBeCalledAfterDispose);
        Sys.UI.DomElement.addCssClass(this._element, className);
    },
    dispose: function() {
        Sys.UI.Control.callBaseMethod(this, 'dispose');
        if (this._element) {
            this._element.control = null;
            delete this._element;
        }
        if (this._parent) delete this._parent;
    },
    onBubbleEvent: function(source, args) {
        /// <param name="source">The object that triggered the event.</param>
        /// <param name="args" type="Sys.EventArgs">The event arguments.</param>
        /// <returns type="Boolean">
        ///  False, because the event was not handled and should bubble up further.
        ///  Derived classes should override that and return true whenever they handle the event to
        ///  prevent it from bubbling up.
        /// </returns>
        return false;
    },
    raiseBubbleEvent: function(source, args) {
        /// <param name="source">The object that triggered the event.</param>
        /// <param name="args" type="Sys.EventArgs">The event arguments.</param>
        this._raiseBubbleEvent(source, args);
    },
    _raiseBubbleEvent: function(source, args) {
        var currentTarget = this.get_parent();
        while (currentTarget) {
            if (currentTarget.onBubbleEvent(source, args)) {
                return;
            }
            currentTarget = currentTarget.get_parent();
        }
    },
    removeCssClass: function(className) {
        /// <summary>Removes a CSS class from the control.</summary>
        /// <param name="className" type="String">The name of the CSS class to remove.</param>
        if (!this._element) throw Error.invalidOperation(Sys.Res.cantBeCalledAfterDispose);
        Sys.UI.DomElement.removeCssClass(this._element, className);
    },
    toggleCssClass: function(className) {
        /// <summary>Toggles a CSS class on and off on the control.</summary>
        /// <param name="className" type="String">The name of the CSS class to toggle.</param>
        if (!this._element) throw Error.invalidOperation(Sys.Res.cantBeCalledAfterDispose);
        Sys.UI.DomElement.toggleCssClass(this._element, className);
    }
}
Sys.UI.Control.registerClass('Sys.UI.Control', Sys.Component);

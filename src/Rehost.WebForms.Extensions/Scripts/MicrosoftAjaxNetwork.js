//!-----------------------------------------------------------------------
//! Copyright (C) Microsoft Corporation. All rights reserved.
//!-----------------------------------------------------------------------
//! MicrosoftAjaxNetwork.js
//! Microsoft AJAX Framework Networking.

//!/ <reference name="MicrosoftAjaxSerialization.js" />

Type._registerScript("MicrosoftAjaxNetwork.js", ["MicrosoftAjaxSerialization.js"]);

 
if (!window.XMLHttpRequest) {
    window.XMLHttpRequest = function() {
        // DevDiv Bugs 150054: Msxml2.XMLHTTP (version independent ProgID) required for mobile IE
        var progIDs = [ 'Msxml2.XMLHTTP.3.0', 'Msxml2.XMLHTTP' ];
        for (var i = 0, l = progIDs.length; i < l; i++) {
            try {
                return new ActiveXObject(progIDs[i]);
            }
            catch (ex) {
            }
        }
        return null;
    }
}

Type.registerNamespace('Sys.Net');

 
Sys.Net.WebRequestExecutor = function() {
    /// <summary>Base class for WebRequestExecutors which handle the actual execution of a WebRequest</summary>
    this._webRequest = null;
    this._resultObject = null;
}

Sys.Net.WebRequestExecutor.prototype = {
    get_webRequest: function() {
        /// <summary>Gets the webRequest property.</summary>
        /// <value type="Sys.Net.WebRequest"/>
        return this._webRequest;
    },

    _set_webRequest: function(value) {

        this._webRequest = value;
    },

    // properties
    get_started: function() {
        /// <summary>Returns whether the executor has started.</summary>
        /// <value type="Boolean"/>
        throw Error.notImplemented();
    },

    get_responseAvailable: function() {
        /// <summary>Returns whether the executor has successfully completed.</summary>
        /// <value type="Boolean"/>
        throw Error.notImplemented();
    },

    get_timedOut: function() {
        /// <summary>Returns whether the executor has timed out.</summary>
        /// <value type="Boolean"/>
        throw Error.notImplemented();
    },
    get_aborted: function() {
        /// <summary>Returns whether the executor has aborted.</summary>
        /// <value type="Boolean"/>
        throw Error.notImplemented();
    },
    get_responseData: function() {
        /// <summary>Returns the response data.</summary>
        /// <value type="String"/>
        throw Error.notImplemented();
    },
    get_statusCode: function() {
        /// <summary>Returns the status code for the response.</summary>
        /// <value type="Number"/>
        throw Error.notImplemented();
    },
    get_statusText: function() {
        /// <summary>Returns the status text for the response.</summary>
        /// <value type="String"/>
        throw Error.notImplemented();
    },
    get_xml: function() {
        /// <summary>Returns the response in xml format.</summary>
        /// <value/>
        throw Error.notImplemented();
    },
    get_object: function() {
        /// <summary>Returns the JSON evaled object of the response.</summary>
        /// <value>The JSON eval'd response.</value>
        if (!this._resultObject) {
            this._resultObject = Sys.Serialization.JavaScriptSerializer.deserialize(this.get_responseData());
        }
        return this._resultObject;
    },

    // methods
    executeRequest: function() {
        /// <summary>Begins execution of the request.</summary>
        throw Error.notImplemented();
    },
    abort: function() {
        /// <summary>Aborts the request.</summary>
        throw Error.notImplemented();
    },
    getResponseHeader: function(header) {
        /// <summary>Returns a response header.</summary>
        /// <param name="header" type="String">The requested header.</param>
        throw Error.notImplemented();
    },
    getAllResponseHeaders: function() {
        /// <summary>Returns all the responses header.</summary>
        throw Error.notImplemented();
    }
}
Sys.Net.WebRequestExecutor.registerClass('Sys.Net.WebRequestExecutor');
 
Sys.Net.XMLDOM = function(markup) {
    /// <summary>Creates an XML document from an XML string.</summary>
    /// <param name="markup" type="String">The XML string to parse.</param>
    if (!window.DOMParser) {
        // DevDiv Bugs 150054: Msxml2.DOMDocument (version independent ProgID) required for mobile IE
        var progIDs = [ 'Msxml2.DOMDocument.3.0', 'Msxml2.DOMDocument' ];
        for (var i = 0, l = progIDs.length; i < l; i++) {
            try {
                var xmlDOM = new ActiveXObject(progIDs[i]);
                xmlDOM.async = false;
                xmlDOM.loadXML(markup);
                xmlDOM.setProperty('SelectionLanguage', 'XPath');
                return xmlDOM;
            }
            catch (ex) {
            }
        }
    }
    else {
        // Mozilla browsers have a DOMParser
        try {
            var domParser = new window.DOMParser();
            return domParser.parseFromString(markup, 'text/xml');
        }
        catch (ex) {
        }
    }
    return null;
}

Sys.Net.XMLHttpExecutor = function() {
    /// <summary>XMLHttpExecutor</summary>

    Sys.Net.XMLHttpExecutor.initializeBase(this);

    var _this = this;
    this._xmlHttpRequest = null;
    this._webRequest = null;
    this._responseAvailable = false;
    this._timedOut = false;
    this._timer = null;
    this._aborted = false;
    this._started = false;

    // Parentheses added around closure methods to work around a preprocessor bug
    // (the preprocessor loses context in that situation and uses the last closure as the name of the current
    // function even when its end has been reached and another function has started.)
    // DevDiv 169493
    this._onReadyStateChange = (function () {
        /*
            readyState values:
            0 = uninitialized
            1 = loading
            2 = loaded
            3 = interactive
            4 = complete
        */
        if (_this._xmlHttpRequest.readyState === 4 /*complete*/) {
            // DevDiv 58581:
            // When a request is pending when the page is closed (navigated away, postback, etc)
            // in FF and Safari, the request is aborted just as if abort() was called on the 
            // xmlhttprequest object.
            // However, even aborted requests have a readyState of 4, which we treat as successful.
            // This happened for example if a regular postback occurred during a partial update request.
            // In FF if you access the 'status' field on an aborted request, an error is thrown,
            // so the error console displayed an error when this happened.
            // On Safari it isn't an error, but status is undefined. That caused PRM to get the completed
            // event, and since the status is not 200, it raises an error.
            // IE and Opera ignore pending requests, or their readyState isn't 4.
            // Devdiv 983362:
            // Many webkit browsers now send a status code 0 when the request has been aborted. 
            try {
                if (typeof(_this._xmlHttpRequest.status) === "undefined" || _this._xmlHttpRequest.status === 0) {
                    // its an aborted request in webkit browsers(e.g. chrome,FF), ignore it
                    return;
                }
            }
            catch(ex) {
                // its an aborted request in Firefox, ignore it
                return;
            }
            
            _this._clearTimer();
            _this._responseAvailable = true;
            try {
                // DevDiv Bugs 148214: Use try/finally to ensure cleanup occurs even
                // if the completed callback causes an exception (such as with async
                // postbacks where a server-side exception occurred)
                _this._webRequest.completed(Sys.EventArgs.Empty);
            }
            finally {
                if (_this._xmlHttpRequest != null) {
                    _this._xmlHttpRequest.onreadystatechange = Function.emptyMethod;
                    _this._xmlHttpRequest = null;
                }
            }
        }
    });

    this._clearTimer = (function() {
        if (_this._timer != null) {
            window.clearTimeout(_this._timer);
            _this._timer = null;
        }
    });

    this._onTimeout = (function() {
        if (!_this._responseAvailable) {
            _this._clearTimer();
            _this._timedOut = true;
            _this._xmlHttpRequest.onreadystatechange = Function.emptyMethod;
            _this._xmlHttpRequest.abort();
            _this._webRequest.completed(Sys.EventArgs.Empty);
            _this._xmlHttpRequest = null;
        }
    });

}

Sys.Net.XMLHttpExecutor.prototype = {

    get_timedOut: function() {
        /// <summary>Returns whether the executor has timed out.</summary>
        /// <value type="Boolean">True if the executor has timed out.</value>
        return this._timedOut;
    },

    get_started: function() {
        /// <summary>Returns whether the executor has started.</summary>
        /// <value type="Boolean">True if the executor has started.</value>
        return this._started;
    },

    get_responseAvailable: function() {
        /// <summary>Returns whether the executor has successfully completed.</summary>
        /// <value type="Boolean">True if a response is available.</value>
        return this._responseAvailable;
    },

    get_aborted: function() {
        /// <summary>Returns whether the executor has been aborted.</summary>
        /// <value type="Boolean">True if the executor has been aborted.</value>
        return this._aborted;
    },

    executeRequest: function() {
        /// <summary>Invokes the request.</summary>
        this._webRequest = this.get_webRequest();


        var body = this._webRequest.get_body();
        var headers = this._webRequest.get_headers();
        this._xmlHttpRequest = new XMLHttpRequest();
        this._xmlHttpRequest.onreadystatechange = this._onReadyStateChange;
        var verb = this._webRequest.get_httpVerb();
        this._xmlHttpRequest.open(verb, this._webRequest.getResolvedUrl(), true /*async*/);
        this._xmlHttpRequest.setRequestHeader("X-Requested-With", "XMLHttpRequest");
        if (headers) {
            for (var header in headers) {
                var val = headers[header];
                if (typeof(val) !== "function")
                    this._xmlHttpRequest.setRequestHeader(header, val);
            }
        }

        if (verb.toLowerCase() === "post") {
            // If it's a POST but no Content-Type was specified, default to application/x-www-form-urlencoded; charset=utf-8
            if ((headers === null) || !headers['Content-Type']) {
                // DevDiv 109456: Include charset=utf-8. Javascript encoding methods always use utf-8, server may be set to assume other encoding.
                this._xmlHttpRequest.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded; charset=utf-8');
            }

            // DevDiv 15893: If POST with no body, default to ""(FireFox needs this)
            if (!body) {
                body = "";
            }
        }

        var timeout = this._webRequest.get_timeout();
        if (timeout > 0) {
            this._timer = window.setTimeout(Function.createDelegate(this, this._onTimeout), timeout);
        }
        this._xmlHttpRequest.send(body);
        this._started = true;
    },

    getResponseHeader: function(header) {
        /// <summary>Returns a response header.</summary>
        /// <param name="header" type="String">The requested header.</param>
        /// <returns type="String">The value of the header.</returns>

        var result;
        try {
            result = this._xmlHttpRequest.getResponseHeader(header);
        } catch (e) {
        }
        if (!result) result = "";
        return result;
    },

    getAllResponseHeaders: function() {
        /// <summary>Returns all the responses header.</summary>
        /// <returns type="String">The text of all the headers.</returns>

        return this._xmlHttpRequest.getAllResponseHeaders();
    },

    get_responseData: function() {
        /// <summary>Returns the response data.</summary>
        /// <value type="String">The text of the response.</value>

        return this._xmlHttpRequest.responseText;
    },

    get_statusCode: function() {
        /// <summary>Returns the status code for the response.</summary>
        /// <value type="Number">The status code of the response.</value>
        var result = 0;
        try {
            result = this._xmlHttpRequest.status;
        }
        catch(ex) {
        }
        return result;
    },

    get_statusText: function() {
        /// <summary>Returns the status text for the response.</summary>
        /// <value type="String">The status text of the repsonse.</value>

        return this._xmlHttpRequest.statusText;
    },

    get_xml: function() {
        /// <summary>Returns the response in xml format.</summary>
        /// <value>The response in xml format.</value>

        var xml = this._xmlHttpRequest.responseXML;
        if (!xml || !xml.documentElement) {

            // This happens if the server doesn't set the content type to text/xml.
            xml = Sys.Net.XMLDOM(this._xmlHttpRequest.responseText);

            // If we still couldn't get an XML DOM, the data is probably not XML
            if (!xml || !xml.documentElement)
                return null;
        }
        // REVIEW: todo this used to use Sys.Runtime get_hostType
        else if (navigator.userAgent.indexOf('MSIE') !== -1 && typeof(xml.setProperty) != 'undefined') {
            xml.setProperty('SelectionLanguage', 'XPath');
        }

        // For Firefox parser errors have document elements of parser error
        if (xml.documentElement.namespaceURI === "http://www.mozilla.org/newlayout/xml/parsererror.xml" &&
            xml.documentElement.tagName === "parsererror") {
            return null;
        }
        
        // For Safari, parser errors are always the first child of the root
        if (xml.documentElement.firstChild && xml.documentElement.firstChild.tagName === "parsererror") {
            return null;
        }
        
        return xml;
    },

    abort: function() {
        /// <summary>Aborts the request.</summary>

        // aborts are no ops if we are done, timedout, or aborted already
        if (this._aborted || this._responseAvailable || this._timedOut)
            return;

        this._aborted = true;

        this._clearTimer();

        if (this._xmlHttpRequest && !this._responseAvailable) {

            // Remove the onreadystatechange first otherwise abort would trigger readyState to become 4
            this._xmlHttpRequest.onreadystatechange = Function.emptyMethod;
            this._xmlHttpRequest.abort();
            
            this._xmlHttpRequest = null;            

            // DevDiv 59229: Call completed on the request instead of raising the event directly
            this._webRequest.completed(Sys.EventArgs.Empty);
        }
    }
}
Sys.Net.XMLHttpExecutor.registerClass('Sys.Net.XMLHttpExecutor', Sys.Net.WebRequestExecutor);
 
Sys.Net._WebRequestManager = function() {
    /// <summary locid="P:J#Sys.Net.WebRequestManager.#ctor"/>
    this._defaultTimeout = 0;
    this._defaultExecutorType = "Sys.Net.XMLHttpExecutor";
}

Sys.Net._WebRequestManager.prototype = {
    add_invokingRequest: function(handler) {
        /// <summary locid="E:J#Sys.Net.WebRequestManager.invokingRequest"/>
        this._get_eventHandlerList().addHandler("invokingRequest", handler);
    },
    remove_invokingRequest: function(handler) {
        this._get_eventHandlerList().removeHandler("invokingRequest", handler);
    },

    add_completedRequest: function(handler) {
        /// <summary locid="E:J#Sys.Net.WebRequestManager.completedRequest"/>
        this._get_eventHandlerList().addHandler("completedRequest", handler);
    },
    remove_completedRequest: function(handler) {
        this._get_eventHandlerList().removeHandler("completedRequest", handler);
    },

    _get_eventHandlerList: function() {
        if (!this._events) {
            this._events = new Sys.EventHandlerList();
        }
        return this._events;
    },

    get_defaultTimeout: function() {
        /// <value type="Number" locid="P:J#Sys.Net.WebRequestManager.defaultTimeout">The default timeout for requests in milliseconds.</value>
        return this._defaultTimeout;
    },
    set_defaultTimeout: function(value) {

        this._defaultTimeout = value;
    },

    get_defaultExecutorType: function() {
        /// <value type="String" locid="P:J#Sys.Net.WebRequestManager.defaultExecutorType">The default executor type name.</value>
        return this._defaultExecutorType;
    },
    set_defaultExecutorType: function(value) {
        this._defaultExecutorType = value;
    },

    executeRequest: function(webRequest) {
        /// <summary locid="M:J#Sys.Net.WebRequestManager.executeRequest">Executes a request.</summary>
        /// <param name="webRequest" type="Sys.Net.WebRequest">The webRequest to execute.</param>
        var executor = webRequest.get_executor();
        // if the request didn't set an executor, use the request manager default executor
        if (!executor) {
            // TODO: Optimize this by caching the type

            var failed = false;
            try {
                var executorType = eval(this._defaultExecutorType);
                executor = new executorType();
            } catch (e) {
                failed = true;
            }


            webRequest.set_executor(executor);
        }

        // skip the request if it has been aborted;
        if (executor.get_aborted()) {
            return;
        }

        var evArgs = new Sys.Net.NetworkRequestEventArgs(webRequest);
        var handler = this._get_eventHandlerList().getHandler("invokingRequest");
        if (handler) {
            handler(this, evArgs);
        }

        if (!evArgs.get_cancel()) {
            executor.executeRequest();
        }
    }
}

Sys.Net._WebRequestManager.registerClass('Sys.Net._WebRequestManager');

// Create a single instance of the class
Sys.Net.WebRequestManager = new Sys.Net._WebRequestManager();
 
Sys.Net.NetworkRequestEventArgs = function(webRequest) {
    /// <summary>This class is raised by the WebRequestManager when a WebRequest is about to be executed.</summary>
    /// <param name="webRequest" type="Sys.Net.WebRequest">The identifier for the event.</param>
    Sys.Net.NetworkRequestEventArgs.initializeBase(this);
    this._webRequest = webRequest;
}

Sys.Net.NetworkRequestEventArgs.prototype = {
    get_webRequest: function() {
        /// <summary>Returns the webRequest.</summary>
        /// <value type="Sys.Net.WebRequest">The request about to be executed.</value>
        return this._webRequest;
    }
}

Sys.Net.NetworkRequestEventArgs.registerClass('Sys.Net.NetworkRequestEventArgs', Sys.CancelEventArgs);
 
Sys.Net.WebRequest = function() {
    /// <summary>WebRequest class</summary>
    this._url = "";
    this._headers = { };
    this._body = null;
    this._userContext = null;
    this._httpVerb = null;
    this._executor = null;
    this._invokeCalled = false;
    this._timeout = 0;
}

// Properties about the request data
Sys.Net.WebRequest.prototype = {
    add_completed: function(handler) {
        this._get_eventHandlerList().addHandler("completed", handler);
    },
    remove_completed: function(handler) {
        this._get_eventHandlerList().removeHandler("completed", handler);
    },

    completed: function(eventArgs) {
        /// <summary>The completed method should be called when the request is completed.</summary>
        /// <param name="eventArgs" type="Sys.EventArgs">The event args to raise the event with.</param>
        var handler = Sys.Net.WebRequestManager._get_eventHandlerList().getHandler("completedRequest");
        if (handler) {
            handler(this._executor, eventArgs);
        }

        handler = this._get_eventHandlerList().getHandler("completed");
        if (handler) {
            handler(this._executor, eventArgs);
        }
    },

    _get_eventHandlerList: function() {
        if (!this._events) {
            this._events = new Sys.EventHandlerList();
        }
        return this._events;
    },

    get_url: function() {
        /// <summary>The get_url method returns the url property of the request.</summary>
        /// <value type="String">The url.</value>
        return this._url;
    },
    set_url: function(value) {
        this._url = value;
    },

    get_headers: function() {
        /// <summary>The get_headers method returns the headers dictionary for the request, set headers by adding to this dictionary.</summary>
        /// <value>The headers dictionary for the request.</value>
        return this._headers;
    },

    get_httpVerb: function() {
        /// <summary>The get_httpVerb method gets the httpVerb property of the request.</summary>
        /// <value type="String">The httpVerb for the request.</value>
        // Default is GET if no body, and POST otherwise
        if (this._httpVerb === null) {
            if (this._body === null) {
                return "GET";
            }
            return "POST";
        }
        return this._httpVerb;
    },
    set_httpVerb: function(value) {

        this._httpVerb = value;
    },

    get_body: function() {
        /// <summary>The get_body method gets the body property of the request.</summary>
        /// <value mayBeNull="true">The body of the request.</value>
        return this._body;
    },
    set_body: function(value) {
        this._body = value;
    },

    get_userContext: function() {
        /// <summary>The get_userContext method gets the userContext property of the request.</summary>
        /// <value mayBeNull="true">The userContext of the request.</value>
        return this._userContext;
    },
    set_userContext: function(value) {
        this._userContext = value;
    },

    get_executor: function() {
        /// <summary>The get_executor method gets the executor property of the request.</summary>
        /// <value type="Sys.Net.WebRequestExecutor">The executor for the request.</value>
        return this._executor;
    },
    set_executor: function(value) {

        this._executor = value;
        this._executor._set_webRequest(this);
    },

    get_timeout: function() {
        /// <summary>The get_timeout method gets the timeout property of the request.</summary>
        /// <value type="Number">The timeout in milliseconds for the request.</value>
        if (this._timeout === 0) {
            return Sys.Net.WebRequestManager.get_defaultTimeout();
        }
        return this._timeout;
    },
    set_timeout: function(value) {

        this._timeout = value;
    },

    getResolvedUrl: function() {
        /// <summary>The getResolvedUrl method returns the url resolved against the base url of the page if set.</summary>
        /// <returns type="String">The resolved url for the request.</returns>
        return Sys.Net.WebRequest._resolveUrl(this._url);
    },

    invoke: function() {
        /// <summary>Invokes the request</summary>

        Sys.Net.WebRequestManager.executeRequest(this);
        this._invokeCalled = true;
    }
}

// Given a url and an optional base url, return an absolute url combining the url and base url
Sys.Net.WebRequest._resolveUrl = function(url, baseUrl) {
    // If the url contains a host, we are done
    if (url && url.indexOf('://') !== -1) {
        return url;
    }

    // If a base url isn't passed in, we use either the base element if specified or the URL from the browser
    if (!baseUrl || baseUrl.length === 0) {
        var baseElement = document.getElementsByTagName('base')[0];
        if (baseElement && baseElement.href && baseElement.href.length > 0) {
            baseUrl = baseElement.href;
        }
        else {
            baseUrl = document.URL;
        }
    }

    // strip off any querystrings
    var qsStart = baseUrl.indexOf('?');
    if (qsStart !== -1) {
        baseUrl = baseUrl.substr(0, qsStart);
    }
    // and hashes
    qsStart = baseUrl.indexOf('#');
    if (qsStart !== -1) {
        baseUrl = baseUrl.substr(0, qsStart);
    }
    baseUrl = baseUrl.substr(0, baseUrl.lastIndexOf('/') + 1);

    // If a url wasn't specified, we just use the base
    if (!url || url.length === 0) {
        return baseUrl;
    }

    // For absolute path url, we need to rebase it against the base url, stripping off everything after the http://host
    if (url.charAt(0) === '/') {
        var slashslash = baseUrl.indexOf('://');

        var nextSlash = baseUrl.indexOf('/', slashslash + 3);

        return baseUrl.substr(0, nextSlash) + url;
    }
    // Otherwise for relative urls we just combine with the base url stripping off the last path component (filename typically)
    // Note the app path always contains a trailing slash so when resolving app paths, we never strip off anything important
    else {
        var lastSlash = baseUrl.lastIndexOf('/');

        return baseUrl.substr(0, lastSlash+1) + url;
    }
}

Sys.Net.WebRequest._createQueryString = function(queryString, encodeMethod, addParams) {
    // By default, use URI encoding
    encodeMethod = encodeMethod || encodeURIComponent;
    var i = 0, obj, val, arg, sb = new Sys.StringBuilder();
    if (queryString) {
        for (arg in queryString) {
            obj = queryString[arg];
            if (typeof(obj) === "function") continue;
            val = Sys.Serialization.JavaScriptSerializer.serialize(obj);
            if (i++) {
                sb.append('&');
            }
            sb.append(arg);
            sb.append('=');
            sb.append(encodeMethod(val));
        }
    }
    if (addParams) {
        if (i) {
            sb.append('&');
        }
        sb.append(addParams);
    }
    return sb.toString();
}

Sys.Net.WebRequest._createUrl = function(url, queryString, addParams) {
    if (!queryString && !addParams) {
        return url;
    }
    var qs = Sys.Net.WebRequest._createQueryString(queryString, null, addParams);
    return qs.length
        ? url + ((url && url.indexOf('?') >= 0) ? "&" : "?") + qs
        : url;
}

Sys.Net.WebRequest.registerClass('Sys.Net.WebRequest');
// ScriptLoaderTask required by both WebForms and WebServices (for jsonp support)
// MSAjaxNetwork is a common dependency between them.
 
// ScriptLoaderTask loads a single script by injecting a dynamic script tag into the DOM.
// It calls the completed callback when the script element's load/readystatechange or error event occus.
// The task should be disposed of after use, as it contains references to the script element.

Sys._ScriptLoaderTask = function(scriptElement, completedCallback) {
    /// <param name="scriptElement" domElement="true">The script element to add to the DOM.</param>
    /// <param name="completedCallback" type="Function">Callback to call when the script has loaded or failed to load.</param>
    this._scriptElement = scriptElement;
    this._completedCallback = completedCallback;
}
Sys._ScriptLoaderTask.prototype = {
    get_scriptElement: function() {
        /// <value domElement="true">The script element.</value>
        return this._scriptElement;
    },
    
    dispose: function() {
        // disposes of the task by removing the load handlers, aborting the window timeout, and releasing the ref to the dom element
        if(this._disposed) {
            // already disposed
            return;
        }
        this._disposed = true;
        this._removeScriptElementHandlers();
        // remove script element from DOM
        Sys._ScriptLoaderTask._clearScript(this._scriptElement);
        this._scriptElement = null;
    },
        
    execute: function() {
        /// <summary>Begins loading the given script element.</summary>
        if (this._ensureReadyStateLoaded()) {
            this._executeInternal();
        }
    },

    _executeInternal: function() {
        this._addScriptElementHandlers();
        // DevDiv Bugs 146697: use lowercase names on getElementsByTagName to work with xhtml content type
        document.getElementsByTagName('head')[0].appendChild(this._scriptElement);
    },

    _ensureReadyStateLoaded: function() {
        // If using IE8 or earlier, we want to do a two-stage script load.  The first stage is
        // to set the 'src' attribute on the script element and then wait for IE to finish
        // downloading it before adding it to the DOM.
        if (this._useReadyState() && this._scriptElement.readyState !== 'loaded' && this._scriptElement.readyState !== 'complete') {
            this._scriptDownloadDelegate = Function.createDelegate(this, this._executeInternal);
            $addHandler(this._scriptElement, 'readystatechange', this._scriptDownloadDelegate);
            return false;
        }

        return true;
    },
       
    _addScriptElementHandlers: function() {
        // adds the necessary event handlers to the script node to know when it is finished loading

        // First, remove the download handler if we used one
        if (this._scriptDownloadDelegate) {
            $removeHandler(this._scriptElement, 'readystatechange', this._scriptDownloadDelegate);
            this._scriptDownloadDelegate = null;
        }

        // Then add a handler to fire when the script is loaded in the DOM
        this._scriptLoadDelegate = Function.createDelegate(this, this._scriptLoadHandler);
        if (this._useReadyState()) {
            $addHandler(this._scriptElement, 'readystatechange', this._scriptLoadDelegate);
        } else {
            $addHandler(this._scriptElement, 'load', this._scriptLoadDelegate);
        }

        // FF throws onerror if the script doesn't exist, not loaded.
        // DevDev Bugs 86101 -- cant use DomElement.addHandler because it throws for 'error' events.
        if (this._scriptElement.addEventListener) {
            this._scriptErrorDelegate = Function.createDelegate(this, this._scriptErrorHandler);
            this._scriptElement.addEventListener('error', this._scriptErrorDelegate, false);
        }
    },    
    
    _removeScriptElementHandlers: function() {
        // removes the load and error handlers from the script element
        if(this._scriptLoadDelegate) {
            var scriptElement = this.get_scriptElement();

            // First, remove the download handler if we used one
            if (this._scriptDownloadDelegate) {
                $removeHandler(this._scriptElement, 'readystatechange', this._scriptDownloadDelegate);
                this._scriptDownloadDelegate = null;
            }

            if (this._useReadyState() && this._scriptLoadDelegate) {
                $removeHandler(scriptElement, 'readystatechange', this._scriptLoadDelegate);
            }
            else {
                $removeHandler(scriptElement, 'load', this._scriptLoadDelegate);
            }
            if (this._scriptErrorDelegate) {
                // DevDev Bugs 86101 -- cant use DomElement.removeHandler because addHandler throws for 'error' events.
                this._scriptElement.removeEventListener('error', this._scriptErrorDelegate, false);
                this._scriptErrorDelegate = null;
            }
            this._scriptLoadDelegate = null;
        }
    },    

    _scriptErrorHandler: function() {
        // handler for when the script element's error event occurs
        if(this._disposed) {
            return;
        }
        
        // false == did not load successfully (404, etc)
        this._completedCallback(this.get_scriptElement(), false);
    },
           
    _scriptLoadHandler: function() {
        // handler for when the script element's load/readystatechange event occurs
        if(this._disposed) {
            return;
        }

        var scriptElement = this.get_scriptElement();
        if (this._useReadyState() && scriptElement.readyState !== 'complete') {
            return;
        }

        this._completedCallback(scriptElement, true);
    },

    _useReadyState: function() {
        return (Sys.Browser.agent === Sys.Browser.InternetExplorer && (Sys.Browser.version < 9 || ((document.documentMode || 0) < 9)));
    }
}
Sys._ScriptLoaderTask.registerClass("Sys._ScriptLoaderTask", null, Sys.IDisposable);

Sys._ScriptLoaderTask._clearScript = function(scriptElement) {
    if (!Sys.Debug.isDebug && scriptElement.parentNode) {
        // In release mode we clear out the script elements that we add
        // so that they don't clutter up the DOM.
        scriptElement.parentNode.removeChild(scriptElement);
    }
}

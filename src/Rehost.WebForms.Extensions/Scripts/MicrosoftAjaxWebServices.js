//!-----------------------------------------------------------------------
//! Copyright (C) Microsoft Corporation. All rights reserved.
//!-----------------------------------------------------------------------
//! MicrosoftAjaxWebServices.js
//! Microsoft AJAX Framework Web Services.

//!/ <reference name="MicrosoftAjaxNetwork.js" />

Type._registerScript("MicrosoftAjaxWebServices.js", ["MicrosoftAjaxNetwork.js"]);

Type.registerNamespace('Sys.Net');

 
Sys.Net.WebServiceProxy = function() {
}
Sys.Net.WebServiceProxy.prototype = {
    get_timeout: function() {
        /// <summary>Returns the timeout in milliseconds for this service.</summary>
        /// <value type="Number">The timeout in milliseconds for the service.</value>
        return this._timeout || 0;
    },
    set_timeout: function(value) {
        if (value < 0) { throw Error.argumentOutOfRange('value', value, Sys.Res.invalidTimeout); }
        this._timeout = value;
    },
    get_defaultUserContext: function() {
        /// <summary>Returns the default userContext for this service.</summary>
        /// <value mayBeNull="true">The default userContext for this service.</value>
        return (typeof(this._userContext) === "undefined") ? null : this._userContext;
    },
    set_defaultUserContext: function(value) {
        this._userContext = value;
    },
    get_defaultSucceededCallback: function() {
        /// <summary>Returns the default succeededCallback for this service.</summary>
        /// <value type="Function" mayBeNull="true">Returns the default succeededCallback for this service.</value>
        return this._succeeded || null;
    },
    set_defaultSucceededCallback: function(value) {
        this._succeeded = value;
    },
    get_defaultFailedCallback: function() {
        /// <summary>Returns the default failedCallback for this service.</summary>
        /// <value type="Function" mayBeNull="true">Returns the default failedCallback for this service.</value>
        return this._failed || null;
    },
    set_defaultFailedCallback: function(value) {
        this._failed = value;
    },
    get_enableJsonp: function() {
        /// <value type="Boolean">Specifies whether the service supports JSONP for cross domain calling.</value>
        return !!this._jsonp;
    },
    set_enableJsonp: function(value) {
        this._jsonp = value;
    },
    get_path: function() {
        /// <summary>The path to this service.</summary>
        /// <value type="String">The path to this service.</value>
        return this._path || null;
    },
    set_path: function(value) {
        this._path = value;
    },
    get_jsonpCallbackParameter: function() {
        /// <value type="String">Specifies the parameter name that contains the callback function name for a JSONP request.</value>
        return this._callbackParameter || "callback";
    },
    set_jsonpCallbackParameter: function(value) {
        this._callbackParameter = value;
    },
    _invoke: function(servicePath, methodName, useGet, params, onSuccess, onFailure, userContext) {
        /// <param name="servicePath" type="String" >Path to the webservice</param>
        /// <param name="methodName" type="String" >Method to invoke</param>
        /// <param name="useGet" type="Boolean">Controls whether requests use HttpGet</param>
        /// <param name="params">Method args.</param>
        /// <param name="onSuccess" type="Function" mayBeNull="true" optional="true">Success callback</param>
        /// <param name="onFailure" type="Function" mayBeNull="true" optional="true">Failure callback</param>
        /// <param name="userContext" mayBeNull="true" optional="true">Success callback</param>
        /// <returns type="Sys.Net.WebRequest" mayBeNull="true">Returns the request that was sent</returns>

        // Resolve against the defaults callbacks/context
        onSuccess = onSuccess || this.get_defaultSucceededCallback();
        onFailure = onFailure || this.get_defaultFailedCallback();
        if (userContext === null || typeof userContext === 'undefined') userContext = this.get_defaultUserContext();
        return Sys.Net.WebServiceProxy.invoke(servicePath, methodName, useGet, params, onSuccess, onFailure, userContext, this.get_timeout(), this.get_enableJsonp(), this.get_jsonpCallbackParameter());
    }
}
Sys.Net.WebServiceProxy.registerClass('Sys.Net.WebServiceProxy');

Sys.Net.WebServiceProxy.invoke = function(servicePath, methodName, useGet, params, onSuccess, onFailure, userContext, timeout, enableJsonp, jsonpCallbackParameter) {
    /// <param name="servicePath" type="String" >Path to the webservice</param>
    /// <param name="methodName" type="String" mayBeNull="true" optional="true">Method to invoke</param>
    /// <param name="useGet" type="Boolean" optional="true">Controls whether requests use HttpGet</param>
    /// <param name="params" mayBeNull="true" optional="true">Method args.</param>
    /// <param name="onSuccess" type="Function" mayBeNull="true" optional="true">Success callback</param>
    /// <param name="onFailure" type="Function" mayBeNull="true" optional="true">Failure callback</param>
    /// <param name="userContext" mayBeNull="true" optional="true">Success callback</param>
    /// <param name="timeout" type="Number" optional="true">Timeout in milliseconds</param>
    /// <param name="enableJsonp" type="Boolean" optional="true" mayBeNull="true">Whether to use JSONP if the servicePath is for a different domain (default is true).</param>
    /// <param name="jsonpCallbackParameter" type="String" optional="true" mayBeNull="true">The name of the callback parameter for JSONP request (default is callback).</param>
    /// <returns type="Sys.Net.WebRequest" mayBeNull="true">Returns the request that was sent (null for JSONP requests).</returns>
    var schemeHost = (enableJsonp !== false) ? Sys.Net.WebServiceProxy._xdomain.exec(servicePath) : null,
        tempCallback, jsonp = schemeHost && (schemeHost.length === 3) && 
            ((schemeHost[1] !== location.protocol) || (schemeHost[2] !== location.host));
    useGet = jsonp || useGet;
    if (jsonp) {
        jsonpCallbackParameter = jsonpCallbackParameter || "callback";
        tempCallback = "_jsonp" + Sys._jsonp++;
    }
    if (!params) params = {};
    var urlParams = params;
    // If using POST, or we don't have any paramaters, start with a blank dictionary
    if (!useGet || !urlParams) urlParams = {};
    var script, error, timeoutcookie = null, loader, body = null,
        url = Sys.Net.WebRequest._createUrl(methodName
            ? (servicePath+"/"+encodeURIComponent(methodName))
            : servicePath, urlParams, jsonp ? (jsonpCallbackParameter + "=Sys." + tempCallback) : null);
    if (jsonp) {
        script = document.createElement("script");
        script.src = url;
        loader = new Sys._ScriptLoaderTask(script, function(script, loaded) {
            if (!loaded || tempCallback) {
                // the script failed to load, or it loaded but the callback wasn't executed
                jsonpComplete({ Message: String.format(Sys.Res.webServiceFailedNoMsg, methodName) }, -1);
            }
        });
        function jsonpTimeout() {
            if (timeoutcookie === null) return;
            timeoutcookie = null;
            error = new Sys.Net.WebServiceError(true, String.format(Sys.Res.webServiceTimedOut, methodName));
            loader.dispose();
            delete Sys[tempCallback];
            if (onFailure) {
                onFailure(error, userContext, methodName);
            }            
        }
        function jsonpComplete(data, statusCode) {
            if (timeoutcookie !== null) {
                window.clearTimeout(timeoutcookie);
                timeoutcookie = null;
            }
            loader.dispose();
            delete Sys[tempCallback];
            // the script's loaded handler knows the callback occurred if this was set to null.
            // If the loaded callback occurs before this is called, it means the script loaded but
            // did not execute the callback, an error condition.
            tempCallback = null; 
            if ((typeof(statusCode) !== "undefined") && (statusCode !== 200)) {
                if (onFailure) {
                    error = new Sys.Net.WebServiceError(false,
                            data.Message || String.format(Sys.Res.webServiceFailedNoMsg, methodName),
                            data.StackTrace || null,
                            data.ExceptionType || null,
                            data);
                    error._statusCode = statusCode;
                    onFailure(error, userContext, methodName);
                }
            }
            else if (onSuccess) {
                onSuccess(data, userContext, methodName);
            }
        }
        Sys[tempCallback] = jsonpComplete;
        // timeout only in release mode (a timeout would interfere with debugging, etc)
        timeout = timeout || Sys.Net.WebRequestManager.get_defaultTimeout();
        if (timeout > 0) {
            timeoutcookie = window.setTimeout(jsonpTimeout, timeout);
        }
        loader.execute();
        return null;
    }
    var request = new Sys.Net.WebRequest();
    request.set_url(url);
    request.get_headers()['Content-Type'] = 'application/json; charset=utf-8';
    // No body when using GET
    if (!useGet) {
        body = Sys.Serialization.JavaScriptSerializer.serialize(params);
        // If there are no parameters, send an empty body (though it will still be a POST)
        if (body === "{}") body = "";
    }
    // Put together the body as a JSON string
    request.set_body(body);
    request.add_completed(onComplete);
    if (timeout && timeout > 0) request.set_timeout(timeout);
    request.invoke();
    
    function onComplete(response, eventArgs) {
        if (response.get_responseAvailable()) {
            var statusCode = response.get_statusCode();
            var result = null;
           
            try {
                var contentType = response.getResponseHeader("Content-Type");
                if (contentType.startsWith("application/json")) {
                    result = response.get_object();
                }
                else if (contentType.startsWith("text/xml")) {
                    result = response.get_xml();
                }
                // Default to the response text
                else {
                    result = response.get_responseData();
                }
            } catch (ex) {
            }

            var error = response.getResponseHeader("jsonerror");
            var errorObj = (error === "true");
            if (errorObj) {
                if (result) {
                    result = new Sys.Net.WebServiceError(false, result.Message, result.StackTrace, result.ExceptionType, result);
                }
            }
            else if (contentType.startsWith("application/json")) {
                //DevDiv 88409: Change JSON wire format to prevent CSRF attack
                //The return value is wrapped inside an object with , 'd' field set to return value 
                //Dev10 549433: The 'd' wrapper is optional
                result = (!result || (typeof(result.d) === "undefined")) ? result : result.d;
            }
            if (((statusCode < 200) || (statusCode >= 300)) || errorObj) {
                if (onFailure) {
                    if (!result || !errorObj) {
                        result = new Sys.Net.WebServiceError(false /*timedout*/, String.format(Sys.Res.webServiceFailedNoMsg, methodName));
                    }
                    result._statusCode = statusCode;
                    onFailure(result, userContext, methodName);
                }
            }
            else if (onSuccess) {
                onSuccess(result, userContext, methodName);
            }
        }
        else {
            var msg;
            if (response.get_timedOut()) {
                msg = String.format(Sys.Res.webServiceTimedOut, methodName);
            }
            else {
                msg = String.format(Sys.Res.webServiceFailedNoMsg, methodName)
            }
            if (onFailure) {
                onFailure(new Sys.Net.WebServiceError(response.get_timedOut(), msg, "", ""), userContext, methodName);
            }
        }
    }

    return request;
}


// Generate a constructor that knows how to build objects of a particular server type,
// and then initialize it from the fields of an arbitrary object.
Sys.Net.WebServiceProxy._generateTypedConstructor = function(type) {
    return function(properties) {
        // If an object was passed in, copy all its fields
        if (properties) {
            for (var name in properties) {
                this[name] = properties[name];
            }
        }
        this.__type = type;
    }
}

// counter ensures unique callback function names to jsonp services
Sys._jsonp = 0;

// regexp used to a uri scheme and host name. The characters included for the scheme are based on RFC2396 section 3.1.
Sys.Net.WebServiceProxy._xdomain = /^\s*([a-zA-Z0-9\+\-\.]+\:)\/\/([^?#\/]+)/;
 
// Class returned to client if server throws an exception during ProcessRequest
Sys.Net.WebServiceError = function(timedOut, message, stackTrace, exceptionType, errorObject) {
    /// <summary>Represents a webservice error</summary>
    /// <param name="timedOut" type="Boolean">The error status enum.</param>
    /// <param name="message" type="String" mayBeNull="true">The error message.</param>
    /// <param name="stackTrace" type="String" mayBeNull="true" optional="true">The stack trace of the error.</param>
    /// <param name="exceptionType" type="String" mayBeNull="true" optional="true">The server exception type.</param>
    /// <param name="errorObject" type="Object" mayBeNull="true" optional="true">The raw error information.</param>
    this._timedOut = timedOut;
    this._message = message;
    this._stackTrace = stackTrace;
    this._exceptionType = exceptionType;
    this._errorObject = errorObject;
    this._statusCode = -1;
}

Sys.Net.WebServiceError.prototype = {
    get_timedOut: function() {
        /// <summary>True if the webservice failed due to timeout.</summary>
        /// <value type="Boolean">Whether the service failed due to timeout.</value>
        return this._timedOut;
    },

    get_statusCode: function() {
        /// <summary>Http status code of the response if any, defaults to -1 otherwise</summary>
        /// <value type="Number">Int representing the status of the response.</value>
        return this._statusCode;
    },

    get_message: function() {
        /// <summary>Error message</summary>
        /// <value type="String">Error message</value>
        return this._message;
    },

    get_stackTrace: function() {
        /// <summary>Stack trace of the error</summary>
        /// <value type="String">Stack trace of the error.</value>
        return this._stackTrace || "";
    },

    get_exceptionType: function() {
        /// <summary>Exception type of the error</summary>
        /// <value type="String">Exception type of the error.</value>
        return this._exceptionType || "";
    },
    
    get_errorObject: function() {
        /// <value type="Object">The raw error object returned by the service.</value>
        return this._errorObject || null;
    }
}
Sys.Net.WebServiceError.registerClass('Sys.Net.WebServiceError');

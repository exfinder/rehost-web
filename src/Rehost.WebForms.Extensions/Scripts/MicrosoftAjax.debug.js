//!-----------------------------------------------------------------------
//! Copyright (C) Microsoft Corporation. All rights reserved.
//!-----------------------------------------------------------------------
//! MicrosoftAjax.js
//! Microsoft AJAX Framework.

// **********************************************
// ** MicrosoftAjaxCore.jsa
// **********************************************

// JavaScript Extensions and Type System

 
Function.__typeName = 'Function';
Function.__class = true;

Function.createCallback = function(method, context) {
    /// <summary>
    ///     Creates a callback function that retains the parameter initially used during its creation.
    ///     The callback is used without parameter but this will call the actual method with the parameter.
    ///     This is especially useful when setting up a handler for a DOM event that must retain a parameter
    ///     despite the DOM event handler needing to be a function with the event object as the only parameter.
    ///     In this case, the function will be called with the event as the first parameter and the context
    ///     as the second.
    ///     If the callback is called with an arbitrary list of parameters, the context is appended.
    /// </summary>
    /// <param name="method" type="Function">The function for which the callback is created.</param>
    /// <param name="context" mayBeNull="true">The parameter for the function.</param>
    /// <returns type="Function">The callback function.</returns>

    // The method still makes sense for null context, but not if the context is omitted altogether
    // (omitted context makes the callback equivalent to the method itself, with one more level of indirection).

    return function() {
        var l = arguments.length;
        if (l > 0) {
            // arguments is not a real array, need to build a real one from it so we can add
            var args = [];
            for (var i = 0; i < l; i++) {
                args[i] = arguments[i];
            }
            args[l] = context;
            return method.apply(this, args);
        }
        return method.call(this, context);
    }
}

Function.createDelegate = function(instance, method) {
    /// <summary>
    ///     Creates a delegate function that retains the context from its creation
    ///     (i.e. what 'this' means from within its scope).
    ///     This is especially useful when setting up an event handler to point to an object method
    ///     that needs to use the 'this' pointer from within its scope.
    /// </summary>
    /// <param name="instance" mayBeNull="true">
    ///     The object instance that will be the context for the function (i.e. what 'this' means from within its scope).
    /// </param>
    /// <param name="method" type="Function">The function from which the delegate is created.</param>
    /// <returns type="Function">The delegate function.</returns>

    // The method still makes some sense with a null instance, in the same way that createCallback still
    // makes sense with a null context.

    return function() {
        return method.apply(instance, arguments);
    }
}

Function.emptyFunction = Function.emptyMethod = function() {
    /// <summary>A function that does nothing.</summary>
    /// <validationOptions enabled="false"/>
}

Function.validateParameters = function(parameters, expectedParameters, validateParameterCount) {
    /// <summary locid="M:J#Function.validateParameters">
    ///     Validates the parameters to a method are as expected.
    /// </summary>
    /// <param name="parameters"/>
    /// <param name="expectedParameters"/>
    /// <param name="validateParameterCount" type="Boolean" optional="true">True if extra parameters are prohibited, false if they should be ignored. The default is true.</param>
    /// <returns type="Error" mayBeNull="true"/>
    /// <example>
    ///     function foo(anyParam, stringParam, anyArrayParam, stringArrayParam,
    ///                  interfaceParam, optionalStringParam) {
    ///         #if DEBUG
    ///         var e = Function.validateParameters(arguments, [
    ///             { name: "anyParam" },
    ///             { name: "mayBeNullParam", mayBeNull: true },
    ///             { name: "stringParam", type: String },
    ///             { name: "floatParam", type: Number },
    ///             { name: "intParam", type: Number, integer: true },
    ///             { name: "domParam", domElement: true },
    ///             { name: "anyArrayParam", type: Array },
    ///             { name: "mayBeNullArrayParam", type: Array, elementMayBeNull: true },
    ///             { name: "stringArrayParam", type: Array, elementType: String },
    ///             { name: "intArrayParam", type: Array, elementType: Number, elementInteger: true },
    ///             { name: "domElementArrayParam", type: Array, elementDomElement: true },
    ///             { name: "interfaceParam", type: Sys.IFoo }
    ///             { name: "optionalStringParam", type: String, optional: true }
    ///             { name: "stringParamArray", type: String, parameterArray: true }
    ///             { name: "mayBeNullParamArray", parameterArray: true, mayBeNull: true }
    ///         ]);
    ///         if (e) throw e;
    ///         #endif
    ///     }
    /// </example>    
    // type info omitted from parameters so that the parameter-parameters are not enumerated and each entry validated
    return Function._validateParams(parameters, expectedParameters, validateParameterCount);
}

Function._validateParams = function(params, expectedParams, validateParameterCount) {
    // *DO NOT* triple-slash comment those. The double-slashes here are on purpose.
    // We don't need to document private functions and those will induce infinite loops
    // if the preprocessor generates validation code for these.
    // <summary>
    //     Validates the parameters to a method.
    // </summary>
    // <example>
    //     function foo(anyParam, stringParam, anyArrayParam, stringArrayParam,
    //                  interfaceParam, optionalStringParam) {
    //         #if DEBUG
    //         var e = Function._validateParams(arguments, [
    //             { name: "anyParam" },
    //             { name: "mayBeNullParam", mayBeNull: true },
    //             { name: "stringParam", type: String },
    //             { name: "floatParam", type: Number },
    //             { name: "intParam", type: Number, integer: true },
    //             { name: "domParam", domElement: true },
    //             { name: "anyArrayParam", type: Array },
    //             { name: "mayBeNullArrayParam", type: Array, elementMayBeNull: true },
    //             { name: "stringArrayParam", type: Array, elementType: String },
    //             { name: "intArrayParam", type: Array, elementType: Number, elementInteger: true },
    //             { name: "domElementArrayParam", type: Array, elementDomElement: true },
    //             { name: "interfaceParam", type: Sys.IFoo }
    //             { name: "optionalStringParam", type: String, optional: true }
    //             { name: "stringParamArray", type: String, parameterArray: true }
    //             { name: "mayBeNullParamArray", parameterArray: true, mayBeNull: true }
    //         ]);
    //         if (e) throw e;
    //         #endif
    //     }
    // </example>
    // <param name="params" type="Array">Array of parameter values passed to the method.</param>
    // <param name="expectedParams" type="Array" optional="true">Array of JSON objects describing the expected parameters.</param>
    var e, expectedLength = expectedParams.length;
    validateParameterCount = validateParameterCount || (typeof(validateParameterCount) === "undefined");
    e = Function._validateParameterCount(params, expectedParams, validateParameterCount);
    if (e) {
        e.popStackFrame();
        return e;
    }
    for (var i = 0, l = params.length; i < l; i++) {
        // If there are more params than expectedParams, then the last expectedParam
        // must be a paramArray.  Use the last expectedParam to validate the remaining
        // params.
        var expectedParam = expectedParams[Math.min(i, expectedLength - 1)],
            paramName = expectedParam.name;
        if (expectedParam.parameterArray) {
            // Append index of parameter in parameterArray
            paramName += "[" + (i - expectedLength + 1) + "]";
        }
        else if (!validateParameterCount && (i >= expectedLength)) {
            // there were more params than expected params.
            // count validation is disabled, and the last expected param
            // is not a parameter array. Just ignore the rest of the parameters.
            break;
        }
        e = Function._validateParameter(params[i], expectedParam, paramName);
        if (e) {
            e.popStackFrame();
            return e;
        }
    }
    return null;
}

Function._validateParameterCount = function(params, expectedParams, validateParameterCount) {
    var i, error,
        expectedLen = expectedParams.length,
        actualLen = params.length;
    if (actualLen < expectedLen) {
        // this might be ok if some parameters are optional or if theres a parameter array
        var minParams = expectedLen;
        for (i = 0; i < expectedLen; i++) {
            var param = expectedParams[i];
            if (param.optional || param.parameterArray) {
                minParams--;
            }
        }        
        if (actualLen < minParams) {
            error = true;
        }
    }
    else if (validateParameterCount && (actualLen > expectedLen)) {
        // this might be ok if a parameter is a parameterArray
        error = true;      
        for (i = 0; i < expectedLen; i++) {
            if (expectedParams[i].parameterArray) {
                error = false; // infinite parameters is ok
                break;
            }
        }  
    }

    if (error) {
        var e = Error.parameterCount();
        e.popStackFrame();
        return e;
    }

    return null;
}

Function._validateParameter = function(param, expectedParam, paramName) {
    var e,
        expectedType = expectedParam.type,
        expectedInteger = !!expectedParam.integer,
        expectedDomElement = !!expectedParam.domElement,
        mayBeNull = !!expectedParam.mayBeNull;

    e = Function._validateParameterType(param, expectedType, expectedInteger, expectedDomElement, mayBeNull, paramName);
    if (e) {
        e.popStackFrame();
        return e;
    }

    // If parameter is an array, and not undefined or null, validate the type of its elements
    var expectedElementType = expectedParam.elementType,
        elementMayBeNull = !!expectedParam.elementMayBeNull;
    if (expectedType === Array && typeof(param) !== "undefined" && param !== null &&
        (expectedElementType || !elementMayBeNull)) {
        var expectedElementInteger = !!expectedParam.elementInteger,
            expectedElementDomElement = !!expectedParam.elementDomElement;
        for (var i=0; i < param.length; i++) {
            var elem = param[i];
            e = Function._validateParameterType(elem, expectedElementType,
                expectedElementInteger, expectedElementDomElement, elementMayBeNull,
                paramName + "[" + i + "]");
            if (e) {
                e.popStackFrame();
                return e;
            }
        }
    }

    return null;
}

Function._validateParameterType = function(param, expectedType, expectedInteger, expectedDomElement, mayBeNull, paramName) {
    var e, i;

    if (typeof(param) === "undefined") {
        if (mayBeNull) {
            return null;
        }
        else {
            e = Error.argumentUndefined(paramName);
            e.popStackFrame();
            return e;
        }
    }

    if (param === null) {
        if (mayBeNull) {
            return null;
        }
        else {
            e = Error.argumentNull(paramName);
            e.popStackFrame();
            return e;
        }
    }

    if (expectedType && expectedType.__enum) {
        if (typeof(param) !== 'number') {
            e = Error.argumentType(paramName, Object.getType(param), expectedType);
            e.popStackFrame();
            return e;
        }
        if ((param % 1) === 0) {
            var values = expectedType.prototype;
            if (!expectedType.__flags || (param === 0)) {
                for (i in values) {
                    if (values[i] === param) return null;
                }
            }
            else {
                var v = param;
                for (i in values) {
                    var vali = values[i];
                    if (vali === 0) continue;
                    if ((vali & param) === vali) {
                        v -= vali;
                    }
                    if (v === 0) return null;
                }
            }
        }
        e = Error.argumentOutOfRange(paramName, param, String.format(Sys.Res.enumInvalidValue, param, expectedType.getName()));
        e.popStackFrame();
        return e;
    }

    // Text nodes are not considered DOM elements here.
    if (expectedDomElement && (!Sys._isDomElement(param) || (param.nodeType === 3))) {
        e = Error.argument(paramName, Sys.Res.argumentDomElement);
        e.popStackFrame();
        return e;
    }

    // If there is no expected type, any type is allowed.
    if (expectedType && !Sys._isInstanceOfType(expectedType, param)) {
        e = Error.argumentType(paramName, Object.getType(param), expectedType);
        e.popStackFrame();
        return e;
    }

    if (expectedType === Number && expectedInteger) {
        // Modulo operator is 5x faster than Math.round().
        // Modulo returns Number.NaN for Number.NaN, Number.POSITIVE_INFINITY, and Number.NEGATIVE_INFINITY.
        if ((param % 1) !== 0) {
            e = Error.argumentOutOfRange(paramName, param, Sys.Res.argumentInteger);
            e.popStackFrame();
            return e;
        }
    }

    return null;
}
 
Error.__typeName = 'Error';
Error.__class = true;

Error.create = function(message, errorInfo) {
    /// <summary>Use this method to create a new error.</summary>
    /// <param name="message" type="String" optional="true" mayBeNull="true">The error message.</param>
    /// <param name="errorInfo" optional="true" mayBeNull="true">
    ///     A plain JavaScript object that contains extended information about the error.
    ///     The object should have a 'name' field that contains a string that identifies the error
    ///     and any additional fields that are necessary to fully describe the error.
    /// </param>
    /// <returns type="Error">An Error object.</returns>

    // If message string can be converted to a number, IE sets e.message to the number, not the string.
    // Workaround this issue by explicitly setting e.message to the string.
    var err = new Error(message);
    err.message = message;

    if (errorInfo) {
        for (var v in errorInfo) {
            err[v] = errorInfo[v];
        }
    }

    err.popStackFrame();
    return err;
}

// The ArgumentException ctor in .NET has the message *before* paramName.  This
// is inconsistent with all the other Argument*Exception ctors in .NET.
// We feel the paramName is more important than the message, and we want all our
// argument errors to be consistent, so our Error.argument() takes the paramName
// before the message.  This is inconsistent with .NET, but overall we feel
// it is the better design.
Error.argument = function(paramName, message) {
    /// <summary>
    ///     Creates an ArgumentException with a specified error message
    ///     and the name of the parameter that caused this exception.
    /// </summary>
    /// <param name="paramName" type="String" optional="true" mayBeNull="true">
    ///     The name of the parameter that caused the exception.
    /// </param>
    /// <param name="message" type="String" optional="true" mayBeNull="true">
    ///     A message that describes the error.
    /// </param>
    /// <returns>An Error instance that represents an ArgumentException.</returns>

    var displayMessage = "Sys.ArgumentException: " + (message ? message : Sys.Res.argument);
    if (paramName) {
        displayMessage += "\n" + String.format(Sys.Res.paramName, paramName);
    }

    var err = Error.create(displayMessage, { name: "Sys.ArgumentException", paramName: paramName });
    err.popStackFrame();
    return err;
}

Error.argumentNull = function(paramName, message) {
    /// <summary>
    ///     Creates an ArgumentNullException with a specified error message
    ///     and the name of the parameter that caused this exception.
    /// </summary>
    /// <param name="paramName" type="String" optional="true" mayBeNull="true">
    ///     The name of the parameter that caused the exception.
    /// </param>
    /// <param name="message" type="String" optional="true" mayBeNull="true">
    ///     A message that describes the error.
    /// </param>
    /// <returns>An Error instance that represents an ArgumentNullException.</returns>

    var displayMessage = "Sys.ArgumentNullException: " + (message ? message : Sys.Res.argumentNull);
    if (paramName) {
        displayMessage += "\n" + String.format(Sys.Res.paramName, paramName);
    }

    var err = Error.create(displayMessage, { name: "Sys.ArgumentNullException", paramName: paramName });
    err.popStackFrame();
    return err;
}

Error.argumentOutOfRange = function(paramName, actualValue, message) {
    /// <summary>
    ///     Creates an ArgumentOutOfRangeException with a specified error message
    ///     and the name and actual value of the parameter that caused this exception.
    /// </summary>
    /// <param name="paramName" type="String" optional="true" mayBeNull="true">
    ///     The name of the parameter that caused the exception.
    /// </param>
    /// <param name="actualValue" optional="true" mayBeNull="true">
    ///     The actual value of the parameter.
    /// </param>
    /// <param name="message" type="String" optional="true" mayBeNull="true">
    ///     A message that describes the error.
    /// </param>
    /// <returns>An Error instance that represents an ArgumentOutOfRangeException.</returns>

    var displayMessage = "Sys.ArgumentOutOfRangeException: " + (message ? message : Sys.Res.argumentOutOfRange);
    if (paramName) {
        displayMessage += "\n" + String.format(Sys.Res.paramName, paramName);
    }

    // .NET implementation of ArgumentOutOfRangeException does not display actualValue if it is null.
    // For parity with .NET, we do not display if actualValue is null or undefined.  This is OK,
    // since more specific exceptions exist for null and undefined.
    if (typeof(actualValue) !== "undefined" && actualValue !== null) {
        displayMessage += "\n" + String.format(Sys.Res.actualValue, actualValue);
    }

    var err = Error.create(displayMessage, {
        name: "Sys.ArgumentOutOfRangeException",
        paramName: paramName,
        actualValue: actualValue
    });
    err.popStackFrame();
    return err;
}

Error.argumentType = function(paramName, actualType, expectedType, message) {
    /// <summary>
    ///     Creates an ArgumentTypeException with a specified error message
    ///     and the name, actual type, and expected type of the parameter that
    ///     caused this exception.
    /// </summary>
    /// <param name="paramName" type="String" optional="true" mayBeNull="true">
    ///     The name of the parameter that caused the exception.
    /// </param>
    /// <param name="actualType" type="Type" optional="true" mayBeNull="true">
    ///     The actual type of the parameter value.
    /// </param>
    /// <param name="expectedType" type="Type" optional="true" mayBeNull="true">
    ///     The expected type of the parameter value.
    /// </param>
    /// <param name="message" type="String" optional="true" mayBeNull="true">
    ///     A message that describes the error.
    /// </param>
    /// <returns>An Error instance that represents an ArgumentTypeException.</returns>

    var displayMessage = "Sys.ArgumentTypeException: ";
    if (message) {
        displayMessage += message;
    }
    else if (actualType && expectedType) {
        displayMessage +=
            String.format(Sys.Res.argumentTypeWithTypes, actualType.getName(), expectedType.getName());
    }
    else {
        displayMessage += Sys.Res.argumentType;
    }

    if (paramName) {
        displayMessage += "\n" + String.format(Sys.Res.paramName, paramName);
    }

    var err = Error.create(displayMessage, {
        name: "Sys.ArgumentTypeException",
        paramName: paramName,
        actualType: actualType,
        expectedType: expectedType
    });
    err.popStackFrame();
    return err;
}

Error.argumentUndefined = function(paramName, message) {
    /// <summary>
    ///     Creates an ArgumentUndefinedException with a specified error message
    ///     and the name of the parameter that caused this exception.
    /// </summary>
    /// <param name="paramName" type="String" optional="true" mayBeNull="true">
    ///     The name of the parameter that caused the exception.
    /// </param>
    /// <param name="message" type="String" optional="true" mayBeNull="true">
    ///     A message that describes the error.
    /// </param>
    /// <returns>An Error instance that represents an ArgumentUndefinedException.</returns>

    var displayMessage = "Sys.ArgumentUndefinedException: " + (message ? message : Sys.Res.argumentUndefined);
    if (paramName) {
        displayMessage += "\n" + String.format(Sys.Res.paramName, paramName);
    }

    var err = Error.create(displayMessage, { name: "Sys.ArgumentUndefinedException", paramName: paramName });
    err.popStackFrame();
    return err;
}

Error.format = function(message) {
    /// <summary>
    ///     Creates a format error.
    /// </summary>
    /// <param name="message" type="String" optional="true" mayBeNull="true">The error message.</param>
    /// <returns>An Error object that represents a FormatException.</returns>
    var displayMessage = "Sys.FormatException: " + (message ? message : Sys.Res.format);
    var err = Error.create(displayMessage, {name: 'Sys.FormatException'});
    err.popStackFrame();
    return err;
}

Error.invalidOperation = function(message) {
    /// <summary>
    ///     Creates an invalid operation error.
    /// </summary>
    /// <param name="message" type="String" optional="true" mayBeNull="true">The error message.</param>
    /// <returns>An Error instance that represents an InvalidOperationException.</returns>
    var displayMessage = "Sys.InvalidOperationException: " + (message ? message : Sys.Res.invalidOperation);

    var err = Error.create(displayMessage, {name: 'Sys.InvalidOperationException'});
    err.popStackFrame();
    return err;
}

Error.notImplemented = function(message) {
    /// <summary>
    ///     Creates a not implemented error.
    /// </summary>
    /// <param name="message" type="String" optional="true" mayBeNull="true">The error message.</param>
    /// <returns>An Error instance that represents a NotImplementedException.</returns>
    var displayMessage = "Sys.NotImplementedException: " + (message ? message : Sys.Res.notImplemented);

    var err = Error.create(displayMessage, {name: 'Sys.NotImplementedException'});
    err.popStackFrame();
    return err;
}

Error.parameterCount = function(message) {
    /// <summary>
    ///     Creates a ParameterCountException with a specified error message.
    /// </summary>
    /// <param name="message" type="String" optional="true" mayBeNull="true">
    ///     A message that describes the error.
    /// </param>
    /// <returns>An Error instance that represents a ParameterCountException.</returns>

    var displayMessage = "Sys.ParameterCountException: " + (message ? message : Sys.Res.parameterCount);
    var err = Error.create(displayMessage, {name: 'Sys.ParameterCountException'});
    err.popStackFrame();
    return err;
}

Error.prototype.popStackFrame = function() {
    /// <summary>
    ///     Updates the fileName and lineNumber fields based on the next frame in the
    ///     stack trace.  Call this method whenever an instance of Error is returned
    ///     from a function.  This makes the fileName and lineNumber reported in the
    ///     FireFox console point to the location where the exception was thrown, not
    ///     the location where the instance of Error was created.
    /// </summary>
    /// <example>
    ///     function checkParam(param, expectedType) {
    ///         if (!expectedType.isInstanceOfType(param)) {
    ///             var e = new Error("invalid type");
    ///             e.popStackFrame();
    ///             return e;
    ///         }
    ///     }
    /// </example>

    // Example stack frame
    // ===================
    // Error("test error")@:0
    // createError()@http://localhost/app/Error.js:2
    // throwError()@http://localhost/app/Error.js:6
    // callThrowError()@http://localhost/app/Error.js:10
    // @http://localhost/app/Error:js:14

    if (typeof(this.stack) === "undefined" || this.stack === null ||
        typeof(this.fileName) === "undefined" || this.fileName === null ||
        typeof(this.lineNumber) === "undefined" || this.lineNumber === null) {
        return;
    }

    var stackFrames = this.stack.split("\n");

    // Find current stack frame.  It may not be the first stack frame, since the very
    // first frame when the Error is constructed does not correspond to any actual file
    // or line number.  See example stack frame above.
    var currentFrame = stackFrames[0];
    var pattern = this.fileName + ":" + this.lineNumber;
    while(typeof(currentFrame) !== "undefined" &&
          currentFrame !== null &&
          currentFrame.indexOf(pattern) === -1) {
        stackFrames.shift();
        currentFrame = stackFrames[0];
    }

    var nextFrame = stackFrames[1];

    // Special-case last stack frame, to stop shifting frames off the stack.
    if (typeof(nextFrame) === "undefined" || nextFrame === null) {
        return;
    }

    // Update fields to correspond with next stack frame
    var nextFrameParts = nextFrame.match(/@(.*):(\d+)$/);
    if (typeof(nextFrameParts) === "undefined" || nextFrameParts === null) {
        return;
    }

    this.fileName = nextFrameParts[1];

    // This should always succeed, since the regex matches "\d+"
    this.lineNumber = parseInt(nextFrameParts[2]);

    stackFrames.shift();
    this.stack = stackFrames.join("\n");
}
 
Object.__typeName = 'Object';
Object.__class = true;

Object.getType = function(instance) {
    /// <param name="instance">The object for which the type must be returned.</param>
    /// <returns type="Type">The type of the object.</returns>
    var ctor = instance.constructor;
    if (!ctor || (typeof(ctor) !== "function") || !ctor.__typeName || (ctor.__typeName === 'Object')) {
        return Object;
    }
    return ctor;
}

Object.getTypeName = function(instance) {
    /// <param name="instance">The object for which the type name must be returned.</param>
    /// <returns type="String">The name of the type of the object.</returns>
    return Object.getType(instance).getName();
}
 
String.__typeName = 'String';
String.__class = true;

String.prototype.endsWith = function(suffix) {
    /// <summary>Determines whether the end of this instance matches the specified string.</summary>
    /// <param name="suffix" type="String">A string to compare to.</param>
    /// <returns type="Boolean">true if suffix matches the end of this instance; otherwise, false.</returns>
    return (this.substr(this.length - suffix.length) === suffix);
}

String.prototype.startsWith = function(prefix) {
    /// <summary>Determines whether the beginning of this instance matches the specified string.</summary>
    /// <param name="prefix" type="String">The String to compare.</param>
    /// <returns type="Boolean">true if prefix matches the beginning of this string; otherwise, false.</returns>
    return (this.substr(0, prefix.length) === prefix);
}

String.prototype.trim = function() {
    /// <summary>
    ///     Removes all leading and trailing white-space characters from the current String object.
    /// </summary>
    /// <returns type="String">
    ///     The string that remains after all white-space characters are removed from the start and end of the current String object.
    /// </returns>
    return this.replace(/^\s+|\s+$/g, '');
}

String.prototype.trimEnd = function() {
    /// <summary>
    ///     Removes all trailing white spaces from the current String object.
    /// </summary>
    /// <returns type="String">
    ///     The string that remains after all white-space characters are removed from the end of the current String object.
    /// </returns>
    return this.replace(/\s+$/, '');
}

String.prototype.trimStart = function() {
    /// <summary>
    ///     Removes all leading white spaces from the current String object.
    /// </summary>
    /// <returns type="String">
    ///     The string that remains after all white-space characters are removed from the start of the current String object.
    /// </returns>
    return this.replace(/^\s+/, '');
}

String.format = function(format, args) {
    /// <summary>
    ///     Replaces the format items in a specified String with the text equivalents of the values of
    ///     corresponding object instances.  The invariant culture will be used to format dates and numbers.
    /// </summary>
    /// <param name="format" type="String">A format string.</param>
    /// <param name="args" parameterArray="true" mayBeNull="true">The objects to format.</param>
    /// <returns type="String">
    ///     A copy of format in which the format items have been replaced by the
    ///     string equivalent of the corresponding instances of object arguments.
    /// </returns>
    return String._toFormattedString(false, arguments);
}

String._toFormattedString = function(useLocale, args) {
    var result = '';
    var format = args[0];

    for (var i=0;;) {
        // Find the next opening or closing brace
        var open = format.indexOf('{', i);
        var close = format.indexOf('}', i);
        if ((open < 0) && (close < 0)) {
            // Not found: copy the end of the string and break
            result += format.slice(i);
            break;
        }
        if ((close > 0) && ((close < open) || (open < 0))) {
            // Closing brace before opening is an error
            if (format.charAt(close + 1) !== '}') {
                throw Error.argument('format', Sys.Res.stringFormatBraceMismatch);
            }
            result += format.slice(i, close + 1);
            i = close + 2;
            continue;
        }

        // Copy the string before the brace
        result += format.slice(i, open);
        i = open + 1;

        // Check for double braces (which display as one and are not arguments)
        if (format.charAt(i) === '{') {
            result += '{';
            i++;
            continue;
        }

        // at this point we have a valid opening brace, which should be matched by a closing brace.
        if (close < 0) throw Error.argument('format', Sys.Res.stringFormatBraceMismatch);

        // Find the closing brace

        // Get the string between the braces, and split it around the ':' (if any)
        var brace = format.substring(i, close);
        var colonIndex = brace.indexOf(':');
        var argNumber = parseInt((colonIndex < 0)? brace : brace.substring(0, colonIndex), 10) + 1;
        if (isNaN(argNumber)) throw Error.argument('format', Sys.Res.stringFormatInvalid);
        var argFormat = (colonIndex < 0)? '' : brace.substring(colonIndex + 1);

        var arg = args[argNumber];
        if (typeof(arg) === "undefined" || arg === null) {
            arg = '';
        }

        // If it has a toFormattedString method, call it.  Otherwise, call toString()
        if (arg.toFormattedString) {
            result += arg.toFormattedString(argFormat);
        }
        else if (useLocale && arg.localeFormat) {
            result += arg.localeFormat(argFormat);
        }
        else if (arg.format) {
            result += arg.format(argFormat);
        }
        else
            result += arg.toString();

        i = close + 1;
    }

    return result;
}
 
Boolean.__typeName = 'Boolean';
Boolean.__class = true;

Boolean.parse = function(value) {
    /// <summary>Creates a bool from its string representation.</summary>
    /// <param name="value" type="String">"true" or "false".</param>
    /// <returns type="Boolean"/>
    /// <validationOptions validateCount="false"/>
    var v = value.trim().toLowerCase();
    if (v === 'false') return false;
    if (v === 'true') return true;
    throw Error.argumentOutOfRange('value', value, Sys.Res.boolTrueOrFalse);
}
 
Date.__typeName = 'Date';
Date.__class = true;
 
Number.__typeName = 'Number';
Number.__class = true;
 
RegExp.__typeName = 'RegExp';
RegExp.__class = true;
 
// Define the root object (for non-browser hosts)
if (!window) this.window = this;

// Alias Function as Type
window.Type = Function;

// This has undistinguishable perf from compiled a RegExp.
// The regexps here are kept a little too wide to allow for Unicode characters but still
// capture the most obvious developer errors. The JavaScript parser, as well as the checks for
// eval('name') === name will take care of the other errors.
// ********************************************************************************************
// NOTE: update ScriptComponentDescriptor.cs with any change to this expression
// so server and client-side are in sync.
Type.__fullyQualifiedIdentifierRegExp = new RegExp("^[^.0-9 \\s|,;:&*=+\\-()\\[\\]{}^%#@!~\\n\\r\\t\\f\\\\]([^ \\s|,;:&*=+\\-()\\[\\]{}^%#@!~\\n\\r\\t\\f\\\\]*[^. \\s|,;:&*=+\\-()\\[\\]{}^%#@!~\\n\\r\\t\\f\\\\])?$", "i");
Type.__identifierRegExp = new RegExp("^[^.0-9 \\s|,;:&*=+\\-()\\[\\]{}^%#@!~\\n\\r\\t\\f\\\\][^. \\s|,;:&*=+\\-()\\[\\]{}^%#@!~\\n\\r\\t\\f\\\\]*$", "i");

Type.prototype.callBaseMethod = function(instance, name, baseArguments) {
    /// <param name="instance">The instance for the base method. Usually 'this'.</param>
    /// <param name="name" type="String">The name of the base method.</param>
    /// <param name="baseArguments" type="Array" optional="true" mayBeNull="true" elementMayBeNull="true">
    ///     The arguments to pass to the base method.
    /// </param>
    /// <returns>The return value of the base method.</returns>
    var baseMethod = Sys._getBaseMethod(this, instance, name);
    if (!baseMethod) throw Error.invalidOperation(String.format(Sys.Res.methodNotFound, name));
    if (!baseArguments) {
        return baseMethod.apply(instance);
    }
    else {
        return baseMethod.apply(instance, baseArguments);
    }
}

Type.prototype.getBaseMethod = function(instance, name) {
    /// <summary>Use this method to get the base implementation of a method from the base class.</summary>
    /// <param name="instance">The instance for which the base method is needed. Usually 'this'.</param>
    /// <param name="name" type="String">The name of the method to get.</param>
    /// <returns type="Function" mayBeNull="true">The base method.</returns>
    return Sys._getBaseMethod(this, instance, name);
}

Type.prototype.getBaseType = function() {
    /// <returns type="Type" mayBeNull="true">The base type.</returns>
    return (typeof(this.__baseType) === "undefined") ? null : this.__baseType;
}

Type.prototype.getInterfaces = function() {
    /// <returns type="Array" elementType="Type" mayBeNull="false" elementMayBeNull="false">
    ///   A copy of the list of interfaces that the type implements.
    /// </returns>
    var result = [];
    var type = this;
    while(type) {
        var interfaces = type.__interfaces;
        if (interfaces) {
            for (var i = 0, l = interfaces.length; i < l; i++) {
                var interfaceType = interfaces[i];
                if (!Array.contains(result, interfaceType)) {
                    result[result.length] = interfaceType;
                }
            }
        }
        type = type.__baseType;
    }
    return result;
}

Type.prototype.getName = function() {
    /// <returns type="String">The name of the type.</returns>
    return (typeof(this.__typeName) === "undefined") ? "" : this.__typeName;
}

Type.prototype.implementsInterface = function(interfaceType) {
    /// <param name="interfaceType" type="Type">The interface to test.</param>
    /// <returns type="Boolean">True if the type implements the interface.</returns>
    this.resolveInheritance();

    var interfaceName = interfaceType.getName();
    var cache = this.__interfaceCache;
    if (cache) {
        var cacheEntry = cache[interfaceName];
        if (typeof(cacheEntry) !== 'undefined') return cacheEntry;
    }
    else {
        cache = this.__interfaceCache = {};
    }

    var baseType = this;
    while (baseType) {
        var interfaces = baseType.__interfaces;
        if (interfaces) {
            if (Array.indexOf(interfaces, interfaceType) !== -1) {
                return cache[interfaceName] = true;
            }
        }

        baseType = baseType.__baseType;
    }

    return cache[interfaceName] = false;
}

Type.prototype.inheritsFrom = function(parentType) {
    /// <param name="parentType" type="Type">The type to test.</param>
    /// <returns type="Boolean">True if the type inherits from parentType.</returns>
    this.resolveInheritance();
    var baseType = this.__baseType;
    while (baseType) {
        if (baseType === parentType) {
            return true;
        }
        baseType = baseType.__baseType;
    }

    return false;
}

Type.prototype.initializeBase = function(instance, baseArguments) {
    /// <summary>
    ///     This method initializes the base type in the context
    ///     of a given instance object (to keep track of the base type, and to
    ///     effectively inherit the object model of the base class, and
    ///     initializing members of the base class).
    ///     This should be called from the derived class constructor.
    /// </summary>
    /// <param name="instance">The object to initialize base types for. Usually 'this'.</param>
    /// <param name="baseArguments" type="Array" optional="true" mayBeNull="true" elementMayBeNull="true">
    ///     The arguments for the base constructor.
    /// </param>
    /// <returns>The instance.</returns>
    if (!Sys._isInstanceOfType(this, instance)) throw Error.argumentType('instance', Object.getType(instance), this);

    this.resolveInheritance();
    if (this.__baseType) {
        if (!baseArguments) {
            this.__baseType.apply(instance);
        }
        else {
            this.__baseType.apply(instance, baseArguments);
        }
    }

    return instance;
}

Type.prototype.isImplementedBy = function(instance) {
    /// <param name="instance" mayBeNull="true">The object on which the interface must be tested.</param>
    /// <returns type="Boolean">True if the instance implements the interface.</returns>
    if (typeof(instance) === "undefined" || instance === null) return false;

    var instanceType = Object.getType(instance);
    return !!(instanceType.implementsInterface && instanceType.implementsInterface(this));
}

Type.prototype.isInstanceOfType = function(instance) {
    /// <param name="instance" mayBeNull="true">The object on which the type must be tested.</param>
    /// <returns type="Boolean">True if the object is an instance of the type or one of its derived types.</returns>
    return Sys._isInstanceOfType(this, instance);
}

Type.prototype.registerClass = function(typeName, baseType, interfaceTypes) {
    /// <summary>
    ///     Registers a class (represented by its ctor function), and
    ///     optional base type, followed by any number of interfaces.
    /// </summary>
    /// <param name="typeName" type="String">The fully-qualified name of the type.</param>
    /// <param name="baseType" type="Type" optional="true" mayBeNull="true">The base type.</param>
    /// <param name="interfaceTypes" parameterArray="true" type="Type">
    ///     One or several interfaces that the type implements.
    /// </param>
    /// <returns type="Type">The registered type.</returns>
    if (!Type.__fullyQualifiedIdentifierRegExp.test(typeName)) throw Error.argument('typeName', Sys.Res.notATypeName);
    // Check if the type name parses to an existing object that matches this.
    var parsedName;
    try {
        parsedName = eval(typeName);
    }
    catch(e) {
        throw Error.argument('typeName', Sys.Res.argumentTypeName);
    }
    if (parsedName !== this) throw Error.argument('typeName', Sys.Res.badTypeName);
    // Check for double registrations
    if (Sys.__registeredTypes[typeName]) throw Error.invalidOperation(String.format(Sys.Res.typeRegisteredTwice, typeName));

    // We never accept undefined for this parameter because this is the only way we can catch
    // registerClass("Sys.Foo", Sys.BArWithATypo, Sys.ISomeInterface).
    if ((arguments.length > 1) && (typeof(baseType) === 'undefined')) throw Error.argumentUndefined('baseType');
    if (baseType && !baseType.__class) throw Error.argument('baseType', Sys.Res.baseNotAClass);

    this.prototype.constructor = this;
    this.__typeName = typeName;
    this.__class = true;
    if (baseType) {
        this.__baseType = baseType;
        this.__basePrototypePending = true;
    }
    // Saving a case-insensitive index of the registered types on each namespace
    Sys.__upperCaseTypes[typeName.toUpperCase()] = this;

    // It is more performant to check "if (interfaceTypes)" than "if (arguments.length > 2)".
    // Accessing the arguments array is relatively expensive, so we only want to do so if there
    // are actually interface parameters.
    if (interfaceTypes) {
        this.__interfaces = [];
        this.resolveInheritance();
        for (var i = 2, l = arguments.length; i < l; i++) {
            var interfaceType = arguments[i];
            if (!interfaceType.__interface) throw Error.argument('interfaceTypes[' + (i - 2) + ']', Sys.Res.notAnInterface);
            for (var methodName in interfaceType.prototype) {
                var method = interfaceType.prototype[methodName];
                if (!this.prototype[methodName]) {
                    this.prototype[methodName] = method;
                }
            }
            this.__interfaces.push(interfaceType);
        }
    }
    Sys.__registeredTypes[typeName] = true;

    return this;
}

Type.prototype.registerInterface = function(typeName) {
    /// <summary>Registers an interface (represented by its ctor function).</summary>
    /// <param name="typeName" type="String">The fully-qualified name of the interface.</param>
    /// <returns type="Type">The registered interface.</returns>
    if (!Type.__fullyQualifiedIdentifierRegExp.test(typeName)) throw Error.argument('typeName', Sys.Res.notATypeName);
    // Check if the type name parses to an existing object that matches this.
    var parsedName;
    try {
        parsedName = eval(typeName);
    }
    catch(e) {
        throw Error.argument('typeName', Sys.Res.argumentTypeName);
    }
    if (parsedName !== this) throw Error.argument('typeName', Sys.Res.badTypeName);
    // Check for double registrations
    if (Sys.__registeredTypes[typeName]) throw Error.invalidOperation(String.format(Sys.Res.typeRegisteredTwice, typeName));
    // Saving a case-insensitive index of the registered types on each namespace
    Sys.__upperCaseTypes[typeName.toUpperCase()] = this;

    this.prototype.constructor = this;
    this.__typeName = typeName;
    this.__interface = true;
    Sys.__registeredTypes[typeName] = true;

    return this;
}

Type.prototype.resolveInheritance = function() {
    /// <summary>
    /// This method is called on the ctor function instance.
    /// It does three things:
    /// 1. It stores __baseType as a property of the constructor function
    /// 2. It copies members from the baseType's prototype into the
    ///    prototype associated with the type represented by this ctor,
    ///    if this type itself doesn't have the same member in its prototype,
    ///    i.e., it doesn't override the method.
    /// 3. It recurses up the inheritance chain to do the same for the base type.
    ///    Note that this logic runs only once per type, because it
    ///    is based on true value for __basePrototypePending property
    ///    off the ctor function.
    /// </summary>

    if (this.__basePrototypePending) {
        var baseType = this.__baseType;

        baseType.resolveInheritance();

        for (var memberName in baseType.prototype) {
            var memberValue = baseType.prototype[memberName];
            if (!this.prototype[memberName]) {
                this.prototype[memberName] = memberValue;
            }
        }
        delete this.__basePrototypePending;
    }
}

Type.getRootNamespaces = function() {
    /// <returns type="Array">Returns an array containing references to all the root namespaces</returns>
    return Array.clone(Sys.__rootNamespaces);
}

Type.isClass = function(type) {
    /// <param name="type" mayBeNull="true">The type to test.</param>
    /// <returns type="Boolean">True if the type is a class.</returns>
    if ((typeof(type) === 'undefined') || (type === null)) return false;
    return !!type.__class;
}

Type.isInterface = function(type) {
    /// <param name="type" mayBeNull="true">The type to test.</param>
    /// <returns type="Boolean">True if the type is an interface.</returns>
    if ((typeof(type) === 'undefined') || (type === null)) return false;
    return !!type.__interface;
}

Type.isNamespace = function(object) {
    /// <param name="object" mayBeNull="true">The type to test.</param>
    /// <returns type="Boolean">True if the object is a namespace.</returns>
    if ((typeof(object) === 'undefined') || (object === null)) return false;
    return !!object.__namespace;
}

Type.parse = function(typeName, ns) {
    /// <summary>
    ///   If a namespace is specified, the type name is searched for on this namespace in a
    ///   case-insensitive way.
    ///   If no namespace is specified, the fully-qualified, case-sensitive type name must be specified.
    /// </summary>
    /// <param name="typeName" type="String" mayBeNull="true">The name of the type.</param>
    /// <param name="ns" optional="true" mayBeNull="true">The namespace where to look for the type.</param>
    /// <returns type="Type" mayBeNull="true">The type or null.</returns>
    var fn;
    if (ns) {
        fn = Sys.__upperCaseTypes[ns.getName().toUpperCase() + '.' + typeName.toUpperCase()];
        return fn || null;
    }
    if (!typeName) return null;
    if (!Type.__htClasses) {
        Type.__htClasses = {};
    }
    fn = Type.__htClasses[typeName];
    if (!fn) {
        fn = eval(typeName);
        if (typeof(fn) !== 'function') throw Error.argument('typeName', Sys.Res.notATypeName);
        Type.__htClasses[typeName] = fn;
    }
    return fn;
}

Type.registerNamespace = function(namespacePath) {
    /// <summary>Creates a namespace.</summary>
    /// <param name="namespacePath" type="String">The full path of the namespace.</param>
    // in debug mode, the private version does all the work to enable bypassing
    // the parameter validation in debug mode when registering 'Sys'.
    Type._registerNamespace(namespacePath);
}
Type._registerNamespace = function(namespacePath) {
    if (!Type.__fullyQualifiedIdentifierRegExp.test(namespacePath)) throw Error.argument('namespacePath', Sys.Res.invalidNameSpace);
    var rootObject = window;
    var namespaceParts = namespacePath.split('.');

    for (var i = 0; i < namespaceParts.length; i++) {
        var currentPart = namespaceParts[i];
        var ns = rootObject[currentPart];
        var nsType = typeof(ns);
        if ((nsType !== "undefined") && (ns !== null)) {
            if (nsType === "function") {
                throw Error.invalidOperation(String.format(Sys.Res.namespaceContainsClass, namespaceParts.splice(0, i + 1).join('.')));
            }
            if ((typeof(ns) !== "object") || (ns instanceof Array)) {
                throw Error.invalidOperation(String.format(Sys.Res.namespaceContainsNonObject, namespaceParts.splice(0, i + 1).join('.')));
            }
        }
        if (!ns) {
            ns = rootObject[currentPart] = {};
        }
        if (!ns.__namespace) {
            if ((i === 0) && (namespacePath !== "Sys")) {
                Sys.__rootNamespaces[Sys.__rootNamespaces.length] = ns;
            }
            ns.__namespace = true;
            ns.__typeName = namespaceParts.slice(0, i + 1).join('.');
            var parsedName;
            try {
                parsedName = eval(ns.__typeName);
            }
            catch(e) {
                parsedName = null;
            }
            if (parsedName !== ns) {
                delete rootObject[currentPart];
                throw Error.argument('namespacePath', Sys.Res.invalidNameSpace);
            }
            ns.getName = function() {return this.__typeName;}
        }
        rootObject = ns;
    }
}

Type._checkDependency = function(dependency, featureName) {
    var scripts = Type._registerScript._scripts, isDependent = (scripts ? (!!scripts[dependency]) : false);
    if ((typeof(featureName) !== 'undefined') && !isDependent) {
        throw Error.invalidOperation(String.format(Sys.Res.requiredScriptReferenceNotIncluded, 
        featureName, dependency));
    }
    return isDependent;
}

Type._registerScript = function(scriptName, dependencies) {
    var scripts = Type._registerScript._scripts;
    if (!scripts) {
        Type._registerScript._scripts = scripts = {};
    }
    if (scripts[scriptName]) {
        throw Error.invalidOperation(String.format(Sys.Res.scriptAlreadyLoaded, scriptName));
    }
    scripts[scriptName] = true;
    if (dependencies) {
        for (var i = 0, l = dependencies.length; i < l; i++) {
            var dependency = dependencies[i];
            if (!Type._checkDependency(dependency)) {
                throw Error.invalidOperation(String.format(Sys.Res.scriptDependencyNotFound, scriptName, dependency));
            }
        }
    }
}

// bypass param validation in debug mode
Type._registerNamespace("Sys");
Sys.__upperCaseTypes = {};
Sys.__rootNamespaces = [Sys];
Sys.__registeredTypes = {};

// a private version of getBaseMethod and isInstanceOfType allows for other public APIs to call getBaseMethod without
// causing a re-validation of the arguments. It's only debug mode, but the perf of debug mode was so bad in some cases
// due to validation that it causes the browser to warn of a run-away script. Even in debug mode it's still important
// we have reasonable perf, and avoiding re-validation of public facing parameters is the primary way to do that.
// The private versions are on Sys to avoid adding another function to the prototype of every function.
Sys._isInstanceOfType = function(type, instance) {
    if (typeof(instance) === "undefined" || instance === null) return false;
    if (instance instanceof type) return true;
    var instanceType = Object.getType(instance);
    return !!(instanceType === type) ||
           (instanceType.inheritsFrom && instanceType.inheritsFrom(type)) ||
           (instanceType.implementsInterface && instanceType.implementsInterface(type));
}

// getBaseMethod's validateParameters was shown to be a huge portion of the time spent disposing of a large
// number of components on a page, where they all call type.callBaseMethod, which also calls getBaseMethod. 
Sys._getBaseMethod = function(type, instance, name) {
    if (!Sys._isInstanceOfType(type, instance)) throw Error.argumentType('instance', Object.getType(instance), type);
    var baseType = type.getBaseType();
    if (baseType) {
        var baseMethod = baseType.prototype[name];
        return (baseMethod instanceof Function) ? baseMethod : null;
    }
    return null;
}

Sys._isDomElement = function(obj) {
    // Using nodeType to check this is a DOM element for lack of a better test on IE and Safari.
    // This is not entirely foolproof ({nodeType: 1} would seem to be of type Sys.UI.DomElement)
    // but we need something that works cross-browser.
    // Opera and Firefox both have an HTMLElement type of which DOM elements are instances but
    // we're not using it here for consistency.
    var val = false;
    if (typeof (obj.nodeType) !== 'number') {
        // Windows and documents are considered elements even though they are not strictly speaking.
        // No node type may still be window or document.
        // Try to get the document for the element, revert to obj if not found:
        var doc = obj.ownerDocument || obj.document || obj;
        if (doc != obj) {
            // The parameter is not the document, but it may be window.
            // Try to get the window for the document:
            var w = doc.defaultView || doc.parentWindow;
            val = (w != obj);
        }
        else {
            // doc is equal to obj, but we still need to check that it's really a document.
            // Using the body property for lack of a better cross-browser test.
            val = (typeof (doc.body) === 'undefined');
        }
    }
    return !val;
}
 
Array.__typeName = 'Array';
Array.__class = true;

Array.add = Array.enqueue = function(array, item) {
    /// <summary>Adds an element at the end of the array.</summary>
    /// <param name="array" type="Array" elementMayBeNull="true">The array to add to.</param>
    /// <param name="item" mayBeNull="true">The object to add.</param>

    // Setting Array[Array.length] is faster than Array.push() for a single element.
    array[array.length] = item;
}

Array.addRange = function(array, items) {
    /// <summary>Adds a range of items at the end of the array.</summary>
    /// <param name="array" type="Array" elementMayBeNull="true">The array to add to.</param>
    /// <param name="items" type="Array" elementMayBeNull="true">The array of items to append.</param>

    // Array.push() for multiple elements is faster than setting Array[Array.length] in a loop.
    array.push.apply(array, items);
}

Array.clear = function(array) {
    /// <summary>Clears the array of its elements.</summary>
    /// <param name="array" type="Array" elementMayBeNull="true">The array to clear.</param>
    array.length = 0;
}

Array.clone = function(array) {
    /// <summary>Makes a clone of the array.</summary>
    /// <param name="array" type="Array" elementMayBeNull="true">The array to clone.</param>
    /// <returns type="Array" elementMayBeNull="true">A clone of the array.</returns>
    if (array.length === 1) {
        return [array[0]];
    }
    else {
        // When the Array ctor is called with 0 or 2 or more arguments, it creates a new
        // Array with the elements from the argument list.
        return Array.apply(null, array);
    }
}

Array.contains = function(array, item) {
    /// <summary>Use this method to determine if an array contains the specified element.</summary>
    /// <param name="array" type="Array" elementMayBeNull="true">The array to look into.</param>
    /// <param name="item" mayBeNull="true">The object to find in the array.</param>
    /// <returns type="Boolean">True if the object was found.</returns>
    return (Sys._indexOf(array, item) >= 0);
}

Array.dequeue = function(array) {
    /// <param name="array" type="Array" elementMayBeNull="true">Removes and returns the object at the beginning of the array.</param>
    /// <returns mayBeNull="true">The object that is removed from the beginning of the array.</returns>
    return array.shift();
}

Array.forEach = function(array, method, instance) {
    /// <summary>Calls the specified function on each element of the array.</summary>
    /// <param name="array" type="Array" elementMayBeNull="true">The array to enumerate.</param>
    /// <param name="method" type="Function">The method to call.
    ///     The method should take the array element, the index of the element and
    ///     the array itself as its parameters.
    /// </param>
    /// <param name="instance" optional="true" mayBeNull="true">The context under which the function must run (i.e. what 'this' means inside the function).</param>
    for (var i = 0, l = array.length; i < l; i++) {
        var elt = array[i];
        if (typeof(elt) !== 'undefined') method.call(instance, elt, i, array);
    }
}

Array.indexOf = function(array, item, start) {
    /// <summary>Finds the index in the array of the provided item.</summary>
    /// <param name="array" type="Array" elementMayBeNull="true">The array to look into.</param>
    /// <param name="item" optional="true" mayBeNull="true">The object to find.</param>
    /// <param name="start" optional="true" mayBeNull="true">The index where the search begins.</param>
    /// <returns type="Number">The index of the item or -1 if it wasn't found.</returns>
    return Sys._indexOf(array, item, start);
}

Array.insert = function(array, index, item) {
    /// <summary>Inserts an item at the specified index.</summary>
    /// <param name="array" type="Array" elementMayBeNull="true">The array to insert into.</param>
    /// <param name="index" mayBeNull="true">The index where the item will be inserted.</param>
    /// <param name="item" mayBeNull="true">The item to insert.</param>
    array.splice(index, 0, item);
}

Array.parse = function(value) {
    /// <summary>Creates an array from a string representation of the form "[elt1, elt2, elt3]".</summary>
    /// <param name="value" type="String" mayBeNull="true">The string representation of the array.</param>
    /// <returns type="Array" elementMayBeNull="true">An array built from the string representation.</returns>
    if (!value) return [];
    var v = eval(value);
    if (!Array.isInstanceOfType(v)) throw Error.argument('value', Sys.Res.arrayParseBadFormat);
    return v;
}

Array.remove = function(array, item) {
    /// <summary>Removes the first occurence of an item from the array.</summary>
    /// <param name="array" type="Array" elementMayBeNull="true">The array to remove from.</param>
    /// <param name="item" mayBeNull="true">The item to remove.</param>
    /// <returns type="Boolean">True if the item was found.</returns>
    var index = Sys._indexOf(array, item);
    if (index >= 0) {
        array.splice(index, 1);
    }
    return (index >= 0);
}

Array.removeAt = function(array, index) {
    /// <summary>Removes the item at the specified index from the array.</summary>
    /// <param name="array" type="Array" elementMayBeNull="true">The array to remove from.</param>
    /// <param name="index" mayBeNull="true">The index of the item to remove.</param>
    array.splice(index, 1);
}

// a private _indexOf allows for other public Array members to perform indexOf() without
// causing a double validation of its public parameters, a huge source of slowness in debug mode
// for code that uses any of the Array members that call indexOf (Array.remove and Array.contains).
// It is Sys._indexOf instead of Array._indexOf to avoid adding another member to a built-in type.
Sys._indexOf = function(array, item, start) {
    if (typeof(item) === "undefined") return -1;
    var length = array.length;
    if (length !== 0) {
        // Coerce into number ("1a" will become NaN, which is consistent with the built-in behavior of similar Array methods)
        start = start - 0;
        // NaN becomes zero
        if (isNaN(start)) {
            start = 0;
        }
        else {
            // If start is positive or negative infinity, don't try to truncate it.
            // The infinite values will be handled correctly by the subsequent code.
            if (isFinite(start)) {
                // This is faster than doing Math.floor or Math.ceil
                start = start - (start % 1);
            }
            // Negative start indices start from the end
            if (start < 0) {
                start = Math.max(0, length + start);
            }
        }

        // A do/while loop seems to have equal performance to a for loop in this scenario
        for (var i = start; i < length; i++) {
            if ((typeof(array[i]) !== "undefined") && (array[i] === item)) {
                return i;
            }
        }
    }
    return -1;
}

Type._registerScript._scripts = {
	"MicrosoftAjaxCore.js": true,
	"MicrosoftAjaxGlobalization.js": true,
	"MicrosoftAjaxSerialization.js": true,
	"MicrosoftAjaxComponentModel.js": true,
	"MicrosoftAjaxHistory.js": true,
	"MicrosoftAjaxNetwork.js" : true,
	"MicrosoftAjaxWebServices.js": true };

 
Sys.IDisposable = function() {
    throw Error.notImplemented();
}
Sys.IDisposable.prototype = {
    dispose: function() {
        throw Error.notImplemented();
    }
}
Sys.IDisposable.registerInterface('Sys.IDisposable');
 
Sys.StringBuilder = function(initialText) {
    /// <summary>Provides an optimized mechanism to concatenate a sequence of strings.</summary>
    /// <param name="initialText" optional="true" mayBeNull="true">The initial text for the StringBuilder.</param>
    this._parts = (typeof(initialText) !== 'undefined' && initialText !== null && initialText !== '') ?
        [initialText.toString()] : [];
    this._value = {};
    this._len = 0;
}

Sys.StringBuilder.prototype = {
    append: function(text) {
        /// <summary>Appends a new string at the end of the StringBuilder.</summary>
        /// <param name="text" mayBeNull="true">The string to append.</param>
        this._parts[this._parts.length] = text;
    },

    appendLine: function(text) {
        /// <summary>Appends a new string as a line of text at the end of the StringBuilder.</summary>
        /// <param name="text" optional="true" mayBeNull="true">The string to append.</param>
        this._parts[this._parts.length] =
            ((typeof(text) === 'undefined') || (text === null) || (text === '')) ?
            '\r\n' : text + '\r\n';
    },

    clear: function() {
        /// <summary>Clears the StringBuilder of its current contents.</summary>
        this._parts = [];
        this._value = {};
        this._len = 0;
    },

    isEmpty: function() {
        /// <summary>Use this method to determine if the StringBuilder has contents.</summary>
        /// <returns type="Boolean">True if the StringBuilder has any contents.</returns>
        if (this._parts.length === 0) return true;
        return this.toString() === '';
    },

    // separator may be null, to match behavior of ECMA Array.join(separator) and
    // .NET String.Join(separator, value)
    toString: function(separator) {
        /// <summary>Creates a string from the contents of the StringBuilder.</summary>
        /// <param name="separator" type="String" optional="true" mayBeNull="true">
        ///     The separator to insert between the elements of the StringBuilder.
        /// </param>
        /// <returns type="String">The string built from the StringBuilder.</returns>
        separator = separator || '';
        var parts = this._parts;
        if (this._len !== parts.length) {
            this._value = {};
            this._len = parts.length;
        }
        var val = this._value;
        if (typeof(val[separator]) === 'undefined') {
            // Need to remove empty elements before joining in the separator case
            if (separator !== '') {
                for (var i = 0; i < parts.length;) {
                    if ((typeof(parts[i]) === 'undefined') || (parts[i] === '') || (parts[i] === null)) {
                        parts.splice(i, 1);
                    }
                    else {
                        i++;
                    }
                }
            }
            val[separator] = this._parts.join(separator);
        }
        return val[separator];
    }
}
Sys.StringBuilder.registerClass('Sys.StringBuilder');

// Compat
 
Sys.Browser = {};

Sys.Browser.InternetExplorer = {};
Sys.Browser.Firefox = {};
Sys.Browser.Safari = {};
Sys.Browser.Opera = {};

Sys.Browser.agent = null;
Sys.Browser.hasDebuggerStatement = false;
Sys.Browser.name = navigator.appName;
Sys.Browser.version = parseFloat(navigator.appVersion);
Sys.Browser.documentMode = 0;

if (navigator.userAgent.indexOf(' MSIE ') > -1) {
    Sys.Browser.agent = Sys.Browser.InternetExplorer;
    Sys.Browser.version = parseFloat(navigator.userAgent.match(/MSIE (\d+\.\d+)/)[1]);
    if (Sys.Browser.version >= 8) {
        if (document.documentMode >= 7) {
            Sys.Browser.documentMode = document.documentMode;    
        }
    }
    Sys.Browser.hasDebuggerStatement = true;
}
else if (navigator.userAgent.indexOf(' Firefox/') > -1) {
    Sys.Browser.agent = Sys.Browser.Firefox;
    Sys.Browser.version = parseFloat(navigator.userAgent.match(/ Firefox\/(\d+\.\d+)/)[1]);
    Sys.Browser.name = 'Firefox';
    Sys.Browser.hasDebuggerStatement = true;
}
else if (navigator.userAgent.indexOf(' AppleWebKit/') > -1) {
    Sys.Browser.agent = Sys.Browser.Safari;
    Sys.Browser.version = parseFloat(navigator.userAgent.match(/ AppleWebKit\/(\d+(\.\d+)?)/)[1]);
    Sys.Browser.name = 'Safari';
}
else if (navigator.userAgent.indexOf('Opera/') > -1) {
    Sys.Browser.agent = Sys.Browser.Opera;
}

 
Sys.EventArgs = function() {
    /// <summary>EventArgs is the base class for classes containing event data.</summary>
}
Sys.EventArgs.registerClass('Sys.EventArgs');

Sys.EventArgs.Empty = new Sys.EventArgs();
 
Sys.CancelEventArgs = function() {
    /// <summary>CancelEventArgs is the base class for classes containing event data, which can be used to cancel the event.</summary>
    Sys.CancelEventArgs.initializeBase(this);

    this._cancel = false;
}

Sys.CancelEventArgs.prototype = {
    get_cancel: function() {
        /// <summary>Returns whether the event should be cancelled.</summary>
        /// <value type="Boolean"/>
        return this._cancel;
    },
    set_cancel: function(value) {
        this._cancel = value;
    }
}

Sys.CancelEventArgs.registerClass('Sys.CancelEventArgs', Sys.EventArgs);

// Base Class Library

Type.registerNamespace('Sys.UI');

 
Sys._Debug = function() {
    /// <summary locid="M:J#Sys.Debug.#ctor">Provides a set of methods that help debug your code.</summary>
    /// <field name="isDebug" type="Boolean" locid="F:J#Sys.Debug.isDebug"/>
}
Sys._Debug.prototype = {

    _appendConsole: function(text) {
        // VS script debugger output window.
        if ((typeof(Debug) !== 'undefined') && Debug.writeln) {
            Debug.writeln(text);
        }
        // FF firebug and Safari console.
        if (window.console && window.console.log) {
            window.console.log(text);
        }
        // Opera console.
        if (window.opera) {
            window.opera.postError(text);
        }
        // WebDevHelper console.
        if (window.debugService) {
            window.debugService.trace(text);
        }
    },

    _appendTrace: function(text) {
        var traceElement = document.getElementById('TraceConsole');
        if (traceElement && (traceElement.tagName.toUpperCase() === 'TEXTAREA')) {
            traceElement.value += text + '\n';
        }
    },

    assert: function(condition, message, displayCaller) {
        /// <summary locid="M:J#Sys.Debug.assert">
        ///     Checks for a condition, displays a message and prompts the user to break
        ///      into the debugger if the condition is false.
        /// </summary>
        /// <param name="condition" type="Boolean">true to prevent a message being displayed; otherwise, false.</param>
        /// <param name="message" type="String" optional="true" mayBeNull="true">A message to display.</param>
        /// <param name="displayCaller" type="Boolean" optional="true">True if the function calling assert should be displayed in the message.</param>
        if (!condition) {
            message = (displayCaller && this.assert.caller) ?
                String.format(Sys.Res.assertFailedCaller, message, this.assert.caller) :
                String.format(Sys.Res.assertFailed, message);

            if (confirm(String.format(Sys.Res.breakIntoDebugger, message))) {
                this.fail(message);
            }
        }
    },

    clearTrace: function() {
        /// <summary locid="M:J#Sys.Debug.clearTrace" />
        var traceElement = document.getElementById('TraceConsole');
        if (traceElement && (traceElement.tagName.toUpperCase() === 'TEXTAREA')) {
            traceElement.value = '';
        }
    },

    fail: function(message) {
        /// <summary locid="M:J#Sys.Debug.fail">Displays a message in the debugger's output window and breaks into the debugger.</summary>
        /// <param name="message" type="String" mayBeNull="true">A message to display.</param>
        this._appendConsole(message);

        // Cannot execute eval('debugger') in browsers that don't have a debugger statement, since it causes a parse error.
        if (Sys.Browser.hasDebuggerStatement) {
            eval('debugger');
        }
    },

    trace: function(text) {
        /// <summary locid="M:J#Sys.Debug.trace">Appends a text line to the debugger console and the TraceConsole textarea element if available.</summary>
        /// <param name="text">Text for trace.</param>
        this._appendConsole(text);
        this._appendTrace(text);
    },

    traceDump: function(object, name) {
        /// <summary locid="M:J#Sys.Debug.traceDump">Dumps an object to the debugger console and the TraceConsole textarea element if available.</summary>
        /// <param name="object" mayBeNull="true">Object for trace dump.</param>
        /// <param name="name" type="String" mayBeNull="true" optional="true">Object name.</param>
        var text = this._traceDump(object, name, true);
    },

    _traceDump: function(object, name, recursive, indentationPadding, loopArray) {
        name = name? name : 'traceDump';
        indentationPadding = indentationPadding? indentationPadding : '';
        if (object === null) {
            this.trace(indentationPadding + name + ': null');
            return;
        }
        switch(typeof(object)) {
            case 'undefined':
                this.trace(indentationPadding + name + ': Undefined');
                break;
            case 'number': case 'string': case 'boolean':
                this.trace(indentationPadding + name + ': ' + object);
                break;
            default:
                if (Date.isInstanceOfType(object) || RegExp.isInstanceOfType(object)) {
                    this.trace(indentationPadding + name + ': ' + object.toString());
                    break;
                }
                if (!loopArray) {
                    loopArray = [];
                }
                else if (Array.contains(loopArray, object)) {
                    this.trace(indentationPadding + name + ': ...');
                    return;
                }
                Array.add(loopArray, object);

                // don't recurse into dom elements.
                // trace dump has to use '==' for window when it's passed as event arg in IE.
                // i.e., body onLoad="Sys.Debug.traceDump(window)"
                if ((object == window) || (object === document) ||
                    (window.HTMLElement && (object instanceof HTMLElement)) ||
                    (typeof(object.nodeName) === 'string')) {
                    var tag = object.tagName? object.tagName : 'DomElement';
                    if (object.id) {
                        tag += ' - ' + object.id;
                    }
                    this.trace(indentationPadding + name + ' {' +  tag + '}');
                }
                // objects and arrays
                else {
                    var typeName = Object.getTypeName(object);
                    this.trace(indentationPadding + name + (typeof(typeName) === 'string' ? ' {' + typeName + '}' : ''));
                    if ((indentationPadding === '') || recursive) {
                        indentationPadding += "    ";
                        var i, length, properties, p, v;
                        if (Array.isInstanceOfType(object)) {
                            length = object.length;
                            for (i = 0; i < length; i++) {
                                this._traceDump(object[i], '[' + i + ']', recursive, indentationPadding, loopArray);
                            }
                        }
                        else {
                            for (p in object) {
                                v = object[p];
                                if (!Function.isInstanceOfType(v)) {
                                    this._traceDump(v, p, recursive, indentationPadding, loopArray);
                                }
                            }
                        }
                    }
                }
                Array.remove(loopArray, object);
        }
    }
}
Sys._Debug.registerClass('Sys._Debug');

Sys.Debug = new Sys._Debug();
    Sys.Debug.isDebug = true;
 
function Sys$Enum$parse(value, ignoreCase) {
    /// <summary>
    ///     Converts the string representation of the name or numeric value of one or more enumerated
    ///     constants to an equivalent enumerated object.
    /// </summary>
    /// <example>
    ///     Sys.UI.LineType.parse("solid, wide");
    /// </example>
    /// <param name="value" type="String">A string containing the name or value to convert.</param>
    /// <param name="ignoreCase" type="Boolean" optional="true">
    ///   If true, the parsing will be done case-insensitively.
    ///   If omitted, the parsing is done case-sensitively.
    /// </param>
    /// <returns>An object of type enumType whose value is represented by value.</returns>
    var values, parsed, val;
    if (ignoreCase) {
        values = this.__lowerCaseValues;
        if (!values) {
            this.__lowerCaseValues = values = {};
            var prototype = this.prototype;
            for (var name in prototype) {
                values[name.toLowerCase()] = prototype[name];
            }
        }
    }
    else {
        values = this.prototype;
    }
    if (!this.__flags) {
        val = (ignoreCase ? value.toLowerCase() : value);
        parsed = values[val.trim()];
        if (typeof(parsed) !== 'number') throw Error.argument('value', String.format(Sys.Res.enumInvalidValue, value, this.__typeName));
        return parsed;
    }
    else {
        var parts = (ignoreCase ? value.toLowerCase() : value).split(',');
        var v = 0;

        for (var i = parts.length - 1; i >= 0; i--) {
            var part = parts[i].trim();
            parsed = values[part];
            if (typeof(parsed) !== 'number') throw Error.argument('value', String.format(Sys.Res.enumInvalidValue, value.split(',')[i].trim(), this.__typeName));
            v |= parsed;
        }
        return v;
    }
}

function Sys$Enum$toString(value) {
    /// <summary>Converts the value of an enum instance to its equivalent string representation.</summary>
    /// <example>
    ///     Sys.UI.LineType.toString(Sys.UI.LineType.solid | Sys.UI.LineType.wide)
    ///     returns "Sys.UI.LineType.solid, Sys.UI.LineType.wide".
    /// </example>
    /// <param name="value" optional="true" mayBeNull="true">The value of the enum instance for which the string representation must be constructed.</param>
    /// <returns type="String">The string representation of "value".</returns>
    // Need to do the type check manually instead of using parameter validation to be able to return
    // an error message that mentions the actual enum type that's expected instead of Number.
    if ((typeof(value) === 'undefined') || (value === null)) return this.__string;
    if ((typeof(value) != 'number') || ((value % 1) !== 0)) throw Error.argumentType('value', Object.getType(value), this);
    var values = this.prototype;
    var i;
    if (!this.__flags || (value === 0)) {
        for (i in values) {
            if (values[i] === value) {
                return i;
            }
        }
    }
    else {
        var sorted = this.__sortedValues;
        if (!sorted) {
            sorted = [];
            for (i in values) {
                sorted[sorted.length] = {key: i, value: values[i]};
            }
            sorted.sort(function(a, b) {
                return a.value - b.value;
            });
            this.__sortedValues = sorted;
        }
        var parts = [];
        var v = value;
        for (i = sorted.length - 1; i >= 0; i--) {
            var kvp = sorted[i];
            var vali = kvp.value;
            if (vali === 0) continue;
            if ((vali & value) === vali) {
                parts[parts.length] = kvp.key;
                v -= vali;
                if (v === 0) break;
            }
        }
        if (parts.length && v === 0) return parts.reverse().join(', ');
    }
    throw Error.argumentOutOfRange('value', value, String.format(Sys.Res.enumInvalidValue, value, this.__typeName));
}

Type.prototype.registerEnum = function(name, flags) {
    /// <summary>Registers an enum type.</summary>
    /// <example>
    ///     Sys.UI.LineType = function() {
    ///         /// &lt;field name="solid" type="Number" integer="true" static="true"/&gt;
    ///         /// &lt;field name="wide" type="Number" integer="true" static="true"/&gt;
    ///         /// &lt;field name="rounded" type="Number" integer="true" static="true"/&gt;
    ///     }
    ///     Sys.UI.LineType.prototype = {
    ///         solid:      1,
    ///         wide:       2,
    ///         rounded:    4,
    ///     }
    ///     Sys.UI.LineType.registerEnum("Sys.UI.LineType", true);
    /// </example>
    /// <param name="name" type="String">The fully-qualified name of the enum.</param>
    /// <param name="flags" type="Boolean" optional="true">True if the enum is a flags collection.</param>
    if (!Type.__fullyQualifiedIdentifierRegExp.test(name)) throw Error.argument('name', Sys.Res.notATypeName);
    // Check if the type name parses to an existing object that matches this.
    var parsedName;
    try {
        parsedName = eval(name);
    }
    catch(e) {
        throw Error.argument('name', Sys.Res.argumentTypeName);
    }
    if (parsedName !== this) throw Error.argument('name', Sys.Res.badTypeName);
    if (Sys.__registeredTypes[name]) throw Error.invalidOperation(String.format(Sys.Res.typeRegisteredTwice, name));
    for (var j in this.prototype) {
        var val = this.prototype[j];
        if (!Type.__identifierRegExp.test(j)) throw Error.invalidOperation(String.format(Sys.Res.enumInvalidValueName, j));
        if (typeof(val) !== 'number' || (val % 1) !== 0) throw Error.invalidOperation(Sys.Res.enumValueNotInteger);
        if (typeof(this[j]) !== 'undefined') throw Error.invalidOperation(String.format(Sys.Res.enumReservedName, j));
    }
    Sys.__upperCaseTypes[name.toUpperCase()] = this;

    for (var i in this.prototype) {
        this[i] = this.prototype[i];
    }
    this.__typeName = name;
    this.parse = Sys$Enum$parse;
    this.__string = this.toString();
    this.toString = Sys$Enum$toString;
    this.__flags = flags;
    this.__enum = true;
    Sys.__registeredTypes[name] = true;
}

Type.isEnum = function(type) {
    /// <param name="type" mayBeNull="true">The type to test.</param>
    /// <returns type="Boolean">True if the type is an enum.</returns>
    if ((typeof(type) === 'undefined') || (type === null)) return false;
    return !!type.__enum;
}

Type.isFlags = function(type) {
    /// <param name="type" mayBeNull="true">The type to test.</param>
    /// <returns type="Boolean">True if the type is a set of flags.</returns>
    if ((typeof(type) === 'undefined') || (type === null)) return false;
    return !!type.__flags;
}

Sys.CollectionChange = function(action, newItems, newStartingIndex, oldItems, oldStartingIndex) {
    /// <summary>Describes a change in a collection.</summary>
    /// <param name="action" type="Sys.NotifyCollectionChangedAction"></param>
    /// <param name="newItems" optional="true" mayBeNull="true">The items that were added when action is add or replace.</param>
    /// <param name="newStartingIndex" type="Number" integer="true" optional="true" mayBeNull="true">The index where new items have been inserted.</param>
    /// <param name="oldItems" optional="true" mayBeNull="true">The items that were removed when action is remove or replace.</param>
    /// <param name="oldStartingIndex" type="Number" integer="true" optional="true" mayBeNull="true">The index where old items have been removed.</param>
    /// <field name="action" type="Sys.NotifyCollectionChangedAction"></field>
    /// <field name="newItems" type="Array" mayBeNull="true" elementMayBeNull="true">The items that were added when action is add.</field>
    /// <field name="newStartingIndex" type="Number" integer="true">The index where new items have been inserted.</field>
    /// <field name="oldItems" type="Array" mayBeNull="true" elementMayBeNull="true">The items that were removed when action is remove.</field>
    /// <field name="oldStartingIndex" type="Number" integer="true">The index where old items have been removed.</field>
    this.action = action;
    if (newItems) {
        if (!(newItems instanceof Array)) {
            newItems = [newItems];
        }
    }
    this.newItems = newItems || null;
    if (typeof newStartingIndex !== "number") {
        newStartingIndex = -1;
    }
    this.newStartingIndex = newStartingIndex;
    if (oldItems) {
        if (!(oldItems instanceof Array)) {
            oldItems = [oldItems];
        }
    }
    this.oldItems = oldItems || null;
    if (typeof oldStartingIndex !== "number") {
        oldStartingIndex = -1;
    }
    this.oldStartingIndex = oldStartingIndex;
}
Sys.CollectionChange.registerClass("Sys.CollectionChange");
Sys.NotifyCollectionChangedAction = function() {
    /// <summary>Describes how a collection has changed.</summary>
    /// <field name="add" type="Number" integer="true" static="true"/>
    /// <field name="remove" type="Number" integer="true" static="true"/>
    /// <field name="reset" type="Number" integer="true" static="true"/>
    throw Error.notImplemented();
}
Sys.NotifyCollectionChangedAction.prototype = {
    add: 0,
    remove: 1,
    reset: 2
}
Sys.NotifyCollectionChangedAction.registerEnum('Sys.NotifyCollectionChangedAction');
Sys.NotifyCollectionChangedEventArgs = function(changes) {
    /// <summary>Describes how the collection was changed.</summary>
    /// <param name="changes" type="Array" elementType="Sys.CollectionChange">A list of changes that were performed on the collection since the last event.</param>
    this._changes = changes;
    Sys.NotifyCollectionChangedEventArgs.initializeBase(this);
}
Sys.NotifyCollectionChangedEventArgs.prototype = {
    get_changes: function() {
        /// <value type="Array" elementType="Sys.CollectionChange"></value>
        return this._changes || [];
    }
}
Sys.NotifyCollectionChangedEventArgs.registerClass("Sys.NotifyCollectionChangedEventArgs", Sys.EventArgs);

Sys.Observer = function() {
    throw Error.invalidOperation();
}
Sys.Observer.registerClass("Sys.Observer");

Sys.Observer.makeObservable = function(target) {
    /// <summary>Makes an object directly observable by adding observable methods to it.</summary>
    /// <param name="target" mayBeNull="false">The object, array, or DOM element to make observable.</param>
    /// <returns>The observable object.</returns>
    var isArray = target instanceof Array,
        o = Sys.Observer;
    Sys.Observer._ensureObservable(target);
    if (target.setValue === o._observeMethods.setValue) return target;
    o._addMethods(target, o._observeMethods);
    if (isArray) {
        o._addMethods(target, o._arrayMethods);
    }
    return target;
}
Sys.Observer._ensureObservable = function(target) {
    var type = typeof target;
    if ((type === "string") || (type === "number") || (type === "boolean") || (type === "date")) {
        throw Error.invalidOperation(String.format(Sys.Res.notObservable, type));
    }
}
Sys.Observer._addMethods = function(target, methods) {
    for (var m in methods) {
        if (target[m] && (target[m] !== methods[m])) {
            throw Error.invalidOperation(String.format(Sys.Res.observableConflict, m));
        }
        target[m] = methods[m];
    }
}
// Make use of private version for significant perf improvement in Binding.
// See references of these functions in binding.js for more details.
Sys.Observer._addEventHandler = function(target, eventName, handler) {
    Sys.Observer._getContext(target, true).events._addHandler(eventName, handler);
}
Sys.Observer.addEventHandler = function(target, eventName, handler) {
    /// <summary>Adds an observable event handler to the target.</summary>
    /// <param name="target"></param>
    /// <param name="eventName" type="String"></param>
    /// <param name="handler" type="Function"></param>
    Sys.Observer._ensureObservable(target);
    Sys.Observer._addEventHandler(target, eventName, handler);
}
// Make use of private version for significant perf improvement in Binding.
// See references of these functions in binding.js for more details.
Sys.Observer._removeEventHandler = function(target, eventName, handler) {
    Sys.Observer._getContext(target, true).events._removeHandler(eventName, handler);
}
Sys.Observer.removeEventHandler = function(target, eventName, handler) {
    /// <summary>Adds an observable event handler to the target.</summary>
    /// <param name="target"></param>
    /// <param name="eventName" type="String"></param>
    /// <param name="handler" type="Function"></param>
    Sys.Observer._ensureObservable(target);
    Sys.Observer._removeEventHandler(target, eventName, handler);
}
Sys.Observer.raiseEvent = function(target, eventName, eventArgs) {
    /// <summary>Raises an observable event on the target.</summary>
    /// <param name="target"></param>
    /// <param name="eventName" type="String"></param>
    /// <param name="eventArgs" type="Sys.EventArgs"></param>
    Sys.Observer._ensureObservable(target);
    var ctx = Sys.Observer._getContext(target);
    if (!ctx) return;
    var handler = ctx.events.getHandler(eventName);
    if (handler) {
        handler(target, eventArgs);
    }
}
Sys.Observer.addPropertyChanged = function(target, handler) {
    /// <summary>Adds a propertyChanged event handler to the target.</summary>
    /// <param name="target" mayBeNull="false">The object to observe.</param>
    /// <param name="handler" type="Function">The event handler.</param>
    Sys.Observer._ensureObservable(target);
    Sys.Observer._addEventHandler(target, "propertyChanged", handler);
}
Sys.Observer.removePropertyChanged = function(target, handler) {
    /// <summary>Removes a propertyChanged event handler from the target.</summary>
    /// <param name="target" mayBeNull="false">The object to observe.</param>
    /// <param name="handler" type="Function">The event handler.</param>
    Sys.Observer._ensureObservable(target);
    Sys.Observer._removeEventHandler(target, "propertyChanged", handler);
}
Sys.Observer.beginUpdate = function(target) {
    /// <summary></summary>
    /// <param name="target" mayBeNull="false"></param>
    Sys.Observer._ensureObservable(target);
    Sys.Observer._getContext(target, true).updating = true;
}
Sys.Observer.endUpdate = function(target) {
    /// <summary></summary>
    /// <param name="target" mayBeNull="false"></param>
    Sys.Observer._ensureObservable(target);
    var ctx = Sys.Observer._getContext(target);
    if (!ctx || !ctx.updating) return;
    ctx.updating = false;
    var dirty = ctx.dirty;
    ctx.dirty = false;
    if (dirty) {
        if (target instanceof Array) {
            var changes = ctx.changes;
            ctx.changes = null;
            Sys.Observer.raiseCollectionChanged(target, changes);
        }
        Sys.Observer.raisePropertyChanged(target, "");
    }
}
Sys.Observer.isUpdating = function(target) {
    /// <summary></summary>
    /// <param name="target" mayBeNull="false"></param>
    /// <returns type="Boolean"></returns>
    Sys.Observer._ensureObservable(target);
    var ctx = Sys.Observer._getContext(target);
    return ctx ? ctx.updating : false;
}
// Make use of private version for significant perf improvement in Binding.
// See references of these functions in binding.js for more details.
Sys.Observer._setValue = function(target, propertyName, value) {
    var getter, setter, mainTarget = target, path = propertyName.split('.');
    for (var i = 0, l = (path.length - 1); i < l ; i++) {
        var name = path[i];
        getter = target["get_" + name]; 
        if (typeof (getter) === "function") {
            target = getter.call(target);
        }
        else {
            target = target[name];
        }
        var type = typeof (target);
        if ((target === null) || (type === "undefined")) {
            throw Error.invalidOperation(String.format(Sys.Res.nullReferenceInPath, propertyName));
        }
    }    
    var currentValue, lastPath = path[l];
    getter = target["get_" + lastPath];
    setter = target["set_" + lastPath];
    if (typeof(getter) === 'function') {
        currentValue = getter.call(target);
    }
    else {
        currentValue = target[lastPath];
    }
    if (typeof(setter) === 'function') {
        setter.call(target, value);
    }
    else {
        target[lastPath] = value;
    }
    if (currentValue !== value) {
        var ctx = Sys.Observer._getContext(mainTarget);
        if (ctx && ctx.updating) {
            ctx.dirty = true;
            return;
        };
        Sys.Observer.raisePropertyChanged(mainTarget, path[0]);
    }
}
Sys.Observer.setValue = function(target, propertyName, value) {
    /// <summary>Sets a property or field on the target in an observable manner.</summary>
    /// <param name="target" mayBeNull="false">The object to set a property on.</param>
    /// <param name="propertyName" type="String">The name of the property to field to set.</param>
    /// <param name="value" mayBeNull="true">The value to set.</param>
    Sys.Observer._ensureObservable(target);
    Sys.Observer._setValue(target, propertyName, value);
}
Sys.Observer.raisePropertyChanged = function(target, propertyName) {
    /// <summary>Raises a change notification event.</summary>
    /// <validationOptions enabled="false"/>
    /// <param name="target" mayBeNull="false">The object to raise the event on.</param>
    /// <param name="propertyName" type="String">The name of the property that changed.</param>
    Sys.Observer.raiseEvent(target, "propertyChanged", new Sys.PropertyChangedEventArgs(propertyName));
}

Sys.Observer.addCollectionChanged = function(target, handler) {
    /// <summary></summary>
    /// <param name="target" type="Array" elementMayBeNull="true"></param>
    /// <param name="handler" type="Function"></param>
    Sys.Observer._addEventHandler(target, "collectionChanged", handler);
}
Sys.Observer.removeCollectionChanged = function(target, handler) {
    /// <summary></summary>
    /// <param name="target" type="Array" elementMayBeNull="true"></param>
    /// <param name="handler" type="Function"></param>
    Sys.Observer._removeEventHandler(target, "collectionChanged", handler);
}
Sys.Observer._collectionChange = function(target, change) {
    var ctx = Sys.Observer._getContext(target);
    if (ctx && ctx.updating) {
        ctx.dirty = true;
        var changes = ctx.changes;
        if (!changes) {
            ctx.changes = changes = [change];
        }
        else {
            changes.push(change);
        }
    }
    else {
        Sys.Observer.raiseCollectionChanged(target, [change]);
        Sys.Observer.raisePropertyChanged(target, 'length');
    }
}
Sys.Observer.add = function(target, item) {
    /// <summary>Adds an item to the collection in an observable manner.</summary>
    /// <param name="target" type="Array" elementMayBeNull="true">The array to add to.</param>
    /// <param name="item" mayBeNull="true">The item to add.</param>
    var change = new Sys.CollectionChange(Sys.NotifyCollectionChangedAction.add, [item], target.length);
    Array.add(target, item);
    Sys.Observer._collectionChange(target, change);
}
Sys.Observer.addRange = function(target, items) {
    /// <summary>Adds items to the collection in an observable manner.</summary>
    /// <param name="target" type="Array" elementMayBeNull="true">The array to add to.</param>
    /// <param name="items" type="Array" elementMayBeNull="true">The array of items to add.</param>
    var change = new Sys.CollectionChange(Sys.NotifyCollectionChangedAction.add, items, target.length);
    Array.addRange(target, items);
    Sys.Observer._collectionChange(target, change);
}
Sys.Observer.clear = function(target) {
    /// <summary>Clears the array of its elements in an observable manner.</summary>
    /// <param name="target" type="Array" elementMayBeNull="true">The array to clear.</param>
    var oldItems = Array.clone(target);
    Array.clear(target);
    Sys.Observer._collectionChange(target, new Sys.CollectionChange(Sys.NotifyCollectionChangedAction.reset, null, -1, oldItems, 0));
}
Sys.Observer.insert = function(target, index, item) {
    /// <summary>Inserts an item at the specified index in an observable manner.</summary>
    /// <param name="target" type="Array" elementMayBeNull="true">The array to insert into.</param>
    /// <param name="index" type="Number" integer="true">The index where the item will be inserted.</param>
    /// <param name="item" mayBeNull="true">The item to insert.</param>
    Array.insert(target, index, item);
    Sys.Observer._collectionChange(target, new Sys.CollectionChange(Sys.NotifyCollectionChangedAction.add, [item], index));
}
Sys.Observer.remove = function(target, item) {
    /// <summary>Removes the first occurence of an item from the array in an observable manner.</summary>
    /// <param name="target" type="Array" elementMayBeNull="true">The array to remove from.</param>
    /// <param name="item" mayBeNull="true">The item to remove.</param>
    /// <returns type="Boolean">True if the item was found.</returns>
    var index = Array.indexOf(target, item);
    if (index !== -1) {
        Array.remove(target, item);
        Sys.Observer._collectionChange(target, new Sys.CollectionChange(Sys.NotifyCollectionChangedAction.remove, null, -1, [item], index));
        return true;
    }
    return false;
}
Sys.Observer.removeAt = function(target, index) {
    /// <summary>Removes the item at the specified index from the array in an observable manner.</summary>
    /// <param name="target" type="Array" elementMayBeNull="true">The array to remove from.</param>
    /// <param name="index" type="Number" integer="true">The index of the item to remove.</param>
    if ((index > -1) && (index < target.length)) {
        var item = target[index];
        Array.removeAt(target, index);
        Sys.Observer._collectionChange(target, new Sys.CollectionChange(Sys.NotifyCollectionChangedAction.remove, null, -1, [item], index));
    }
}
Sys.Observer.raiseCollectionChanged = function(target, changes) {
    /// <summary>Raises the collectionChanged event.</summary>
    /// <validationOptions enabled="false"/>
    /// <param name="target">The collection to raise the event on.</param>
    /// <param name="changes" type="Array" elementType="Sys.CollectionChange">A list of changes that were performed on the collection since the last event.</param>
    Sys.Observer.raiseEvent(target, "collectionChanged", new Sys.NotifyCollectionChangedEventArgs(changes));
}

// note: triple-slash comments in these methods works with the preprocessor despite them being defined in this unique way
Sys.Observer._observeMethods = {
    add_propertyChanged: function(handler) {
        Sys.Observer._addEventHandler(this, "propertyChanged", handler);
    },
    remove_propertyChanged: function(handler) {
        Sys.Observer._removeEventHandler(this, "propertyChanged", handler);
    },
    addEventHandler: function(eventName, handler) {
        /// <summary>Adds an observable event handler.</summary>
        /// <param name="eventName" type="String"></param>
        /// <param name="handler" type="Function"></param>
        Sys.Observer._addEventHandler(this, eventName, handler);
    },
    removeEventHandler: function(eventName, handler) {
        /// <summary>Removes an observable event handler.</summary>
        /// <param name="eventName" type="String"></param>
        /// <param name="handler" type="Function"></param>
        Sys.Observer._removeEventHandler(this, eventName, handler);
    },
    get_isUpdating: function() {
        /// <summary></summary>
        /// <validationOptions enabled="false"/>
        /// <returns type="Boolean"></returns>
        return Sys.Observer.isUpdating(this);
    },
    beginUpdate: function() {
        /// <summary></summary>
        /// <validationOptions enabled="false"/>
        Sys.Observer.beginUpdate(this);
    },
    endUpdate: function() {
        /// <summary></summary>
        /// <validationOptions enabled="false"/>
        Sys.Observer.endUpdate(this);
    },
    setValue: function(name, value) {
        /// <summary>Sets a property or field on the target in an observable manner.</summary>
        /// <param name="name" type="String">The name of the property to field to set.</param>
        /// <param name="value" mayBeNull="true">The value to set.</param>
        Sys.Observer._setValue(this, name, value);
    },
    raiseEvent: function(eventName, eventArgs) {
        /// <summary>Raises an observable event.</summary>
        /// <validationOptions enabled="false"/>
        /// <param name="eventName" type="String"></param>
        /// <param name="eventArgs" type="Sys.EventArgs"></param>
        Sys.Observer.raiseEvent(this, eventName, eventArgs);
    },
    raisePropertyChanged: function(name) {
        /// <summary>Raises a change notification event.</summary>
        /// <validationOptions enabled="false"/>
        /// <param name="name" type="String">The name of the property that changed.</param>
        Sys.Observer.raiseEvent(this, "propertyChanged", new Sys.PropertyChangedEventArgs(name));
    }
}
Sys.Observer._arrayMethods = {
    add_collectionChanged: function(handler) {
        Sys.Observer._addEventHandler(this, "collectionChanged", handler);
    },
    remove_collectionChanged: function(handler) {
        Sys.Observer._removeEventHandler(this, "collectionChanged", handler);
    },
    add: function(item) {
        /// <summary>Adds an item to the collection in an observable manner.</summary>
        /// <validationOptions enabled="false"/>
        /// <param name="item" mayBeNull="true">The item to add.</param>
        Sys.Observer.add(this, item);
    },
    addRange: function(items) {
        /// <summary>Adds items to the collection in an observable manner.</summary>
        /// <validationOptions enabled="false"/>
        /// <param name="items" type="Array" elementMayBeNull="true">The array of items to add.</param>
        Sys.Observer.addRange(this, items);
    },
    clear: function() {
        /// <summary>Clears the array of its elements in an observable manner.</summary>
        /// <validationOptions enabled="false"/>
        Sys.Observer.clear(this);
    },
    insert: function(index, item) { 
        /// <summary>Inserts an item at the specified index in an observable manner.</summary>
        /// <validationOptions enabled="false"/>
        /// <param name="index" type="Number" integer="true">The index where the item will be inserted.</param>
        /// <param name="item" mayBeNull="true">The item to insert.</param>
        Sys.Observer.insert(this, index, item);
    },
    remove: function(item) {
        /// <summary>Removes the first occurence of an item from the array in an observable manner.</summary>
        /// <validationOptions enabled="false"/>
        /// <param name="item" mayBeNull="true">The item to remove.</param>
        /// <returns type="Boolean">True if the item was found.</returns>
        return Sys.Observer.remove(this, item);
    },
    removeAt: function(index) {
        /// <summary>Removes the item at the specified index from the array in an observable manner.</summary>
        /// <validationOptions enabled="false"/>
        /// <param name="index" type="Number" integer="true">The index of the item to remove.</param>
        Sys.Observer.removeAt(this, index);
    },
    raiseCollectionChanged: function(changes) {
        /// <summary>Raises the collectionChanged event.</summary>
        /// <validationOptions enabled="false"/>
        /// <param name="changes" type="Array" elementType="Sys.CollectionChange">A list of changes that were performed on the collection since the last event.</param>
        Sys.Observer.raiseEvent(this, "collectionChanged", new Sys.NotifyCollectionChangedEventArgs(changes));
    }
}
Sys.Observer._getContext = function(obj, create) {
    var ctx = obj._observerContext;
    if (ctx) return ctx();
    if (create) {
        return (obj._observerContext = Sys.Observer._createContext())();
    }
    return null;
}
Sys.Observer._createContext = function() {
    // instead of attaching an EventHandlerList, etc directly onto the observed object, we attach a function
    // which returns it as a closure. This prevents the need to attach a field to the object which is not a 
    // function, which could have a negative impact on serializers, whereas they typically always skip functions.
    // It's also better for sparse arrays, where a for-var-in loop need only ignore functions.
    // Return an object instead of the handler list directly so we have a place to put other fields without having
    // to add separate getX() methods for each.
    var ctx = {
        events: new Sys.EventHandlerList()
    };
    return function() {
        return ctx;
    }
}

// **********************************************
// ** MicrosoftAjaxGlobalization.jsa
// **********************************************


Date._appendPreOrPostMatch = function(preMatch, strBuilder) {
    // appends pre- and post- token match strings while removing escaped characters.
    // Returns a single quote count which is used to determine if the token occurs
    // in a string literal.
    var quoteCount = 0;
    var escaped = false;
    for (var i = 0, il = preMatch.length; i < il; i++) {
        var c = preMatch.charAt(i);
        switch (c) {
        case '\'':
            if (escaped) strBuilder.append("'");
            else quoteCount++;
            escaped = false;
            break;
        case '\\':
            if (escaped) strBuilder.append("\\");
            escaped = !escaped;
            break;
        default:
            strBuilder.append(c);
            escaped = false;
            break;
        }
    }
    return quoteCount;
}

Date._expandFormat = function(dtf, format) {
    // expands unspecified or single character date formats into the full pattern.
    if (!format) {
        format = "F";
    }
    var len = format.length;
    if (len === 1) {
        switch (format) {
        case "d":
            return dtf.ShortDatePattern;
        case "D":
            return dtf.LongDatePattern;
        case "t":
            return dtf.ShortTimePattern;
        case "T":
            return dtf.LongTimePattern;
        case "f":
            return dtf.LongDatePattern + " " + dtf.ShortTimePattern;
        case "F":
            return dtf.FullDateTimePattern;
        case "M": case "m":
            return dtf.MonthDayPattern;
        case "s":
            return dtf.SortableDateTimePattern;
        case "Y": case "y":
            return dtf.YearMonthPattern;
        default:
            throw Error.format(Sys.Res.formatInvalidString);
        }
    }
    else if ((len === 2) && (format.charAt(0) === "%")) {
        // Dev10 580024: %X escape format -- intended as a custom format string that
        // is only one character, not a built-in format.
        format = format.charAt(1);
    }
    return format;
}

Date._expandYear = function(dtf, year) {
    // expands 2-digit year into 4 digits.
    var now = new Date(),
        era = Date._getEra(now);
    if (year < 100) {
        var curr = Date._getEraYear(now, dtf, era);
        year += curr - (curr % 100);
        if (year > dtf.Calendar.TwoDigitYearMax) {
            year -= 100;
        }
    }
    return year;
}

Date._getEra = function(date, eras) {
    if (!eras) return 0;
    var start, ticks = date.getTime();
    for (var i = 0, l = eras.length; i < l; i += 4) {
        start = eras[i+2];
        if ((start === null) || (ticks >= start)) {
            return i;
        }
    }
    return 0;
}
Date._getEraYear = function(date, dtf, era, sortable) {
    var year = date.getFullYear();
    if (!sortable && dtf.eras) {
        // convert normal gregorian year to era-shifted gregorian
        // year by subtracting the era offset
        year -= dtf.eras[era + 3];
    }    
    return year;
}

Date._getParseRegExp = function(dtf, format) {
    // converts a format string into a regular expression with groups that
    // can be used to extract date fields from a date string.
    // check for a cached parse regex.
    if (!dtf._parseRegExp) {
        dtf._parseRegExp = {};
    }
    else if (dtf._parseRegExp[format]) {
        return dtf._parseRegExp[format];
    }

    // expand single digit formats, then escape regular expression characters.
    var expFormat = Date._expandFormat(dtf, format);
    expFormat = expFormat.replace(/([\^\$\.\*\+\?\|\[\]\(\)\{\}])/g, "\\\\$1");

    var regexp = new Sys.StringBuilder("^");
    var groups = [];
    var index = 0;
    var quoteCount = 0;
    var tokenRegExp = Date._getTokenRegExp();
    var match;

    // iterate through each date token found.
    while ((match = tokenRegExp.exec(expFormat)) !== null) {
        var preMatch = expFormat.slice(index, match.index);
        index = tokenRegExp.lastIndex;

        // don't replace any matches that occur inside a string literal.
        quoteCount += Date._appendPreOrPostMatch(preMatch, regexp);
        if ((quoteCount%2) === 1) {
            regexp.append(match[0]);
            continue;
        }

        // add a regex group for the token.
        switch (match[0]) {
            case 'dddd': case 'ddd':
            case 'MMMM': case 'MMM':
            case 'gg': case 'g':
                regexp.append("(\\D+)");
                break;
            case 'tt': case 't':
                regexp.append("(\\D*)");
                break;
            case 'yyyy':
                regexp.append("(\\d{4})");
                break;
            case 'fff':
                regexp.append("(\\d{3})");
                break;
            case 'ff':
                regexp.append("(\\d{2})");
                break;
            case 'f':
                regexp.append("(\\d)");
                break;
            case 'dd': case 'd':
            case 'MM': case 'M':
            case 'yy': case 'y':
            case 'HH': case 'H':
            case 'hh': case 'h':
            case 'mm': case 'm':
            case 'ss': case 's':
                regexp.append("(\\d\\d?)");
                break;
            case 'zzz':
                regexp.append("([+-]?\\d\\d?:\\d{2})");
                break;
            case 'zz': case 'z':
                regexp.append("([+-]?\\d\\d?)");
                break;
            case '/':
                regexp.append("(\\" + dtf.DateSeparator + ")");
                break;
        }
        Array.add(groups, match[0]);
    }
    Date._appendPreOrPostMatch(expFormat.slice(index), regexp);
    regexp.append("$");
    // allow whitespace to differ when matching formats.
    var regexpStr = regexp.toString().replace(/\s+/g, "\\s+");
    var parseRegExp = {'regExp': regexpStr, 'groups': groups};
    // cache the regex for this format.
    dtf._parseRegExp[format] = parseRegExp;
    return parseRegExp;
}

Date._getTokenRegExp = function() {
    // regular expression for matching dateTime tokens in format strings.
    return /\/|dddd|ddd|dd|d|MMMM|MMM|MM|M|yyyy|yy|y|hh|h|HH|H|mm|m|ss|s|tt|t|fff|ff|f|zzz|zz|z|gg|g/g;
}

Date.parseLocale = function(value, formats) {
    /// <summary>Creates a date from a locale-specific string representation.</summary>
    /// <param name="value" type="String">A locale-specific string that can parse to a date.</param>
    /// <param name="formats" parameterArray="true" optional="true" mayBeNull="true">Custom formats to match.</param>
    /// <returns type="Date"/>
    return Date._parse(value, Sys.CultureInfo.CurrentCulture, arguments);
}

Date.parseInvariant = function(value, formats) {
    /// <summary>Creates a date from its string representation.</summary>
    /// <param name="value" type="String">A string that can parse to a date.</param>
    /// <param name="formats" parameterArray="true" optional="true" mayBeNull="true">Custom formats to match.</param>
    /// <returns type="Date"/>
    return Date._parse(value, Sys.CultureInfo.InvariantCulture, arguments);
}

Date._parse = function(value, cultureInfo, args) {
    // args is a params array with value as the first item, followed by custom formats.
    // try parse with custom formats.
    var i, l, date, format, formats, custom = false;
    for (i = 1, l = args.length; i < l; i++) {
        format = args[i];
        if (format) {
            custom = true;
            date = Date._parseExact(value, format, cultureInfo);
            if (date) return date;
        }
    }
    // try parse with culture formats.
    if (! custom) {
        formats = cultureInfo._getDateTimeFormats();
        for (i = 0, l = formats.length; i < l; i++) {
            date = Date._parseExact(value, formats[i], cultureInfo);
            if (date) return date;
        }
    }
    return null;
}

Date._parseExact = function(value, format, cultureInfo) {
    // try to parse the date string value by matching against the format string
    // while using the specified culture for date field names.
    value = value.trim();
    var dtf = cultureInfo.dateTimeFormat,
    // convert date formats into regular expressions with groupings.
    // use the regexp to determine the input format and extract the date fields.
        parseInfo = Date._getParseRegExp(dtf, format),
        match = new RegExp(parseInfo.regExp).exec(value);
    // DevDiv 124696: Return null to avoid Firefox warning "does not always return a value"
    if (match === null) return null;
    
    // found a date format that matches the input.
    var groups = parseInfo.groups,
        era = null, year = null, month = null, date = null, weekDay = null,
        hour = 0, hourOffset, min = 0, sec = 0, msec = 0, tzMinOffset = null,
        pmHour = false;
    // iterate the format groups to extract and set the date fields.
    for (var j = 0, jl = groups.length; j < jl; j++) {
        var matchGroup = match[j+1];
        if (matchGroup) {
            switch (groups[j]) {
                case 'dd': case 'd':
                    // Day of month.
                    date = parseInt(matchGroup, 10);
                    // check that date is generally in valid range, also checking overflow below.
                    if ((date < 1) || (date > 31)) return null;
                    break;
                case 'MMMM':
                    // Month, long name.
                    month = cultureInfo._getMonthIndex(matchGroup);
                    if ((month < 0) || (month > 11)) return null;
                    break;
                case 'MMM':
                    // Month, short name.
                    month = cultureInfo._getAbbrMonthIndex(matchGroup);
                    if ((month < 0) || (month > 11)) return null;
                    break;
                case 'M': case 'MM':
                    // Month.
                    month = parseInt(matchGroup, 10) - 1;
                    if ((month < 0) || (month > 11)) return null;
                    break;
                case 'y': case 'yy':
                    // 2-Digit Year.
                    year = Date._expandYear(dtf,parseInt(matchGroup, 10));
                    if ((year < 0) || (year > 9999)) return null;
                    break;
                case 'yyyy':
                    // 4-Digit Year.
                    year = parseInt(matchGroup, 10);
                    if ((year < 0) || (year > 9999)) return null;
                    break;
                case 'h': case 'hh':
                    // Hours (12-hour clock).
                    hour = parseInt(matchGroup, 10);
                    if (hour === 12) hour = 0;
                    if ((hour < 0) || (hour > 11)) return null;
                    break;
                case 'H': case 'HH':
                    // Hours (24-hour clock).
                    hour = parseInt(matchGroup, 10);
                    if ((hour < 0) || (hour > 23)) return null;
                    break;
                case 'm': case 'mm':
                    // Minutes.
                    min = parseInt(matchGroup, 10);
                    if ((min < 0) || (min > 59)) return null;
                    break;
                case 's': case 'ss':
                    // Seconds.
                    sec = parseInt(matchGroup, 10);
                    if ((sec < 0) || (sec > 59)) return null;
                    break;
                case 'tt': case 't':
                    // AM/PM designator.
                    var upperToken = matchGroup.toUpperCase();
                    pmHour = (upperToken === dtf.PMDesignator.toUpperCase());
                    if (!pmHour && (upperToken !== dtf.AMDesignator.toUpperCase())) return null;
                    break;
                case 'f':
                    // Deciseconds.
                    msec = parseInt(matchGroup, 10) * 100;
                    if ((msec < 0) || (msec > 999)) return null;
                    break;
                case 'ff':
                    // Centiseconds.
                    msec = parseInt(matchGroup, 10) * 10;
                    if ((msec < 0) || (msec > 999)) return null;
                    break;
                case 'fff':
                    // Milliseconds.
                    msec = parseInt(matchGroup, 10);
                    if ((msec < 0) || (msec > 999)) return null;
                    break;
                case 'dddd':
                    // Day of week.
                    weekDay = cultureInfo._getDayIndex(matchGroup);
                    if ((weekDay < 0) || (weekDay > 6)) return null;
                    break;
                case 'ddd':
                    // Day of week.
                    weekDay = cultureInfo._getAbbrDayIndex(matchGroup);
                    if ((weekDay < 0) || (weekDay > 6)) return null;
                    break;
                case 'zzz':
                    // Time zone offset in +/- hours:min.
                    var offsets = matchGroup.split(/:/);
                    if (offsets.length !== 2) return null;
                    hourOffset = parseInt(offsets[0], 10);
                    if ((hourOffset < -12) || (hourOffset > 13)) return null;
                    var minOffset = parseInt(offsets[1], 10);
                    if ((minOffset < 0) || (minOffset > 59)) return null;
                    tzMinOffset = (hourOffset * 60) + (matchGroup.startsWith('-')? -minOffset : minOffset);
                    break;
                case 'z': case 'zz':
                    // Time zone offset in +/- hours.
                    hourOffset = parseInt(matchGroup, 10);
                    if ((hourOffset < -12) || (hourOffset > 13)) return null;
                    tzMinOffset = hourOffset * 60;
                    break;
                case 'g': case 'gg':
                    var eraName = matchGroup;
                    if (!eraName || !dtf.eras) return null;
                    eraName = eraName.toLowerCase().trim();
                    for (var i = 0, l = dtf.eras.length; i < l; i += 4) {
                        if (eraName === dtf.eras[i + 1].toLowerCase()) {
                            era = i;
                            break;
                        }
                    }
                    // could not find an era with that name
                    if (era === null) return null;
                    break;
            }
        }
    }
    var result = new Date(), defaultYear, convert = dtf.Calendar.convert;
    if (convert) {
        defaultYear = convert.fromGregorian(result)[0];
    }
    else {
        defaultYear = result.getFullYear();
    }
    if (year === null) {
        year = defaultYear;
    }
    else if (dtf.eras) {
        // year must be shifted to normal gregorian year
        // but not if year was not specified, its already normal gregorian
        // per the main if clause above.
        year += dtf.eras[(era || 0) + 3];
    }
    // Dev10 747832: set default day and month to 1 and January to be consistent with .Net behavior.
    if (month === null) {
        month = 0;
    }
    if (date === null) {
        date = 1;
    }
    // now have year, month, and date, but in the culture's calendar.
    // convert to gregorian if necessary
    if (convert) {
        result = convert.toGregorian(year, month, date);
        // conversion failed, must be an invalid match
        if (result === null) return null;
    }
    else {
        // have to set year, month and date together to avoid overflow based on current date.
        result.setFullYear(year, month, date);
        // check to see if date overflowed for specified month (only checked 1-31 above).
        if (result.getDate() !== date) return null;
        // invalid day of week.
        if ((weekDay !== null) && (result.getDay() !== weekDay)) {
            return null;
        }
    }
    // if pm designator token was found make sure the hours fit the 24-hour clock.
    if (pmHour && (hour < 12)) {
        hour += 12;
    }
    result.setHours(hour, min, sec, msec);
    if (tzMinOffset !== null) {
        // adjust timezone to utc before applying local offset.
        var adjustedMin = result.getMinutes() - (tzMinOffset + result.getTimezoneOffset());
        // Safari limits hours and minutes to the range of -127 to 127.  We need to use setHours
        // to ensure both these fields will not exceed this range.  adjustedMin will range
        // somewhere between -1440 and 1500, so we only need to split this into hours.
        result.setHours(result.getHours() + parseInt(adjustedMin/60, 10), adjustedMin%60);
    }
    return result;
}

Date.prototype.format = function(format) {
    /// <summary>Format a date using the invariant culture.</summary>
    /// <param name="format" type="String">Format string.</param>
    /// <returns type="String">Formatted date.</returns>
    return this._toFormattedString(format, Sys.CultureInfo.InvariantCulture);
}

Date.prototype.localeFormat = function(format) {
    /// <summary>Format a date using the current culture.</summary>
    /// <param name="format" type="String">Format string.</param>
    /// <returns type="String">Formatted date.</returns>
    return this._toFormattedString(format, Sys.CultureInfo.CurrentCulture);
}

Date.prototype._toFormattedString = function(format, cultureInfo) {
    var dtf = cultureInfo.dateTimeFormat,
        convert = dtf.Calendar.convert;
    if (!format || !format.length || (format === 'i')) {
        if (cultureInfo && cultureInfo.name.length) {
            if (convert) {
                // non-gregorian calendar, so we cannot use built-in toLocaleString()
                return this._toFormattedString(dtf.FullDateTimePattern, cultureInfo);
            }
            else {
                var eraDate = new Date(this.getTime());
                var era = Date._getEra(this, dtf.eras);
                eraDate.setFullYear(Date._getEraYear(this, dtf, era));
                return eraDate.toLocaleString();
            }
        }
        else {
            return this.toString();
        }
    }

    var eras = dtf.eras,
        sortable = (format === "s");
    format = Date._expandFormat(dtf, format);

    // Start with an empty string
    var ret = new Sys.StringBuilder();
    var hour;

    function addLeadingZero(num) {
        if (num < 10) {
            return '0' + num;
        }
        return num.toString();
    }

    function addLeadingZeros(num) {
        if (num < 10) {
            return '00' + num;
        }
        if (num < 100) {
            return '0' + num;
        }
        return num.toString();
    }
    function padYear(year) {
        if (year < 10) {
            return '000' + year;
        }
        else if (year < 100) {
            return '00' + year;
        }
        else if (year < 1000) {
            return '0' + year;
        }
        return year.toString();
    }
    
    var foundDay, checkedDay, dayPartRegExp = /([^d]|^)(d|dd)([^d]|$)/g;
    function hasDay() {
        if (foundDay || checkedDay) {
            return foundDay;
        }
        foundDay = dayPartRegExp.test(format);
        checkedDay = true;
        return foundDay;
    }
    
    var quoteCount = 0,
        tokenRegExp = Date._getTokenRegExp(),
        converted;
    if (!sortable && convert) {
        converted = convert.fromGregorian(this);
    }
    for (;;) {

        // Save the current index
        var index = tokenRegExp.lastIndex;

        // Look for the next pattern
        var ar = tokenRegExp.exec(format);

        // Append the text before the pattern (or the end of the string if not found)
        var preMatch = format.slice(index, ar ? ar.index : format.length);
        quoteCount += Date._appendPreOrPostMatch(preMatch, ret);

        if (!ar) break;

        // do not replace any matches that occur inside a string literal.
        if ((quoteCount%2) === 1) {
            ret.append(ar[0]);
            continue;
        }
        
        function getPart(date, part) {
            if (converted) {
                return converted[part];
            }
            switch (part) {
                case 0: return date.getFullYear();
                case 1: return date.getMonth();
                case 2: return date.getDate();
            }
        }

        switch (ar[0]) {
        case "dddd":
            // Day of the week, using the full name
            ret.append(dtf.DayNames[this.getDay()]);
            break;
        case "ddd":
            //Day of the week, as a three-letter abbreviation
            ret.append(dtf.AbbreviatedDayNames[this.getDay()]);
            break;
        case "dd":
            foundDay = true;
            // Day of month, with leading zero for single-digit days
            ret.append(addLeadingZero(getPart(this, 2)));
            break;
        case "d":
            foundDay = true;
            // Day of month, without leading zero for single-digit days
            ret.append(getPart(this, 2));
            break;
        case "MMMM":
            // Month, using the full name
            ret.append((dtf.MonthGenitiveNames && hasDay())
                ? dtf.MonthGenitiveNames[getPart(this, 1)]
                : dtf.MonthNames[getPart(this, 1)]);
            break;
        case "MMM":
            // Month, as a three-letter abbreviation
            ret.append((dtf.AbbreviatedMonthGenitiveNames && hasDay())
                ? dtf.AbbreviatedMonthGenitiveNames[getPart(this, 1)]
                : dtf.AbbreviatedMonthNames[getPart(this, 1)]);
            break;
        case "MM":
            // Month, as digits, with leading zero for single-digit months
            ret.append(addLeadingZero(getPart(this, 1) + 1));
            break;
        case "M":
            // Month, as digits, with no leading zero for single-digit months
            ret.append(getPart(this, 1) + 1);
            break;
        case "yyyy":
            // Year represented by four full digits
            ret.append(padYear(converted ? converted[0] : Date._getEraYear(this, dtf, Date._getEra(this, eras), sortable)));
            break;
        case "yy":
            // Year, as two digits, with leading zero for years less than 10
            ret.append(addLeadingZero((converted ? converted[0] : Date._getEraYear(this, dtf, Date._getEra(this, eras), sortable)) % 100));
            break;
        case "y":
            // Year, as two digits, but with no leading zero for years less than 10
            ret.append((converted ? converted[0] : Date._getEraYear(this, dtf, Date._getEra(this, eras), sortable)) % 100);
            break;
        case "hh":
            // Hours with leading zero for single-digit hours, using 12-hour clock
            hour = this.getHours() % 12;
            if (hour === 0) hour = 12;
            ret.append(addLeadingZero(hour));
            break;
        case "h":
            // Hours with no leading zero for single-digit hours, using 12-hour clock
            hour = this.getHours() % 12;
            if (hour === 0) hour = 12;
            ret.append(hour);
            break;
        case "HH":
            // Hours with leading zero for single-digit hours, using 24-hour clock
            ret.append(addLeadingZero(this.getHours()));
            break;
        case "H":
            // Hours with no leading zero for single-digit hours, using 24-hour clock
            ret.append(this.getHours());
            break;
        case "mm":
            // Minutes with leading zero  for single-digit minutes
            ret.append(addLeadingZero(this.getMinutes()));
            break;
        case "m":
            // Minutes with no leading zero  for single-digit minutes
            ret.append(this.getMinutes());
            break;
        case "ss":
            // Seconds with leading zero for single-digit seconds
            ret.append(addLeadingZero(this.getSeconds()));
            break;
        case "s":
            // Seconds with no leading zero for single-digit seconds
            ret.append(this.getSeconds());
            break;
        case "tt":
            // Multicharacter am/pm indicator
            ret.append((this.getHours() < 12) ? dtf.AMDesignator : dtf.PMDesignator);
            break;
        case "t":
            // One character am/pm indicator ("a" or "p")
            ret.append(((this.getHours() < 12) ? dtf.AMDesignator : dtf.PMDesignator).charAt(0));
            break;
        case "f":
            // Deciseconds
            ret.append(addLeadingZeros(this.getMilliseconds()).charAt(0));
            break;
        case "ff":
            // Centiseconds
            ret.append(addLeadingZeros(this.getMilliseconds()).substr(0, 2));
            break;
        case "fff":
            // Milliseconds
            ret.append(addLeadingZeros(this.getMilliseconds()));
            break;
        case "z":
            // Time zone offset, no leading zero
            hour = this.getTimezoneOffset() / 60;
            ret.append(((hour <= 0) ? '+' : '-') + Math.floor(Math.abs(hour)));
            break;
        case "zz":
            // Time zone offset with leading zero
            hour = this.getTimezoneOffset() / 60;
            ret.append(((hour <= 0) ? '+' : '-') + addLeadingZero(Math.floor(Math.abs(hour))));
            break;
        case "zzz":
            // Time zone offset with leading zero
            hour = this.getTimezoneOffset() / 60;
            ret.append(((hour <= 0) ? '+' : '-') + addLeadingZero(Math.floor(Math.abs(hour))) +
                // Dev10 580009: Server has hard coded ":" separator, rather than using dtf.TimeSeparator
                // Repeated here for consistency, plus ":" was already assumed in date parsing.
                ":" + addLeadingZero(Math.abs(this.getTimezoneOffset() % 60)));
            break;
        case "g":
        case "gg":
            if (dtf.eras) {
                ret.append(dtf.eras[Date._getEra(this, eras) + 1]);
            }
            break;
        case "/":
            ret.append(dtf.DateSeparator);
            break;
        }
    }
    return ret.toString();
}

String.localeFormat = function(format, args) {
    /// <summary>
    ///     Replaces the format items in a specified String with the text equivalents of the values of
    ///     corresponding object instances.  The current culture will be used to format dates and numbers.
    /// </summary>
    /// <param name="format" type="String">A format string.</param>
    /// <param name="args" parameterArray="true" mayBeNull="true">The objects to format.</param>
    /// <returns type="String">
    ///     A copy of format in which the format items have been replaced by the
    ///     string equivalent of the corresponding instances of object arguments.
    /// </returns>
    return String._toFormattedString(true, arguments);
}

Number.parseLocale = function(value) {
    /// <summary>Creates a number from its locale string representation.</summary>
    /// <param name="value" type="String">A string that can parse to a number.</param>
    /// <returns type="Number"/>
    /// <validationOptions validateCount="false"/>
    return Number._parse(value, Sys.CultureInfo.CurrentCulture);
}
Number.parseInvariant = function(value) {
    /// <summary>Creates a number from its string representation.</summary>
    /// <param name="value" type="String">A string that can parse to a number.</param>
    /// <returns type="Number"/>
    /// <validationOptions validateCount="false"/>
    return Number._parse(value, Sys.CultureInfo.InvariantCulture);
}
Number._parse = function(value, cultureInfo) {
    // <param name="value" type="String">A string that can parse to a number.</param>
    // <param name="cultureInfo" type="Sys.CultureInfo">Culture information.</param>
    // <returns type="Number">Parsed number or Number.NaN if parsing failed.</returns>
    value = value.trim();
    
    // allow infinity or hexidecimal for javascript compatability.
    if (value.match(/^[+-]?infinity$/i)) {
        return parseFloat(value);
    }
    if (value.match(/^0x[a-f0-9]+$/i)) {
        return parseInt(value);
    }

    var numFormat = cultureInfo.numberFormat;
    var signInfo = Number._parseNumberNegativePattern(value, numFormat, numFormat.NumberNegativePattern);
    var sign = signInfo[0];
    var num = signInfo[1];
    
    // support leading sign without space in addition to culture negative format for .NET compatability
    if ((sign === '') && (numFormat.NumberNegativePattern !== 1)) {
        signInfo = Number._parseNumberNegativePattern(value, numFormat, 1);
        sign = signInfo[0];
        num = signInfo[1];
    }
    if (sign === '') sign = '+';
    
    var exponent;
    var intAndFraction;
    var exponentPos = num.indexOf('e');
    if (exponentPos < 0) exponentPos = num.indexOf('E');
    if (exponentPos < 0) {
        intAndFraction = num;
        exponent = null;
    }
    else {
        intAndFraction = num.substr(0, exponentPos);
        exponent = num.substr(exponentPos + 1);
    }
    
    var integer;
    var fraction;
    var decimalPos = intAndFraction.indexOf(numFormat.NumberDecimalSeparator);
    if (decimalPos < 0) {
        integer = intAndFraction;
        fraction = null;
    }
    else {
        integer = intAndFraction.substr(0, decimalPos);
        fraction = intAndFraction.substr(decimalPos + numFormat.NumberDecimalSeparator.length);
    }
    
    // strip group separators from the integer portion
    integer = integer.split(numFormat.NumberGroupSeparator).join('');
    // allow spaces in place of non-breaking spaces (\u00A0) in group separators.
    var altNumGroupSeparator = numFormat.NumberGroupSeparator.replace(/\u00A0/g, " ");
    if (numFormat.NumberGroupSeparator !== altNumGroupSeparator) {
        integer = integer.split(altNumGroupSeparator).join('');
    }
    
    var p = sign + integer;
    if (fraction !== null) {
        p += '.' + fraction;
    }
    if (exponent !== null) {
        var expSignInfo = Number._parseNumberNegativePattern(exponent, numFormat, 1);
        if (expSignInfo[0] === '') {
            expSignInfo[0] = '+';
        }
        p += 'e' + expSignInfo[0] + expSignInfo[1];
    }

    // don't allow multiple decimals separators, group separators after decimal or trailing strings.
    if (p.match(/^[+-]?\d*\.?\d*(e[+-]?\d+)?$/)) {
        return parseFloat(p);
    }
    return Number.NaN;
}
Number._parseNumberNegativePattern = function(value, numFormat, numberNegativePattern) {
    // <summary>
    //     Extracts the sign and number from a numeric input string using the culture-specific number format
    //     information and number negative pattern.
    // </summary>
    // <param name="value" type="String">Numerical string value.</param>
    // <param name="numFormat" type="Object">Culture-specific number formatting information.</param>
    // <param name="numberNegativePattern" type="Number">Culture-specific specifier for the negative number format.
    // </param>
    // <returns type="Array">Sign and number for the input value.  The sign is the invariant symbol or an empty
    //     string if the number was unsigned.
    // </returns>
    var neg = numFormat.NegativeSign;
    var pos = numFormat.PositiveSign;    
    switch (numberNegativePattern) {
        case 4: // trailing sign with space
            neg = ' ' + neg;
            pos = ' ' + pos;
        case 3: // trailing sign no space
            if (value.endsWith(neg)) {
                return ['-', value.substr(0, value.length - neg.length)];
            }
            else if (value.endsWith(pos)) {
                return ['+', value.substr(0, value.length - pos.length)];
            }
            break;
        case 2: // leading sign with space
            neg += ' ';
            pos += ' ';
        case 1: // leading sign no space
            if (value.startsWith(neg)) {
                return ['-', value.substr(neg.length)];
            }
            else if (value.startsWith(pos)) {
                return ['+', value.substr(pos.length)];
            }
            break;
        case 0: // parenthesis
            if (value.startsWith('(') && value.endsWith(')')) {
                return ['-', value.substr(1, value.length - 2)];
            }
            break;
    }
    return ['', value];
}

Number.prototype.format = function(format) {
    /// <summary>Format a number using the invariant culture.</summary>
    /// <param name="format" type="String">Format string.</param>
    /// <returns type="String">Formatted number.</returns>
    return this._toFormattedString(format, Sys.CultureInfo.InvariantCulture);
}
Number.prototype.localeFormat = function(format) {
    /// <summary>Format a number using the current culture.</summary>
    /// <param name="format" type="String">Format string.</param>
    /// <returns type="String">Formatted number.</returns>
    return this._toFormattedString(format, Sys.CultureInfo.CurrentCulture);
}
Number.prototype._toFormattedString = function(format, cultureInfo) {
    if (!format || (format.length === 0) || (format === 'i')) {
        if (cultureInfo && (cultureInfo.name.length > 0)) {
            return this.toLocaleString();
        }
        else {
            return this.toString();
        }
    }
    
    // All the enum patterns for the various NumberFormats
    var _percentPositivePattern = ["n %", "n%", "%n" ];
    var _percentNegativePattern = ["-n %", "-n%", "-%n"];
    var _numberNegativePattern = ["(n)","-n","- n","n-","n -"];
    var _currencyPositivePattern = ["$n","n$","$ n","n $"];
    var _currencyNegativePattern = ["($n)","-$n","$-n","$n-","(n$)","-n$","n-$","n$-","-n $","-$ n","n $-","$ n-","$ -n","n- $","($ n)","(n $)"];

    function zeroPad(str, count, left) {
        for (var l=str.length; l < count; l++) {
            str = (left ? ('0' + str) : (str + '0'));
        }
        return str;
    }
    
    // Handles expanding numbers into some specified grouping i.e. [2, 3, 5] would be ...,XXXXX,XXXXX,XXX,XX,
    function expandNumber(number, precision, groupSizes, sep, decimalChar) {
        var curSize = groupSizes[0];
        var curGroupIndex = 1;

        // Round the number off to the correct number of decimal places

        // note: we could use the built-in toFixed(precision), which would give us a rounded
        // off number with the right number of digits, but it is inconsistent across browsers
        // when it comes to rounding errors. It also only supports up to 20 digits, after which
        // all browsers start throwing an error.

        var factor = Math.pow(10, precision);
        // shift decimal place so that we can round to a whole number, then shift back again
        var rounded = (Math.round(number * factor) / factor);
        if (!isFinite(rounded)) {
            // multiplying by the factor may have caused the number to reach Infinity,
            // in which case we will not try to round it.
            rounded = number;
        }
        number = rounded;
        
        // Make the number a string
        var numberString = number.toString();
        var right = "";
        var exponent;
        
        // Exponent may or may not exist with a decimal point.
        // If it does it is always after the decimal point.
        
        // Split: left is integer and decimal, right exponent.
        // e.g. 1.234e+10 or 1e+10
        var split = numberString.split(/e/i);
        numberString = split[0];
        exponent = (split.length > 1 ? parseInt(split[1]) : 0);
        // Split: left is integer, right is decimal
        // e.g. 1.234 or 1234
        split = numberString.split('.');
        numberString = split[0];
        right = split.length > 1 ? split[1] : "";
        
        // expand exponents
        var l;
        if (exponent > 0) {
            // positive exponent, shift digits from decimal to number
            // make sure decimal is at least that many digits long, padded right with 0s
            right = zeroPad(right, exponent, false);
            // add digits to the end of number
            numberString += right.slice(0, exponent);
            // and remove from beginning decimal
            right = right.substr(exponent);
        }
        else if (exponent < 0) {
            exponent = -exponent;
            // negative exponent, shift digits from number to decimal
            // make sure number is at least that many digits long, plus 1, padded left with 0s,
            // so we know it will always have at least '0' left over.
            numberString = zeroPad(numberString, exponent+1, true);
            // add digits to the beginning of decimal
            right = numberString.slice(-exponent, numberString.length) + right;
            // and remove from the end of number
            numberString = numberString.slice(0, -exponent);
        }

        // now check precision, if its 0, drop right, otherwise pad it
        if (precision > 0) {
            // trim right down to precision size
            if (right.length > precision) {
                right = right.slice(0, precision);
            }
            else {
                right = zeroPad(right, precision, false);
            }
            // finally add the separator
            right = decimalChar + right;
        }
        else { // No precision wanted, so drop the right
            right = "";
        }

        var stringIndex = numberString.length-1;
        var ret = "";
        while (stringIndex >= 0) {

            // group size of 0 or larger than the rest of the string means take the rest of the string
            if (curSize === 0 || curSize > stringIndex) {
                if (ret.length > 0)
                    return numberString.slice(0, stringIndex + 1) + sep + ret + right;
                else
                    return numberString.slice(0, stringIndex + 1) + right;
            }

            if (ret.length > 0)
                ret = numberString.slice(stringIndex - curSize + 1, stringIndex+1) + sep + ret;
            else
                ret = numberString.slice(stringIndex - curSize + 1, stringIndex+1);

            stringIndex -= curSize;

            if (curGroupIndex < groupSizes.length) {
                curSize = groupSizes[curGroupIndex];
                curGroupIndex++;
            }
        }
        return numberString.slice(0, stringIndex + 1) + sep + ret + right;
    }
    var nf = cultureInfo.numberFormat;

    // Number is always positive for printing purposes (negative treated separately)
    var number = Math.abs(this);

    // Default to number format
    if (!format)
        format = "D";

    var precision = -1;
    if (format.length > 1) precision = parseInt(format.slice(1), 10);

    var pattern;
    switch (format.charAt(0)) {
    case "d":
    case "D":
        pattern = 'n';

        // precision for decimal is padding
        if (precision !== -1) {
            number = zeroPad(""+number, precision, true);
        }

        // We do want the negative for this scenario only
        if (this < 0) number = -number;
        break;
    case "c":
    case "C":
        if (this < 0) pattern = _currencyNegativePattern[nf.CurrencyNegativePattern];
        else pattern = _currencyPositivePattern[nf.CurrencyPositivePattern];
        if (precision === -1) precision = nf.CurrencyDecimalDigits;
        number = expandNumber(Math.abs(this), precision, nf.CurrencyGroupSizes, nf.CurrencyGroupSeparator, nf.CurrencyDecimalSeparator);
        break;
    case "n":
    case "N":
        if (this < 0) pattern = _numberNegativePattern[nf.NumberNegativePattern];
        else pattern = 'n';
        if (precision === -1) precision = nf.NumberDecimalDigits;
        number = expandNumber(Math.abs(this), precision, nf.NumberGroupSizes, nf.NumberGroupSeparator, nf.NumberDecimalSeparator);
        break;
    case "p":
    case "P":
        if (this < 0) pattern = _percentNegativePattern[nf.PercentNegativePattern];
        else pattern = _percentPositivePattern[nf.PercentPositivePattern];
        if (precision === -1) precision = nf.PercentDecimalDigits;
        // DevDiv Bugs 188012: Number is considered in percent form already, so adding '%' requires multiplication
        // by 100, this is consistent with server-side formatting.
        number = expandNumber(Math.abs(this) * 100, precision, nf.PercentGroupSizes, nf.PercentGroupSeparator, nf.PercentDecimalSeparator);
        break;
    default:
        throw Error.format(Sys.Res.formatBadFormatSpecifier);
    }

    var regex = /n|\$|-|%/g;

    // Start with an empty string
    var ret = "";

    for (;;) {

        // Save the current index
        var index = regex.lastIndex;

        // Look for the next pattern
        var ar = regex.exec(pattern);

        // Append the text before the pattern (or the end of the string if not found)
        ret += pattern.slice(index, ar ? ar.index : pattern.length);

        if (!ar)
            break;

        switch (ar[0]) {
        case "n":
            ret += number;
            break;
        case "$":
            ret += nf.CurrencySymbol;
            break;
        case "-":
            // Dev10 Bug 436534: Only add '-' sign for non zero numbers.
            if (/[1-9]/.test(number)) {
                ret += nf.NegativeSign;
            }
            break;
        case "%":
            ret += nf.PercentSymbol;
            break;
        }
    }

    return ret;
}
 
Sys.CultureInfo = function(name, numberFormat, dateTimeFormat) {
    /// <param name="name" type="String">CultureInfo name.</param>
    /// <param name="numberFormat" type="Object">CultureInfo number format information.</param>
    /// <param name="dateTimeFormat" type="Object">CultureInfo date time format information.</param>
    this.name = name;
    this.numberFormat = numberFormat;
    this.dateTimeFormat = dateTimeFormat;
}
Sys.CultureInfo.prototype = {
    _getDateTimeFormats: function() {
        if (! this._dateTimeFormats) {
            var dtf = this.dateTimeFormat;
            this._dateTimeFormats =
              [ dtf.MonthDayPattern,
                dtf.YearMonthPattern,
                dtf.ShortDatePattern,
                dtf.ShortTimePattern,
                dtf.LongDatePattern,
                dtf.LongTimePattern,
                dtf.FullDateTimePattern,
                dtf.RFC1123Pattern,
                dtf.SortableDateTimePattern,
                dtf.UniversalSortableDateTimePattern ];
        }
        return this._dateTimeFormats;
    },
    _getIndex: function(value, a1, a2) {
        var upper = this._toUpper(value),
            i = Array.indexOf(a1, upper);
        if (i === -1) {
            i = Array.indexOf(a2, upper);
        }
        return i;
    },
    _getMonthIndex: function(value) {
        if (!this._upperMonths) {
            this._upperMonths = this._toUpperArray(this.dateTimeFormat.MonthNames);
            this._upperMonthsGenitive = this._toUpperArray(this.dateTimeFormat.MonthGenitiveNames);
        }
        return this._getIndex(value, this._upperMonths, this._upperMonthsGenitive);
    },
    _getAbbrMonthIndex: function(value) {
        if (!this._upperAbbrMonths) {
            this._upperAbbrMonths = this._toUpperArray(this.dateTimeFormat.AbbreviatedMonthNames);
            this._upperAbbrMonthsGenitive = this._toUpperArray(this.dateTimeFormat.AbbreviatedMonthGenitiveNames);
        }
        return this._getIndex(value, this._upperAbbrMonths, this._upperAbbrMonthsGenitive);
    },
    _getDayIndex: function(value) {
        if (!this._upperDays) {
            this._upperDays = this._toUpperArray(this.dateTimeFormat.DayNames);
        }
        return Array.indexOf(this._upperDays, this._toUpper(value));
    },
    _getAbbrDayIndex: function(value) {
        if (!this._upperAbbrDays) {
            this._upperAbbrDays = this._toUpperArray(this.dateTimeFormat.AbbreviatedDayNames);
        }
        return Array.indexOf(this._upperAbbrDays, this._toUpper(value));
    },
    _toUpperArray: function(arr) {
        var result = [];
        for (var i = 0, il = arr.length; i < il; i++) {
            result[i] = this._toUpper(arr[i]);
        }
        return result;
    },
    _toUpper: function(value) {
        // 'he-IL' has non-breaking space (\u00A0) in weekday names.  In this case replace
        // didn't work using the space escape code ('\s'), so must match the exact character.
        return value.split("\u00A0").join(' ').toUpperCase();
    }
}
Sys.CultureInfo.registerClass('Sys.CultureInfo');
Sys.CultureInfo._parse = function(value) {
    var dtf = value.dateTimeFormat;
    if (dtf && !dtf.eras) {
        dtf.eras = value.eras;
    }
    return new Sys.CultureInfo(value.name, value.numberFormat, dtf);
}

// Make sure the invariant and 'en-US' cultureInfos contained in this file contain unicode in
// place of the non-ascii characters so it matches the encoding of the MicrosoftAjax.js script.
// This is especially required when jsCrunch builds the release script, because it will not
// convert non-ascii characters to unicode correctly for the current MicrosoftAjax.js encoding.
Sys.CultureInfo.InvariantCulture = Sys.CultureInfo._parse({"name":"","numberFormat":{"CurrencyDecimalDigits":2,"CurrencyDecimalSeparator":".","IsReadOnly":true,"CurrencyGroupSizes":[3],"NumberGroupSizes":[3],"PercentGroupSizes":[3],"CurrencyGroupSeparator":",","CurrencySymbol":"\u00A4","NaNSymbol":"NaN","CurrencyNegativePattern":0,"NumberNegativePattern":1,"PercentPositivePattern":0,"PercentNegativePattern":0,"NegativeInfinitySymbol":"-Infinity","NegativeSign":"-","NumberDecimalDigits":2,"NumberDecimalSeparator":".","NumberGroupSeparator":",","CurrencyPositivePattern":0,"PositiveInfinitySymbol":"Infinity","PositiveSign":"+","PercentDecimalDigits":2,"PercentDecimalSeparator":".","PercentGroupSeparator":",","PercentSymbol":"%","PerMilleSymbol":"\u2030","NativeDigits":["0","1","2","3","4","5","6","7","8","9"],"DigitSubstitution":1},"dateTimeFormat":{"AMDesignator":"AM","Calendar":{"MinSupportedDateTime":"@-62135568000000@","MaxSupportedDateTime":"@253402300799999@","AlgorithmType":1,"CalendarType":1,"Eras":[1],"TwoDigitYearMax":2029,"IsReadOnly":true},"DateSeparator":"/","FirstDayOfWeek":0,"CalendarWeekRule":0,"FullDateTimePattern":"dddd, dd MMMM yyyy HH:mm:ss","LongDatePattern":"dddd, dd MMMM yyyy","LongTimePattern":"HH:mm:ss","MonthDayPattern":"MMMM dd","PMDesignator":"PM","RFC1123Pattern":"ddd, dd MMM yyyy HH\':\'mm\':\'ss \'GMT\'","ShortDatePattern":"MM/dd/yyyy","ShortTimePattern":"HH:mm","SortableDateTimePattern":"yyyy\'-\'MM\'-\'dd\'T\'HH\':\'mm\':\'ss","TimeSeparator":":","UniversalSortableDateTimePattern":"yyyy\'-\'MM\'-\'dd HH\':\'mm\':\'ss\'Z\'","YearMonthPattern":"yyyy MMMM","AbbreviatedDayNames":["Sun","Mon","Tue","Wed","Thu","Fri","Sat"],"ShortestDayNames":["Su","Mo","Tu","We","Th","Fr","Sa"],"DayNames":["Sunday","Monday","Tuesday","Wednesday","Thursday","Friday","Saturday"],"AbbreviatedMonthNames":["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec",""],"MonthNames":["January","February","March","April","May","June","July","August","September","October","November","December",""],"IsReadOnly":true,"NativeCalendarName":"Gregorian Calendar","AbbreviatedMonthGenitiveNames":["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec",""],"MonthGenitiveNames":["January","February","March","April","May","June","July","August","September","October","November","December",""]},"eras":[1,"A.D.",null,0]});
if (typeof(__cultureInfo) === "object") {
    Sys.CultureInfo.CurrentCulture = Sys.CultureInfo._parse(__cultureInfo);
    delete __cultureInfo;    
}
else {
    Sys.CultureInfo.CurrentCulture = Sys.CultureInfo._parse({"name":"en-US","numberFormat":{"CurrencyDecimalDigits":2,"CurrencyDecimalSeparator":".","IsReadOnly":false,"CurrencyGroupSizes":[3],"NumberGroupSizes":[3],"PercentGroupSizes":[3],"CurrencyGroupSeparator":",","CurrencySymbol":"$","NaNSymbol":"NaN","CurrencyNegativePattern":0,"NumberNegativePattern":1,"PercentPositivePattern":0,"PercentNegativePattern":0,"NegativeInfinitySymbol":"-Infinity","NegativeSign":"-","NumberDecimalDigits":2,"NumberDecimalSeparator":".","NumberGroupSeparator":",","CurrencyPositivePattern":0,"PositiveInfinitySymbol":"Infinity","PositiveSign":"+","PercentDecimalDigits":2,"PercentDecimalSeparator":".","PercentGroupSeparator":",","PercentSymbol":"%","PerMilleSymbol":"\u2030","NativeDigits":["0","1","2","3","4","5","6","7","8","9"],"DigitSubstitution":1},"dateTimeFormat":{"AMDesignator":"AM","Calendar":{"MinSupportedDateTime":"@-62135568000000@","MaxSupportedDateTime":"@253402300799999@","AlgorithmType":1,"CalendarType":1,"Eras":[1],"TwoDigitYearMax":2029,"IsReadOnly":false},"DateSeparator":"/","FirstDayOfWeek":0,"CalendarWeekRule":0,"FullDateTimePattern":"dddd, MMMM dd, yyyy h:mm:ss tt","LongDatePattern":"dddd, MMMM dd, yyyy","LongTimePattern":"h:mm:ss tt","MonthDayPattern":"MMMM dd","PMDesignator":"PM","RFC1123Pattern":"ddd, dd MMM yyyy HH\':\'mm\':\'ss \'GMT\'","ShortDatePattern":"M/d/yyyy","ShortTimePattern":"h:mm tt","SortableDateTimePattern":"yyyy\'-\'MM\'-\'dd\'T\'HH\':\'mm\':\'ss","TimeSeparator":":","UniversalSortableDateTimePattern":"yyyy\'-\'MM\'-\'dd HH\':\'mm\':\'ss\'Z\'","YearMonthPattern":"MMMM, yyyy","AbbreviatedDayNames":["Sun","Mon","Tue","Wed","Thu","Fri","Sat"],"ShortestDayNames":["Su","Mo","Tu","We","Th","Fr","Sa"],"DayNames":["Sunday","Monday","Tuesday","Wednesday","Thursday","Friday","Saturday"],"AbbreviatedMonthNames":["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec",""],"MonthNames":["January","February","March","April","May","June","July","August","September","October","November","December",""],"IsReadOnly":false,"NativeCalendarName":"Gregorian Calendar","AbbreviatedMonthGenitiveNames":["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec",""],"MonthGenitiveNames":["January","February","March","April","May","June","July","August","September","October","November","December",""]},"eras":[1,"A.D.",null,0]});
}



// **********************************************
// ** MicrosoftAjaxSerialization.jsa
// **********************************************

Type.registerNamespace('Sys.Serialization');


Sys.Serialization.JavaScriptSerializer = function() {
    /// <summary>Provides serialization from JavaScript object to JavaScript object notation.</summary>
    // DevDiv #62350: Considered making all methods static and removing this constructor,
    // but this would have been a breaking change from Atlas 1.0 to Atlas Orcas so was rejected.
}
Sys.Serialization.JavaScriptSerializer.registerClass('Sys.Serialization.JavaScriptSerializer');

Sys.Serialization.JavaScriptSerializer._charsToEscapeRegExs = [];
Sys.Serialization.JavaScriptSerializer._charsToEscape = [];
Sys.Serialization.JavaScriptSerializer._dateRegEx = new RegExp('(^|[^\\\\])\\"\\\\/Date\\((-?[0-9]+)(?:[a-zA-Z]|(?:\\+|-)[0-9]{4})?\\)\\\\/\\"', 'g');
Sys.Serialization.JavaScriptSerializer._escapeChars = {};
// DevDiv Bugs 139383:
// Escape the backslashes in escapeRegEx so that we pass an escaped sequence to the RegExp,
// instead of literal characters. Safari does not support literal characters as it fails on iPhone 1.01.
Sys.Serialization.JavaScriptSerializer._escapeRegEx = new RegExp('["\\\\\\x00-\\x1F]', 'i');
Sys.Serialization.JavaScriptSerializer._escapeRegExGlobal = new RegExp('["\\\\\\x00-\\x1F]', 'g');
Sys.Serialization.JavaScriptSerializer._jsonRegEx = new RegExp('[^,:{}\\[\\]0-9.\\-+Eaeflnr-u \\n\\r\\t]', 'g');
Sys.Serialization.JavaScriptSerializer._jsonStringRegEx = new RegExp('"(\\\\.|[^"\\\\])*"', 'g');
Sys.Serialization.JavaScriptSerializer._serverTypeFieldName = '__type';

Sys.Serialization.JavaScriptSerializer._init = function() {
    var replaceChars = ['\\u0000','\\u0001','\\u0002','\\u0003','\\u0004','\\u0005','\\u0006','\\u0007',
                        '\\b','\\t','\\n','\\u000b','\\f','\\r','\\u000e','\\u000f','\\u0010','\\u0011',
                        '\\u0012','\\u0013','\\u0014','\\u0015','\\u0016','\\u0017','\\u0018','\\u0019',
                        '\\u001a','\\u001b','\\u001c','\\u001d','\\u001e','\\u001f'];
    // Backslash needs to be put at the beginning of the charsToEscape array so that it
    // gets replaced first in the serializeStringWithBuilder method to avoid adding
    // an extra backslash for the newly added backslash of other escaped chars.
    Sys.Serialization.JavaScriptSerializer._charsToEscape[0] = '\\';
    Sys.Serialization.JavaScriptSerializer._charsToEscapeRegExs['\\'] = new RegExp('\\\\', 'g');
    Sys.Serialization.JavaScriptSerializer._escapeChars['\\'] = '\\\\';
    Sys.Serialization.JavaScriptSerializer._charsToEscape[1] = '"';
    Sys.Serialization.JavaScriptSerializer._charsToEscapeRegExs['"'] = new RegExp('"', 'g');
    Sys.Serialization.JavaScriptSerializer._escapeChars['"'] = '\\"';
    for (var i = 0; i < 32; i++) {
        var c = String.fromCharCode(i);
        Sys.Serialization.JavaScriptSerializer._charsToEscape[i+2] = c;
        Sys.Serialization.JavaScriptSerializer._charsToEscapeRegExs[c] = new RegExp(c, 'g');
        Sys.Serialization.JavaScriptSerializer._escapeChars[c] = replaceChars[i];
    }
}

Sys.Serialization.JavaScriptSerializer._serializeBooleanWithBuilder = function(object, stringBuilder) {
    stringBuilder.append(object.toString());
}

Sys.Serialization.JavaScriptSerializer._serializeNumberWithBuilder = function(object, stringBuilder) {
    if (isFinite(object)) {
        stringBuilder.append(String(object));
    }
    else {
        throw Error.invalidOperation(Sys.Res.cannotSerializeNonFiniteNumbers);
    }
}

// DevDiv 154418: for performance improvement, this function has been considered in different ways to come up
// with the following solutions, which depend on the length of the input strings and the browser types, implement
// one of the below three methods for scanning strings and performing escaped char replacements. Other solutions
// that had been considered and their performance comparisons are attached in DevDiv 154418.
Sys.Serialization.JavaScriptSerializer._serializeStringWithBuilder = function(string, stringBuilder) {
    stringBuilder.append('"');
    if (Sys.Serialization.JavaScriptSerializer._escapeRegEx.test(string)) {
        // Initialization the following arrays once: charsToEscapeRegExs, charsToEscape, and
        // escapeChars. These arrays are used for perf reasons, not neccessary required.
        if (Sys.Serialization.JavaScriptSerializer._charsToEscape.length === 0) {
            Sys.Serialization.JavaScriptSerializer._init();
        }
        // DevDiv154418: Depending on the string length, different methods for replacing escaped chars are used for
        // perf reason. Currently 128 is used as a cutoff point based on perf comparisons attached in DevDiv154418
        if (string.length < 128) {
            string = string.replace(Sys.Serialization.JavaScriptSerializer._escapeRegExGlobal,
                function(x) { return Sys.Serialization.JavaScriptSerializer._escapeChars[x]; });
        }
        else {
            for (var i = 0; i < 34; i++) {
                var c = Sys.Serialization.JavaScriptSerializer._charsToEscape[i];
                if (string.indexOf(c) !== -1) {
                    if (Sys.Browser.agent === Sys.Browser.Opera || Sys.Browser.agent === Sys.Browser.FireFox) {
                        string = string.split(c).join(Sys.Serialization.JavaScriptSerializer._escapeChars[c]);
                    }
                    else {
                        string = string.replace(Sys.Serialization.JavaScriptSerializer._charsToEscapeRegExs[c],
                            Sys.Serialization.JavaScriptSerializer._escapeChars[c]);
                    }
                }
            }
       }
    }
    stringBuilder.append(string);
    stringBuilder.append('"');
}

Sys.Serialization.JavaScriptSerializer._serializeWithBuilder = function(object, stringBuilder, sort, prevObjects) {
    var i;
    switch (typeof object) {
    case 'object':
        if (object) {
            if (prevObjects){
                // The loop below makes serilzation O(n^2) worst case for linked list like struture
                // where in depth of graph is in linear proportion to number of elements.
                // However the depth of graph is limited by call stack size(less than 1000 in IE7) hence 
                // the performance hit is within reasonable bounds for debug mode
                for( var j = 0; j < prevObjects.length; j++) {
                    if (prevObjects[j] === object) {
                        throw Error.invalidOperation(Sys.Res.cannotSerializeObjectWithCycle);
                    }
                }
            }
            else {
                prevObjects = new Array();
            }
            try {
                Array.add(prevObjects, object);
                
                if (Number.isInstanceOfType(object)){
                    Sys.Serialization.JavaScriptSerializer._serializeNumberWithBuilder(object, stringBuilder);
                }
                else if (Boolean.isInstanceOfType(object)){
                    Sys.Serialization.JavaScriptSerializer._serializeBooleanWithBuilder(object, stringBuilder);
                }
                else if (String.isInstanceOfType(object)){
                    Sys.Serialization.JavaScriptSerializer._serializeStringWithBuilder(object, stringBuilder);
                }
            
                // Arrays
                else if (Array.isInstanceOfType(object)) {
                    stringBuilder.append('[');
                   
                    for (i = 0; i < object.length; ++i) {
                        if (i > 0) {
                            stringBuilder.append(',');
                        }
                        Sys.Serialization.JavaScriptSerializer._serializeWithBuilder(object[i], stringBuilder,false,prevObjects);
                    }
                    stringBuilder.append(']');
                }
                else {
                    // DivDev 41125: Do not confuse atlas serialized strings with dates
                    // Currently it always serialize as \/Date({milliseconds from 1970/1/1})\/
                    // For example \/Date(123)\/
                    if (Date.isInstanceOfType(object)) {
                        stringBuilder.append('"\\/Date(');
                        stringBuilder.append(object.getTime());
                        stringBuilder.append(')\\/"');
                        break;
                    }

                    var properties = [];
                    var propertyCount = 0;
                    for (var name in object) {
                        // skip internal properties that should not be serialized.
                        if (name.startsWith('$')) {
                            continue;
                        }
                        //DevDiv 74427 : Need to make sure that _type is first item on JSON serialization
                        if (name === Sys.Serialization.JavaScriptSerializer._serverTypeFieldName && propertyCount !== 0){
                            // if current propery Name is __type, swap it with the first element on property array.
                            properties[propertyCount++] = properties[0];
                            properties[0] = name;
                        }
                        else{
                            properties[propertyCount++] = name;
                        }
                    }
                    if (sort) properties.sort();

                    stringBuilder.append('{');
                    var needComma = false;
                     
                    for (i=0; i<propertyCount; i++) {
                        var value = object[properties[i]];
                        if (typeof value !== 'undefined' && typeof value !== 'function') {
                            if (needComma) {
                                stringBuilder.append(',');
                            }
                            else {
                                needComma = true;
                            }
                           
                            // Serialize the name of the object property, then the value
                            Sys.Serialization.JavaScriptSerializer._serializeWithBuilder(properties[i], stringBuilder, sort, prevObjects);
                            stringBuilder.append(':');
                            Sys.Serialization.JavaScriptSerializer._serializeWithBuilder(value, stringBuilder, sort, prevObjects);
                          
                        }
                    }
                stringBuilder.append('}');
                }
            }
            finally {
                Array.removeAt(prevObjects, prevObjects.length - 1);
            }
        }
        else {
            stringBuilder.append('null');
        }
        break;

    case 'number':
        Sys.Serialization.JavaScriptSerializer._serializeNumberWithBuilder(object, stringBuilder);
        break;

    case 'string':
        Sys.Serialization.JavaScriptSerializer._serializeStringWithBuilder(object, stringBuilder);
        break;

    case 'boolean':
        Sys.Serialization.JavaScriptSerializer._serializeBooleanWithBuilder(object, stringBuilder);
        break;

    default:
        stringBuilder.append('null');
        break;
    }
}

Sys.Serialization.JavaScriptSerializer.serialize = function(object) {
    /// <summary>Generates a JSON string from an object.</summary>
    /// <param name="object" mayBeNull="true">The object to serialize.</param>
    /// <returns type="String">The JSON string representation of the object.</returns>
    var stringBuilder = new Sys.StringBuilder();
    Sys.Serialization.JavaScriptSerializer._serializeWithBuilder(object, stringBuilder, false);
    return stringBuilder.toString();
}

Sys.Serialization.JavaScriptSerializer.deserialize = function(data, secure) {
    /// <summary>Deserializes a JSON string.</summary>
    /// <param name="data" type="String">The JSON string to eval.</param>
    /// <param name="secure" type="Boolean" optional="true">
    ///     True if the method should perform JSON conformance checks before evaluating. False by default.
    /// </param>
    /// <returns>The results of eval applied to data.</returns>
    
    if (data.length === 0) throw Error.argument('data', Sys.Res.cannotDeserializeEmptyString);
    // DevDiv 41127: Never confuse atlas serialized strings with dates.
    // DevDiv 74430: JavasciptSerializer will need to handle date time offset - following WCF design
    // serilzed dates might look like "\/Date(123)\/" or "\/Date(123A)" or "Date(123+4567)" or Date(123-4567)"
    // the regex escaped version of this pattern is \"\\/Date\(123(?:[a-zA-Z]|(?:\+|-)[0-9]{4})?\)\\/\"
    // but we must also do js escaping to put it in the string. Escape all \ with \\
    // Result: \\"\\\\/Date\\(123(?:[a-zA-Z]|(?:\\+|-)[0-9]{4})?\\)\\\\/\\"
    // The 123 can really be any number with an optional -, and we want to capture it.
    // Regex for that is: (-?[0-9]+)
    // Result: \\"\\\\/Date\\((-?[0-9]+)(?:[a-zA-Z]|(?:\\+|-)[0-9]{4})?\\)\\\\/\\"
    // We want to avoid replacing serialized strings that happen to contain this string as a substring.
    // We can do that by excluding matches that start with a slash \ since that means the first quote is escaped.
    // The first quote of a real date string will never be escaped and so will never be preceeded with \
    // So we want to add regex pattern (^|[^\\]) to the beginning, which means "beginning of string or anything but slash".
    // JS Escaped version: (^|[^\\\\])
    // Result: (^|[^\\\\])\\"\\\\/Date\\((-?[0-9]+)(?:[a-zA-Z]|(?:\\+|-)[0-9]{4})?\\)\\\\/\\"
    // Finally, the replace string is $1new Date($2). We must include $1 so we put back the potentially matched character we captured.

    try {    
        var exp = data.replace(Sys.Serialization.JavaScriptSerializer._dateRegEx, "$1new Date($2)");
        
        if (secure && Sys.Serialization.JavaScriptSerializer._jsonRegEx.test(
             exp.replace(Sys.Serialization.JavaScriptSerializer._jsonStringRegEx, ''))) throw null;

        return eval('(' + exp + ')');
    }
    catch (e) {
         throw Error.argument('data', Sys.Res.cannotDeserializeInvalidJson);
    }
}

// **********************************************
// ** MicrosoftAjaxComponentModel.jsa
// **********************************************

Type.registerNamespace('Sys.UI');

 
Sys.EventHandlerList = function() {
    /// <summary>The EventHandlerList class contains a dictionary of multicast events.</summary>
    this._list = {};
}

Sys.EventHandlerList.prototype = {
    _addHandler: function(id, handler) {
        Array.add(this._getEvent(id, true), handler);
    },
    addHandler: function(id, handler) {
        /// <summary>The addHandler method adds a handler to the event identified by id.</summary>
        /// <param name="id" type="String">The identifier for the event.</param>
        /// <param name="handler" type="Function">The handler to add to the event.</param>
        this._addHandler(id, handler);
    },
    _removeHandler: function(id, handler) {
        var evt = this._getEvent(id);
        if (!evt) return;
        Array.remove(evt, handler);
    },
    removeHandler: function(id, handler) {
        /// <summary>The removeHandler method removes a handler to the event identified by id.</summary>
        /// <param name="id" type="String">The identifier for the event.</param>
        /// <param name="handler" type="Function">The handler to remove from the event.</param>
        this._removeHandler(id, handler);
    },
    getHandler: function(id) {
        /// <summary>
        ///     The getHandler method returns a single function that will call all
        ///     handlers sequentially for the specified event.
        /// </summary>
        /// <param name="id" type="String">The identifier for the event.</param>
        /// <returns type="Function">A function that will call each handler sequentially.</returns>
        var evt = this._getEvent(id);
        if (!evt || (evt.length === 0)) return null;
        evt = Array.clone(evt);
        return function(source, args) {
            for (var i = 0, l = evt.length; i < l; i++) {
                evt[i](source, args);
            }
        };
    },
    _getEvent: function(id, create) {
        if (!this._list[id]) {
            if (!create) return null;
            this._list[id] = [];
        }
        return this._list[id];
    }
}
Sys.EventHandlerList.registerClass('Sys.EventHandlerList');
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
 
Sys.INotifyPropertyChange = function() {
    /// <summary>Implement this interface to become a provider of property change notifications.</summary>
    throw Error.notImplemented();
}
Sys.INotifyPropertyChange.prototype = {
    add_propertyChanged: function(handler) {
        throw Error.notImplemented();
    },
    remove_propertyChanged: function(handler) {
        throw Error.notImplemented();
    }
}
Sys.INotifyPropertyChange.registerInterface('Sys.INotifyPropertyChange');
 
Sys.PropertyChangedEventArgs = function(propertyName) {
    /// <summary>Describes property changes.</summary>
    /// <param name="propertyName" type="String">The name of the property that changed.</param>
    Sys.PropertyChangedEventArgs.initializeBase(this);
    this._propertyName = propertyName;
}
 Sys.PropertyChangedEventArgs.prototype = {
    get_propertyName: function() {
        /// <value type="String">The name of the property that changed.</value>
        return this._propertyName;
    }
}
Sys.PropertyChangedEventArgs.registerClass('Sys.PropertyChangedEventArgs', Sys.EventArgs);
 
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


// **********************************************
// ** MicrosoftAjaxHistory.jsa
// **********************************************

Sys.HistoryEventArgs = function(state) {
    /// <param name="state" type="Object"/>
    Sys.HistoryEventArgs.initializeBase(this);
    this._state = state;
}
Sys.HistoryEventArgs.prototype = {
    get_state: function() {
        /// <value type="Object"/>
        return this._state;
    }
}
Sys.HistoryEventArgs.registerClass('Sys.HistoryEventArgs', Sys.EventArgs);

// History fields ("entry" in the code always designates a string representation of the state):

Sys.Application._appLoadHandler = null;
Sys.Application._beginRequestHandler = null;
Sys.Application._clientId = null;
Sys.Application._currentEntry = '';
Sys.Application._endRequestHandler = null;
Sys.Application._history = null;
Sys.Application._enableHistory = false;
Sys.Application._historyEnabledInScriptManager = false;
Sys.Application._historyFrame = null;
Sys.Application._historyInitialized = false;
Sys.Application._historyPointIsNew = false;
Sys.Application._ignoreTimer = false;
Sys.Application._initialState = null;
Sys.Application._state = {};
Sys.Application._timerCookie = 0;
Sys.Application._timerHandler = null;
Sys.Application._uniqueId = null;

// Add methods to support history

Sys._Application.prototype.get_stateString = function() {
    /// <value type="String" locid="P:J#Sys.Application.stateString"/>
    var hash = null;
    
    // Need to do an additional check because of a bug in Firefox
    // (https://bugzilla.mozilla.org/show_bug.cgi?id=378962).
    // Firefox incorrectly calls decodeURIComponent on window.hash
    // before returning it.
    if (Sys.Browser.agent === Sys.Browser.Firefox) {
        var href = window.location.href;
        var hashIndex = href.indexOf('#');
        if (hashIndex !== -1) {
            hash = href.substring(hashIndex + 1);
        }
        else {
            hash = "";
        }
        return hash;
    }
    else {
        hash = window.location.hash;
    }
    
    if ((hash.length > 0) && (hash.charAt(0) === '#')) {
        hash = hash.substring(1);
    }

    return hash;
};

Sys._Application.prototype.get_enableHistory = function() {
    /// <value type="Boolean" locid="P:J#Sys.Application.enableHistory"/>
    return this._enableHistory;
};

Sys._Application.prototype.set_enableHistory = function(value) {
    if (this._initialized && !this._initializing) {
        throw Error.invalidOperation(Sys.Res.historyCannotEnableHistory);
    }
    else if (this._historyEnabledInScriptManager && !value) {
        throw Error.invalidOperation(Sys.Res.invalidHistorySettingCombination);
    }
    this._enableHistory = value;
};

Sys._Application.prototype.add_navigate = function(handler) {
    /// <summary locid="E:J#Sys.Application.navigate"/>
    /// <param name="handler" type="Function"></param>
    this.get_events().addHandler("navigate", handler);
};

Sys._Application.prototype.remove_navigate = function(handler) {
    /// <param name="handler" type="Function"></param>
    this.get_events().removeHandler("navigate", handler);
};

Sys._Application.prototype.addHistoryPoint = function(state, title) {
    /// <summary locid="M:J#Sys.Application.addHistoryPoint"/>
    /// <param name="state" type="Object">
    ///     A dictionary of state bits that will be added to the main state
    ///     to form the global state of the new history point.
    ///     The state must be a string dictionary. The application is responsible
    ///     for converting the state bits from and into the relevant types.
    /// </param>
    /// <param name="title" type="String" optional="true" mayBeNull="true">The title for the new history point.</param>
    if (!this._enableHistory) throw Error.invalidOperation(Sys.Res.historyCannotAddHistoryPointWithHistoryDisabled);
    for (var n in state) {
        var v = state[n];
        var t = typeof(v);
        if ((v !== null) && ((t === 'object') || (t === 'function') || (t === 'undefined'))) {
            throw Error.argument('state', Sys.Res.stateMustBeStringDictionary);
        }
    }
    this._ensureHistory();
    var initialState = this._state;
    for (var key in state) {
        var value = state[key];
        if (value === null) {
            if (typeof(initialState[key]) !== 'undefined') {
                delete initialState[key];
            }
        }
        else {
            initialState[key] = value;
        }
    }
    var entry = this._serializeState(initialState);
    this._historyPointIsNew = true;
    this._setState(entry, title);
    // Raising navigate even when creating a history point so that independant
    // observers such as PermaLink can get all state changes.
    this._raiseNavigate();
};

Sys._Application.prototype.setServerId = function(clientId, uniqueId) {
    /// <summary locid="M:J#Sys.Application.setServerId"/>
    /// <param name="clientId" type="String"/>
    /// <param name="uniqueId" type="String"/>
    this._clientId = clientId;
    this._uniqueId = uniqueId;
};

Sys._Application.prototype.setServerState = function(value) {
    /// <summary locid="M:J#Sys.Application.setServerState"/>
    /// <param name="value" type="String"/>
    this._ensureHistory();
    this._state.__s = value;
    this._updateHiddenField(value);
};

Sys._Application.prototype._deserializeState = function(entry) {
    // <summary>Deserializes a querystring to a string dictionary.</summary>
    // <param name="entry" type="String" mayBeNull="true">The serialized dictionary.</returns>
    // <returns type="Object">The deserialized string dictionary.</param>
    var result = {};
    entry = entry || '';
    // Extract the server part (everything after &&)
    var serverSeparator = entry.indexOf('&&');
    if ((serverSeparator !== -1) && (serverSeparator + 2 < entry.length)) {
        result.__s = entry.substr(serverSeparator + 2);
        entry = entry.substr(0, serverSeparator);
    }
    // Deserialize the client part
    var tokens = entry.split('&');
    for (var i = 0, l = tokens.length; i < l; i++) {
        var token = tokens[i];
        var equal = token.indexOf('=');
        if ((equal !== -1) && (equal + 1 < token.length)) {
            var name = token.substr(0, equal);
            var value = token.substr(equal + 1);
            result[name] = decodeURIComponent(value);
        }
    }
    return result;
};

Sys._Application.prototype._enableHistoryInScriptManager = function() {
    this._enableHistory = true;
    this._historyEnabledInScriptManager = true;
};

Sys._Application.prototype._ensureHistory = function() {
    if (!this._historyInitialized && this._enableHistory) {
        if ((Sys.Browser.agent === Sys.Browser.InternetExplorer) && 
            ((!document.documentMode) || document.documentMode < 8)) {
            this._historyFrame = document.getElementById('__historyFrame');
            if (!this._historyFrame) throw Error.invalidOperation(Sys.Res.historyMissingFrame);
            this._ignoreIFrame = true;
        }
        this._timerHandler = Function.createDelegate(this, this._onIdle);
        this._timerCookie = window.setTimeout(this._timerHandler, 100);
        
        try {
            this._initialState = this._deserializeState(this.get_stateString());
        } catch(e) {}
        
        this._historyInitialized = true;
    }
};

Sys._Application.prototype._navigate = function(entry) {
    this._ensureHistory();

    var state = this._deserializeState(entry);
    
    if (this._uniqueId) {
        var oldServerEntry = this._state.__s || '';
        var newServerEntry = state.__s || '';
        if (newServerEntry !== oldServerEntry) {
            this._updateHiddenField(newServerEntry);
            __doPostBack(this._uniqueId, newServerEntry);
            this._state = state;
            return;
        }
    }
    this._setState(entry);
    this._state = state;
    this._raiseNavigate();
};

Sys._Application.prototype._onIdle = function() {
    delete this._timerCookie;
    
    var entry = this.get_stateString();
    if (entry !== this._currentEntry) {
        if (!this._ignoreTimer) {
            this._historyPointIsNew = false;
            this._navigate(entry);
        }
    }
    else {
        this._ignoreTimer = false;
    }
    this._timerCookie = window.setTimeout(this._timerHandler, 100);
};

// devdiv 617545: Ajax history iframe rendered in IE8+ after .NET 4.5 installed
// the fix of DevDiv 13920 enables iframe rendering on server side for IE8+ 
// as the latter could be in a lower document mode. On client side, 
// in case documentMode >= 8, make sure that the _onIframeLoad handler does nothing
// Note document.documentMode for IE7 is undefined 
Sys._Application.prototype._onIFrameLoad = function(entry) {
    if ((!document.documentMode) || document.documentMode < 8 ) {
        this._ensureHistory();
        if (!this._ignoreIFrame) {
            this._historyPointIsNew = false;
            this._navigate(entry);
        }
        this._ignoreIFrame = false;
    }
};

Sys._Application.prototype._onPageRequestManagerBeginRequest = function(sender, args) {
    this._ignoreTimer = true;
    this._originalTitle = document.title;
};

Sys._Application.prototype._onPageRequestManagerEndRequest = function(sender, args) {
    var dataItem = args.get_dataItems()[this._clientId];
    var originalTitle = this._originalTitle;
    this._originalTitle = null;

    // Reset event target if set by history
    var eventTarget = document.getElementById("__EVENTTARGET");
    if (eventTarget && eventTarget.value === this._uniqueId) {
        eventTarget.value = '';
    }
    if (typeof(dataItem) !== 'undefined') {
        this.setServerState(dataItem);
        this._historyPointIsNew = true;
    }
    else {
        this._ignoreTimer = false;
    }
    var entry = this._serializeState(this._state);
    if (entry !== this._currentEntry) {
        this._ignoreTimer = true;
        if (typeof(originalTitle) === "string") {
            // At this point the PRM has already updated the document title.
            // We want the title of the target entry will be the new page title
            // but since the title has been set, it has been applied to the current
            // entry. Temporarily restoring the old title while setting the hash.
            // However, this has the opposite effect in IE7.
            if (Sys.Browser.agent !== Sys.Browser.InternetExplorer || Sys.Browser.version > 7) {
                var newTitle = document.title;
                document.title = originalTitle;
                this._setState(entry);
                document.title = newTitle;
            }
            else {
                this._setState(entry);
            }
            this._raiseNavigate();
        }
        else {
            this._setState(entry);
            this._raiseNavigate();
        }
    }
};

Sys._Application.prototype._raiseNavigate = function() {
    var isNew = this._historyPointIsNew;
    var h = this.get_events().getHandler("navigate");
    var stateClone = {};
    for (var key in this._state) {
        if (key !== '__s') {
            stateClone[key] = this._state[key];
        }
    }
    var args = new Sys.HistoryEventArgs(stateClone);
    if (h) {
        h(this, args);
    }
    // Dev10 475308 
    // History: Firefox 3.0: Entries on History dropdown button are shifted after navigation
    // The entry in the history dropdown ends up with the title of the page you came from rather than
    // the title of the page you navigated to. This is just how it works. In order for the title
    // of the entry in the dropdown to be correct, the title must be set before the hash is changed,
    // which obviously is not possible since we only set the title in response to the hash changing.
    // So, we re-do another hash change, to force FF to re-think the title of the entry. Since the value
    // is the same, it doesn't actually cause a navigation. 
    // The only problem is that doing this causes a page refresh if there isn't a hash at all.
    // There is no other known workaround for this problem in that scenario, so this bug remains 
    // when you navigate from a certain state to one with no hash. Setting a blank hash like "#" 
    // makes things worse as it is considered a new state and resets the history.
    // See Mozilla bug: https://bugzilla.mozilla.org/show_bug.cgi?id=442060
    // Dev10 622219: don't use the go() workaround if the document is an iframe and the top window
    // does not have a hash, because that will cause the page to refresh.
    // If the top document is in a different domain we can't detect that due to the same origin policy,
    // so the try/catch prevents an error. In that scenario it is the best we can do.
    if (!isNew) {
        // !isNew means we raised the navigate event because the user navigated with the browser,
        // not because a history point was added. In this scenario, the history point is navigated
        // to first, then the title is changed in reaction. In FF that causes the history point navigated
        // to to get the title of the page we were on before. Applying this hack fixes it. Unfortunately
        // the hack required varies in 3.0 and 3.5.
        var err;
        try {
            if ((Sys.Browser.agent === Sys.Browser.Firefox) && window.location.hash &&
                (!window.frameElement || window.top.location.hash)) {
                (Sys.Browser.version < 3.5) ?
                    window.history.go(0) :
                    location.hash = this.get_stateString();
            }
        }
        catch(err) {
        }
    }
};

Sys._Application.prototype._serializeState = function(state) {
    // <summary>Serializes a string dictionary to a querystring form.</summary>
    // <param type="Object" name="state">The dictionary to serialize.</param>
    // <returns type="String">The serialized dictionary.</returns>
    var serialized = [];
    for (var key in state) {
        var value = state[key];
        if (key === '__s') {
            var serverState = value;
        }
        else {
            if (key.indexOf('=') !== -1) throw Error.argument('state', Sys.Res.stateFieldNameInvalid);
            serialized[serialized.length] = key + '=' + encodeURIComponent(value);
        }
    }
    return serialized.join('&') + (serverState ? '&&' + serverState : '');
};

Sys._Application.prototype._setState = function(entry, title) {
    if (this._enableHistory) {
        entry = entry || '';
        if (entry !== this._currentEntry) {
            // Replace the hash on the current ASP.NET form if it exists
            if (window.theForm) {
                var action = window.theForm.action;
                var hashIndex = action.indexOf('#');
                window.theForm.action = ((hashIndex !== -1) ? action.substring(0, hashIndex) : action) + '#' + entry;
            }
        
            if (this._historyFrame && this._historyPointIsNew) {
                // need to HTML-encode the document title
                var newDiv = document.createElement("div");
                newDiv.appendChild(document.createTextNode(title || document.title));
                var htmlEncodedTitle = newDiv.innerHTML;

                this._ignoreIFrame = true;
                var frameDoc = this._historyFrame.contentWindow.document;
                frameDoc.open("javascript:'<html></html>'");
                frameDoc.write("<html><head><title>" + htmlEncodedTitle +
                    "</title><scri" + "pt type=\"text/javascript\">parent.Sys.Application._onIFrameLoad(" + 
                    Sys.Serialization.JavaScriptSerializer.serialize(entry) +
                    ");</scri" + "pt></head><body></body></html>");
                frameDoc.close();
            }
            this._ignoreTimer = false;
            this._currentEntry = entry;
            if (this._historyFrame || this._historyPointIsNew) {
                // Dev 10 Bug: 594690
                // dont set the hash unless this is a new history point, since that's pointless, and it
                // causes an occassional loss of forward state in IE8.
                // unless the iframe is in use, we need to set it to keep the hash in sync with it.
                var currentHash = this.get_stateString();
                if (entry !== currentHash) {
                    // Check length before setting it
                    var loc = document.location;
                    if (loc.href.length - loc.hash.length + entry.length > 2048) {
                        throw Error.invalidOperation(String.format(Sys.Res.urlTooLong, 2048));
                    }
                    window.location.hash = entry;
                    // reset currentEntry to the statestring in case the browser has
                    // encoded any characters in it
                    this._currentEntry = this.get_stateString();
                    if ((typeof(title) !== 'undefined') && (title !== null)) {
                        document.title = title;
                    }
                }
            }
            this._historyPointIsNew = false;
        }
    }
};

Sys._Application.prototype._updateHiddenField = function(value) {
    if (this._clientId) {
        var serverStateField = document.getElementById(this._clientId);
        if (serverStateField) {
            serverStateField.value = value;
        }
    }
};


// **********************************************
// ** MicrosoftAjaxNetwork.jsa
// **********************************************

 
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
        if (this.get_started()) {
            throw Error.invalidOperation(String.format(Sys.Res.cannotCallOnceStarted, 'set_webRequest'));
        }

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
                // DevDiv Bugs 148214: Use try/finally to ensure cleanup occurs even
                // if the completed callback causes an exception (such as with async
                // postbacks where a server-side exception occurred)
                _this._webRequest.completed(Sys.EventArgs.Empty);
                if (_this._xmlHttpRequest != null) {
                    _this._xmlHttpRequest.onreadystatechange = Function.emptyMethod;
                    _this._xmlHttpRequest = null;
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

        if (this._started) {
            throw Error.invalidOperation(String.format(Sys.Res.cannotCallOnceStarted, 'executeRequest'));
        }
        if (this._webRequest === null) {
            throw Error.invalidOperation(Sys.Res.nullWebRequest);
        }

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
        if (!this._responseAvailable) {
            throw Error.invalidOperation(String.format(Sys.Res.cannotCallBeforeResponse, 'getResponseHeader'));
        }
        if (!this._xmlHttpRequest) {
            throw Error.invalidOperation(String.format(Sys.Res.cannotCallOutsideHandler, 'getResponseHeader'));
        }

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
        if (!this._responseAvailable) {
            throw Error.invalidOperation(String.format(Sys.Res.cannotCallBeforeResponse, 'getAllResponseHeaders'));
        }
        if (!this._xmlHttpRequest) {
            throw Error.invalidOperation(String.format(Sys.Res.cannotCallOutsideHandler, 'getAllResponseHeaders'));
        }

        return this._xmlHttpRequest.getAllResponseHeaders();
    },

    get_responseData: function() {
        /// <summary>Returns the response data.</summary>
        /// <value type="String">The text of the response.</value>
        if (!this._responseAvailable) {
            throw Error.invalidOperation(String.format(Sys.Res.cannotCallBeforeResponse, 'get_responseData'));
        }
        if (!this._xmlHttpRequest) {
            throw Error.invalidOperation(String.format(Sys.Res.cannotCallOutsideHandler, 'get_responseData'));
        }

        return this._xmlHttpRequest.responseText;
    },

    get_statusCode: function() {
        /// <summary>Returns the status code for the response.</summary>
        /// <value type="Number">The status code of the response.</value>
        if (!this._responseAvailable) {
            throw Error.invalidOperation(String.format(Sys.Res.cannotCallBeforeResponse, 'get_statusCode'));
        }
        if (!this._xmlHttpRequest) {
            throw Error.invalidOperation(String.format(Sys.Res.cannotCallOutsideHandler, 'get_statusCode'));
        }
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
        if (!this._responseAvailable) {
            throw Error.invalidOperation(String.format(Sys.Res.cannotCallBeforeResponse, 'get_statusText'));
        }
        if (!this._xmlHttpRequest) {
            throw Error.invalidOperation(String.format(Sys.Res.cannotCallOutsideHandler, 'get_statusText'));
        }

        return this._xmlHttpRequest.statusText;
    },

    get_xml: function() {
        /// <summary>Returns the response in xml format.</summary>
        /// <value>The response in xml format.</value>
        if (!this._responseAvailable) {
            throw Error.invalidOperation(String.format(Sys.Res.cannotCallBeforeResponse, 'get_xml'));
        }
        if (!this._xmlHttpRequest) {
            throw Error.invalidOperation(String.format(Sys.Res.cannotCallOutsideHandler, 'get_xml'));
        }

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
        if (!this._started) {
            throw Error.invalidOperation(Sys.Res.cannotAbortBeforeStart);
        }

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
        if (value < 0) {
            throw Error.argumentOutOfRange("value", value, Sys.Res.invalidTimeout);
        }

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

            if (failed  || !Sys.Net.WebRequestExecutor.isInstanceOfType(executor) || !executor) {
                throw Error.argument("defaultExecutorType", String.format(Sys.Res.invalidExecutorType, this._defaultExecutorType));
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
        if (value.length === 0) {
            throw Error.argument('value', Sys.Res.invalidHttpVerb);
        }

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
        if (this._executor !== null && this._executor.get_started()) {
            throw Error.invalidOperation(Sys.Res.setExecutorAfterActive);
        }

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
        if (value < 0) {
            throw Error.argumentOutOfRange("value", value, Sys.Res.invalidTimeout);
        }

        this._timeout = value;
    },

    getResolvedUrl: function() {
        /// <summary>The getResolvedUrl method returns the url resolved against the base url of the page if set.</summary>
        /// <returns type="String">The resolved url for the request.</returns>
        return Sys.Net.WebRequest._resolveUrl(this._url);
    },

    invoke: function() {
        /// <summary>Invokes the request</summary>
        if (this._invokeCalled) {
            throw Error.invalidOperation(Sys.Res.invokeCalledTwice);
        }

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
        if (slashslash === -1) {
            throw Error.argument("baseUrl", Sys.Res.badBaseUrl1);
        }

        var nextSlash = baseUrl.indexOf('/', slashslash + 3);
        if (nextSlash === -1) {
            throw Error.argument("baseUrl", Sys.Res.badBaseUrl2);
        }

        return baseUrl.substr(0, nextSlash) + url;
    }
    // Otherwise for relative urls we just combine with the base url stripping off the last path component (filename typically)
    // Note the app path always contains a trailing slash so when resolving app paths, we never strip off anything important
    else {
        var lastSlash = baseUrl.lastIndexOf('/');
        if (lastSlash === -1) {
            throw Error.argument("baseUrl", Sys.Res.badBaseUrl3);
        }

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
        // DevDiv Bugs 146327: In debug mode, report useful error message for pages without <head> element
        var headElements = document.getElementsByTagName('head');
        if (headElements.length === 0) {
             throw new Error.invalidOperation(Sys.Res.scriptLoadFailedNoHead);
        }
        else {
             headElements[0].appendChild(this._scriptElement);
        }
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



// **********************************************
// ** MicrosoftAjaxWebServices.jsa
// **********************************************

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
                else {
                    if (data.StackTrace && data.Message) {
                        error = data.StackTrace + "-- " + data.Message;
                    }
                    else {
                        error = data.StackTrace || data.Message;
                    }
                    error = String.format(error ? Sys.Res.webServiceFailed : Sys.Res.webServiceFailedNoMsg, methodName, error);
                    throw Sys.Net.WebServiceProxy._createFailedError(methodName, String.format(Sys.Res.webServiceFailed, methodName, error));
                }
            }
            else if (onSuccess) {
                onSuccess(data, userContext, methodName);
            }
        }
        Sys[tempCallback] = jsonpComplete;
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
                else {
                    // In debug mode, if no error was registered, display some trace information
                    if (result && errorObj) {
                        // If we got a result, we're likely dealing with an error in the method itself
                        error = result.get_exceptionType() + "-- " + result.get_message();
                    }
                    else {
                        // Otherwise, it's probably a 'top-level' error, in which case we dump the
                        // whole response in the trace
                        error = response.get_responseData();
                    }
                    // DevDiv 89485: throw, not alert()
                    throw Sys.Net.WebServiceProxy._createFailedError(methodName, String.format(Sys.Res.webServiceFailed, methodName, error));
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
            else {
                // In debug mode, if no error was registered, display some trace information
                // DevDiv 89485: throw, don't alert()
                throw Sys.Net.WebServiceProxy._createFailedError(methodName, msg);
            }
        }
    }

    return request;
}

Sys.Net.WebServiceProxy._createFailedError = function(methodName, errorMessage) {
    var displayMessage = "Sys.Net.WebServiceFailedException: " + errorMessage;
    var e = Error.create(displayMessage, { 'name': 'Sys.Net.WebServiceFailedException', 'methodName': methodName });
    e.popStackFrame();
    return e;
}

Sys.Net.WebServiceProxy._defaultFailedCallback = function(err, methodName) {
    var error = err.get_exceptionType() + "-- " + err.get_message();
    throw Sys.Net.WebServiceProxy._createFailedError(methodName, String.format(Sys.Res.webServiceFailed, methodName, error));
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

// Permalink temporarily removed
// #include "Sys\UI\Controls\PermaLink.js"

// TEST COMMENT REMOVAL -- THIS COMMENT SHOULD NOT BE PRESENT IN THE
// ASSEMBLY RESOURCE STREAM
/*
   TEST COMMENT REMOVAL -- THIS COMMENT SHOULD NOT BE PRESENT IN THE
   ASSEMBLY RESOURCE STREAM
*/

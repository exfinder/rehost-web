//!-----------------------------------------------------------------------
//! Copyright (C) Microsoft Corporation. All rights reserved.
//!-----------------------------------------------------------------------
//! MicrosoftAjaxCore.js
//! Microsoft AJAX Framework Core Type System and Extensions.

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

        // This test is just done to break a potential infinite loop for invalid format strings.
        // The code here is minimal because this is an error condition in debug mode anyway.
        if (close < 0) break;

        // Find the closing brace

        // Get the string between the braces, and split it around the ':' (if any)
        var brace = format.substring(i, close);
        var colonIndex = brace.indexOf(':');
        var argNumber = parseInt((colonIndex < 0)? brace : brace.substring(0, colonIndex), 10) + 1;
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

Type.prototype.callBaseMethod = function(instance, name, baseArguments) {
    /// <param name="instance">The instance for the base method. Usually 'this'.</param>
    /// <param name="name" type="String">The name of the base method.</param>
    /// <param name="baseArguments" type="Array" optional="true" mayBeNull="true" elementMayBeNull="true">
    ///     The arguments to pass to the base method.
    /// </param>
    /// <returns>The return value of the base method.</returns>
    var baseMethod = Sys._getBaseMethod(this, instance, name);
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
        for (var i = 2, l = arguments.length; i < l; i++) {
            var interfaceType = arguments[i];
            this.__interfaces.push(interfaceType);
        }
    }

    return this;
}

Type.prototype.registerInterface = function(typeName) {
    /// <summary>Registers an interface (represented by its ctor function).</summary>
    /// <param name="typeName" type="String">The fully-qualified name of the interface.</param>
    /// <returns type="Type">The registered interface.</returns>
    // Saving a case-insensitive index of the registered types on each namespace
    Sys.__upperCaseTypes[typeName.toUpperCase()] = this;

    this.prototype.constructor = this;
    this.__typeName = typeName;
    this.__interface = true;

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
        Type.__htClasses[typeName] = fn;
    }
    return fn;
}

Type.registerNamespace = function(namespacePath) {
    /// <summary>Creates a namespace.</summary>
    /// <param name="namespacePath" type="String">The full path of the namespace.</param>
    var rootObject = window;
    var namespaceParts = namespacePath.split('.');

    for (var i = 0; i < namespaceParts.length; i++) {
        var currentPart = namespaceParts[i];
        var ns = rootObject[currentPart];
        if (!ns) {
            ns = rootObject[currentPart] = {};
        }
        if (!ns.__namespace) {
            if ((i === 0) && (namespacePath !== "Sys")) {
                Sys.__rootNamespaces[Sys.__rootNamespaces.length] = ns;
            }
            ns.__namespace = true;
            ns.__typeName = namespaceParts.slice(0, i + 1).join('.');
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

Type.registerNamespace("Sys");
Sys.__upperCaseTypes = {};
Sys.__rootNamespaces = [Sys];

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
    return eval(value);
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

Type._registerScript("MicrosoftAjaxCore.js");

 
Sys.IDisposable = function() {
}
Sys.IDisposable.prototype = {
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

// Base Class Library

 
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
    Sys.Debug.isDebug = false;
 
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
    return '';
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

 
Sys.INotifyPropertyChange = function() {
    /// <summary>Implement this interface to become a provider of property change notifications.</summary>
}
Sys.INotifyPropertyChange.prototype = {
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
Sys.Observer = function() {
}
Sys.Observer.registerClass("Sys.Observer");

Sys.Observer.makeObservable = function(target) {
    /// <summary>Makes an object directly observable by adding observable methods to it.</summary>
    /// <param name="target" mayBeNull="false">The object, array, or DOM element to make observable.</param>
    /// <returns>The observable object.</returns>
    var isArray = target instanceof Array,
        o = Sys.Observer;
    if (target.setValue === o._observeMethods.setValue) return target;
    o._addMethods(target, o._observeMethods);
    if (isArray) {
        o._addMethods(target, o._arrayMethods);
    }
    return target;
}
Sys.Observer._addMethods = function(target, methods) {
    for (var m in methods) {
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
    Sys.Observer._removeEventHandler(target, eventName, handler);
}
Sys.Observer.raiseEvent = function(target, eventName, eventArgs) {
    /// <summary>Raises an observable event on the target.</summary>
    /// <param name="target"></param>
    /// <param name="eventName" type="String"></param>
    /// <param name="eventArgs" type="Sys.EventArgs"></param>
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
    Sys.Observer._addEventHandler(target, "propertyChanged", handler);
}
Sys.Observer.removePropertyChanged = function(target, handler) {
    /// <summary>Removes a propertyChanged event handler from the target.</summary>
    /// <param name="target" mayBeNull="false">The object to observe.</param>
    /// <param name="handler" type="Function">The event handler.</param>
    Sys.Observer._removeEventHandler(target, "propertyChanged", handler);
}
Sys.Observer.beginUpdate = function(target) {
    /// <summary></summary>
    /// <param name="target" mayBeNull="false"></param>
    Sys.Observer._getContext(target, true).updating = true;
}
Sys.Observer.endUpdate = function(target) {
    /// <summary></summary>
    /// <param name="target" mayBeNull="false"></param>
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

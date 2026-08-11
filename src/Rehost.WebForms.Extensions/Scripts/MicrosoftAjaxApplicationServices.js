//!-----------------------------------------------------------------------
//! Copyright (C) Microsoft Corporation. All rights reserved.
//!-----------------------------------------------------------------------
//! MicrosoftAjaxApplicationServices.js
//! Microsoft AJAX Framework Support for ASP.NET Application Services.

//!/ <reference name="MicrosoftAjaxWebServices.js" />

Type._registerScript("MicrosoftAjaxApplicationServices.js", ["MicrosoftAjaxWebServices.js"]);

Type.registerNamespace('Sys.Services');


Sys.Services._ProfileService = function() {
    /// <summary locid="M:J#Sys.Net.ProfileService.#ctor"/>
    Sys.Services._ProfileService.initializeBase(this);
    this.properties = {};
}
Sys.Services._ProfileService.DefaultWebServicePath = '';

Sys.Services._ProfileService.prototype = {
    _defaultLoadCompletedCallback: null,
    _defaultSaveCompletedCallback: null,
    _path: '',
    _timeout: 0,

    get_defaultLoadCompletedCallback: function() {
        /// <value type="Function" mayBeNull="true" locid="P:J#Sys.Services.ProfileService.defaultLoadCompletedCallback">
        /// Default callback to call when loading is complete.</value>
        return this._defaultLoadCompletedCallback;
    },
    set_defaultLoadCompletedCallback: function(value) {
        this._defaultLoadCompletedCallback = value;
    },

    get_defaultSaveCompletedCallback: function() {
        /// <value type="Function" mayBeNull="true" locid="P:J#Sys.Services.ProfileService.defaultSaveCompletedCallback">
        /// Default callback to call when saving is complete.</value>
        return this._defaultSaveCompletedCallback;
    },
    set_defaultSaveCompletedCallback: function(value) {
        this._defaultSaveCompletedCallback = value;
    },
    
    get_path: function() {
        /// <value type="String" mayBeNull="true" locid="P:J#Sys.Services.ProfileService.path">
        /// Path to a profile webservice.</value>    
        // override from base to ensure returned value is '' even if usercode sets to null.
        // also refactored from v1 to ensure empty string on getter instead of setter.
        return this._path || '';
    },
        
    load: function(propertyNames, loadCompletedCallback, failedCallback, userContext) {
        /// <summary locid="M:J#Sys.Services.ProfileService.load"/>
        /// <param name="propertyNames" type="Array" elementType="String" optional="true" elementMayBeNull="false" mayBeNull="true">A string array of profile properties to load.</param>
        /// <param name="loadCompletedCallback" type="Function" optional="true" mayBeNull="true">Callback to call when loading has completed.</param>
        /// <param name="failedCallback" type="Function" optional="true" mayBeNull="true">Callback to call if loading fails.</param>
        /// <param name="userContext" optional="true" mayBeNull="true">Custom context passed to the completed or failed callback.</param>
        var parameters;
        var methodName;
        if (!propertyNames) {
            methodName = "GetAllPropertiesForCurrentUser";
            parameters = { authenticatedUserOnly: false };
        }
        else {
            methodName = "GetPropertiesForCurrentUser";
            parameters = { properties: this._clonePropertyNames(propertyNames), authenticatedUserOnly: false };
        }

        this._invoke(this._get_path(),
                                        methodName,
                                        false,
                                        parameters,
                                        Function.createDelegate(this, this._onLoadComplete),
                                        Function.createDelegate(this, this._onLoadFailed),
                                        [loadCompletedCallback, failedCallback, userContext]);
    },

    save: function(propertyNames, saveCompletedCallback, failedCallback, userContext) {
        /// <summary locid="M:J#Sys.Services.ProfileService.save"/>
        /// <param name="propertyNames" type="Array" elementType="String" optional="true" elementMayBeNull="false" mayBeNull="true">A string array of profile properties to save.</param>
        /// <param name="saveCompletedCallback" type="Function" optional="true" mayBeNull="true">Callback to call when saving has completed.</param>
        /// <param name="failedCallback" type="Function" optional="true" mayBeNull="true">Callback to call if saving fails.</param>
        /// <param name="userContext" optional="true" mayBeNull="true">Custom context passed to the completed or failed callback.</param>
        var flattenedProperties = this._flattenProperties(propertyNames, this.properties);
        this._invoke(this._get_path(),
                                        "SetPropertiesForCurrentUser",
                                        false,
                                        { values: flattenedProperties.value, authenticatedUserOnly: false },
                                        Function.createDelegate(this, this._onSaveComplete),
                                        Function.createDelegate(this, this._onSaveFailed),
                                        [saveCompletedCallback, failedCallback, userContext, flattenedProperties.count]);
    },
    
    // DevDiv 31283: calling load with two of the same property names throws an error, so we strip dups
    _clonePropertyNames: function(arr) {
        var nodups = [];
        var seen = {};
        for (var i=0; i < arr.length; i++) {
            var prop = arr[i];
            if(!seen[prop]) { Array.add(nodups, prop); seen[prop]=true; };
        }
        return nodups;
    },    

    // convert properties like properties.ProfileGroup.ProfileSetting to properties["ProfileGroup.ProfileSetting"].
    // propertyNames: list of properties that should be included in the flattened list (others are excluded)
    // properties: object containing properties to flatten
    // groupName: current group name used for recursion
    _flattenProperties: function(/*string[]*/propertyNames, properties, groupName) {
        var flattenedProperties = {};
        var val;
        var key;
        var count = 0;
        if (propertyNames && propertyNames.length === 0) {
            return { value: flattenedProperties, count: 0 };
        }

        for (var property in properties) {
            val = properties[property];
            key = groupName ? groupName + "." + property : property;
            // is it a property group?
            if(Sys.Services.ProfileGroup.isInstanceOfType(val)) {
                var obj = this._flattenProperties(propertyNames, val, key);
                var groupProperties = obj.value;
                count += obj.count; // count all the group's properties we're about to merge in
                // merge in group's properties
                // NOTE: We don't use Array.addRange because flattenedProperties is not an Array.
                //       It can't be an array because it polutes the associative array and we need it to be purely properties.
                //       Array.prototype.addRange.apply() doesn't work either.
                // NOTE: In the case where a group exists but has no inner properties of its own, the for loop will short out
                //       and there will be no keys added to the collection, as expected.
                for(var subKey in groupProperties) {
                    var subVal = groupProperties[subKey];
                    flattenedProperties[subKey] = subVal;
                }
            }
            else {
                // is this a specified property (or use all properties)?
                if(!propertyNames || Array.indexOf(propertyNames, key) !== -1) {
                    flattenedProperties[key] = val;
                    count++; // keep track of how many properties are in the flattened dictionary
                }
            }
        }
        return { value: flattenedProperties, count: count };
    },
    
    _get_path: function() {
        var path = this.get_path();
        if (!path.length) {
            path = Sys.Services._ProfileService.DefaultWebServicePath;
        }
        if (!path || !path.length) {
            throw Error.invalidOperation(Sys.Res.servicePathNotSet);
        }
        return path;
    },    

    _onLoadComplete: function(result, context, methodName) {
        if (typeof(result) !== "object") {
            throw Error.invalidOperation(String.format(Sys.Res.webServiceInvalidReturnType, methodName, "Object"));
        }

        var unflattened = this._unflattenProperties(result);
        for (var name in unflattened) {
            this.properties[name] = unflattened[name];
        }
        
        var callback = context[0] || this.get_defaultLoadCompletedCallback() || this.get_defaultSucceededCallback();
        if (callback) {
            var userContext = context[2] || this.get_defaultUserContext();        
            callback(result.length, userContext, "Sys.Services.ProfileService.load");
        }
    },
    
    _onLoadFailed: function(err, context, methodName) {
        var callback = context[1] || this.get_defaultFailedCallback();
        if (callback) {
            var userContext = context[2] || this.get_defaultUserContext();        
            callback(err, userContext, "Sys.Services.ProfileService.load");
        }
    },
    
    _onSaveComplete: function(result, context, methodName) {
        // context[3] is the number of properties we sent to the server.
        var count = context[3];
        if (result !== null) { // dont use if(result), might be number 0
            if (result instanceof Array) {
                // result is a list of properties that failed. Subtract the count to get the # succeeded
                count -= result.length;
            }
            else if (typeof(result) === 'number') {
                // legacy server API -- the number of successful properties is returned directly
                count = result;
            }
            else {
                // no other types allowed
                throw Error.invalidOperation(String.format(Sys.Res.webServiceInvalidReturnType, methodName, "Array"));
            }
        }
        // else: if result is null, treat as an empty array (no failures)
        
        var callback = context[0] || this.get_defaultSaveCompletedCallback() || this.get_defaultSucceededCallback();
        if (callback) {
            var userContext = context[2] || this.get_defaultUserContext();
            callback(count, userContext, "Sys.Services.ProfileService.save");
        }
    },
    
    _onSaveFailed: function(err, context, methodName) {
        var callback = context[1] || this.get_defaultFailedCallback();
        if (callback) {
            var userContext = context[2] || this.get_defaultUserContext();
            callback(err, userContext, "Sys.Services.ProfileService.save");
        }
    },
    
    _unflattenProperties: function(properties) {
        var unflattenedProperties = {};
        var dotIndex;
        var val;
        var count = 0;
        for (var key in properties) {
            count++;
            val = properties[key];

            dotIndex = key.indexOf('.');
            if (dotIndex !== -1) {
                var groupName = key.substr(0, dotIndex);
                key = key.substr(dotIndex+1);
                var group = unflattenedProperties[groupName];
                if (!group || !Sys.Services.ProfileGroup.isInstanceOfType(group)) {
                    group = new Sys.Services.ProfileGroup();
                    unflattenedProperties[groupName] = group;
                }
                group[key] = val;
            }
            else {
                unflattenedProperties[key] = val;
            }
        }
        properties.length = count;
        return unflattenedProperties;
    }
}
Sys.Services._ProfileService.registerClass('Sys.Services._ProfileService', Sys.Net.WebServiceProxy);
Sys.Services.ProfileService = new Sys.Services._ProfileService();

Sys.Services.ProfileGroup = function(properties) {
    /// <param name="properties" optional="true" mayBeNull="true">An object containing the settings for this profile group.</param>
    if (properties) {
        for (var property in properties) {
            this[property] = properties[property];
        }
    }
}
Sys.Services.ProfileGroup.registerClass('Sys.Services.ProfileGroup');

Sys.Services._AuthenticationService = function() {
    /// <summary locid="M:J#Sys.Services.AuthenticationService.#ctor">A proxy to an authentication service.</summary>
    Sys.Services._AuthenticationService.initializeBase(this);
}
Sys.Services._AuthenticationService.DefaultWebServicePath = '';
Sys.Services._AuthenticationService.prototype = {
    _defaultLoginCompletedCallback: null,
    _defaultLogoutCompletedCallback: null,
    _path: '',
    _timeout: 0,
    _authenticated: false,
    
    get_defaultLoginCompletedCallback: function() {
        /// <value type="Function" mayBeNull="true" locid="P:J#Sys.Services.AuthenticationService.defaultLoginCompletedCallback">
        /// Default callback to call when login completes.</value>        
        return this._defaultLoginCompletedCallback;
    },
    set_defaultLoginCompletedCallback: function(value) {
        this._defaultLoginCompletedCallback = value;
    },

    get_defaultLogoutCompletedCallback: function() {
        /// <value type="Function" mayBeNull="true" locid="P:J#Sys.Services.AuthenticationService.defaultLogoutCompletedCallback">
        /// Default callback to call when logout completes.</value>        
        return this._defaultLogoutCompletedCallback;
    },
    set_defaultLogoutCompletedCallback: function(value) {
        this._defaultLogoutCompletedCallback = value;
    },

    get_isLoggedIn: function() {
        /// <value type="Boolean" locid="P:J#Sys.Services.AuthenticationService.isLoggedIn">True if the user is currently authenticated.</value>
        return this._authenticated;
    },

    get_path: function() {
        /// <value type="String" mayBeNull="true" locid="P:J#Sys.Services.AuthenticationService.path">Path to an authentication webservice.</value>    
        // override from base to ensure returned value is '' even if usercode sets to null.
        // also refactored from v1 to ensure empty string on getter instead of setter.
        return this._path || '';
    },  
    
    login: function(username, password, isPersistent, customInfo, redirectUrl, loginCompletedCallback, failedCallback, userContext) {
        /// <summary locid="M:J#Sys.Services.AuthenticationService.login"/>
        /// <param name="username" type="String" mayBeNull="false">The username to authenticate.</param>
        /// <param name="password" type="String" mayBeNull="true">The user's password.</param>
        /// <param name="isPersistent" type="Boolean" optional="true" mayBeNull="true">Whether the issued authentication ticket should be persistent across browser sessions.</param>        
        /// <param name="customInfo" type="String" optional="true" mayBeNull="true">Not used. Reserved for future use.</param>        
        /// <param name="redirectUrl" type="String" optional="true" mayBeNull="true">A URL to redirect the browser to upon successful authentication. If you omit the redirectUrl parameter or pass null, no redirect will occur.</param> 
        /// <param name="loginCompletedCallback" type="Function" optional="true" mayBeNull="true">Callback to be called when login has completed, success or fail.</param> 
        /// <param name="failedCallback" type="Function" optional="true" mayBeNull="true">Callback to be called if the authenticaiton webservice fails.</param> 
        /// <param name="userContext" optional="true" mayBeNull="true">Custom context passed to the completed or failed callback.</param> 
        // note: use of internal type here, but theres no other way
        this._invoke(this._get_path(), "Login", false,
                                        { userName: username, password: password, createPersistentCookie: isPersistent },
                                        Function.createDelegate(this, this._onLoginComplete),
                                        Function.createDelegate(this, this._onLoginFailed),
                                        [username, password, isPersistent, customInfo, redirectUrl, loginCompletedCallback, failedCallback, userContext]);
    },
    
    logout: function(redirectUrl, logoutCompletedCallback, failedCallback, userContext) {
        /// <summary locid="M:J#Sys.Services.AuthenticationService.logout">
        /// If you omit the redirectUrl parameter or pass null, no redirect will occur.</summary>
        /// <param name="redirectUrl" type="String" optional="true" mayBeNull="true">A URL to redirect the browser to upon successful logout. If you omit the redirectUrl parameter or pass null, a redirect occurs back to the current page.</param> 
        /// <param name="logoutCompletedCallback" type="Function" optional="true" mayBeNull="true">Callback to be called when logout has completed, success or fail.</param> 
        /// <param name="failedCallback" type="Function" optional="true" mayBeNull="true">Callback to be called if the authenticaiton webservice fails.</param> 
        /// <param name="userContext" optional="true" mayBeNull="true">Custom context passed to the completed or failed callback.</param> 
        // note: use of internal type here, but theres no other way
        this._invoke(this._get_path(), "Logout", false, {}, 
                                        Function.createDelegate(this, this._onLogoutComplete),
                                        Function.createDelegate(this, this._onLogoutFailed),
                                        [redirectUrl, logoutCompletedCallback, failedCallback, userContext]);
    },
    
    _get_path: function() {
        var path = this.get_path();
        if(!path.length) {
            path = Sys.Services._AuthenticationService.DefaultWebServicePath;
        }
        if(!path || !path.length) {
            throw Error.invalidOperation(Sys.Res.servicePathNotSet);
        }
        return path;
    },
    
    _onLoginComplete: function(result, /*login param list*/context, methodName) {
        if(typeof(result) !== "boolean") {
            throw Error.invalidOperation(String.format(Sys.Res.webServiceInvalidReturnType, methodName, "Boolean"));
        }
        
        var redirectUrl = context[4];
        var userContext = context[7] || this.get_defaultUserContext();
        var callback = context[5] || this.get_defaultLoginCompletedCallback() || this.get_defaultSucceededCallback();
        
        if(result) {
            this._authenticated = true;

            if (callback) {
                callback(true, userContext, "Sys.Services.AuthenticationService.login");
            }
            
            if (typeof(redirectUrl) !== "undefined" && redirectUrl !== null) {
                // url may be empty which is a valid link
                window.location.href = redirectUrl;
            }
        }
        else if (callback) {
            callback(false, userContext, "Sys.Services.AuthenticationService.login");
        }
    },
    
    _onLoginFailed: function(err, context, methodName) {
        var callback = context[6] || this.get_defaultFailedCallback();
        if (callback) {
            var userContext = context[7] || this.get_defaultUserContext();
            callback(err, userContext, "Sys.Services.AuthenticationService.login");
        }
    },
    
    _onLogoutComplete: function(result, context, methodName) {
        if(result !== null) {
            throw Error.invalidOperation(String.format(Sys.Res.webServiceInvalidReturnType, methodName, "null"));
        }
        
        var redirectUrl = context[0];
        var userContext = context[3] || this.get_defaultUserContext();
        var callback = context[1] || this.get_defaultLogoutCompletedCallback() || this.get_defaultSucceededCallback();

        this._authenticated = false;
        
        if (callback) {
            callback(null, userContext, "Sys.Services.AuthenticationService.logout");
        }
        
        // always redirect when logging out
        if(!redirectUrl) {
            window.location.reload();
        }
        else {
            window.location.href = redirectUrl;
        }
    },
    
    _onLogoutFailed: function(err, context, methodName) {
        var callback = context[2] || this.get_defaultFailedCallback();
        if (callback) {
            callback(err, context[3], "Sys.Services.AuthenticationService.logout");
        }
    },
    
    _setAuthenticated: function(authenticated) {
        this._authenticated = authenticated;
    }    
}

Sys.Services._AuthenticationService.registerClass('Sys.Services._AuthenticationService', Sys.Net.WebServiceProxy);
Sys.Services.AuthenticationService = new Sys.Services._AuthenticationService();

Sys.Services._RoleService = function() {
    /// <summary locid="M:J#Sys.Services.RoleService.#ctor"/>
    Sys.Services._RoleService.initializeBase(this);
    this._roles = [];
}
Sys.Services._RoleService.DefaultWebServicePath = '';
Sys.Services._RoleService.prototype = {
    _defaultLoadCompletedCallback: null,
    _rolesIndex: null,
    _timeout: 0,
    _path: '',

    get_defaultLoadCompletedCallback: function() {
        /// <value type="Function" mayBeNull="true" locid="P:J#Sys.Services.RoleService.defaultLoadCompletedCallback">
        /// Default callback to call when loading is complete.</value>
        return this._defaultLoadCompletedCallback;
    },
    set_defaultLoadCompletedCallback: function(value) {
        this._defaultLoadCompletedCallback = value;
    },
    
    get_path: function() {
        /// <value type="String" mayBeNull="true" locid="P:J#Sys.Services.RoleService.path">Path to a role webservice.</value>    
        // override from base to ensure returned value is '' even if usercode sets to null, consistent with other appservices in v1.
        return this._path || '';
    },

    get_roles: function() {
        /// <value type="Array" elementType="String" mayBeNull="false" locid="P:J#Sys.Services.RoleService.roles">
        /// An array of role names for the current user.</value>
        return Array.clone(this._roles);
    },

    isUserInRole: function(role) {
        /// <summary locid="M:J#Sys.Services.RoleService.isUserInRole">Gets a value indicating whether the current user is in the specified role.</summary>
        /// <param name="role" type="String" mayBeNull="false">The name of the role to search in.</param>
        /// <returns type="Boolean"/>
        var v = this._get_rolesIndex()[role.trim().toLowerCase()];
        return !!v;
    },
    
    load: function(loadCompletedCallback, failedCallback, userContext) {
        /// <summary locid="M:J#Sys.Services.RoleService.load"/>
        /// <param name="loadCompletedCallback" type="Function" optional="true" mayBeNull="true">Callback to call when loading has completed.</param>
        /// <param name="failedCallback" type="Function" optional="true" mayBeNull="true">Callback to call if loading fails.</param>
        /// <param name="userContext" optional="true" mayBeNull="true">Custom context passed to the completed or failed callback.</param>
        Sys.Net.WebServiceProxy.invoke(
                    this._get_path(),
                    "GetRolesForCurrentUser",
                    false,
                    {} /* no params*/,
                    Function.createDelegate(this, this._onLoadComplete),
                    Function.createDelegate(this, this._onLoadFailed),
                    [loadCompletedCallback, failedCallback, userContext],
                    this.get_timeout());
    },

    _get_path: function() {
        var path = this.get_path();
        if(!path || !path.length) {
            path = Sys.Services._RoleService.DefaultWebServicePath;
        }
        if(!path || !path.length) {
            throw Error.invalidOperation(Sys.Res.servicePathNotSet);
        }
        return path;
    },  
    
    _get_rolesIndex: function() {
        if (!this._rolesIndex) {
            var index = {};
            for(var i=0; i < this._roles.length; i++) {
                index[this._roles[i].toLowerCase()] = true;
            }
            this._rolesIndex = index;
        }
        return this._rolesIndex;
    },

    _onLoadComplete: function(result, context, methodName) {
        if(result && !(result instanceof Array)) {
            throw Error.invalidOperation(String.format(Sys.Res.webServiceInvalidReturnType, methodName, "Array"));
        }

        this._roles = result;
        this._rolesIndex = null;

        var callback = context[0] || this.get_defaultLoadCompletedCallback() || this.get_defaultSucceededCallback();
        if (callback) {
            var userContext = context[2] || this.get_defaultUserContext();
            var clonedResult = Array.clone(result);
            callback(clonedResult, userContext, "Sys.Services.RoleService.load");
        }
    },

    _onLoadFailed: function(err, context, methodName) {
        var callback = context[1] || this.get_defaultFailedCallback();
        if (callback) {
            var userContext = context[2] || this.get_defaultUserContext();
            callback(err, userContext, "Sys.Services.RoleService.load");
        }
    }
}

Sys.Services._RoleService.registerClass('Sys.Services._RoleService', Sys.Net.WebServiceProxy);
Sys.Services.RoleService = new Sys.Services._RoleService();

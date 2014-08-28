﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CefSharp.Internals
{
    public class JavascriptObjectRepository : DisposableResource
    {
        private static long lastId;
        private static readonly object Lock = new object();

        private readonly Dictionary<long, JavascriptObject> objects = new Dictionary<long, JavascriptObject>();
        public JavascriptRootObject RootObject { get; private set; }

        public JavascriptObjectRepository()
        {
            RootObject = new JavascriptRootObject();
        }

        internal JavascriptObject CreateJavascriptObject()
        {
            long id;
            lock (Lock)
            {
                id = lastId++;
            }
            var result = new JavascriptObject { Id = id };
            objects[id] = result;

            return result;
        }

        public void Register(string name, object value)
        {
            var jsObject = CreateJavascriptObject();
            jsObject.Value = value;
            jsObject.Name = name;
            jsObject.JavascriptName = LowercaseFirst(name);

            Analyse(jsObject);

            RootObject.MemberObjects.Add(jsObject);
        }

        public bool TryCallMethod(long objectId, string name, object[] parameters, out object result)
        {
            result = null;
            JavascriptObject obj;
            if (!objects.TryGetValue(objectId, out obj))
            {
                return false;
            }

            var method = obj.Methods.FirstOrDefault(p => p.ManagedName == name);
            if (method == null)
            {
                throw new InvalidOperationException(string.Format("Method {0} not found on Object of Type {1}", name, obj.Value.GetType()));
            }

            result = method.Function(obj.Value, parameters);
            return true;
        }

        public bool TryGetProperty(long objectId, string name, out object result)
        {
            result = null;
            JavascriptObject obj;
            if (!objects.TryGetValue(objectId, out obj))
            {
                return false;
            }

            var property = obj.Properties.FirstOrDefault(p => p.ManagedName == name);
            if (property == null)
            {
                throw new InvalidOperationException(string.Format("Property {0} not found on Object of Type {1}", name, obj.Value.GetType()));
            }

            result = property.GetValue(obj.Value);
            return true;
        }

        public bool TrySetProperty(long objectId, string name, object value)
        {
            JavascriptObject obj;
            if (!objects.TryGetValue(objectId, out obj))
            {
                return false;
            }

            var property = obj.Properties.FirstOrDefault(p => p.ManagedName == name);
            if (property == null)
            {
                throw new InvalidOperationException(string.Format("Property {0} not found on Object of Type {1}", name, obj.Value.GetType()));
            }

            property.SetValue(obj.Value, value);
            return true;
        }

        private void Analyse(JavascriptObject obj)
        {
            if (obj.Value == null)
            {
                return;
            }

            var type = obj.Value.GetType();
            if (type.IsPrimitive || type == typeof(string))
            {
                return;
            }

            foreach (var methodInfo in type.GetMethods(BindingFlags.Instance | BindingFlags.Public).Where(p => !p.IsSpecialName))
            {
                // Type objects can not be serialized.
                if (methodInfo.ReturnType == typeof(Type))
                {
                    continue;
                }

                var jsMethod = CreateJavaScriptMethod(methodInfo);
                obj.Methods.Add(jsMethod);
            }

            foreach (var propertyInfo in type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => !p.IsSpecialName))
            {
                if (propertyInfo.PropertyType == typeof(Type))
                {
                    continue;
                }

                var jsProperty = CreateJavaScriptProperty(propertyInfo);
                jsProperty.Value = CreateJavascriptObject();
                jsProperty.Value.Value = jsProperty.GetValue(obj.Value);
                Analyse(jsProperty.Value);
                obj.Properties.Add(jsProperty);
            }
        }

        private static JavascriptMethod CreateJavaScriptMethod(MethodInfo methodInfo)
        {
            var jsMethod = new JavascriptMethod();

            jsMethod.ManagedName = methodInfo.Name;
            jsMethod.JavascriptName = LowercaseFirst(methodInfo.Name);
            jsMethod.Function = methodInfo.Invoke;

            return jsMethod;
        }

        private static JavascriptProperty CreateJavaScriptProperty(PropertyInfo propertyInfo)
        {
            var jsProperty = new JavascriptProperty();

            jsProperty.ManagedName = propertyInfo.Name;
            jsProperty.JavascriptName = LowercaseFirst(propertyInfo.Name);
            jsProperty.SetValue = (o, v) => propertyInfo.SetValue(o, v, null);
            jsProperty.GetValue = (o) => propertyInfo.GetValue(o, null);

            jsProperty.IsComplexType = !propertyInfo.PropertyType.IsPrimitive && propertyInfo.PropertyType != typeof(string);

            return jsProperty;
        }

        private static string LowercaseFirst(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return string.Empty;
            }

            return char.ToLower(str[0]) + str.Substring(1);
        }
    }
}

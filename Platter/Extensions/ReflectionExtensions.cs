// <copyright file="ReflectionExtensions.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Extensions {
    using System.Reflection;

    /// <summary>
    /// Extention methods to make reflection easier.
    /// </summary>
    public static class ReflectionExtensions {
        /// <summary>
        /// Quick way to use all relevent binding flags.
        /// </summary>
        public static readonly BindingFlags AllFlags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.GetProperty;

        /// <summary>
        /// Uses reflection to get the value of a member of an object.
        /// </summary>
        /// <param name="obj">Object to be reflected.</param>
        /// <param name="memberName">String name of member.</param>
        /// <returns>Value of the member of the origial object.</returns>
        /// <exception cref="System.Exception">Exception for not finding the specified member name within the object.</exception>
        public static object GetMemberValue(this object obj, string memberName) {
            var memInf = GetMemberInfo(obj, memberName);
            if (memInf == null) {
                PlatterMod.Instance.Log.Error(new System.Exception("memberName"), $"{nameof(ReflectionExtensions)} {nameof(GetMemberInfo)} Couldn't find member name! ");
            }

            return memInf is PropertyInfo
                ? memInf.As<PropertyInfo>().GetValue(obj, null)
                : memInf is FieldInfo ? memInf.As<FieldInfo>().GetValue(obj) : throw new System.Exception();
        }

        /// <summary>
        /// Uses Reflection to Set to value of a member of an object.
        /// </summary>
        /// <param name="obj">Object to be reflected.</param>
        /// <param name="memberName">String name of member.</param>
        /// <param name="newValue">New value to be set.</param>
        /// <returns>Returns old value.</returns>
        /// <exception cref="System.Exception">Exception thrown if member name is not found on object.</exception>
        public static object SetMemberValue(this object obj, string memberName, object newValue) {
            var memInf = GetMemberInfo(obj, memberName);
            if (memInf == null) {
                PlatterMod.Instance.Log.Error(new System.Exception("memberName"), $"{nameof(ReflectionExtensions)} {nameof(GetMemberInfo)} Couldn't find member name! ");
            }

            var oldValue = obj.GetMemberValue(memberName);
            if (memInf is PropertyInfo) {
                memInf.As<PropertyInfo>().SetValue(obj, newValue, null);
            } else if (memInf is FieldInfo) {
                memInf.As<FieldInfo>().SetValue(obj, newValue);
            } else {
                throw new System.Exception();
            }

            return oldValue;
        }

        /// <summary>
        /// Invokes an instance method (public or internal) on an object using reflection.
        /// </summary>
        /// <param name="obj">The object instance containing the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="parameters">Parameters to pass to the method.</param>
        /// <returns>The return value of the invoked method, or null if the method returns void.</returns>
        /// <exception cref="System.Exception">Thrown if the method cannot be found.</exception>
        public static object InvokeMethod(this object obj, string methodName, params object[] parameters) {
            var method = obj.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (method == null) {
                PlatterMod.Instance.Log.Error(
                    new System.Exception("methodName"),
                    $"{nameof(ReflectionExtensions)} {nameof(InvokeMethod)} Couldn't find instance method '{methodName}' on type '{obj.GetType().FullName}'!");
                throw new System.Exception($"Method '{methodName}' not found on type '{obj.GetType().FullName}'");
            }

            return method.Invoke(obj, parameters);
        }

        /// <summary>
        /// Tries to invoke an instance method (public or internal) on an object using reflection.
        /// </summary>
        /// <param name="obj">The object instance containing the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="result">The return value of the invoked method, or null if method not found or returns void.</param>
        /// <param name="parameters">Parameters to pass to the method.</param>
        /// <returns>True if the method was found and invoked successfully; otherwise, false.</returns>
        public static bool TryInvokeMethod(this object obj, string methodName, out object result, params object[] parameters) {
            result = null;
            
            var method = obj.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (method == null) {
                PlatterMod.Instance.Log.Error($"{nameof(ReflectionExtensions)} {nameof(TryInvokeMethod)} Failed to find method '{methodName}' on type '{obj.GetType().FullName}'");
                return false;
            }

            try {
                result = method.Invoke(obj, parameters);
                return true;
            } catch (System.Exception ex) {
                PlatterMod.Instance.Log.Error(
                    ex,
                    $"{nameof(ReflectionExtensions)} {nameof(TryInvokeMethod)} Failed to invoke method '{methodName}' on type '{obj.GetType().FullName}'");
                return false;
            }
        }

        /// <summary>
        /// Invokes a static method (public or internal) on a type using reflection.
        /// </summary>
        /// <param name="type">The type containing the static method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="parameters">Parameters to pass to the method.</param>
        /// <returns>The return value of the invoked method, or null if the method returns void.</returns>
        /// <exception cref="System.Exception">Thrown if the method cannot be found.</exception>
        public static object InvokeStaticMethod(this System.Type type, string methodName, params object[] parameters) {
            var method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (method == null) {
                PlatterMod.Instance.Log.Error(
                    new System.Exception("methodName"),
                    $"{nameof(ReflectionExtensions)} {nameof(InvokeStaticMethod)} Couldn't find static method '{methodName}' on type '{type.FullName}'!");
                throw new System.Exception($"Method '{methodName}' not found on type '{type.FullName}'");
            }

            return method.Invoke(null, parameters);
        }

        /// <summary>
        /// Tries to invoke a static method (public or internal) on a type using reflection.
        /// </summary>
        /// <param name="type">The type containing the static method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="result">The return value of the invoked method, or null if method not found or returns void.</param>
        /// <param name="parameters">Parameters to pass to the method.</param>
        /// <returns>True if the method was found and invoked successfully; otherwise, false.</returns>
        public static bool TryInvokeStaticMethod(this System.Type type, string methodName, out object result, params object[] parameters) {
            result = null;
            
            var method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (method == null) {
                return false;
            }

            try {
                result = method.Invoke(null, parameters);
                return true;
            } catch (System.Exception ex) {
                PlatterMod.Instance.Log.Error(
                    ex,
                    $"{nameof(ReflectionExtensions)} {nameof(TryInvokeStaticMethod)} Failed to invoke method '{methodName}' on type '{type.FullName}'");
                return false;
            }
        }

        /// <summary>
        /// Uses reflection to get member info.
        /// </summary>
        /// <param name="obj">Object to be reflected.</param>
        /// <param name="memberName">String name of member.</param>
        /// <returns>Member info.</returns>
        private static MemberInfo GetMemberInfo(object obj, string memberName) {
            var prps = new System.Collections.Generic.List<PropertyInfo>
        {
            obj.GetType().GetProperty(
                memberName,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy),
        };
            prps = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(prps, i => i is not null));
            if (prps.Count != 0) {
                return prps[0];
            }

            var flds = new System.Collections.Generic.List<FieldInfo>
        {
            obj.GetType().GetField(
                memberName,
                bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy),
        };
            flds = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(flds, i => i is not null));
            return flds.Count != 0 ? flds[0] : (MemberInfo)null;
        }

        [System.Diagnostics.DebuggerHidden]
        private static T As<T>(this object obj) {
            return (T)obj;
        }
    }
}
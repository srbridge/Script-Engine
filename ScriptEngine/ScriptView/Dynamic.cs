using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace System.Dynamic
{
    /// <summary>
    /// uses linq expressions to precompile getter and setter methods.
    /// </summary>
    public static class DynamicGetSet
    {
        #region getter and setter compilers

        #region Get Method Compilers

        /// <summary>
        /// returns a dictionary of compile get delegates that access all the public get properties of the specified type
        /// </summary>
        /// <typeparam name="TInput"></typeparam>
        /// <returns></returns>
        public static Dictionary<string, Func<TInput, object>> CompileGettersByName<TInput>()
        {
            var list = new Dictionary<string, Func<TInput, object>>();
            var props = typeof(TInput).GetProperties();
            foreach (var p in props)
            {
                if (p.CanRead && !p.GetMethod.IsStatic)
                {
                    var getter = p.CompileGetter<TInput, object>();
                    list.Add(p.Name, getter);
                }

            }
            return list;
        }
        /// <summary>
        /// returns a dictionary of compile get delegates that access all the public get properties of the specified type
        /// </summary>
        /// <typeparam name="TInput"></typeparam>
        /// <returns></returns>
        public static Dictionary<PropertyInfo, Func<TInput, object>> CompileGetters<TInput>()
        {
            var list = new Dictionary<PropertyInfo, Func<TInput, object>>();
            var props = typeof(TInput).GetProperties();
            foreach (var p in props)
            {
                if (p.CanRead && !p.GetMethod.IsStatic)
                {
                    var getter = p.CompileGetter<TInput, object>();
                    list.Add(p, getter);
                }

            }
            return list;
        }
        /// <summary>
        /// returns a dictionary of compiled get delegates that access the public get properties of the specified type that have a compatible 
        /// property type with <typeparamref name="TOutput"/>
        /// </summary>
        /// <remarks>
        /// could be used to retrieve a dictionary of getters for all the string properties of a specified type.
        /// </remarks>
        /// <example>
        /// <code>
        /// 
        /// </code>
        /// </example>
        /// <typeparam name="TInput">the type defining the properties to get</typeparam>
        /// <typeparam name="TOutput">the property type to retrieve</typeparam>
        /// <returns></returns>
        public static Dictionary<PropertyInfo, Func<TInput, TOutput>> CompileGetters<TInput, TOutput>()
        {
            var list = new Dictionary<PropertyInfo, Func<TInput, TOutput>>();
            var props = typeof(TInput).GetProperties();
            foreach (var p in props)
            {
                if (p.CanRead && !p.GetMethod.IsStatic && p.PropertyType.IsAssignableFrom(typeof(TOutput)))
                {
                    var getter = p.CompileGetter<TInput, TOutput>();
                    list.Add(p, getter);
                }

            }
            return list;
        }

        /// <summary>
        /// compiles a lambda expression that gets the value of the specified property as an object.
        /// </summary>
        /// <typeparam name="TInput">
        /// the type defining the property.
        /// </typeparam>
        /// <param name="property">the property to access</param>
        /// <returns>
        /// a delegate to a compile lambda that will retrieve the value of the property.
        /// </returns>
        public static Func<TInput, object> CompileGetter<TInput>(this PropertyInfo property)
        {
            // get the get method:
            var getMethod = property.GetGetMethod();

            // define the instance parameter:
            var instanceParam = Expression.Parameter(typeof(TInput), "Instance");

            // create an expression to get the value of the property:
            var lambda = Expression.Lambda<Func<TInput, object>>(Expression.Convert(Expression.Call(instanceParam, getMethod), typeof(object)), instanceParam);

            // compile the expression:
            return lambda.Compile();
        }

        /// <summary>
        /// compiles a lambda expression that gets the value of the specified property as an object.
        /// </summary>
        /// <typeparam name="TInput">
        /// the type defining the property
        /// </typeparam>
        /// <typeparam name="TOutput">
        /// the property type
        /// </typeparam>
        /// <param name="property">
        /// the property.
        /// </param>
        /// <returns></returns>
        public static Func<TInput, TOutput> CompileGetter<TInput, TOutput>(this PropertyInfo property)
        {
            // get the get method:
            var getMethod = property.GetGetMethod();

            // define the instance parameter:
            var instanceParam = Expression.Parameter(typeof(TInput), "Instance");

            // create an expression to get the value of the property:
            var lambda = Expression.Lambda<Func<TInput, TOutput>>(Expression.Convert(Expression.Call(Expression.Convert(instanceParam, property.DeclaringType), getMethod), typeof(TOutput)), instanceParam);

            // compile the expression:
            return lambda.Compile();
        }

		/// <summary>
		/// creates a compiled function to access a field
		/// </summary>
		/// <typeparam name="Tin"></typeparam>
		/// <typeparam name="Tout"></typeparam>
		/// <param name="field"></param>
		/// <returns></returns>
		public static Func<Tin,Tout> CompileFieldAccessor<Tin, Tout>(this FieldInfo field)
		{
			// define the instance parameter:
			var instanceParam = Expression.Parameter(typeof(Tin), "Instance");

			// create an expression to access a field:
			var accessField = Expression.Field(instanceParam, field);

			// create a lambda (add the parameter(s))
			var lambda = Expression.Lambda<Func<Tin, Tout>>(accessField, instanceParam);

			// compile the field accessor and return
			return lambda.Compile();
		}

        /// <summary>
        /// compiles a property-getter delegate for the specified type and property.
        /// </summary>
        /// <typeparam name="TInput"></typeparam>
        /// <typeparam name="TOutput"></typeparam>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static Func<TInput, TOutput> CompileGetter<TInput, TOutput>(string propertyName)
        {
            PropertyInfo property = typeof(TInput).GetProperty(propertyName);
            if (property == null)
                throw new ArgumentException("Uknown Property: " + propertyName);
            if (!property.PropertyType.Equals(typeof(TOutput)))
                throw new ArgumentException("Property " + property.Name + " Type: " + property.PropertyType.Name + " doesn't match generic parameter: " + typeof(TOutput).Name);

            if (property.CanRead)
            {
                return property.CompileGetter<TInput, TOutput>();
            }
            else
            {
                throw new ArgumentException("Cannot read property: " + propertyName);
            }
        }

        /// <summary>
        /// compiles a lambda expression that gets the value of the specified property as an object.
        /// </summary>
        /// <typeparam name="TInput"></typeparam>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static Func<TInput, object> CompileGetter<TInput>(string propertyName)
        {
            PropertyInfo property = typeof(TInput).GetProperty(propertyName);
            if (property == null)
                throw new ArgumentException("Uknown Property: " + propertyName);

            // get the get method:
            var getMethod = property.GetGetMethod();

            // define the instance parameter:
            var instanceParam = Expression.Parameter(typeof(TInput), "Instance");

            // create an expression to get the value of the property:
            var lambda = Expression.Lambda<Func<TInput, object>>(Expression.Convert(Expression.Call(instanceParam, getMethod), typeof(object)), instanceParam);

            // compile the expression:
            return lambda.Compile();
        }

        /// <summary>
        /// compiles a lambda expression that gets the value of the specified property as an object.
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static Func<object, object> CompileGetter(this Type t, string propertyName)
        {
            PropertyInfo property = t.GetProperty(propertyName);
            if (property == null)
                throw new ArgumentException("Uknown Property: " + propertyName);

            return CompileGetter(property);
        }

        /// <summary>
        /// compiles a lambda expression that gets the value of the specified property as an object.
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static Func<object, object> CompileGetter(this PropertyInfo property)
        {
            // get the get method:
            var getMethod = property.GetGetMethod();

            // define the instance parameter:
            var instanceParam = Expression.Parameter(typeof(object), "Instance");

            // create an expression to get the value of the property:
            var lambda = Expression.Lambda(Expression.Convert(Expression.Call(Expression.Convert(instanceParam, property.DeclaringType), getMethod), typeof(object)), instanceParam);

            // compile the expression:
            return (Func<object, object>)lambda.Compile();
        }



        #endregion

        #region Set Method Compilers

        /// <summary>
        /// compiles a setter method for a property.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static Action<object, object> CompileSetter(this Type t, string propertyName)
        {
            PropertyInfo property = t.GetProperty(propertyName);
            if (property == null)
                throw new ArgumentException("Uknown Property: " + propertyName);

            return CompileSetter(property);
        }

        /// <summary>
        /// creates a delegate that can write to the specified property as an object value.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static Action<object, object> CompileSetter(this PropertyInfo property)
        {
            if (property.CanWrite)
            {
                // create a parameter for the expression: this represents the instance (the object who's property is to be set)
                var instance = Expression.Parameter(typeof(object), "Instance");

                // create a parameter for the expression: this represents the value (the value to be set on the property)
                var value = Expression.Parameter(typeof(object), "value");

                // create an expression to convert the object value to the correct type for the expression:
                var convert = Expression.Convert(value, property.PropertyType);

                // create a lambda expression that sets the property value:
                var lambda = Expression.Lambda<Action<object, object>>(
                    Expression.Assign(
                        Expression.Property(
                            Expression.Convert(instance, property.DeclaringType), property), convert),
                    instance, value);

                // compile the expression to a delegate and return:
                return lambda.Compile();
            }
            else
                throw new ArgumentException("Property is Read Only!");
        }

        /// <summary>
        /// creates a delegate that can write to the specified property as an object value.
        /// </summary>
        /// <typeparam name="TInstance"></typeparam>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static Action<TInstance, object> CompileSetter<TInstance>(this PropertyInfo property)
        {
            if (property.CanWrite)
            {
                // create a parameter for the expression: this represents the instance (the object who's property is to be set)
                var instance = Expression.Parameter(typeof(TInstance), "Instance");

                // create a parameter for the expression: this represents the value (the value to be set on the property)
                var value = Expression.Parameter(typeof(object), "value");

                // create an expression to convert the object value to the correct type for the expression:
                var convert = Expression.Convert(value, property.PropertyType);

                // create a lambda expression that sets the property value:
                var lambda = Expression.Lambda<Action<TInstance, object>>(Expression.Assign(Expression.Property(instance, property), convert), instance, value);

                // compile the expression to a delegate and return:
                return lambda.Compile();
            }
            else
                throw new ArgumentException("Property is Read Only!");
        }

        /// <summary>
        /// creates a delegate that can write to the specified property
        /// </summary>
        /// <typeparam name="TInstance"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static Action<TInstance, TValue> CompileSetter<TInstance,TValue>(this PropertyInfo property)
        {
            if (property.CanWrite)
            {
                // create a parameter for the expression: this represents the instance (the object who's property is to be set)
                var instance = Expression.Parameter(typeof(TInstance), "Instance");

                // create a parameter for the expression: this represents the value (the value to be set on the property)
                var value = Expression.Parameter(typeof(TValue), "value");

                // create an expression to convert the object value to the correct type for the expression:
                var convert = Expression.Convert(value, property.PropertyType);

                // create a lambda expression that sets the property value:
                var lambda = Expression.Lambda<Action<TInstance, TValue>>(Expression.Assign(Expression.Property(instance, property), convert), instance, value);

                // compile the expression to a delegate and return:
                return lambda.Compile();
            }
            else
                throw new ArgumentException("Property is Read Only!");
        }

        /// <summary>
        /// creates a delegate that can write to the specified property as an object value.
        /// </summary>
        /// <typeparam name="TInstance"></typeparam>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static Action<TInstance, object> CompileSetter<TInstance>(string propertyName)
        {
            PropertyInfo property = typeof(TInstance).GetProperty(propertyName);
            if (property == null)
                throw new ArgumentException("Uknown Property: " + propertyName);

            if (property.CanWrite)
            {
                return property.CompileSetter<TInstance, object>();
            }
            else
                throw new ArgumentException("Property is Read Only!");
        }

        /// <summary>
        /// creates a delegate that can write to the specified property type.
        /// </summary>
        /// <typeparam name="TInstance">the type of object the property is instanced on</typeparam>
        /// <typeparam name="TValue">the type of the property</typeparam>
        /// <param name="propertyName">the name of the property</param>
        /// <returns></returns>
        public static Action<TInstance, TValue> CompileSetter<TInstance, TValue>(string propertyName)
        {
            PropertyInfo property = typeof(TInstance).GetProperty(propertyName);
            if (property == null)
                throw new ArgumentException("Uknown Property: " + propertyName);

            if (property.CanWrite)
            {
                var instance = Expression.Parameter(property.DeclaringType, "Instance");

                var param = Expression.Parameter(property.PropertyType, "Setter");

                var lambda = Expression.Lambda<Action<TInstance, TValue>>(Expression.Assign(Expression.Property(instance, property), param), instance, param);

                return lambda.Compile();
            }
            else
                throw new ArgumentException("Property is Read Only!");
        }

        #endregion

        #endregion getter and setter compilers
    }


    /// <summary>
    /// extension methods for <see cref="DynamicObject"/>
    /// </summary>
    public static class DynamicExtensions
    {
        /// <summary>
        /// simple helper extension method that allows a get from a named property, determined at runtime by interrogating the member names.
        /// </summary>
        /// <param name="dyn"></param>
        /// <param name="memberName"></param>
        /// <returns></returns>
        public static object GetValue(this DynamicObject dyn, string memberName)
        {
            var pd = new ProxyGetMemberBinder(memberName, true);
            object result;
            if (dyn.TryGetMember(pd, out result))
            {
                return result;
            }
            return null;
        }

        /// <summary>
        /// a proxy get-member binder for the GetValue extension method 
        /// </summary>
        public class ProxyGetMemberBinder : GetMemberBinder
        {
            public ProxyGetMemberBinder(string name, bool ignoreCase)
                : base(name, ignoreCase)
            {
            }

            public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
            {
                return target;
            }
        }
    }

}

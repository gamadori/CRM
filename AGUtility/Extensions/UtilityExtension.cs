using CRM.Shared;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AGUtility.Extensions
{
    public static class UtilityExtension
    {
        public static T GetPropertyValue<T>(this object sourceInstance, string targetPropertyName, bool throwExceptionIfNotExists = false)
        {
            string errorMsg = null;

            try
            {
                if (sourceInstance == null || string.IsNullOrWhiteSpace(targetPropertyName))
                {
                    errorMsg = $"Source object is null or property name is null or whitespace. '{targetPropertyName}'";
                    Console.WriteLine(errorMsg);

                    if (throwExceptionIfNotExists)
                        throw new ArgumentException(errorMsg);
                    else
                        return default(T);
                }

                Type returnType = typeof(T);
                Type sourceType = sourceInstance.GetType();

                PropertyInfo propertyInfo = sourceType.GetProperty(targetPropertyName);
                if (propertyInfo == null)
                {
                    errorMsg = $"Property name '{targetPropertyName}' of type '{returnType}' not found for source object of type '{sourceType}'";
                    Console.WriteLine(errorMsg);

                    if (throwExceptionIfNotExists)
                        throw new ArgumentException(errorMsg);
                    else
                        return default(T);
                }

                return (T)propertyInfo.GetValue(sourceInstance, null);
            }
            catch (Exception ex)
            {
                errorMsg = $"Problem getting property name '{targetPropertyName}' from source instance.";
                Console.WriteLine(errorMsg, ex);

                if (throwExceptionIfNotExists)
                    throw;
            }

            return default(T);
        }

        /// <summary>
        /// ✅ Versione sicura di GetPropertyValue con default value personalizzabile.
        /// Gestisce automaticamente eccezioni e proprietà mancanti senza logging eccessivo.
        /// </summary>
        /// <typeparam name="T">Tipo della proprietà da recuperare</typeparam>
        /// <param name="sourceInstance">Oggetto sorgente</param>
        /// <param name="targetPropertyName">Nome della proprietà da leggere</param>
        /// <param name="defaultValue">Valore di default se proprietà non esiste o genera errori</param>
        /// <returns>Valore della proprietà o defaultValue</returns>
        /// <example>
        /// <code>
        /// // Uso base con default value
        /// var users = item.GetPropertyValueSafe("AssignedUserNames", new List&lt;string&gt;());
        /// 
        /// // Con default implicito (null per reference types)
        /// var description = item.GetPropertyValueSafe&lt;string&gt;("Description");
        /// </code>
        /// </example>
        public static T GetPropertyValueSafe<T>(this object sourceInstance, string targetPropertyName, T defaultValue = default)
        {
            try
            {
                // Validazione input
                if (sourceInstance == null || string.IsNullOrWhiteSpace(targetPropertyName))
                {
                    return defaultValue;
                }

                Type sourceType = sourceInstance.GetType();
                PropertyInfo propertyInfo = sourceType.GetProperty(targetPropertyName);
                
                // Proprietà non trovata
                if (propertyInfo == null)
                {
                    return defaultValue;
                }

                // Recupera valore
                var value = propertyInfo.GetValue(sourceInstance, null);
                
                // Gestione null
                if (value == null)
                {
                    return defaultValue;
                }

                return (T)value;
            }
            catch
            {
                // Silenziosamente ritorna default senza logging
                // (utile per reflection su ViewModel dinamici)
                return defaultValue;
            }
        }

        /// <summary>
        /// ✅ NUOVO: Versione con lazy evaluation del default value tramite factory.
        /// Utile quando il default value è costoso da creare e potrebbe non servire.
        /// </summary>
        /// <typeparam name="T">Tipo della proprietà da recuperare</typeparam>
        /// <param name="sourceInstance">Oggetto sorgente</param>
        /// <param name="targetPropertyName">Nome della proprietà da leggere</param>
        /// <param name="defaultValueFactory">Factory che genera il default value solo se necessario</param>
        /// <returns>Valore della proprietà o risultato di defaultValueFactory()</returns>
        /// <example>
        /// <code>
        /// // Factory eseguita SOLO se proprietà mancante (lazy evaluation)
        /// var users = item.GetPropertyValueSafe("AssignedUserNames", () => LoadDefaultUsers());
        /// 
        /// // Factory inline
        /// var config = item.GetPropertyValueSafe("Config", () => new ConfigObject { IsDefault = true });
        /// </code>
        /// </example>
        public static T GetPropertyValueSafe<T>(this object sourceInstance, string targetPropertyName, Func<T> defaultValueFactory)
        {
            if (defaultValueFactory == null)
            {
                throw new ArgumentNullException(nameof(defaultValueFactory), "Default value factory cannot be null. Use the overload with T defaultValue instead.");
            }

            try
            {
                // Validazione input
                if (sourceInstance == null || string.IsNullOrWhiteSpace(targetPropertyName))
                {
                    return defaultValueFactory();
                }

                Type sourceType = sourceInstance.GetType();
                PropertyInfo propertyInfo = sourceType.GetProperty(targetPropertyName);
                
                // Proprietà non trovata
                if (propertyInfo == null)
                {
                    return defaultValueFactory();
                }

                // Recupera valore
                var value = propertyInfo.GetValue(sourceInstance, null);
                
                // Gestione null
                if (value == null)
                {
                    return defaultValueFactory();
                }

                return (T)value;
            }
            catch
            {
                // Esegui factory solo in caso di errore
                return defaultValueFactory();
            }
        }

        /// <summary>
        /// ✅ NUOVO: Versione con validazione tipo strict mode.
        /// Lancia InvalidCastException se la proprietà esiste ma ha un tipo diverso da T.
        /// </summary>
        /// <typeparam name="T">Tipo della proprietà da recuperare</typeparam>
        /// <param name="sourceInstance">Oggetto sorgente</param>
        /// <param name="targetPropertyName">Nome della proprietà da leggere</param>
        /// <param name="defaultValue">Valore di default se proprietà non esiste</param>
        /// <param name="strictMode">Se true, valida che il tipo della proprietà corrisponda esattamente a T</param>
        /// <returns>Valore della proprietà o defaultValue</returns>
        /// <exception cref="InvalidCastException">Lanciata in strict mode se il tipo non corrisponde</exception>
        /// <example>
        /// <code>
        /// // Strict mode: lancia eccezione se AssignedUserNames è int invece di List&lt;string&gt;
        /// var users = item.GetPropertyValueSafe("AssignedUserNames", new List&lt;string&gt;(), strictMode: true);
        /// 
        /// // Non-strict mode: ritorna default senza eccezione
        /// var users = item.GetPropertyValueSafe("AssignedUserNames", new List&lt;string&gt;(), strictMode: false);
        /// </code>
        /// </example>
        public static T GetPropertyValueSafe<T>(this object sourceInstance, string targetPropertyName, T defaultValue, bool strictMode)
        {
            try
            {
                // Validazione input
                if (sourceInstance == null || string.IsNullOrWhiteSpace(targetPropertyName))
                {
                    return defaultValue;
                }

                Type sourceType = sourceInstance.GetType();
                PropertyInfo propertyInfo = sourceType.GetProperty(targetPropertyName);
                
                // Proprietà non trovata
                if (propertyInfo == null)
                {
                    return defaultValue;
                }

                // ✅ STRICT MODE: Validazione tipo
                if (strictMode)
                {
                    Type expectedType = typeof(T);
                    Type actualType = propertyInfo.PropertyType;

                    // Controllo tipo esatto (no conversioni implicite)
                    if (!expectedType.IsAssignableFrom(actualType))
                    {
                        throw new InvalidCastException(
                            $"Property '{targetPropertyName}' has type '{actualType.FullName}' but expected '{expectedType.FullName}'. " +
                            $"Source type: {sourceType.FullName}");
                    }
                }

                // Recupera valore
                var value = propertyInfo.GetValue(sourceInstance, null);
                
                // Gestione null
                if (value == null)
                {
                    return defaultValue;
                }

                return (T)value;
            }
            catch (InvalidCastException)
            {
                // Re-throw strict mode exceptions
                throw;
            }
            catch
            {
                // Altre eccezioni: ritorna default
                return defaultValue;
            }
        }

        public static bool SetPropertyValue(this object sourceInstance, string targetPropertyName, object value)
        {
            string errorMsg = null;

            try
            {
                if (sourceInstance == null || string.IsNullOrWhiteSpace(targetPropertyName))
                {
                    errorMsg = $"Source object is null or property name is null or whitespace. '{targetPropertyName}'";
                    Console.WriteLine(errorMsg);


                    return false;
                }

                
                Type sourceType = sourceInstance.GetType();

                PropertyInfo propertyInfo = sourceType.GetProperty(targetPropertyName);
                if (propertyInfo == null)
                {
                   

                    return false;
                }

                propertyInfo.SetValue(sourceInstance, value);

                return true;
            }
            catch (Exception ex)
            {
                errorMsg = $"Problem getting property name '{targetPropertyName}' from source instance.";
                Console.WriteLine(errorMsg, ex);

                return false;
            }

           
        }

        public static List<TaskDependency> ToDependencyList(this IEnumerable<TaskData> data)
        {
            List<TaskDependency> list = new List<TaskDependency>();

            foreach (var t in data)
            {
                list.Add(new TaskDependency { Id = t.Id, Name = t.Name });
            }

            return list;
        }

        public static string ToListString<T>(this IEnumerable<T> data) where T: class
        {
            string s = "";

            foreach (var t in data)
            {
                if (s.Length > 0)
                    s += ",";
                
                s += (t.GetPropertyValue<int>("Id")).ToString();
            }
            return s;
        }

        public static string ToListString(this IEnumerable<int> data)
        {
            string s = "";

            foreach (var t in data)
            {
                if (s.Length > 0)
                    s += ",";

                s += t.ToString();
            }
            return s;
        }

        public static List<string> ReadAsList(this IFormFile file)
        {
            var result = new List<string>();
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                while (reader.Peek() >= 0)
                    result.Add(reader.ReadLine());
            }
            return result;
        }
    }
}


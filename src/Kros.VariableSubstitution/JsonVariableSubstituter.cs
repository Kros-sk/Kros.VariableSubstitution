using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Kros.VariableSubstitution
{
    /// <summary>
    /// Substituter which substitute variables in Json.
    /// </summary>
    internal class JsonVariableSubstituter
    {
        private static readonly Func<string, object> _toStringConverter = (value) => value;
        private static Dictionary<JTokenType, Func<string, object>> _converters = InitConverters();
        private readonly ILogger _logger;

        public JsonVariableSubstituter(ILogger logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public SubstitutionResult Substitute(IVariablesProvider variables, string source)
        {
            try
            {
                bool wasSubstituted = false;
                var json = JObject.Parse(source);

                foreach (var variable in variables.GetVariables())
                {
                    try
                    {
                        JValue token = FindToken(json, variable.Key);
                        if (token != null)
                        {
                            wasSubstituted = true;
                            token.Value = GetConverter(variable.Key, token.Type)(variable.Value);
                            _logger.LogInformation("|    Substituting value on key '{name}' with ({type}) value: {value}",
                                variable.Key, token.Type, token.Value);
                        }
                    }
                    catch
                    {
                        _logger.LogError($"Failed processing variable '{variable.Key}'.");
                        throw;
                    }
                }

                return new(wasSubstituted ? json.ToString() : source, wasSubstituted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Substitution failed.");
                return new(source, false);
            }
        }

        private static JValue FindToken(JObject json, string variableKey)
        {
            int dotIndex = variableKey.IndexOf(".");
            string key = dotIndex == -1 ? variableKey : variableKey.Substring(0, dotIndex);

            if (json.Property(key, StringComparison.OrdinalIgnoreCase) is var p && p != null)
            {
                if (p.Value.Type == JTokenType.Array)
                {
                    return GetTokenFromArray(p.Value as JArray, variableKey[(dotIndex + 1)..]);
                }
                else if (p.Value.Type == JTokenType.Object)
                {
                    return FindToken(p.Value as JObject, variableKey[(dotIndex + 1)..]);
                }
                else
                {
                    return p.Value as JValue;
                }
            }
            else
            {
                return null;
            }
        }

        private static JValue GetTokenFromArray(JArray array, string variableKey)
        {
            int dotIndex = variableKey.IndexOf(".");
            int index = int.Parse(dotIndex == -1 ? variableKey : variableKey.Substring(0, dotIndex));

            JToken token = array[index];

            if (token.Type == JTokenType.Object)
            {
                return FindToken(token as JObject, variableKey[(dotIndex + 1)..]);
            }
            else
            {
                return token as JValue;
            }
        }

        private Func<string, object> GetConverter(string propertyName, JTokenType tokenType)
        {
            if (_converters.ContainsKey(tokenType))
            {
                return _converters[tokenType];
            }
            else
            {
                _logger.LogWarning(
                    "It is not possible to specify a conversion method for property '{property}' with type '{type}'.",
                    propertyName, tokenType);
                return _toStringConverter;
            }
        }

        private static Dictionary<JTokenType, Func<string, object>> InitConverters()
        {
            static object C<T>(string value)
                => TypeDescriptor.GetConverter(typeof(T)).ConvertFromString(value);

            _converters = new();

            _converters.Add(JTokenType.Integer, C<long>);
            _converters.Add(JTokenType.String, _toStringConverter);
            _converters.Add(JTokenType.Float, C<float>);
            _converters.Add(JTokenType.Boolean, C<bool>);
            _converters.Add(JTokenType.Null, _toStringConverter);
            _converters.Add(JTokenType.Date, C<DateTime>);
            _converters.Add(JTokenType.Guid, C<Guid>);
            _converters.Add(JTokenType.TimeSpan, C<TimeSpan>);

            return _converters;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MediaSFU.Unity.Json
{
    public enum JsonValueKind
    {
        Undefined = 0,
        Object = 1,
        Array = 2,
        String = 3,
        Number = 4,
        True = 5,
        False = 6,
        Null = 7
    }

    public class JsonException : Exception
    {
        public JsonException()
        {
        }

        public JsonException(string message)
            : base(message)
        {
        }

        public JsonException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public abstract class JsonNamingPolicy
    {
        private static readonly JsonNamingPolicy CamelCasePolicy = new CamelCaseJsonNamingPolicy();

        public static JsonNamingPolicy CamelCase => CamelCasePolicy;

        public abstract string ConvertName(string name);

        private sealed class CamelCaseJsonNamingPolicy : JsonNamingPolicy
        {
            public override string ConvertName(string name)
            {
                if (string.IsNullOrEmpty(name) || !char.IsUpper(name[0]))
                {
                    return name;
                }

                if (name.Length == 1)
                {
                    return char.ToLowerInvariant(name[0]).ToString();
                }

                return char.ToLowerInvariant(name[0]) + name.Substring(1);
            }
        }
    }

    public sealed class JsonSerializerOptions
    {
        public bool PropertyNameCaseInsensitive { get; set; }

        public JsonNamingPolicy PropertyNamingPolicy { get; set; }

        public bool WriteIndented { get; set; }
    }

    public static class JsonSerializer
    {
        public static string Serialize<TValue>(TValue value, JsonSerializerOptions options = null)
        {
            try
            {
                return JsonConvert.SerializeObject(value, CreateSettings(options));
            }
            catch (Newtonsoft.Json.JsonException error)
            {
                throw new JsonException(error.Message, error);
            }
        }

        public static TValue Deserialize<TValue>(string json, JsonSerializerOptions options = null)
        {
            try
            {
                if (typeof(TValue) == typeof(JsonElement))
                {
                    object element = JsonDocument.Parse(json).RootElement.Clone();
                    return (TValue)element;
                }

                if (typeof(TValue) == typeof(JsonElement?))
                {
                    JsonElement? element = JsonDocument.Parse(json).RootElement.Clone();
                    return (TValue)(object)element;
                }

                return JsonConvert.DeserializeObject<TValue>(json, CreateSettings(options));
            }
            catch (Newtonsoft.Json.JsonException error)
            {
                throw new JsonException(error.Message, error);
            }
        }

        private static JsonSerializerSettings CreateSettings(JsonSerializerOptions options)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = CreateContractResolver(options),
                Formatting = options?.WriteIndented == true ? Formatting.Indented : Formatting.None
            };

            settings.Converters.Add(new JsonElementConverter());
            return settings;
        }

        private static IContractResolver CreateContractResolver(JsonSerializerOptions options)
        {
            if (options?.PropertyNamingPolicy == null)
            {
                return new DefaultContractResolver();
            }

            if (ReferenceEquals(options.PropertyNamingPolicy, JsonNamingPolicy.CamelCase))
            {
                return new CamelCasePropertyNamesContractResolver();
            }

            return new JsonNamingPolicyContractResolver(options.PropertyNamingPolicy);
        }

        private sealed class JsonNamingPolicyContractResolver : DefaultContractResolver
        {
            private readonly JsonNamingPolicy _namingPolicy;

            public JsonNamingPolicyContractResolver(JsonNamingPolicy namingPolicy)
            {
                _namingPolicy = namingPolicy;
            }

            protected override string ResolvePropertyName(string propertyName)
            {
                return _namingPolicy.ConvertName(propertyName);
            }
        }

        private sealed class JsonElementConverter : Newtonsoft.Json.JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(JsonElement) || objectType == typeof(JsonElement?);
            }

            public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
            {
                JsonElement element;
                if (value is JsonElement jsonElement)
                {
                    element = jsonElement;
                }
                else
                {
                    writer.WriteNull();
                    return;
                }

                var token = element.ToJToken();
                if (token == null)
                {
                    writer.WriteNull();
                    return;
                }

                token.WriteTo(writer);
            }

            public override object ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
            {
                var token = JToken.Load(reader);
                var element = JsonElement.FromJToken(token);
                if (objectType == typeof(JsonElement?))
                {
                    return (JsonElement?)element;
                }

                return element;
            }
        }
    }

    public sealed class JsonDocument : IDisposable
    {
        private readonly JToken _rootToken;

        private JsonDocument(JToken rootToken)
        {
            _rootToken = rootToken;
        }

        public JsonElement RootElement => JsonElement.FromJToken(_rootToken);

        public static JsonDocument Parse(string json)
        {
            try
            {
                return new JsonDocument(JToken.Parse(json));
            }
            catch (Newtonsoft.Json.JsonException error)
            {
                throw new JsonException(error.Message, error);
            }
        }

        public void Dispose()
        {
        }
    }

    public readonly struct JsonElement
    {
        private readonly JToken _token;

        internal JsonElement(JToken token)
        {
            _token = token;
        }

        public JsonValueKind ValueKind => ConvertValueKind(_token);

        public JsonElement this[int index]
        {
            get
            {
                if (_token is JArray array)
                {
                    return FromJToken(array[index]);
                }

                throw new InvalidOperationException("JSON value is not an array.");
            }
        }

        internal static JsonElement FromJToken(JToken token)
        {
            return new JsonElement(token);
        }

        internal JToken ToJToken()
        {
            return _token;
        }

        public JsonElement Clone()
        {
            return _token == null ? default : FromJToken(_token.DeepClone());
        }

        public int GetArrayLength()
        {
            if (_token is JArray array)
            {
                return array.Count;
            }

            throw new InvalidOperationException("JSON value is not an array.");
        }

        public string GetRawText()
        {
            return _token?.ToString(Formatting.None) ?? string.Empty;
        }

        public string GetString()
        {
            if (_token == null || _token.Type == JTokenType.Null || _token.Type == JTokenType.Undefined)
            {
                return null;
            }

            return (_token as JValue)?.Value?.ToString();
        }

        public ArrayEnumerator EnumerateArray()
        {
            return new ArrayEnumerator(_token as JArray);
        }

        public ObjectEnumerator EnumerateObject()
        {
            return new ObjectEnumerator(_token as JObject);
        }

        public bool TryGetInt32(out int value)
        {
            if (_token != null &&
                int.TryParse(_token.ToString(Formatting.None), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetSingle(out float value)
        {
            if (_token != null &&
                float.TryParse(_token.ToString(Formatting.None), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetProperty(string propertyName, out JsonElement value)
        {
            if (_token is JObject obj)
            {
                var property = obj.Property(propertyName, StringComparison.Ordinal);
                if (property != null)
                {
                    value = FromJToken(property.Value);
                    return true;
                }
            }

            value = default;
            return false;
        }

        public override string ToString()
        {
            return ValueKind == JsonValueKind.String ? GetString() ?? string.Empty : GetRawText();
        }

        private static JsonValueKind ConvertValueKind(JToken token)
        {
            if (token == null)
            {
                return JsonValueKind.Undefined;
            }

            switch (token.Type)
            {
                case JTokenType.Object:
                    return JsonValueKind.Object;
                case JTokenType.Array:
                    return JsonValueKind.Array;
                case JTokenType.String:
                case JTokenType.Guid:
                case JTokenType.Uri:
                case JTokenType.TimeSpan:
                case JTokenType.Date:
                    return JsonValueKind.String;
                case JTokenType.Integer:
                case JTokenType.Float:
                    return JsonValueKind.Number;
                case JTokenType.Boolean:
                    return token.Value<bool>() ? JsonValueKind.True : JsonValueKind.False;
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return JsonValueKind.Null;
                default:
                    return JsonValueKind.Undefined;
            }
        }
    }

    public readonly struct JsonProperty
    {
        public JsonProperty(string name, JsonElement value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public JsonElement Value { get; }
    }

    public struct ArrayEnumerator : IEnumerable<JsonElement>, IEnumerator<JsonElement>
    {
        private readonly JArray _array;
        private int _index;

        internal ArrayEnumerator(JArray array)
        {
            _array = array;
            _index = -1;
        }

        public JsonElement Current => _array != null && _index >= 0 && _index < _array.Count
            ? JsonElement.FromJToken(_array[_index])
            : default;

        object IEnumerator.Current => Current;

        public ArrayEnumerator GetEnumerator()
        {
            return this;
        }

        IEnumerator<JsonElement> IEnumerable<JsonElement>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool MoveNext()
        {
            if (_array == null)
            {
                return false;
            }

            _index++;
            return _index < _array.Count;
        }

        public void Reset()
        {
            _index = -1;
        }

        public void Dispose()
        {
        }
    }

    public struct ObjectEnumerator : IEnumerable<JsonProperty>, IEnumerator<JsonProperty>
    {
        private readonly IList<JProperty> _properties;
        private int _index;

        internal ObjectEnumerator(JObject obj)
        {
            _properties = obj?.Properties() is IEnumerable<JProperty> properties
                ? new List<JProperty>(properties)
                : null;
            _index = -1;
        }

        public JsonProperty Current => _properties != null && _index >= 0 && _index < _properties.Count
            ? new JsonProperty(_properties[_index].Name, JsonElement.FromJToken(_properties[_index].Value))
            : default;

        object IEnumerator.Current => Current;

        public ObjectEnumerator GetEnumerator()
        {
            return this;
        }

        IEnumerator<JsonProperty> IEnumerable<JsonProperty>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool MoveNext()
        {
            if (_properties == null)
            {
                return false;
            }

            _index++;
            return _index < _properties.Count;
        }

        public void Reset()
        {
            _index = -1;
        }

        public void Dispose()
        {
        }
    }
}
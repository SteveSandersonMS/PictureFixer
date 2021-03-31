using System;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Reflection;
using System.Linq;

namespace PictureFixer.Client
{
    internal class WorkaroundPreview3Bug<T> : JsonConverter<T> where T: new()
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // In preview 3, the JSRuntime's JSON converters aren't wired up to the code that parses incoming custom event args.
            // Work around it by accessing the converter directly through reflection. This will be fixed shortly.
            var jsRuntimeType = typeof(WebAssemblyHost).Assembly.GetType("Microsoft.AspNetCore.Components.WebAssembly.Services.DefaultWebAssemblyJSRuntime", true);
            var jsRuntime = jsRuntimeType.GetField("Instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var jsRuntimeOptions = (JsonSerializerOptions)typeof(JSRuntime).GetProperty("JsonSerializerOptions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(jsRuntime);
            var result = new T();
            var properties = typeof(T).GetProperties().ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var propertyName = reader.GetString();
                        var propertyType = properties.TryGetValue(propertyName, out var propertyInfo)
                            ? propertyInfo.PropertyType
                            : typeof(JsonElement);
                        var propertyValue = JsonSerializer.Deserialize(ref reader, propertyType, jsRuntimeOptions);
                        if (propertyInfo != null)
                        {
                            propertyInfo.SetValue(result, propertyValue);
                        }
                        break;
                    case JsonTokenType.EndObject:
                        return result;
                }
            }

            throw new InvalidOperationException("Invalid JSON. Didn't find EndObject token.");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => throw new NotImplementedException();
    }
}

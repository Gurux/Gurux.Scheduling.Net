using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gurux.Scheduling
{
    /// <summary>
    /// Serializes <see cref="GXDateTime"/> values as invariant date-time expressions.
    /// </summary>
    public sealed class GXDateTimeJsonConverter : JsonConverter<GXDateTime>
    {
        /// <inheritdoc />
        public override GXDateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("GXDateTime must be a JSON string.");
            }
            string? value = reader.GetString();
            if (value == null)
            {
                throw new JsonException("GXDateTime cannot be null.");
            }
            return new GXDateTime(value, CultureInfo.InvariantCulture);
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, GXDateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToFormatString(CultureInfo.InvariantCulture));
        }
    }
}

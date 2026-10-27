using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookKiosk.CMS.Models.Common
{
    /// <summary>
    /// FlexibleNullableEnumConverter: Đọc an toàn enum nullable từ cả số nguyên (1, 2),
    /// chuỗi số ("1", "2"), chuỗi định danh ("ProductDiscount", "OrderDiscount"),
    /// và null/empty. Nếu không xác định được, trả về null an toàn, không ném exception làm sập app.
    /// </summary>
    public class FlexibleNullableEnumConverter<T> : JsonConverter<T?> where T : struct, Enum
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var intVal))
            {
                if (Enum.IsDefined(typeof(T), intVal))
                {
                    return (T)Enum.ToObject(typeof(T), intVal);
                }
                return null;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();
                if (string.IsNullOrWhiteSpace(str))
                {
                    return null;
                }

                if (Enum.TryParse<T>(str, true, out var result) && Enum.IsDefined(typeof(T), result))
                {
                    return result;
                }

                if (int.TryParse(str, out var parsedInt) && Enum.IsDefined(typeof(T), parsedInt))
                {
                    return (T)Enum.ToObject(typeof(T), parsedInt);
                }

                return null;
            }

            return null;
        }

        public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
        {
            if (!value.HasValue)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteNumberValue(Convert.ToInt32(value.Value));
            }
        }
    }
}

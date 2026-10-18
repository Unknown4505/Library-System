using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookKiosk.CMS.Models.Common
{
    /// <summary>
    /// FlexibleEnumConverter: Đọc an toàn cả số nguyên (1) lẫn chuỗi ("Pending").
    /// Nếu gặp giá trị không xác định, ném JsonException để phát hiện sự thay đổi contract, không trả default âm thầm.
    /// </summary>
    public class FlexibleEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var intVal))
            {
                if (Enum.IsDefined(typeof(T), intVal))
                {
                    return (T)Enum.ToObject(typeof(T), intVal);
                }
                throw new JsonException($"Giá trị số '{intVal}' không hợp lệ cho enum {typeof(T).Name}.");
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();
                if (string.IsNullOrWhiteSpace(str))
                {
                    throw new JsonException($"Chuỗi rỗng không thể chuyển đổi thành enum {typeof(T).Name}.");
                }

                if (Enum.TryParse<T>(str, true, out var result) && Enum.IsDefined(typeof(T), result))
                {
                    return result;
                }

                if (int.TryParse(str, out var parsedInt) && Enum.IsDefined(typeof(T), parsedInt))
                {
                    return (T)Enum.ToObject(typeof(T), parsedInt);
                }

                throw new JsonException($"Giá trị chuỗi '{str}' không hợp lệ cho enum {typeof(T).Name}.");
            }

            throw new JsonException($"Token type '{reader.TokenType}' không thể chuyển đổi thành enum {typeof(T).Name}.");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(Convert.ToInt32(value));
        }
    }
}

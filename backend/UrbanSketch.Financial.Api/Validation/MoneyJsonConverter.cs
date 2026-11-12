using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UrbanSketch.Financial.Api.Validation;

// Validate the original JSON token so decimal conversion cannot round/underflow first.
internal sealed class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number) throw InvalidMoney();
        using var token = JsonDocument.ParseValue(ref reader);
        var raw = token.RootElement.GetRawText();
        var negative = raw.StartsWith('-');
        if (negative) raw = raw[1..];
        var exponentIndex = raw.IndexOfAny(['e', 'E']);
        var significand = exponentIndex < 0 ? raw : raw[..exponentIndex];
        var dot = significand.IndexOf('.');
        var digits = significand.Replace(".", "").TrimStart('0');
        if (digits.Length == 0) return 0m;

        long exponent = 0;
        if (exponentIndex >= 0 && !long.TryParse(raw[(exponentIndex + 1)..],
                NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
            throw InvalidMoney();
        // Outside these magnitude bounds a nonzero token cannot possibly fit numeric(20,2).
        if (exponent > significand.Length + 20L || exponent < -significand.Length - 20L)
            throw InvalidMoney();
        var scale = (dot < 0 ? 0L : significand.Length - dot - 1L) - exponent;
        if (scale > 0)
        {
            var trailingZeroes = digits.Length - digits.TrimEnd('0').Length;
            var trim = (int)Math.Min(scale, trailingZeroes);
            if (trim > 0) { digits = digits[..^trim]; scale -= trim; }
        }
        if (scale > 2 || digits.Length - scale > 18) throw InvalidMoney();
        if (scale <= 0) digits += new string('0', (int)-scale);
        else
        {
            digits = digits.PadLeft((int)scale + 1, '0');
            digits = digits.Insert(digits.Length - (int)scale, ".");
        }
        return decimal.Parse((negative ? "-" : "") + digits, CultureInfo.InvariantCulture);
    }

    private static JsonException InvalidMoney() => new("Денежное поле должно быть точным числом numeric(20,2).");

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}

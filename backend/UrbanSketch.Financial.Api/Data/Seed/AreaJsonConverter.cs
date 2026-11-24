using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UrbanSketch.Financial.Api.Data.Seed;

// Check the original numeric token before decimal conversion can round or underflow.
internal sealed class AreaJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number)
            throw new JsonException();
        using var token = JsonDocument.ParseValue(ref reader);
        var raw = token.RootElement.GetRawText();
        if (raw.Length > 1024)
            throw PrecisionError();

        var negative = raw.StartsWith('-');
        if (negative) raw = raw[1..];
        var exponentIndex = raw.IndexOfAny(['e', 'E']);
        var exponent = 0;
        if (exponentIndex >= 0)
        {
            if (!int.TryParse(raw[(exponentIndex + 1)..], NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out exponent) || exponent is < -1000 or > 1000)
                throw PrecisionError();
            raw = raw[..exponentIndex];
        }
        var dot = raw.IndexOf('.');
        var scale = (dot < 0 ? 0 : raw.Length - dot - 1) - exponent;
        var digits = raw.Replace(".", "").TrimStart('0');
        if (digits.Length == 0) return 0m;
        while (scale > 0 && digits.EndsWith('0'))
        {
            digits = digits[..^1];
            scale--;
        }
        if (scale > 2 || digits.Length - scale > 16)
            throw PrecisionError();
        if (scale <= 0) digits += new string('0', -scale);
        else
        {
            digits = digits.PadLeft(scale + 1, '0');
            digits = digits.Insert(digits.Length - scale, ".");
        }
        return decimal.Parse((negative ? "-" : "") + digits, CultureInfo.InvariantCulture);
    }

    private static SeedImportException PrecisionError() =>
        new("Площадь должна помещаться в numeric(18,2), без округления: до 16 цифр до запятой и 2 после.");

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}

using System.Text.Json;
using System.Text.Json.Serialization;
using UrbanSketch.Financial.Api.Dtos;
using UrbanSketch.Financial.Api.Models;

namespace UrbanSketch.Financial.Api.Validation;

internal static class FinancialModelValidation
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        NumberHandling = JsonNumberHandling.Strict,
        Converters = { new MoneyJsonConverter() }
    };

    public static async Task<(T? Request, Dictionary<string, string[]> Errors)> ReadAsync<T>(
        HttpRequest request, CancellationToken cancellationToken) where T : FinancialModelParametersRequest
    {
        if (!request.HasJsonContentType()) return InvalidBody<T>("Требуется Content-Type: application/json.");
        T? input;
        try { input = await request.ReadFromJsonAsync<T>(JsonOptions, cancellationToken); }
        catch (JsonException)
        {
            return InvalidBody<T>("Некорректный JSON: проверьте поля, Guid и типы. Денежные значения должны точно помещаться в numeric(20,2); неизвестные и повторяющиеся поля запрещены.");
        }
        catch (BadHttpRequestException) { return InvalidBody<T>("Не удалось прочитать тело JSON-запроса."); }
        if (input == null) return InvalidBody<T>("Требуется объект финансовых параметров, а не null.");

        var errors = new Dictionary<string, string[]>();
        foreach (var (field, value) in new (string, decimal?)[]
        {
            ("residentialConstructionCost", input.ResidentialConstructionCost),
            ("commercialConstructionCost", input.CommercialConstructionCost),
            ("parkingCost", input.ParkingCost), ("residentialSalePrice", input.ResidentialSalePrice),
            ("commercialSalePrice", input.CommercialSalePrice), ("parkingSalePrice", input.ParkingSalePrice),
            ("landCost", input.LandCost), ("additionalCosts", input.AdditionalCosts)
        })
        {
            if (value == null) errors[field] = ["Поле обязательно."];
            else if (value < 0) errors[field] = ["Значение не может быть отрицательным."];
            else if (value > 999999999999999999.99m || decimal.Round(value.Value, 2) != value.Value)
                errors[field] = ["Значение должно точно помещаться в numeric(20,2), без округления."];
        }
        if (input.ProjectDuration == null) errors["projectDuration"] = ["Поле обязательно."];
        else if (input.ProjectDuration <= 0) errors["projectDuration"] = ["Продолжительность должна быть положительным целым числом лет."];
        if (input is CreateFinancialModelRequest create && (create.ConceptVariantId == null || create.ConceptVariantId == Guid.Empty))
            errors["conceptVariantId"] = ["Обязателен непустой Guid концепции."];
        return (input, errors);
    }

    private static (T?, Dictionary<string, string[]>) InvalidBody<T>(string message) where T : FinancialModelParametersRequest =>
        (null, new Dictionary<string, string[]> { ["body"] = [message] });

    public static void Apply(FinancialModelParametersRequest input, FinancialModel model)
    {
        // Called only after successful validation. Identity, owner and calculation results are untouched.
        model.ResidentialConstructionCost = input.ResidentialConstructionCost!.Value;
        model.CommercialConstructionCost = input.CommercialConstructionCost!.Value;
        model.ParkingCost = input.ParkingCost!.Value;
        model.ResidentialSalePrice = input.ResidentialSalePrice!.Value;
        model.CommercialSalePrice = input.CommercialSalePrice!.Value;
        model.ParkingSalePrice = input.ParkingSalePrice!.Value;
        model.LandCost = input.LandCost!.Value;
        model.AdditionalCosts = input.AdditionalCosts!.Value;
        model.ProjectDuration = input.ProjectDuration!.Value;
    }
}

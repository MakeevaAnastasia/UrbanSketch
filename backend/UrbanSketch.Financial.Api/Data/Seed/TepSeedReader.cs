using System.Text.Json;
using System.Text.Json.Serialization;
using UrbanSketch.Financial.Api.Models;

namespace UrbanSketch.Financial.Api.Data.Seed;

public static class TepSeedReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        Converters = { new AreaJsonConverter() }
    };

    public static async Task<Project> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            return await ReadAsync(stream, cancellationToken);
        }
        catch (FileNotFoundException) { throw new SeedImportException("JSON-файл ТЭП не найден."); }
        catch (DirectoryNotFoundException) { throw new SeedImportException("Каталог JSON-файла ТЭП не найден."); }
        catch (UnauthorizedAccessException) { throw new SeedImportException("Нет доступа к JSON-файлу ТЭП."); }
        catch (IOException) { throw new SeedImportException("Не удалось прочитать JSON-файл ТЭП."); }
    }

    public static async Task<Project> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        TepSeedDocument? document;
        try { document = await JsonSerializer.DeserializeAsync<TepSeedDocument>(stream, Options, cancellationToken); }
        catch (JsonException)
        {
            throw new SeedImportException("Некорректный JSON ТЭП: проверьте структуру, типы, синтаксис, повторяющиеся и неизвестные поля.");
        }
        if (document == null) throw new SeedImportException("JSON должен содержать объект проекта.");
        var ids = new HashSet<Guid>();
        var project = new Project { Id = RequiredId(document.Id, ids), Name = RequiredName(document.Name, "проекта") };
        if (document.Variants == null || document.Variants.Count == 0)
            throw new SeedImportException("Проект должен содержать непустой массив variants.");
        foreach (var input in document.Variants)
        {
            if (input == null) throw new SeedImportException("В variants не допускается null вместо концепции.");
            var variant = new ConceptVariant
            {
                Id = RequiredId(input.Id, ids),
                Name = RequiredName(input.Name, "концепции"),
                ProjectId = project.Id,
                Project = project,
                ResidentialArea = RequiredArea(input.ResidentialArea, "residentialArea"),
                CommercialArea = RequiredArea(input.CommercialArea, "commercialArea"),
                ParkingSpaces = Nonnegative(input.ParkingSpaces, "parkingSpaces")
                    ?? throw new SeedImportException("Обязательно поле parkingSpaces."),
                ConstructionResidentialArea = Nonnegative(input.ConstructionResidentialArea, "constructionResidentialArea"),
                ConstructionCommercialArea = Nonnegative(input.ConstructionCommercialArea, "constructionCommercialArea"),
                ResidentsCount = Nonnegative(input.ResidentsCount, "residentsCount"),
                BuildingsCount = Nonnegative(input.BuildingsCount, "buildingsCount"),
                MaxFloors = Nonnegative(input.MaxFloors, "maxFloors"),
                SocialAreas = Nonnegative(input.SocialAreas, "socialAreas"),
                PreschoolPlaces = Nonnegative(input.PreschoolPlaces, "preschoolPlaces"),
                SchoolPlaces = Nonnegative(input.SchoolPlaces, "schoolPlaces")
            };
            project.Variants.Add(variant);
        }
        return project;
    }

    private static Guid RequiredId(Guid? id, HashSet<Guid> ids)
    {
        if (id == null || id == Guid.Empty) throw new SeedImportException("Обязателен непустой Guid в поле id.");
        if (!ids.Add(id.Value)) throw new SeedImportException("В JSON повторяются идентификаторы id.");
        return id.Value;
    }

    private static string RequiredName(string? name, string entity)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('\0'))
            throw new SeedImportException($"Необходимо непустое название {entity}, без нулевого символа.");
        return name.Trim();
    }

    private static decimal RequiredArea(decimal? value, string field) =>
        Nonnegative(value, field) ?? throw new SeedImportException($"Обязательно поле {field}.");

    private static T? Nonnegative<T>(T? value, string field) where T : struct, IComparable<T>
    {
        if (value.HasValue && value.Value.CompareTo(default) < 0)
            throw new SeedImportException($"Поле {field} не может быть отрицательным.");
        return value;
    }
}

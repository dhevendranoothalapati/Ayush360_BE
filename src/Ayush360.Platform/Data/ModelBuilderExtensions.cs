using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ayush360.Platform.Data;

public static partial class ModelBuilderExtensions
{
    /// <summary>
    /// Call LAST in OnModelCreating: renames every table, column, key, foreign key and index to snake_case
    /// (Users.DisplayName -> users.display_name, IX_Users_NormalizedUsername -> ix_users_normalized_username).
    /// </summary>
    public static ModelBuilder ApplySnakeCaseNames(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is not null)
            {
                entity.SetTableName(ToSnakeCase(table));
            }

            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName();
                property.SetColumnName(ToSnakeCase(column));
            }

            foreach (var key in entity.GetKeys())
            {
                var name = key.GetName();
                if (name is not null)
                {
                    key.SetName(ToSnakeCase(name));
                }
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                var name = foreignKey.GetConstraintName();
                if (name is not null)
                {
                    foreignKey.SetConstraintName(ToSnakeCase(name));
                }
            }

            foreach (var index in entity.GetIndexes())
            {
                var name = index.GetDatabaseName();
                if (name is not null)
                {
                    index.SetDatabaseName(ToSnakeCase(name));
                }
            }
        }

        return modelBuilder;
    }

    /// <summary>Store an enum as readable text: InConsultation -> "IN_CONSULTATION" (same codes as the frontend).</summary>
    public static PropertyBuilder<TEnum> HasEnumAsText<TEnum>(this PropertyBuilder<TEnum> property, int maxLength = 30)
        where TEnum : struct, Enum =>
        property
            .HasConversion(value => EnumText.ToCode(value), code => EnumText.FromCode<TEnum>(code))
            .HasMaxLength(maxLength);

    /// <summary>Same as <see cref="HasEnumAsText{TEnum}"/> for optional enums (NULL stays NULL).</summary>
    public static PropertyBuilder<TEnum?> HasNullableEnumAsText<TEnum>(this PropertyBuilder<TEnum?> property, int maxLength = 30)
        where TEnum : struct, Enum =>
        property
            .HasConversion(new ValueConverter<TEnum, string>(
                value => EnumText.ToCode(value),
                code => EnumText.FromCode<TEnum>(code)))
            .HasMaxLength(maxLength);

    /// <summary>
    /// Stores a details class as one PostgreSQL jsonb column (e.g. visits.additional_details).
    /// New properties can be added to the class later WITHOUT a migration: the database only sees one jsonb column.
    /// </summary>
    public static PropertyBuilder<T> HasJsonColumn<T>(this PropertyBuilder<T> property)
        where T : class, new()
    {
        var converter = new ValueConverter<T, string>(
            value => JsonColumn.Serialize(value),
            json => JsonColumn.Deserialize<T>(json));

        // Lets EF notice changes made inside the object (e.g. details.Attendant.Phone = "...").
        var comparer = new ValueComparer<T>(
            (left, right) => JsonColumn.Serialize(left) == JsonColumn.Serialize(right),
            value => JsonColumn.Serialize(value).GetHashCode(),
            value => JsonColumn.Deserialize<T>(JsonColumn.Serialize(value)));

        property.HasConversion(converter, comparer).HasColumnType("jsonb").IsRequired();
        return property;
    }

    public static string ToSnakeCase(string name)
    {
        var step1 = LowerToUpper().Replace(name, "$1_$2");
        var step2 = AcronymToWord().Replace(step1, "$1_$2");
        return step2.ToLowerInvariant();
    }

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex LowerToUpper();

    [GeneratedRegex("([A-Z]+)([A-Z][a-z])")]
    private static partial Regex AcronymToWord();
}

public static class EnumText
{
    public static string ToCode<TEnum>(TEnum value) where TEnum : struct, Enum =>
        ModelBuilderExtensions.ToSnakeCase(value.ToString()).ToUpperInvariant();

    public static TEnum FromCode<TEnum>(string code) where TEnum : struct, Enum =>
        Enum.Parse<TEnum>(code.Replace("_", string.Empty), ignoreCase: true);
}

/// <summary>JSON format for jsonb columns: camelCase names, enums as "OTHER_HOSPITAL", empty values left out.</summary>
public static class JsonColumn
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };

    public static string Serialize<T>(T? value) where T : class =>
        value is null ? "{}" : JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) where T : class, new() =>
        string.IsNullOrWhiteSpace(json) ? new T() : JsonSerializer.Deserialize<T>(json, Options) ?? new T();
}

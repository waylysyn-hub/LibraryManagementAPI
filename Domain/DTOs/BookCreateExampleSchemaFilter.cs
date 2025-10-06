using Domain.DTOs;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public class BookCreateExampleSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(BookCreateDto))
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiObject
            {
                ["title"] = new Microsoft.OpenApi.Any.OpenApiString("Clean Architecture"),
                ["author"] = new Microsoft.OpenApi.Any.OpenApiString("Robert C. Martin"),
                ["category"] = new Microsoft.OpenApi.Any.OpenApiString("Software Engineering"),
                ["year"] = new Microsoft.OpenApi.Any.OpenApiInteger(Math.Min(DateTime.UtcNow.Year, 2025)),
                ["copiesCount"] = new Microsoft.OpenApi.Any.OpenApiInteger(5),
                ["isbn"] = new Microsoft.OpenApi.Any.OpenApiString("978-0134494166")
            };
        }
        else if (context.Type == typeof(BookUpdateDto))
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiObject
            {
                ["title"] = new Microsoft.OpenApi.Any.OpenApiString("Refactoring"),
                ["author"] = new Microsoft.OpenApi.Any.OpenApiString("Martin Fowler"),
                ["category"] = new Microsoft.OpenApi.Any.OpenApiString("Software Engineering"),
                ["year"] = new Microsoft.OpenApi.Any.OpenApiInteger(Math.Min(DateTime.UtcNow.Year, 2025)),
                ["copiesCount"] = new Microsoft.OpenApi.Any.OpenApiInteger(3),
                ["isbn"] = new Microsoft.OpenApi.Any.OpenApiString("0-201-48567-2")
            };
        }
    }
}

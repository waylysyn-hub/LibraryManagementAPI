using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;


namespace ApiProject
{
    public class SwaggerFileOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var fileParams = context.MethodInfo.GetParameters()
                .Where(p => p.ParameterType == typeof(IFormFile)
                         || p.ParameterType.GetProperties().Any(pr => pr.PropertyType == typeof(IFormFile)))
                .ToList();

            if (!fileParams.Any()) return;

            operation.RequestBody = new OpenApiRequestBody
            {
                Content =
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = fileParams
                                .SelectMany(p =>
                                    p.ParameterType == typeof(IFormFile)
                                    ? new[] { new { Name = p.Name, Type = "string" } }
                                    : p.ParameterType.GetProperties()
                                        .Where(pr => pr.PropertyType == typeof(IFormFile))
                                        .Select(pr => new { Name = pr.Name, Type = "string" })
                                )
                                .ToDictionary(x => x.Name, x => new OpenApiSchema { Type = x.Type, Format = "binary" })
                        }
                    }
                }
            };
        }
    }
}

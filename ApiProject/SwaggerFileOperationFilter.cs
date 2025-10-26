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

            if (fileParams.Count == 0) return;

            operation.RequestBody = new OpenApiRequestBody
            {
                Content =
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                       Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties =
                            fileParams
                            .SelectMany(p =>
                                p.ParameterType == typeof(IFormFile)
                                    // اسم البراميتر قد يكون nullable، فنوفر بديل آمن
                                    ? [p.Name ?? "file"]
                                    : p.ParameterType.GetProperties()
                                        .Where(pr => pr.PropertyType == typeof(IFormFile))
                                        .Select(pr => pr.Name) // PropertyInfo.Name غير nullable
                            )
                            .Distinct() // لو تكرر نفس الحقل من أكثر من مسار
                            .ToDictionary(
                                name => name,
                                name => new OpenApiSchema { Type = "string", Format = "binary" }
                            )
                        }

                    }
                }
            };
        }
    }
}

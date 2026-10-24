using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api.Swagger;

public sealed class BearerSecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        var path=context.ApiDescription.RelativePath?.Split('?')[0];
        if(path is "api/game-tickets" or "api/me/matches")
        {
            // Separate requirements mean OR. Guest authentication does not authorize RoomsController.
            operation.Security=new List<OpenApiSecurityRequirement> {
                new() { [new OpenApiSecuritySchemeReference("Bearer",context.Document)]=new List<string>() },
                new() { [new OpenApiSecuritySchemeReference("GuestProof",context.Document)]=new List<string>() }
            };
            return;
        }
        if(path?.StartsWith("internal/")==true) return;
        if(metadata.OfType<IAllowAnonymous>().Any() || !metadata.OfType<IAuthorizeData>().Any()) return;
        operation.Security=new List<OpenApiSecurityRequirement> { new() { [new OpenApiSecuritySchemeReference("Bearer",context.Document)]=new List<string>() } };
    }
}

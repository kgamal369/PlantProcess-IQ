using Microsoft.Extensions.DependencyInjection;
using PlantProcess.Application.Assistant;
using PlantProcess.Application.Top15.Phase34;

namespace PlantProcess.Infrastructure.Assistant;

public static class AssistantInfrastructureExtensions
{
    /// <summary>
    /// Registers the assistant retrieval index, tool layer, grounding model and service.
    /// To use a real LLM/embeddings, register your own IAssistantModel / IEmbedder AFTER this call.
    /// </summary>
    public static IServiceCollection AddAssistant(this IServiceCollection services)
    {
        services.AddSingleton<IEmbedder, PlantProcess.Application.Assistant.LocalSemanticEmbedder>();
        services.AddScoped<IRetrievalIndex, NpgsqlRetrievalIndex>();

        services.AddScoped<ITool, FetchFindingTool>();
        services.AddScoped<ITool, OpenSuggestionTool>();
        services.AddScoped<ITool, RunKpiTool>();
        services.AddScoped<ToolRegistry>();

                services.AddSingleton<ITop15AssistantModelClient, Top15HttpAssistantModelClient>();
        services.AddSingleton<IAssistantModel>(sp =>
        {
            var phase34ModelConfig = Top15ModelEndpointConfig.FromEnvironment();

            return phase34ModelConfig.IsConfigured
                ? new Top15RealAssistantModel(phase34ModelConfig, sp.GetRequiredService<ITop15AssistantModelClient>())
                : new ExtractiveAssistantModel();
        });
        services.AddScoped<IAssistantChunkProducer, CanonicalChunkProducer>();
        services.AddScoped<AssistantService>();
        return services;
    }
}

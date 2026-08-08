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
        // T-073. The registered producer is a COMPOSITE. CanonicalChunkProducer is
        // untouched and keeps its five families; the widget-result family is a
        // separate class, so the type that reads the canonical substrate does not
        // acquire a dependency on dashboard execution.
        services.AddScoped<CanonicalChunkProducer>();
        services.AddScoped<WidgetResultChunkProducer>();

        // T-073 validation point 4. The tenant-scoped snapshot read, which is a
        // different contract from the provenance resolver on purpose: the
        // resolver proves a handle exists and carries no tenant, this returns
        // content and therefore must carry one.
        services.AddScoped<IWidgetResultEvidenceReader, NpgsqlWidgetResultEvidenceReader>();

        // T-074: the parameter registry, sole authority for quantity semantics.
        services.AddScoped<IParameterQuantityRegistry, NpgsqlParameterQuantityRegistry>();
        // T-073: AssistantService now requires it for the contextual evidence anchor,
        // so a missing registration is a startup failure rather than a silent
        // disabling of the rule.
        services.AddScoped<IAssistantChunkProducer, CompositeChunkProducer>();
        services.AddScoped<AssistantService>();
        return services;
    }
}

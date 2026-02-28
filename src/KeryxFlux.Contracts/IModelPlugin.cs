namespace KeryxFlux.Contracts;

/// <summary>
/// Plugin interface for model-enhanced transformations.
/// 
/// Model plugins work in steps:
/// 1. ParseInitialMessage: Determine what model calls are needed
/// 2. TransformModelResponse: Process each model response, decide next step
/// 
/// Plugin orchestrates the workflow, infrastructure handles HTTP/gRPC calls.
/// </summary>
public interface IModelPlugin : IKeryxFluxPlugin
{
    /// <summary>
    /// Parse the initial message to determine model invocation strategy.
    /// Plugin decides: call model immediately, or process directly without model.
    /// </summary>
    /// <param name="data">Raw message bytes</param>
    /// <param name="context">Execution context</param>
    /// <returns>Plan indicating first model step or direct completion</returns>
    ModelInvocationPlan ParseInitialMessage(byte[] data, TransformationContext context);

    /// <summary>
    /// Transform a model response, deciding what to do next.
    /// 
    /// Plugin receives the model's response and decides:
    /// - ContinueWith(nextStep): Need another model call
    /// - Complete(finalData): Processing done, forward the message
    /// 
    /// Example flow:
    /// 1. Step: Call warm_brain for extraction ? ContinueWith(validator)
    /// 2. Step: Call validator for verification ? ContinueWith(enricher)
    /// 3. Step: Call enricher for final output ? Complete(result)
    /// </summary>
    /// <param name="currentStep">The step that just executed</param>
    /// <param name="modelResponse">Response from the model</param>
    /// <param name="state">Accumulated state across all steps</param>
    /// <returns>Next step to execute, or completion with final data</returns>
    ModelStepResult TransformModelResponse(
        ModelStep currentStep,
        byte[] modelResponse,
        AccumulatedState state);
}

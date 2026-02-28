namespace KeryxFlux.Contracts;

/// <summary>
/// Plugin interface for poller-type dockets (scheduled polling of external APIs).
/// 
/// Pollers work in two phases:
/// 1. ParseInitialResponse: Extract items from initial poll (e.g., list of articles, jobs)
/// 2. TransformItemStep: Process each step for an item, deciding the next step
/// 
/// Each item becomes an independent workflow with sequential steps.
/// Plugin decides one step at a time: "process current step ? return next step or complete"
/// </summary>
public interface IPollerPlugin : IKeryxFluxPlugin
{
    /// <summary>
    /// Parse the initial polling response to extract individual items.
    /// Each item will be processed as an independent workflow.
    /// 
    /// Example: Initial poll returns 10 items ? Create 10 independent workflows
    /// </summary>
    /// <param name="source">Raw bytes from initial poll</param>
    /// <param name="context">Execution context</param>
    /// <returns>Result containing extracted items or error</returns>
    InitialPollingResult ParseInitialResponse(byte[] source, TransformationContext context);

    /// <summary>
    /// Transform a single step for a specific item, deciding what to do next.
    /// 
    /// Plugin receives the current step's response and decides:
    /// - ContinueWith(nextStep) ? Need one more API call
    /// - Complete(finalData) ? This item is done, forward the message
    /// 
    /// Example flow:
    /// 1. Step: GET /Job/j1 ? ContinueWith(GET /Job/j1/Details)
    /// 2. Step: GET /Job/j1/Details ? ContinueWith(GET /Job/j1/Status)
    /// 3. Step: GET /Job/j1/Status ? Complete(aggregatedBundle)
    /// </summary>
    /// <param name="currentStep">The step that just executed</param>
    /// <param name="stepResponse">Response from that step</param>
    /// <param name="itemState">State accumulated for THIS item only</param>
    /// <param name="itemId">Unique identifier for this item</param>
    /// <returns>Next step to execute, or completion with final data</returns>
    StepTransformationResult TransformItemStep(
        NextStep currentStep,
        byte[] stepResponse,
        AccumulatedState itemState,
        string itemId);
}



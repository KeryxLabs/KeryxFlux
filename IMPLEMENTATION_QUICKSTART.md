# KeryxFlux 2.0: Implementation Quick-Start Guide

**For:** Getting started on the pivot THIS WEEK  
**Target:** Ship first new feature (conditional branching) in 7 days

---

## Day 1 (Monday): Design & Architecture

### Morning: Define the Conditional Step Schema

Create: `docs/yaml-schema/conditional-steps.md`

```yaml
# Example of what we're building
name: fraud-detection-workflow
steps:
  - id: fetch-transaction
    action: http_request
    url: "https://api.bank.com/transactions/${input.txn_id}"
    
  - id: calculate-risk
    action: csharp_script
    script: |
      var amount = input.amount;
      var location = input.location;
      return new { score = amount > 1000 && location == "foreign" ? 90 : 10 };
    
  - id: risk-decision
    action: conditional
    condition: "${steps.calculate-risk.output.score > 80}"
    then:
      - id: manual-review
        action: http_request
        url: "https://review.com/api/create"
      - id: wait-for-approval
        action: wait_for_event
        event_type: "approval_decision"
        timeout: "24h"
    else:
      - id: auto-approve
        action: http_request
        url: "https://api.bank.com/approve/${input.txn_id}"
```

### Afternoon: Create Domain Models

**File:** `src/KeryxFlux.Domain/Models/Steps/ConditionalStep.cs`

```csharp
namespace KeryxFlux.Domain.Models.Steps;

/// <summary>
/// Represents a conditional step that executes different branches based on an expression
/// </summary>
public sealed class ConditionalStep : IStep
{
    public required string Id { get; init; }
    
    /// <summary>
    /// Expression to evaluate (e.g., "${steps.risk.score > 80}")
    /// </summary>
    public required string Condition { get; init; }
    
    /// <summary>
    /// Steps to execute if condition is true
    /// </summary>
    public List<IStep> ThenSteps { get; init; } = new();
    
    /// <summary>
    /// Steps to execute if condition is false (optional)
    /// </summary>
    public List<IStep>? ElseSteps { get; init; }
    
    public StepType Type => StepType.Conditional;
}

public enum StepType
{
    HttpRequest,
    Conditional,
    Parallel,
    Plugin,
    Script,
    WaitForEvent
}
```

---

## Day 2 (Tuesday): Expression Evaluator

### Create: `src/KeryxFlux.Domain/Expressions/ExpressionEvaluator.cs`

```csharp
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KeryxFlux.Domain.Expressions;

/// <summary>
/// Evaluates expressions like "${steps.risk.score > 80}"
/// </summary>
public class ExpressionEvaluator
{
    private readonly Dictionary<string, object> _context;
    
    public ExpressionEvaluator(Dictionary<string, object> context)
    {
        _context = context;
    }
    
    public bool EvaluateBoolean(string expression)
    {
        // Remove ${ and }
        expression = expression.Trim().TrimStart('$', '{').TrimEnd('}');
        
        // Parse expression: "steps.risk.score > 80"
        var parts = Regex.Match(expression, @"^(.+?)\s*(==|!=|>|<|>=|<=)\s*(.+)$");
        
        if (!parts.Success)
        {
            throw new ArgumentException($"Invalid expression: {expression}");
        }
        
        var leftPath = parts.Groups[1].Value.Trim();
        var op = parts.Groups[2].Value;
        var rightValue = parts.Groups[3].Value.Trim();
        
        // Resolve left side (e.g., "steps.risk.score")
        var leftResolved = ResolvePath(leftPath);
        
        // Parse right side
        var rightResolved = ParseValue(rightValue);
        
        // Compare
        return Compare(leftResolved, op, rightResolved);
    }
    
    private object ResolvePath(string path)
    {
        // Split by dots: "steps.risk.score" ? ["steps", "risk", "score"]
        var parts = path.Split('.');
        object current = _context;
        
        foreach (var part in parts)
        {
            if (current is Dictionary<string, object> dict)
            {
                if (!dict.TryGetValue(part, out var value))
                {
                    throw new KeyNotFoundException($"Path not found: {path}");
                }
                current = value;
            }
            else if (current is JsonElement json)
            {
                current = json.GetProperty(part);
            }
            else
            {
                var prop = current.GetType().GetProperty(part);
                if (prop == null)
                {
                    throw new KeyNotFoundException($"Property not found: {part}");
                }
                current = prop.GetValue(current)!;
            }
        }
        
        return current;
    }
    
    private object ParseValue(string value)
    {
        // Try parse as number
        if (double.TryParse(value, out var number))
        {
            return number;
        }
        
        // Try parse as boolean
        if (bool.TryParse(value, out var boolean))
        {
            return boolean;
        }
        
        // Remove quotes if string literal
        if (value.StartsWith("\"") && value.EndsWith("\""))
        {
            return value.Trim('"');
        }
        
        return value;
    }
    
    private bool Compare(object left, string op, object right)
    {
        return op switch
        {
            "==" => Equals(left, right),
            "!=" => !Equals(left, right),
            ">" => Convert.ToDouble(left) > Convert.ToDouble(right),
            "<" => Convert.ToDouble(left) < Convert.ToDouble(right),
            ">=" => Convert.ToDouble(left) >= Convert.ToDouble(right),
            "<=" => Convert.ToDouble(left) <= Convert.ToDouble(right),
            _ => throw new NotSupportedException($"Operator not supported: {op}")
        };
    }
}
```

### Tests: `tests/KeryxFlux.Domain.Tests/Expressions/ExpressionEvaluatorTests.cs`

```csharp
using KeryxFlux.Domain.Expressions;
using Shouldly;
using Xunit;

namespace KeryxFlux.Domain.Tests.Expressions;

public class ExpressionEvaluatorTests
{
    [Fact]
    public void EvaluateBoolean_NumberComparison_ReturnsTrue()
    {
        var context = new Dictionary<string, object>
        {
            ["steps"] = new Dictionary<string, object>
            {
                ["risk"] = new { score = 90 }
            }
        };
        
        var evaluator = new ExpressionEvaluator(context);
        var result = evaluator.EvaluateBoolean("${steps.risk.score > 80}");
        
        result.ShouldBeTrue();
    }
    
    [Fact]
    public void EvaluateBoolean_StringComparison_ReturnsFalse()
    {
        var context = new Dictionary<string, object>
        {
            ["input"] = new { status = "pending" }
        };
        
        var evaluator = new ExpressionEvaluator(context);
        var result = evaluator.EvaluateBoolean("${input.status == \"approved\"}");
        
        result.ShouldBeFalse();
    }
}
```

---

## Day 3 (Wednesday): Conditional Step Execution

### Create: `src/KeryxFlux.Application/Executors/ConditionalStepExecutor.cs`

```csharp
namespace KeryxFlux.Application.Executors;

public class ConditionalStepExecutor
{
    private readonly IStepExecutorFactory _executorFactory;
    
    public ConditionalStepExecutor(IStepExecutorFactory executorFactory)
    {
        _executorFactory = executorFactory;
    }
    
    public async Task<StepResult> ExecuteAsync(
        ConditionalStep step, 
        WorkflowContext context)
    {
        // Build context for expression evaluation
        var expressionContext = new Dictionary<string, object>
        {
            ["steps"] = context.StepOutputs,
            ["input"] = context.Input,
            ["env"] = context.Environment
        };
        
        // Evaluate condition
        var evaluator = new ExpressionEvaluator(expressionContext);
        bool conditionResult;
        
        try
        {
            conditionResult = evaluator.EvaluateBoolean(step.Condition);
        }
        catch (Exception ex)
        {
            return StepResult.Failed($"Condition evaluation failed: {ex.Message}");
        }
        
        // Execute appropriate branch
        var stepsToExecute = conditionResult ? step.ThenSteps : step.ElseSteps;
        
        if (stepsToExecute == null || !stepsToExecute.Any())
        {
            return StepResult.Success(new { branch = conditionResult ? "then" : "else", executed = false });
        }
        
        // Execute each step in the branch
        var results = new List<object>();
        
        foreach (var branchStep in stepsToExecute)
        {
            var executor = _executorFactory.GetExecutor(branchStep.Type);
            var result = await executor.ExecuteAsync(branchStep, context);
            
            if (!result.IsSuccess)
            {
                return result; // Fail fast
            }
            
            results.Add(result.Output);
        }
        
        return StepResult.Success(new 
        { 
            branch = conditionResult ? "then" : "else",
            executed = true,
            results = results
        });
    }
}
```

---

## Day 4 (Thursday): Integration

### Update: `src/KeryxFlux.Domain/Models/Docket.cs`

Add support for new step types:

```csharp
[YamlMember(Alias = "steps")]
public List<IStep>? Steps { get; init; }

// Steps can now be: HttpRequestStep, ConditionalStep, ParallelStep, etc.
```

### Create YAML Deserializer:

```csharp
// src/KeryxFlux.Infrastructure/Serialization/StepDeserializer.cs
public class StepDeserializer : INodeDeserializer
{
    public bool Deserialize(IParser reader, Type expectedType, 
        Func<IParser, Type, object?> nestedObjectDeserializer, 
        out object? value)
    {
        if (expectedType != typeof(IStep))
        {
            value = null;
            return false;
        }
        
        // Read the step YAML
        var node = (MappingNode)reader.Consume<MappingNode>();
        var action = node.Children
            .FirstOrDefault(x => x.Key.ToString() == "action")
            .Value?.ToString();
        
        return action switch
        {
            "conditional" => DeserializeConditionalStep(node, out value),
            "http_request" => DeserializeHttpRequestStep(node, out value),
            _ => throw new NotSupportedException($"Unknown step action: {action}")
        };
    }
}
```

---

## Day 5 (Friday): Testing & CLI

### Integration Test:

```csharp
[Fact]
public async Task ConditionalStep_WhenConditionTrue_ExecutesThenBranch()
{
    var workflow = """
        name: test-conditional
        steps:
          - id: set-value
            action: http_request
            url: "https://api.example.com/value"
            
          - id: check
            action: conditional
            condition: "${steps.set-value.output.value > 50}"
            then:
              - id: high-value
                action: http_request
                url: "https://api.example.com/high"
            else:
              - id: low-value
                action: http_request
                url: "https://api.example.com/low"
        """;
    
    var result = await _orchestrator.ExecuteWorkflowAsync(workflow);
    
    result.ShouldSatisfyAllConditions(
        () => result.IsSuccess.ShouldBeTrue(),
        () => result.StepsExecuted.ShouldContain("high-value"),
        () => result.StepsExecuted.ShouldNotContain("low-value")
    );
}
```

### CLI Command:

```sh
# Test conditional workflow
keryxflux test conditional-workflow.yaml --mock-data test-input.json

# Validate
keryxflux validate conditional-workflow.yaml

# Dry-run (show which branch would execute)
keryxflux dry-run conditional-workflow.yaml --input test-input.json
```

---

## Day 6-7 (Weekend): Documentation & Example

### Create: `examples/conditional-fraud-detection.yaml`

```yaml
name: fraud-detection
description: "Detect fraudulent transactions and route for review"

steps:
  - id: fetch-transaction
    action: http_request
    url: "https://api.bank.com/transactions/${input.txn_id}"
    method: GET
    
  - id: calculate-risk
    action: csharp_script
    script: |
      var amount = (double)input["amount"];
      var location = (string)input["location"];
      var history = (int)input["customer_history_months"];
      
      var score = 0;
      if (amount > 1000) score += 40;
      if (location == "foreign") score += 30;
      if (history < 6) score += 30;
      
      return new { score = score, reason = $"Amount: {amount}, Location: {location}" };
    
  - id: fraud-check
    action: conditional
    condition: "${steps.calculate-risk.output.score >= 70}"
    then:
      # High risk - manual review
      - id: create-review-task
        action: http_request
        url: "https://review.acme.com/api/tasks"
        method: POST
        body:
          transaction_id: "${input.txn_id}"
          risk_score: "${steps.calculate-risk.output.score}"
          reason: "${steps.calculate-risk.output.reason}"
          
      - id: notify-fraud-team
        action: http_request
        url: "https://slack.com/api/chat.postMessage"
        method: POST
        headers:
          Authorization: "Bearer ${env.SLACK_TOKEN}"
        body:
          channel: "#fraud-alerts"
          text: "?? High-risk transaction detected: ${input.txn_id}"
          
      - id: wait-for-decision
        action: wait_for_event
        event_type: "review_completed"
        timeout: "24h"
        
    else:
      # Low risk - auto-approve
      - id: approve-transaction
        action: http_request
        url: "https://api.bank.com/transactions/${input.txn_id}/approve"
        method: POST
        
      - id: send-confirmation
        action: http_request
        url: "https://api.bank.com/notifications/send"
        method: POST
        body:
          customer_id: "${input.customer_id}"
          message: "Your transaction has been approved"

tests:
  - name: high-risk-foreign-transaction
    input:
      txn_id: "txn-123"
      amount: 5000
      location: "foreign"
      customer_history_months: 2
    expected_steps_executed:
      - fetch-transaction
      - calculate-risk
      - fraud-check
      - create-review-task
      - notify-fraud-team
    expected_final_state:
      status: "pending_review"
      
  - name: low-risk-domestic-transaction
    input:
      txn_id: "txn-456"
      amount: 50
      location: "domestic"
      customer_history_months: 24
    expected_steps_executed:
      - fetch-transaction
      - calculate-risk
      - fraud-check
      - approve-transaction
      - send-confirmation
    expected_final_state:
      status: "approved"
```

### Write: `docs/features/conditional-branching.md`

Complete documentation with:
- Concept explanation
- YAML syntax reference
- Expression language guide
- Real-world examples
- Best practices
- Troubleshooting

---

## Week 2: Parallel Execution

Follow similar pattern:
1. Design YAML schema
2. Create domain models
3. Implement executor (using Task.WhenAll)
4. Add tests
5. Update CLI
6. Document

---

## Success Criteria for Week 1

By end of Day 7:
- [ ] Conditional steps work end-to-end
- [ ] Expression evaluator supports >, <, ==, !=, >=, <=
- [ ] 10+ passing tests
- [ ] Example workflow runs successfully
- [ ] Documentation complete
- [ ] CLI can validate conditional workflows

**Ship it!** Blog post: "KeryxFlux 2.0: Conditional Workflows"

---

## Recommended Development Flow

**Daily Routine:**
1. Morning: Design/architecture (1-2 hours)
2. Afternoon: Implementation (3-4 hours)
3. Evening: Testing & documentation (1-2 hours)

**Weekend:**
- Polish
- Examples
- Blog post draft
- Tweet progress

**Tools:**
- Branch: `feature/conditional-steps`
- Commit often
- Push daily (show progress)

---

**Ready to start?** 

Pick a day this week to begin. Even 2 hours/day gets you there in 7 days.

**First commit:** Create `ConditionalStep.cs` model. That's it. Just start.

Let me know when you're ready and I'll help with any specific piece! ??

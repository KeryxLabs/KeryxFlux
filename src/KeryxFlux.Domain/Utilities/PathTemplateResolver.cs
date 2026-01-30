namespace KeryxFlux.Domain.Utilities;

/// <summary>
/// Resolves path templates with variable substitution.
/// Example: "/api/{environment}/patients/{business_id}" with {environment: "prod", business_id: "123"}
/// becomes: "/api/prod/patients/123"
/// </summary>
public static class PathTemplateResolver
{
    /// <summary>
    /// Resolve template variables in a path string
    /// </summary>
    /// <param name="template">Path template with {variable} placeholders</param>
    /// <param name="variables">Dictionary of variable names and values</param>
    /// <returns>Resolved path with variables substituted</returns>
    public static string Resolve(string template, IReadOnlyDictionary<string, string>? variables)
    {
        if (string.IsNullOrWhiteSpace(template) || variables == null || variables.Count == 0)
        {
            return template;
        }

        var result = template;

        foreach (var (key, value) in variables)
        {
            // Support both {key} and {Key} (case-insensitive)
            result = result.Replace($"{{{key}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    /// <summary>
    /// Check if a template contains any unresolved variables
    /// </summary>
    /// <param name="path">Path to check</param>
    /// <returns>True if path contains {variable} patterns</returns>
    public static bool HasUnresolvedVariables(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path.Contains('{') && path.Contains('}');
    }

    /// <summary>
    /// Extract variable names from a template
    /// </summary>
    /// <param name="template">Path template</param>
    /// <returns>List of variable names found in template</returns>
    public static IEnumerable<string> ExtractVariableNames(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            yield break;
        }

        var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var startIndex = 0;

        while (true)
        {
            var openBrace = template.IndexOf('{', startIndex);
            if (openBrace == -1) break;

            var closeBrace = template.IndexOf('}', openBrace);
            if (closeBrace == -1) break;

            var variableName = template.Substring(openBrace + 1, closeBrace - openBrace - 1).Trim();
            if (!string.IsNullOrWhiteSpace(variableName))
            {
                variables.Add(variableName);
            }

            startIndex = closeBrace + 1;
        }

        foreach (var variable in variables)
        {
            yield return variable;
        }
    }

    /// <summary>
    /// Validate that all required variables are provided
    /// </summary>
    /// <param name="template">Path template</param>
    /// <param name="variables">Available variables</param>
    /// <param name="missingVariables">Output: list of missing variables</param>
    /// <returns>True if all variables are available</returns>
    public static bool ValidateVariables(
        string template, 
        IReadOnlyDictionary<string, string>? variables,
        out List<string> missingVariables)
    {
        missingVariables = new List<string>();
        
        var requiredVariables = ExtractVariableNames(template).ToList();
        if (requiredVariables.Count == 0)
        {
            return true; // No variables needed
        }

        if (variables == null || variables.Count == 0)
        {
            missingVariables.AddRange(requiredVariables);
            return false;
        }

        foreach (var required in requiredVariables)
        {
            if (!variables.ContainsKey(required))
            {
                missingVariables.Add(required);
            }
        }

        return missingVariables.Count == 0;
    }
}

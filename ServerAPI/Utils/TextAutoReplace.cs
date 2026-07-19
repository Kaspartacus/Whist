using System.Text.RegularExpressions;
using Core;
using Microsoft.Extensions.Logging;

namespace ServerAPI.Utils;

/// <summary>
/// Centralt sted til interne "text rules".
/// </summary>
public static class TextAutoReplace
{
    private static readonly Regex KsdhRegex = new(
        @"(?<![\p{L}\p{N}])K[\W_]*S[\W_]*D[\W_]*H(?![\p{L}\p{N}])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AgfRegex = new(
        @"(?<![\p{L}\p{N}])A[\W_]*G[\W_]*F(?![\p{L}\p{N}])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex HaderBrondbyRegex = new(
        @"(?<![\p{L}\p{N}])h[\W_]*a[\W_]*d[\W_]*e[\W_]*r[\W_]+b[\W_]*r[\W_]*ø[\W_]*n[\W_]*d[\W_]*b[\W_]*y(?![\p{L}\p{N}])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Erstatter interne tekstmønstre case-insensitive og med tolerant tegnsætning.</summary>
    public static string? Apply(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var updated = KsdhRegex.Replace(input, "BIF <3");
        updated = AgfRegex.Replace(updated, "BIF <3");
        updated = HaderBrondbyRegex.Replace(updated, "elsker Brøndby");
        return updated;
    }

    public static string? Apply(string? input, ILogger logger, string fieldName)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var updated = Apply(input);
        if (!string.Equals(input, updated, StringComparison.Ordinal))
            logger.LogInformation("Text auto-replace applied to {FieldName}.", fieldName);

        return updated;
    }

    // --- Model-specifik helpers (så repos forbliver simple) ---

    public static void Apply(User user)
    {
        // Email and authentication credentials are intentionally handled elsewhere.
        user.Name = Apply(user.Name) ?? "";
        user.NickName = Apply(user.NickName) ?? "";
        user.Address = Apply(user.Address) ?? "";
        user.Description = Apply(user.Description) ?? "";
        user.FunFact = Apply(user.FunFact) ?? "";
        // ImageUrl lader vi være.
    }

    public static void Apply(Fine fine)
    {
        fine.Comment = Apply(fine.Comment) ?? "";
    }

    public static void Apply(Fine fine, ILogger logger)
    {
        fine.Comment = Apply(fine.Comment, logger, "Fine.Comment") ?? "";
    }

    public static void Apply(Highlight highlight)
    {
        highlight.Title = Apply(highlight.Title) ?? "";
        highlight.Description = Apply(highlight.Description) ?? "";
        // ImageUrl lader vi være.
    }

    public static void Apply(Highlight highlight, ILogger logger)
    {
        highlight.Title = Apply(highlight.Title, logger, "Highlight.Title") ?? "";
        highlight.Description = Apply(highlight.Description, logger, "Highlight.Description") ?? "";
        // ImageUrl lader vi være.
    }

    public static void Apply(Calendar calendar)
    {
        calendar.Note = Apply(calendar.Note) ?? "";
    }

    public static void Apply(Calendar calendar, ILogger logger)
    {
        calendar.Note = Apply(calendar.Note, logger, "Calendar.Note") ?? "";
    }

    public static void Apply(Rule rule)
    {
        rule.Text = Apply(rule.Text) ?? "";
    }

    public static void Apply(Rule rule, ILogger logger)
    {
        rule.Text = Apply(rule.Text, logger, "Rule.Text") ?? "";
    }
}

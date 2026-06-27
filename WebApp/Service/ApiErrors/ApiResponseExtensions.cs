using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace WebApp.Service.ApiErrors;

public static class ApiResponseExtensions
{
    public static async Task EnsureSuccessWithApiMessageAsync(this HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        throw new ApiRequestException(
            response.StatusCode,
            await GetSafeErrorMessageAsync(response));
    }

    public static async Task<T?> ReadFromJsonOrThrowAsync<T>(this HttpResponseMessage response)
    {
        await response.EnsureSuccessWithApiMessageAsync();
        return await response.Content.ReadFromJsonAsync<T>();
    }

    private static async Task<string> GetSafeErrorMessageAsync(HttpResponseMessage response)
    {
        if ((int)response.StatusCode >= 500)
            return "Der skete en serverfejl. Prøv igen om lidt.";

        var content = await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(content))
        {
            var message = TryReadJsonErrorMessage(content) ?? TryReadPlainTextErrorMessage(content);
            if (!string.IsNullOrWhiteSpace(message))
                return message;
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Du skal logge ind igen.",
            HttpStatusCode.Forbidden => "Du har ikke adgang til denne handling.",
            HttpStatusCode.TooManyRequests => "For mange forsøg. Prøv igen om lidt.",
            HttpStatusCode.BadRequest => "Der er fejl i de indtastede oplysninger.",
            HttpStatusCode.NotFound => "Det ønskede indhold blev ikke fundet.",
            _ => "Handlingen kunne ikke gennemføres. Prøv igen."
        };
    }

    private static string? TryReadJsonErrorMessage(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            if (TryGetStringProperty(root, "message", out var message))
                return message;

            var validationMessages = ReadValidationMessages(root);
            if (validationMessages.Count > 0)
                return string.Join(" ", validationMessages);

            if (TryGetStringProperty(root, "detail", out var detail))
                return detail;

            if (TryGetStringProperty(root, "title", out var title))
                return title;
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static List<string> ReadValidationMessages(JsonElement root)
    {
        var messages = new List<string>();

        if (!root.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Object)
            return messages;

        foreach (var property in errors.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var error in property.Value.EnumerateArray())
            {
                if (error.ValueKind == JsonValueKind.String)
                    messages.Add(error.GetString() ?? "");
            }
        }

        return messages
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Take(3)
            .ToList();
    }

    private static bool TryGetStringProperty(JsonElement root, string propertyName, out string message)
    {
        message = "";

        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return false;

        message = property.GetString() ?? "";
        return !string.IsNullOrWhiteSpace(message);
    }

    private static string? TryReadPlainTextErrorMessage(string content)
    {
        var trimmed = content.Trim();
        return trimmed.StartsWith('<') ? null : trimmed;
    }
}

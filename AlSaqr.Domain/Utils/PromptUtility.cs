using System.Text;

namespace AlSaqr.Domain.Utils
{
    /// <summary>
    /// Builds the standard prompt sent to the Gemini LLM so every Yumna request
    /// shares the same persona, guardrails and formatting (§DRY — one place, not
    /// per-controller string concatenation).
    /// </summary>
    public static class PromptUtility
    {
        public static string BuildStandardPrompt(
            string agentName,
            string userPrompt,
            string? username = null,
            string? context = null,
            string? responseStyle = null,
            int? maxResponseWords = null)
        {
            var prompt = new StringBuilder();

            prompt.AppendLine($"You are {agentName}, a friendly and knowledgeable AI assistant for the AlSaqr platform.");
            prompt.AppendLine("Answer the user's message helpfully and respectfully.");

            if (!string.IsNullOrWhiteSpace(username))
                prompt.AppendLine($"The user you are talking to is named {username}.");

            if (!string.IsNullOrWhiteSpace(context))
            {
                prompt.AppendLine("Additional context for this conversation:");
                prompt.AppendLine(context);
            }

            if (!string.IsNullOrWhiteSpace(responseStyle))
                prompt.AppendLine($"Respond in the following style: {responseStyle}.");

            if (maxResponseWords.HasValue)
                prompt.AppendLine($"Keep your response under {maxResponseWords.Value} words.");

            prompt.AppendLine();
            prompt.AppendLine("User message:");
            prompt.AppendLine(userPrompt);

            return prompt.ToString();
        }
    }
}

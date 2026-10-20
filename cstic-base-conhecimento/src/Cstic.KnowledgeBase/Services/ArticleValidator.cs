using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml;
using Cstic.KnowledgeBase.Models;

namespace Cstic.KnowledgeBase.Services;

public static class ArticleValidator
{
    /// <summary>Maximum size of the JSON form sent by the browser to generate a document.</summary>
    public const int MaxPayloadLength = 180_000;
    public const string PayloadTooLongMessage = "O POP excede o limite total de 180.000 caracteres. Reduza os textos ou divida o conteúdo em mais de um POP.";
    // Matches JSON.stringify in the browser: camelCase names and accents kept unescaped.
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static int PayloadLength(KnowledgeArticle article) => JsonSerializer.Serialize(article, PayloadOptions).Length;

    public static Dictionary<string, string[]> Validate(KnowledgeArticle article)
    {
        var errors = new Dictionary<string, List<string>>();
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var messages)) errors[field] = messages = [];
            if (!messages.Contains(message)) messages.Add(message);
        }
        void CheckText(string field, string? text)
        {
            if (text is null) return;
            try { XmlConvert.VerifyXmlChars(text); }
            catch (XmlException) { Add(field, "Remova os caracteres de controle inválidos deste campo."); }
        }
        void ValidateObject(object value, string prefix)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(value, new ValidationContext(value), results, true);
            foreach (var result in results)
                foreach (var member in result.MemberNames.DefaultIfEmpty(""))
                    Add(prefix + JsonNamingPolicy.CamelCase.ConvertName(member), result.ErrorMessage ?? "Campo inválido.");
            foreach (var property in value.GetType().GetProperties().Where(p => p.PropertyType == typeof(string)))
                CheckText(prefix + JsonNamingPolicy.CamelCase.ConvertName(property.Name), property.GetValue(value) as string);
        }

        ValidateObject(article, "");
        if (article.Actions is null || article.Actions.Count == 0)
            Add("actions", "Adicione pelo menos uma ação ao procedimento.");
        else if (article.Actions.Count > 20)
            Add("actions", "Use no máximo 20 ações no documento.");
        else
        {
            var totalSteps = 0;
            for (var i = 0; i < article.Actions.Count; i++)
            {
                var action = article.Actions[i];
                var actionPrefix = $"actions[{i}].";
                if (action is null) { Add("actions", "Ação inválida."); continue; }
                ValidateObject(action, actionPrefix);
                if (action.Steps is null || action.Steps.Count == 0)
                    Add(actionPrefix + "steps", "Adicione pelo menos uma subetapa nesta ação.");
                else if (action.Steps.Count > 100) Add(actionPrefix + "steps", "Use até 100 subetapas no documento.");
                else
                {
                    totalSteps += action.Steps.Count;
                    for (var j = 0; j < action.Steps.Count; j++)
                    {
                        var step = action.Steps[j];
                        var prefix = actionPrefix + $"steps[{j}].";
                        if (step is null) { Add(actionPrefix + "steps", "Subetapa inválida."); continue; }
                        ValidateObject(step, prefix);
                        if (step.Details is null) Add(prefix + "details", "Lista de detalhes inválida.");
                        else if (step.Details.Count > 20) Add(prefix + "details", "Use até 20 detalhes por subetapa.");
                        else
                        {
                            for (var k = 0; k < step.Details.Count; k++)
                            {
                                var detail = step.Details[k];
                                if (detail is null) Add(prefix + $"details[{k}]", "Detalhe inválido.");
                                else
                                {
                                    if (detail.Length > 2000) Add(prefix + $"details[{k}]", "Cada detalhe deve ter até 2.000 caracteres.");
                                    CheckText(prefix + $"details[{k}]", detail);
                                }
                            }
                        }
                    }
                }
            }
            if (totalSteps > 100) Add("actions", "Use no máximo 100 subetapas somando todas as ações.");
        }
        return errors.ToDictionary(item => item.Key, item => item.Value.ToArray());
    }
}

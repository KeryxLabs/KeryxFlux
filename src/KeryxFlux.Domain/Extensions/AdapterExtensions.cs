

using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Models.Http;
using System.Text;
using System.Text.Json;

namespace KeryxFlux.Domain.Extensions
{
    internal static class AdapterExtensions
    {
        public static string AsSerializableStr(this byte[] array)
        {
            var convertedStr = Encoding.UTF8.GetString(array);

            if (string.IsNullOrWhiteSpace(convertedStr)) return "{}";
            return convertedStr;

        }
        public static string FormatAsString(this DateTemplate template, DateTime date, bool isCached = false)
        {
            return template.DelayType switch
            {
                DateDelay.Minute => date.AddMinutes(isCached ? 0 : -template.DelayTime).ToString(template.StringFormat),
                DateDelay.Hour => date.AddHours(isCached ? 0 : -template.DelayTime).ToString(template.StringFormat),
                DateDelay.Day => date.AddDays(isCached ? 0 : -template.DelayTime).ToString(template.StringFormat),
                DateDelay.Week => date.AddDays(isCached ? 0 : -(template.DelayTime * 7)).ToString(template.StringFormat),
                DateDelay.Month => date.AddMonths(isCached ? 0 : -template.DelayTime).ToString(template.StringFormat),
                DateDelay.Year => date.AddYears(isCached ? 0 : -template.DelayTime).ToString(template.StringFormat),
                _ => date.ToString(template.StringFormat),
            };
        }

        public static void FillAllDates(this Endpoint endpoint)
        {
            var currentTime = DateTime.UtcNow;
            foreach (var arg in endpoint.TemplateArguments.Where(arg => arg is { Type: TemplateArgType.Date, DateTemplate: not null }))
            {
                endpoint.Path = endpoint.Path.Replace($"{{{arg.Name}}}", arg.DateTemplate!.FormatAsString(currentTime));
            }
        }

        public static void FillDate(this Endpoint endpoint, DateTime date, bool isCached = false)
        {
            var templateName = endpoint.CachedProperty;
            var arg = endpoint.TemplateArguments.Where(arg => arg is { Type: TemplateArgType.Date, DateTemplate: not null } && arg.Name.Equals(templateName)).FirstOrDefault();

            if (arg is null) return;

            endpoint.Path = endpoint.Path.Replace($"{{{arg.Name}}}", arg.DateTemplate!.FormatAsString(date, isCached));
            return;
        }

        public static string GenerateTemplatedPath(this Endpoint endpoint)
        {
            var filledPath = endpoint.Path;
            var strTemplateArgs = endpoint.TemplateArguments.Where(arg => arg is { Type: TemplateArgType.Value, Value: not null });

            foreach (var arg in strTemplateArgs)
            {
                filledPath = filledPath.Replace($"{{{arg.Name}}}", arg.Value);
            }
            
            return filledPath;

        }

        public static EntryProcessTime? AsProcessTime(this string value) => JsonSerializer.Deserialize<EntryProcessTime>(value);

    }
}

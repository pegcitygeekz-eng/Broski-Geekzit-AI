using System.IO;
using System.Text.Json.Nodes;

namespace ChoppaAI.Shared
{
    public static class LanguageProcessor
    {
        private static readonly JsonObject Learning = LoadLearning();
        private static readonly Dictionary<string, string> Corrections = LoadDictionary("Corrections");
        private static readonly Dictionary<string, string> Aliases = LoadDictionary("Aliases");
        private static readonly Dictionary<string, string> Commands = LoadDictionary("Commands");
        private static readonly Dictionary<string, string> Slang = LoadDictionary("Slang");

        private static JsonObject LoadLearning()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "AIData", "learning.json");
            if (!File.Exists(path))
            {
                return [];
            }

            return JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                ?? throw new InvalidDataException("The packaged learning.json file must contain an object.");
        }

        private static Dictionary<string, string> LoadDictionary(string section)
        {
            if (Learning[section] is not JsonObject values)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in values)
            {
                if (value is JsonValue jsonValue
                    && jsonValue.TryGetValue<string>(out var text)
                    && !string.IsNullOrWhiteSpace(text))
                {
                    result[key] = text;
                }
            }

            return result;
        }

        public static string Process(string input)
        {
            var words = input.Split(' ');

            for (int i = 0; i < words.Length; i++)
            {
                var lower = words[i].ToLower();

                if (Corrections.ContainsKey(lower))
                {
                    words[i] = Corrections[lower];
                    lower = words[i].ToLower();
                }

                if (Aliases.ContainsKey(lower))
                {
                    words[i] = Aliases[lower];
                    lower = words[i].ToLowerInvariant();
                }

                if (Commands.TryGetValue(lower, out var command))
                {
                    words[i] = command;
                    lower = command.ToLowerInvariant();
                }

                if (Slang.TryGetValue(lower, out var slang))
                {
                    words[i] = slang;
                }
            }

            return string.Join(" ", words);
        }
    }
}
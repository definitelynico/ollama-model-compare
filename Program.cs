using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OllamaModelCompare
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Ollama Model Comparison Tool");
            Console.WriteLine("----------------------------");

            // Get model names from user
            Console.Write("Enter first model name: ");
            string model1 = Console.ReadLine() ?? string.Empty;

            Console.Write("Enter second model name: ");
            string model2 = Console.ReadLine() ?? string.Empty;

            // Validate input
            if (string.IsNullOrWhiteSpace(model1) || string.IsNullOrWhiteSpace(model2))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: Model names cannot be empty.");
                Console.ResetColor();
                return;
            }

            Console.WriteLine($"\nComparing models {model1} and {model2}...");

            // Get model details
            var model1Details = RunOllamaShowCommand(model1);
            var model2Details = RunOllamaShowCommand(model2);

            // Compare and show differences
            Console.WriteLine($"\nComparison Results: {model1} vs {model2}");
            Console.WriteLine("----------------------------");

            int missingInModel1 = 0;
            int missingInModel2 = 0;
            int valuesDiffer = 0;

            // Compare as dictionaries to find differences
            foreach (var key in model1Details.Keys.Union(model2Details.Keys))
            {
                bool model1HasKey = model1Details.TryGetValue(key, out string? value1);
                bool model2HasKey = model2Details.TryGetValue(key, out string? value2);

                // Normalize values for comparison (trim extra spaces)
                string normalizedValue1 = value1?.Trim() ?? string.Empty;
                string normalizedValue2 = value2?.Trim() ?? string.Empty;

                if (!model1HasKey)
                {
                    missingInModel1++;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"- {key}: missing in {model1}");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"+ {key}: {normalizedValue2}");
                    Console.WriteLine();
                }
                else if (!model2HasKey)
                {
                    missingInModel2++;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"+ {key}: {normalizedValue1}");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"- {key}: missing in {model2}");
                    Console.WriteLine();
                }
                else if (normalizedValue1 != normalizedValue2)
                {
                    valuesDiffer++;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"* {key} differs:");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"- {model1}: {normalizedValue1}");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"+ {model2}: {normalizedValue2}");
                    Console.WriteLine();
                }
            }

            // Reset color and show final result
            Console.ResetColor();
            Console.WriteLine("----------------------------");
            Console.WriteLine("Comparison Summary:");

            if (missingInModel1 > 0 || missingInModel2 > 0 || valuesDiffer > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Differences detected!");
                Console.ResetColor();
                Console.WriteLine($"- Keys missing in {model1}: {missingInModel1}");
                Console.WriteLine($"- Keys missing in {model2}: {missingInModel2}");
                Console.WriteLine($"- Keys with different values: {valuesDiffer}");
                Console.WriteLine($"- Total differences: {missingInModel1 + missingInModel2 + valuesDiffer}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("No differences found between the models.");
                Console.ResetColor();
            }
        }

        static Dictionary<string, string> RunOllamaShowCommand(string modelName)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = $"show --verbose {modelName}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: Failed to start process for model {modelName}");
                    Console.ResetColor();
                    return result;
                }

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(output))
                {
                    string currentSection = string.Empty;
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var line in lines)
                    {
                        // Skip empty lines
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        string trimmedLine = line.Trim();

                        // Check if this is a section header (no leading spaces and no key-value pattern)
                        if (!line.StartsWith(" ") && !line.StartsWith("\t") && !trimmedLine.Contains("  "))
                        {
                            currentSection = trimmedLine;
                            continue;
                        }

                        // Parse key-value pairs - split by 2+ spaces
                        var parts = Regex.Split(trimmedLine, @"\s{2,}");
                        if (parts.Length >= 2)
                        {
                            string key = parts[0].Trim();
                            string value = string.Join(" ", parts.Skip(1)).Trim();

                            // Add section name to key if we're in a section
                            if (!string.IsNullOrEmpty(currentSection))
                            {
                                key = $"{currentSection}.{key}";
                            }

                            // Handle duplicate keys (especially for 'stop' parameters)
                            // by appending a numeric index
                            string uniqueKey = key;
                            int index = 1;
                            while (result.ContainsKey(uniqueKey))
                            {
                                uniqueKey = $"{key}_{index++}";
                            }

                            result[uniqueKey] = value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error running command for model {modelName}: {ex.Message}");
                Console.ResetColor();
            }

            return result;
        }
    }
}

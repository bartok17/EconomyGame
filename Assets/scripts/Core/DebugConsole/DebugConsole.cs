using System;
using System.Collections.Generic;
using System.Text;

namespace MonopolyGame.Debugging
{
    /// <summary>Parses and runs console commands; every failure becomes a result, never a throw.</summary>
    public sealed class DebugConsole
    {
        private const int HelpColumnWidth = 28;

        private readonly List<DebugCommandDefinition> commands = new List<DebugCommandDefinition>();
        private readonly Dictionary<string, DebugCommandDefinition> lookup =
            new Dictionary<string, DebugCommandDefinition>(StringComparer.OrdinalIgnoreCase);

        public DebugConsole(IEnumerable<DebugCommandDefinition> definitions = null)
        {
            RegisterHelpCommand();

            if (definitions == null)
            {
                return;
            }

            foreach (DebugCommandDefinition definition in definitions)
            {
                Add(definition);
            }
        }

        public IReadOnlyList<DebugCommandDefinition> Commands => commands;

        public void Add(DebugCommandDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (lookup.ContainsKey(definition.Name))
            {
                throw new InvalidOperationException($"A debug command named '{definition.Name}' already exists.");
            }

            commands.Add(definition);
            lookup[definition.Name] = definition;

            for (int i = 0; i < definition.Aliases.Count; i++)
            {
                string alias = definition.Aliases[i];

                if (string.IsNullOrWhiteSpace(alias))
                {
                    continue;
                }

                if (lookup.ContainsKey(alias))
                {
                    throw new InvalidOperationException($"The debug command alias '{alias}' is already in use.");
                }

                lookup[alias] = definition;
            }
        }

        public bool TryGetCommand(string name, out DebugCommandDefinition definition)
        {
            definition = null;

            return !string.IsNullOrWhiteSpace(name) && lookup.TryGetValue(name.Trim(), out definition);
        }

        public DebugCommandResult Execute(string rawLine)
        {
            IReadOnlyList<string> tokens = Tokenize(rawLine);

            if (tokens.Count == 0)
            {
                return DebugCommandResult.Ok();
            }

            if (!TryGetCommand(tokens[0], out DebugCommandDefinition command))
            {
                return DebugCommandResult.Fail($"Unknown command '{tokens[0]}'. Type 'help' for the list.");
            }

            var args = new DebugConsoleArgs(rawLine, tokens);

            try
            {
                return command.Handler(args) ?? DebugCommandResult.Fail($"{command.Name} produced no result.");
            }
            catch (Exception exception)
            {
                return DebugCommandResult.Fail($"{command.Name}: {exception.Message}");
            }
        }

        public IReadOnlyList<string> Complete(string prefix)
        {
            string trimmed = (prefix ?? string.Empty).Trim();
            var matches = new List<string>();

            foreach (DebugCommandDefinition command in commands)
            {
                if (command.Name.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(command.Name);
                }

                for (int i = 0; i < command.Aliases.Count; i++)
                {
                    string alias = command.Aliases[i];

                    if (!string.IsNullOrWhiteSpace(alias) &&
                        alias.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase) &&
                        !matches.Exists(existing => string.Equals(existing, alias, StringComparison.OrdinalIgnoreCase)))
                    {
                        matches.Add(alias);
                    }
                }
            }

            matches.Sort(StringComparer.OrdinalIgnoreCase);
            return matches;
        }

        public DebugCommandResult Describe(string name)
        {
            if (!TryGetCommand(name, out DebugCommandDefinition command))
            {
                return DebugCommandResult.Fail($"Unknown command '{name}'. Type 'help' for the list.");
            }

            var lines = new List<string>
            {
                command.Usage,
                $"  {command.Summary}"
            };

            if (command.Aliases.Count > 0)
            {
                lines.Add($"  Aliases: {string.Join(", ", command.Aliases)}");
            }

            lines.Add(command.Mode == DebugCommandExecutionMode.Server
                ? "  Runs on the host."
                : "  Runs locally.");

            return DebugCommandResult.OkLines(lines);
        }

        public IReadOnlyList<string> DescribeAll()
        {
            var lines = new List<string>();
            var categories = new List<string>();

            foreach (DebugCommandDefinition command in commands)
            {
                if (!categories.Contains(command.Category))
                {
                    categories.Add(command.Category);
                }
            }

            for (int c = 0; c < categories.Count; c++)
            {
                if (lines.Count > 0)
                {
                    lines.Add(string.Empty);
                }

                lines.Add(categories[c]);

                var inCategory = new List<DebugCommandDefinition>();

                foreach (DebugCommandDefinition command in commands)
                {
                    if (string.Equals(command.Category, categories[c], StringComparison.Ordinal))
                    {
                        inCategory.Add(command);
                    }
                }

                inCategory.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));

                foreach (DebugCommandDefinition command in inCategory)
                {
                    string usage = command.Usage.Length >= HelpColumnWidth
                        ? command.Usage
                        : command.Usage.PadRight(HelpColumnWidth);
                    lines.Add($"  {usage} {command.Summary}");
                }
            }

            return lines;
        }

        /// <summary>Splits a line into tokens; quotes group words and '#' starts a comment.</summary>
        public static IReadOnlyList<string> Tokenize(string line)
        {
            var tokens = new List<string>();

            if (string.IsNullOrEmpty(line))
            {
                return tokens;
            }

            var current = new StringBuilder();
            bool inToken = false;
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char character = line[i];

                if (inQuotes)
                {
                    if (character == '"')
                    {
                        inQuotes = false;
                    }
                    else
                    {
                        current.Append(character);
                    }

                    inToken = true;
                    continue;
                }

                if (character == '"')
                {
                    inQuotes = true;
                    inToken = true;
                    continue;
                }

                if (character == '#')
                {
                    break;
                }

                if (char.IsWhiteSpace(character))
                {
                    if (inToken)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                        inToken = false;
                    }

                    continue;
                }

                current.Append(character);
                inToken = true;
            }

            if (inToken)
            {
                tokens.Add(current.ToString());
            }

            return tokens;
        }

        private void RegisterHelpCommand()
        {
            Add(new DebugCommandDefinition(
                "help",
                "List every command, or describe one.",
                "help [command]",
                "Console",
                DebugCommandExecutionMode.Local,
                args => args.IsEmpty
                    ? DebugCommandResult.OkLines(DescribeAll())
                    : Describe(args.Get(0)),
                "?"));
        }
    }
}

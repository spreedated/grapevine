#pragma warning disable S3220 // Method calls should not resolve ambiguously to overloads with "params" parameters

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Grapevine
{
    public delegate string RouteConstraintResolver(string args);

    public interface IRouteTemplate
    {
        Regex Pattern { get; }

        List<string> PatternKeys { get; }

        bool Matches(string endpoint);

        IDictionary<string, string> ParseEndpoint(string endpoint);
    }

    public class RouteTemplate : IRouteTemplate
    {
        private static readonly Regex Default = new(@"^.*$");

        public Regex Pattern { get; set; } = new(@"^.*$");

        public List<string> PatternKeys { get; set; } = new();

        public RouteTemplate() { }

        public RouteTemplate(string pattern)
        {
            this.Pattern = ConvertToRegex(pattern, out var patternKeys);
            this.PatternKeys = patternKeys;
        }

        public RouteTemplate(Regex pattern, List<string> patternKeys = null)
        {
            this.Pattern = pattern;
            if (patternKeys != null) this.PatternKeys = patternKeys;
        }

        public bool Matches(string endpoint) => this.Pattern.IsMatch(endpoint);

        public IDictionary<string, string> ParseEndpoint(string endpoint)
        {
            Dictionary<string, string> parsed = new();
            var idx = 0;

            var matches = this.Pattern.Matches(endpoint)[0].Groups;
            for (int i = 1; i < matches.Count; i++)
            {
                var key = (this.PatternKeys?.Count > 0 && this.PatternKeys?.Count > idx)
                    ? this.PatternKeys[idx]
                    : $"p{idx}";

                parsed.Add(key, matches[i].Value);
                idx++;
            }

            return parsed;
        }

        public static Regex ConvertToRegex(string pattern, out List<string> patternKeys)
        {
            patternKeys = new();

            if (string.IsNullOrEmpty(pattern)) return Default;
            if (pattern.StartsWith("^")) return new(pattern);

            StringBuilder builder = new("(?i)^");
            var sections = pattern.SanitizePath() // Ensures the string begins with '/'
                .TrimEnd('$')                     // Removes any trailing '$'
                .Split('{', '}');  // splits into sections

            for (var i = 0; i < sections.Length; i++)
            {
                if ((i % 2) == 0) // is even
                {
                    // Even sections don't contain constraints
                    builder.Append(sections[i]);
                }
                else
                {
                    var constraints = sections[i].Split(':').ToList();
                    patternKeys.Add(constraints[0]);
                    constraints.RemoveAt(0);
                    builder.Append(RouteConstraints.Resolve(constraints));
                }
            }

            builder.Append('$');
            return new(builder.ToString());
        }
    }

    internal static class RouteConstraints
    {
        public static readonly string DefaultPattern = "([^/]+)";

        private static readonly Dictionary<string, RouteConstraintResolver> _resolvers = new()
        {
            { "alpha", AlphaResolver },
            { "alphanum", AlphaNumericResolver },
            { "guid", GuidResolver },
            { "num", NumericResolver },
            { "string", StringResolver }
        };

        private static readonly string[] _protectedKeys = new string[]
        {
            "alpha",
            "alphanum",
            "guid",
            "num",
            "string"
        };

        public static void AddResolver(string key, RouteConstraintResolver resolver)
        {
            if (_protectedKeys.Contains(key)) throw new ArgumentException($"Cannot override protected resolver {key}");
            _resolvers[key] = resolver;
        }

        private static string GetContraintWithRoundBrackets(IList<string> constraints)
        {
            if (constraints[0].Contains('('))
            {
                return constraints[0];
            }

            if (constraints.Count > 1 && constraints[1].Contains('('))
            {
                return constraints[1];
            }

            return string.Empty;
        }

        public static string Resolve(List<string> constraints)
        {
            if (constraints == null || !constraints.Any())
            {
                return DefaultPattern;
            }

            string constraint = GetContraintWithRoundBrackets(constraints);

            var resolver = _resolvers.ContainsKey(constraints[0])
                ? _resolvers[constraints[0]]
                : _resolvers["string"];

            return resolver(constraint);
        }

        private static string AlphaResolver(string args)
        {
            var quantifier = LengthResolver(args);
            return $"([a-zA-Z]{quantifier})";
        }

        private static string AlphaNumericResolver(string args)
        {
            var quantifier = LengthResolver(args);
            return $"(\\w{quantifier})";
        }

        private static string GuidResolver(string args)
        {
            return @"[({]?[a-fA-F0-9]{8}[-]?([a-fA-F0-9]{4}[-]?){3}[a-fA-F0-9]{12}[})]?";
        }

        private static string NumericResolver(string args)
        {
            var quantifier = LengthResolver(args);
            return $"(\\d{quantifier})";
        }

        private static string StringResolver(string args)
        {
            var quantifier = LengthResolver(args);
            return $"([^/]{quantifier})";
        }

        public static string LengthResolver(string args)
        {
            if (string.IsNullOrWhiteSpace(args)) return "+";
            var sections = args.Split('(', ')');

            if (sections.Length < 2) throw new ArgumentException($"Length parameters not specified in {args}");
            var range = sections[1].Split(',');

            string length = sections[0].ToLower() switch
            {
                "minlength" => "{" + Int32.Parse(range[0]) + ",}",
                "maxlength" => "{1," + Int32.Parse(range[0]) + "}",
                "length" => (range.Length == 2)
                                        ? "{" + Int32.Parse(range[0]) + "," + Int32.Parse(range[1]) + "}"
                                        : "{" + Int32.Parse(range[0]) + "}",
                _ => throw new ArgumentException($"Invalid length parameter specified in {args}"),
            };

            return length;
        }
    }
}
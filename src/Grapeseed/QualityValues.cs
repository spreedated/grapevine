using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Grapevine
{
    public static class QualityValues
    {
        private static Regex[] specificities = new Regex[2]
        {
            new("[^*]+"),       // totally specific
            new(@"^[^*/]/\*$"), // partially specific
        };

        public static IList<string> Parse(string header)
        {
            List<string> values = new();

            foreach (KeyValuePair<decimal, List<string>> item in GroupByQualityFactor(header))
                values.AddRange(SortBySpecificity(item.Value));

            return values;
        }

        public static SortedDictionary<decimal, List<string>> GroupByQualityFactor(string value)
        {
            SortedDictionary<decimal, List<string>> factors = new();
            foreach (var entry in value.Split(','))
            {
                var itemFactorPair = entry.Trim().Split(new string[] { ";q=" }, StringSplitOptions.None);
                var item = itemFactorPair[0];
                var factor = (itemFactorPair.Length == 2)
                    ? Convert.ToDecimal(itemFactorPair[1])
                    : Convert.ToDecimal(1);

                if (!factors.ContainsKey(factor)) factors.Add(factor, new());
                factors[factor].Add(item);
            }

            return factors;
        }

        public static IList<string> SortBySpecificity(IList<string> values)
        {
            if (values.Count == 1) return values;

            List<string> totally = new();
            List<string> partial = new();
            List<string> nonspec = new();

            foreach (var value in values)
            {
                if (specificities[0].IsMatch(value)) totally.Add(value);
                else if (specificities[1].IsMatch(value)) partial.Add(value);
                else nonspec.Add(value);
            }

            partial.AddRange(nonspec);
            totally.AddRange(partial);

            return totally;
        }
    }
}
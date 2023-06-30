using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Grapevine
{
    public class ContentType
    {
        #region Static Implementations

        public static ContentType Binary { get; } = new ContentType("application/octet-stream");

        public static ContentType Css { get; } = new ContentType("text/css", false, "UTF-8");

        public static ContentType FormUrlEncoded { get; } = new ContentType("application/x-www-form-urlencoded");

        public static ContentType Gif { get; } = new ContentType("image/gif");

        public static ContentType Html { get; } = new ContentType("text/html", false, "UTF-8");

        public static ContentType Icon { get; } = new ContentType("image/x-icon");

        public static ContentType JavaScript { get; } = new ContentType("application/javascript", false, "UTF-8");

        public static ContentType Json { get; } = new ContentType("application/json", false, "UTF-8");

        public static ContentType Jpg { get; } = new ContentType("image/jpeg");

        public static ContentType Mp3 { get; } = new ContentType("audio/mpeg");

        public static ContentType Mp4 { get; } = new ContentType("video/mp4");

        public static ContentType MultipartFormData { get; } = new ContentType("multipart/form-data");

        public static ContentType Pdf { get; } = new ContentType("application/pdf");

        public static ContentType Png { get; } = new ContentType("image/png");

        public static ContentType Svg { get; } = new ContentType("image/svg+xml", false, "UTF-8");

        public static ContentType Text { get; } = new ContentType("text/plain", false, "UTF-8");

        public static ContentType Xml { get; } = new ContentType("application/xml", false, "UTF-8");

        public static ContentType Zip { get; } = new ContentType("application/zip");

        #endregion

        #region Static Initialization

        private static readonly Dictionary<string, ContentType> _contentTypes = new();

        private static readonly Dictionary<string, ContentType> _extensions = new();

        static ContentType()
        {
            var ct = typeof(ContentType);
            var fields = ct.GetFields(BindingFlags.Public | BindingFlags.Static).ToList();

            foreach (var field in fields)
            {
                if (field.GetValue(null) is not ContentType contentType) return;

                _contentTypes.Add(contentType, contentType);
                _extensions.Add(field.Name.ToLower(), contentType);
            }
        }

        #endregion

        private string _value;

        public string Value { get; }

        public bool IsBinary { get; }

        public string CharSet { get; }

        public string Boundary { get; protected set; }

        public ContentType(string value, bool isBinary = true, string charSet = "")
        {
            Value = value;
            IsBinary = isBinary;
            CharSet = charSet;
        }

        public void ResetBoundary(string newBoundary = null)
        {
            if (Value.StartsWith("multipart"))
            {
                _value = null;
                Boundary = MultiPartBoundary.Generate(newBoundary);
            }
        }

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(_value))
            {
                var sb = new StringBuilder(Value);

                if (!string.IsNullOrWhiteSpace(Boundary))
                {
                    sb.Append($"; boundary={Boundary}");
                }
                else if (!string.IsNullOrWhiteSpace(CharSet))
                {
                    sb.Append($"; charset={CharSet}");
                }

                _value = sb.ToString();
            }

            return _value;
        }

        public static implicit operator ContentType(string value)
        {
            return Find(value);
        }

        public static implicit operator string(ContentType value)
        {
            return value.ToString();
        }

        public static ContentType Find(string value)
        {
            Add(value);
            return _contentTypes[value];
        }

        public static ContentType FindKey(string key)
        {
            var k = key.ToLower();
            return (_extensions.ContainsKey(k))
                ? _extensions[k]
                : ContentType.Binary;
        }

        public static void Add(string value, bool isBinary = true, string charSet = "")
        {
            if (_contentTypes.ContainsKey(value)) return;

            var key = (value.Contains(';') && string.IsNullOrWhiteSpace(charSet))
                ? value
                : string.IsNullOrWhiteSpace(charSet)
                    ? value
                    : $"{value}; charset={charSet}";

            if (_contentTypes.ContainsKey(key)) return;

            if (value.Contains(';') && string.IsNullOrWhiteSpace(charSet))
            {
                var parts = value.Split(';');
                value = parts[0];
                charSet = parts[1]?.Replace("charset=", "").Trim();
            }

            var contentType = new ContentType(value, isBinary, charSet);
            _contentTypes.Add(contentType, contentType);
        }

        public static void Add(string key, ContentType contentType)
        {
            _contentTypes.Add(contentType, contentType);
            _extensions.Add(key, contentType);
        }

        public static ContentType MultipartContent(Multipart multipart = default, string boundary = null)
        {
            if (string.IsNullOrWhiteSpace(boundary))
                boundary = MultiPartBoundary.Generate();

            return new ContentType($"multipart/{multipart.ToString().ToLower()}", false, "")
            {
                Boundary = boundary.Substring(0, 70).TrimEnd()
            };
        }
    }

    public static class MultiPartBoundary
    {
        public const int MAX_BOUNDARY_LENGTH = 70;
        public const int MIN_BOUNDARY_LENGTH = 30;

        private static readonly char[] _multipartChars = "-_1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

        private static readonly Random _random = new Random();

        public static string Generate(string firstPart = "----=NextPart_")
        {
            if (firstPart == null)
            {
                return firstPart;
            }

            if (firstPart?.Length >= MIN_BOUNDARY_LENGTH) return firstPart;

            var sb = new StringBuilder(firstPart);
            var init_size = firstPart.Length;
            var end_size = _random.Next(MIN_BOUNDARY_LENGTH, MAX_BOUNDARY_LENGTH);

            for (int i = init_size; i <= end_size; i++)
            {
                sb.Append(_multipartChars[_random.Next(_multipartChars.Length)]);
            }

            return sb.ToString();
        }
    }

    public enum Multipart
    {
        Mixed,
        Alternative,
        Digest,
        Encrypted,
        FormData,
        Related,
        Signed,
        Parallel
    }
}
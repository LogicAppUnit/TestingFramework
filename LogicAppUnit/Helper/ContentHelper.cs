using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace LogicAppUnit.Helper
{
    /// <summary>
    /// Helper class to make it easier to create content that can be used for API requests and test assertions.
    /// </summary>
    public static class ContentHelper
    {
        private const string JsonContentType = "application/json";
        private const string PlainTextContentType = "text/plain";
        private const string XmlContentType = "application/xml";

        #region Files

        /// <summary>
        /// Read the contents of a file using the given path.
        /// </summary>
        /// <param name="path">Path of the file to be read.</param>
        /// <returns>The file content, as a <see cref="string"/>.</returns>
        public static string ReadFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            var fullPath = Path.GetFullPath(path);
            return File.ReadAllText(fullPath);
        }

        #endregion // Files

        #region HTTP Content

        /// <summary>
        /// Create HTTP JSON content from the given <see cref="Stream"/>.
        /// </summary>
        /// <param name="jsonStream">The stream to be used for the HTTP content.</param>
        /// <returns>The HTTP content.</returns>
        public static StreamContent CreateJsonStreamContent(Stream jsonStream)
        {
            StreamContent content = new StreamContent(jsonStream);
            content.Headers.ContentType = new MediaTypeHeaderValue(JsonContentType);
            return content;
        }

        /// <summary>
        /// Create HTTP JSON content from the JSON string provided.
        /// </summary>
        /// <param name="jsonString">JSON string to be converted to HTTP content.</param>
        /// <returns>The HTTP content.</returns>
        public static StringContent CreateJsonStringContent(string jsonString)
        {
            if (jsonString == null)
                throw new ArgumentNullException(nameof(jsonString));

            return new StringContent(jsonString, Encoding.UTF8, JsonContentType);
        }

        /// <summary>
        /// Create HTTP JSON content from the JSON object provided.
        /// </summary>
        /// <param name="jsonObject">JSON object to be converted to HTTP content.</param>
        /// <returns>The HTTP content.</returns>
        public static StringContent CreateJsonStringContent(object jsonObject)
        {
            if (jsonObject == null)
                throw new ArgumentNullException(nameof(jsonObject));

            var json = JsonConvert.SerializeObject(jsonObject);
            return new StringContent(json, Encoding.UTF8, JsonContentType);
        }

        /// <summary>
        /// Create HTTP plain-text content from the given string value.
        /// </summary>
        /// <param name="value">String to be converted to HTTP content.</param>
        /// <returns>The HTTP content.</returns>
        public static StringContent CreatePlainStringContent(string value)
        {
            return new StringContent(value, Encoding.UTF8, PlainTextContentType);
        }

        /// <summary>
        /// Create HTTP XML content from the given string value.
        /// </summary>
        /// <param name="xmlString">XML string to be converted to HTTP content.</param>
        /// <returns>The HTTP content.</returns>
        public static StringContent CreateXmlStringContent(string xmlString)
        {
            return new StringContent(xmlString, Encoding.UTF8, XmlContentType);
        }

        /// <summary>
        /// Create HTTP XML content from the given <see cref="XmlDocument"/>.
        /// </summary>
        /// <param name="xmlDoc">XML document to be converted to HTTP content.</param>
        /// <returns>The HTTP content.</returns>
        public static StringContent CreateXmlStringContent(XmlDocument xmlDoc)
        {
            return new StringContent(xmlDoc.ToString(), Encoding.UTF8, XmlContentType);
        }

        #endregion // HTTP Content

        #region JSON Serialization

        /// <summary>
        /// Serialize a .NET object into a JSON string.
        /// </summary>
        /// <param name="inputObject">The .NET object to be serialized.</param>
        /// <returns>THe JSON string representation of the object.</returns>
        public static string SerializeObject(this object inputObject)
        {
            return JsonConvert.SerializeObject(inputObject, Newtonsoft.Json.Formatting.Indented);
        }

        /// <summary>
        /// Deserialize a JSON string into a .NET object.
        /// </summary>
        /// <param name="inputObject">The JSON string to be deserialized.</param>
        /// <returns>The .NET object representation of the JSON.</returns>
        public static object DeserializeObject(string inputObject)
        {
            return JsonConvert.DeserializeObject(inputObject);
        }

        /// <summary>
        /// Clone a dynamic JSON object.
        /// </summary>
        /// <param name="source">THe object to be cloned.</param>
        /// <returns>The clone.</returns>
        public static dynamic JClone(dynamic source)
        {
            if (source == null)
                return null;

            return JsonConvert.DeserializeObject(JsonConvert.SerializeObject(source));
        }

        #endregion // JSON Serialization

        #region Streams

        /// <summary>
        /// Read a <see cref="Stream"/> and return the content as a <see cref="string"/> value.
        /// </summary>
        /// <param name="input">The <see cref="Stream"/> to be read.</param>
        /// <returns>The stream content as a <see cref="string"/>.</returns>
        public static string ConvertStreamToString(Stream input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            string convertedValue = string.Empty;
            using (var sr = new StreamReader(input))
            {
                convertedValue = sr.ReadToEnd();
            }

            return convertedValue;
        }

        /// <summary>
        /// Convert a <see cref="string"/> value into a <see cref="Stream"/>.
        /// </summary>
        /// <param name="input">The <see cref="string"/> to be read.</param>
        /// <returns>The stream representation.</returns>
        public static Stream ConvertStringToStream(string input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            byte[] byteArray = Encoding.ASCII.GetBytes(input);
            return new MemoryStream(byteArray);
        }

        #endregion // Streams

        #region Formatting

        /// <summary>
        /// Format a JSON string to enable reliable comparison for test assertions.
        /// </summary>
        /// <param name="json">The JSON string to be formatted.</param>
        /// <returns>The formatted JSON.</returns>
        /// <remarks>
        /// Comments in the JSON string are removed, this means that comments can be added to the 'expected' JSON string to highlight areas of interest, without breaking any test assertions.
        /// </remarks>
        public static string FormatJson(string json)
        {
            var settings = new JsonLoadSettings()
            {
                CommentHandling = CommentHandling.Ignore
            };

            // Format the JSON by loading into a JObject and then extracting it as a string.
            // Perhaps a little heavy-handed, but it does the trick.
            var obj = JObject.Parse(json, settings);
            return obj.ToString();
        }

        /// <summary>
        /// Format a XML <see cref="Stream"/> to enable reliable comparison for test assertions.
        /// </summary>
        /// <param name="xmlStream">The XML to be formatted.</param>
        /// <returns>The formatted XML.</returns>
        /// <remarks>
        /// Comments in the XML are removed, this means that comments can be added to the 'expected' XML to highlight areas of interest, without breaking any test assertions.
        /// </remarks>
        public static string FormatXml(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(xmlStream);

            return FormatXml(xmlDoc);
        }

        /// <summary>
        /// Format a XML <see cref="string"/> to enable reliable comparison for test assertions.
        /// </summary>
        /// <param name="xml">The XML <see cref="string"/> to be formatted.</param>
        /// <returns>The formatted XML.</returns>
        /// <remarks>
        /// Comments in the XML are removed, this means that comments can be added to the 'expected' XML to highlight areas of interest, without breaking any test assertions.
        /// </remarks>
        public static string FormatXml(string xml)
        {
            if (xml == null)
                throw new ArgumentNullException(nameof(xml));

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);

            return FormatXml(xmlDoc);
        }

        /// <summary>
        /// Format a <see cref="XmlDocument"/> (using a C14N XML canonicalization transform) to enable reliable comparison for test assertions.
        /// </summary>
        /// <param name="xmlDoc">The XML document to be formatted.</param>
        /// <returns>The formatted XML.</returns>
        private static string FormatXml(XmlDocument xmlDoc)
        {
            if (xmlDoc == null)
                throw new ArgumentNullException(nameof(xmlDoc));

            XmlDsigC14NTransform xmlTransform = new XmlDsigC14NTransform();
            xmlTransform.LoadInput(xmlDoc);

            Stream xmlCanonical = (Stream)xmlTransform.GetOutput(typeof(Stream));
            return ConvertStreamToString(xmlCanonical);
        }

        #endregion // Formatting
    }
}

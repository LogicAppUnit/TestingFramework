using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace LogicAppUnit.Wrapper
{
    /// <summary>
    /// Wrapper class to manage the <i>parameters.json</i> file.
    /// </summary>
    internal class ParametersWrapper
    {
        private readonly JObject _jObjectParameters;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParametersWrapper"/> class.
        /// </summary>
        /// <param name="parametersContent">The contents of the parameters file, or <c>null</c> if the file does not exist.</param>
        public ParametersWrapper(string parametersContent)
        {
            if (!string.IsNullOrEmpty(parametersContent))
            {
                _jObjectParameters = JObject.Parse(parametersContent);
            }
        }

        /// <summary>
        /// Returns the parameters content.
        /// </summary>
        /// <returns>The parameters content.</returns>
        public override string ToString()
        {
            if (_jObjectParameters == null)
                return null;

            return _jObjectParameters.ToString();
        }

        /// <summary>
        /// Get the value for a parameter.
        /// </summary>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <typeparam name="T">The type of the parameter.</typeparam>
        /// <returns>The value of the parameter, or <c>null</c> if the parameter does not exist.</returns>
        public T GetParameterValue<T>(string parameterName)
        {
            ArgumentNullException.ThrowIfNull(parameterName);

            var param = _jObjectParameters.Children<JProperty>().Where(x => x.Name == parameterName).FirstOrDefault();
            if (param == null)
                return default;

            return ((JObject)param.Value)["value"].Value<T>();
        }

        /// <summary>
        /// Expand the parameters in <paramref name="value"/> as a string value.
        /// </summary>
        /// <param name="value">The string value containing the parameters to be expanded.</param>
        /// <returns>Expanded parameter value.</returns>
        public string ExpandParametersAsString(string value)
        {
            // If there is no parameters file then the value is not replaced
            if (_jObjectParameters == null)
                return value;

            const string parametersPattern = @"@parameters\('[\w.:-]*'\)";
            string expandedValue = value;

            MatchCollection matches = Regex.Matches(value, parametersPattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                string parameterName = match.Value[13..^2];
                expandedValue = expandedValue.Replace(match.Value, GetParameterValue<string>(parameterName));
            }

            return expandedValue;
        }

        /// <summary>
        /// Expand the parameter in <paramref name="value"/> as an object.
        /// </summary>
        /// <param name="value">The string value containing the parameter to be expanded.</param>
        /// <returns>Expanded parameter value.</returns>
        public JObject ExpandParameterAsObject(string value)
        {
            // If there is no parameters file then the value is not replaced
            if (_jObjectParameters == null)
                return null;

            const string parametersPattern = @"^@parameters\('[\w.:-]*'\)$";

            MatchCollection matches = Regex.Matches(value, parametersPattern, RegexOptions.IgnoreCase);
            return  GetParameterValue<JObject>(matches[0].Value[13..^2]);
        }
    }
}
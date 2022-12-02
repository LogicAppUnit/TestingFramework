using System;

namespace LogicAppUnit.InternalHelper
{
    /// <summary>
    /// Helper class for writing to the test execution log.
    /// </summary>
    internal class LoggingHelper
    {
        /// <summary>
        /// Write a banner to the test execution log.
        /// </summary>
        /// <param name="bannerText">The text to be shown in the banner.</param>
        internal static void LogBanner(string bannerText)
        {
            const int bannerSize = 80;
            const int bannerPaddingOnEachSide = 2;

            if (string.IsNullOrEmpty(bannerText))
                throw new ArgumentNullException(nameof(bannerText));
            if (bannerText.Length > bannerSize - (bannerPaddingOnEachSide * 2))
                throw new ArgumentException($"The size of the banner text cannot be more than {bannerSize - (bannerPaddingOnEachSide * 2)} characters.");

            int paddingStart = (bannerSize - (bannerPaddingOnEachSide * 2) - bannerText.Length) / 2;
            int paddingEnd = bannerSize - (bannerPaddingOnEachSide * 2) - bannerText.Length - paddingStart;

            Console.WriteLine();
            Console.WriteLine(new string('-', bannerSize));
            Console.WriteLine($"- {new string(' ', paddingStart)}{bannerText.ToUpperInvariant()}{new string(' ', paddingEnd)} -");
            Console.WriteLine(new string('-', bannerSize));
            Console.WriteLine();
        }
    }
}

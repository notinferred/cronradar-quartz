using System;
using System.Text;

namespace CronRadar.Quartz
{
    /// <summary>
    /// Normalizes monitor keys to ensure consistency across sync and ping operations.
    /// Converts Quartz.NET job keys (Group.Name) to valid CronRadar monitor keys.
    /// </summary>
    public static class CronRadarKeyNormalizer
    {
        /// <summary>
        /// Normalizes a Quartz.NET job key into a CronRadar monitor key.
        /// Examples:
        /// - "Reports.DailyReport" -> "reports-dailyreport"
        /// - "DEFAULT.BackupJob" -> "default-backupjob"
        /// - "My Reports/Daily Report" -> "my-reports-daily-report"
        /// </summary>
        /// <param name="group">The Quartz job group</param>
        /// <param name="name">The Quartz job name</param>
        /// <returns>Normalized monitor key suitable for CronRadar</returns>
        public static string Normalize(string group, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            // Combine group and name
            var combined = string.IsNullOrWhiteSpace(group)
                ? name
                : $"{group}.{name}";

            return Normalize(combined);
        }

        /// <summary>
        /// Normalizes a string key into a CronRadar monitor key.
        /// </summary>
        /// <param name="key">The key to normalize</param>
        /// <returns>Normalized monitor key in lowercase with only alphanumeric and hyphens</returns>
        public static string Normalize(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return key;

            var result = new StringBuilder();
            var lastWasSeparator = false;

            foreach (var c in key)
            {
                if (char.IsLetterOrDigit(c))
                {
                    result.Append(char.ToLowerInvariant(c));
                    lastWasSeparator = false;
                }
                else if (c == '.' || c == '/' || c == '_' || c == ' ' || c == '-')
                {
                    // Convert all separators to hyphens, but avoid consecutive hyphens
                    if (!lastWasSeparator && result.Length > 0)
                    {
                        result.Append('-');
                        lastWasSeparator = true;
                    }
                }
                // Skip other characters
            }

            // Remove trailing hyphen if present
            if (result.Length > 0 && result[result.Length - 1] == '-')
            {
                result.Length--;
            }

            return result.ToString();
        }
    }
}

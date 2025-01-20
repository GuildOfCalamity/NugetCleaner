using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Storage;
using Windows.Storage.Streams;

namespace NugetCleaner;

public static class Extensions
{
    /// <summary>
    /// Determine if the application has been launched as an administrator.
    /// </summary>
    public static bool IsAppRunAsAdmin()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
    }

    /// <summary>
    ///	Checks if the taskbar is set to auto-hide.
    /// </summary>
    public static bool IsAutoHideTaskbarEnabled()
    {
        const string registryKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3";
        const string valueName = "Settings";

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(registryKey);
            var value = key?.GetValue(valueName) as byte[];
            // The least significant bit of the 9th byte controls the auto-hide setting																		
            return value != null && ((value[8] & 0x01) == 1);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] IsAutoHideTaskbarEnabled: {ex.Message}");
        }
        
        return false;
    }

    /// <summary>
    /// Get OS version by way of <see cref="Windows.System.Profile.AnalyticsInfo"/>.
    /// </summary>
    /// <returns><see cref="Version"/></returns>
    public static Version GetWindowsVersionUsingAnalyticsInfo()
    {
        try
        {
            ulong version = ulong.Parse(Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamilyVersion);
            var Major = (ushort)((version & 0xFFFF000000000000L) >> 48);
            var Minor = (ushort)((version & 0x0000FFFF00000000L) >> 32);
            var Build = (ushort)((version & 0x00000000FFFF0000L) >> 16);
            var Revision = (ushort)(version & 0x000000000000FFFFL);

            return new Version(Major, Minor, Build, Revision);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetWindowsVersionUsingAnalyticsInfo: {ex.Message}", $"{nameof(Extensions)}");
            return new Version(); // 0.0
        }
    }

    #region [Keyboard Helpers]
    public static bool IsShiftKeyDown()
    {
        var shiftKey = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
        return shiftKey is Windows.UI.Core.CoreVirtualKeyStates.Down or (Windows.UI.Core.CoreVirtualKeyStates.Down | Windows.UI.Core.CoreVirtualKeyStates.Locked);
    }

    public static bool IsCtrlKeyDown()
    {
        var ctrlKey = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        return ctrlKey is Windows.UI.Core.CoreVirtualKeyStates.Down or (Windows.UI.Core.CoreVirtualKeyStates.Down | Windows.UI.Core.CoreVirtualKeyStates.Locked);
    }
    #endregion

    /// <summary>
    /// Copies one <see cref="List{T}"/> to another <see cref="List{T}"/> by value (deep copy).
    /// </summary>
    /// <returns><see cref="List{T}"/></returns>
    /// <remarks>
    /// If your model does not inherit from <see cref="ICloneable"/>
    /// then a manual DTO copying technique could be used instead.
    /// </remarks>
    public static List<T> DeepCopy<T>(this List<T> source) where T : ICloneable
    {
        if (source == null)
            throw new ArgumentNullException($"{nameof(source)} list cannot be null.");

        List<T> destination = new List<T>(source.Count);
        foreach (T item in source)
        {
            if (item is ICloneable cloneable)
                destination.Add((T)cloneable.Clone());
            else
                throw new InvalidOperationException($"Type {typeof(T).FullName} does not implement ICloneable.");
        }

        return destination;
    }

    /// <summary>
    /// An updated string truncation helper.
    /// </summary>
    /// <remarks>
    /// This can be helpful when the CharacterEllipsis TextTrimming Property is not available.
    /// </remarks>
    public static string Truncate(this string text, int maxLength, string mesial = "…")
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (maxLength > 0 && text.Length > maxLength)
        {
            var limit = maxLength / 2;
            if (limit > 1)
            {
                return String.Format("{0}{1}{2}", text.Substring(0, limit).Trim(), mesial, text.Substring(text.Length - limit).Trim());
            }
            else
            {
                var tmp = text.Length <= maxLength ? text : text.Substring(0, maxLength).Trim();
                return String.Format("{0}{1}", tmp, mesial);
            }
        }
        return text;
    }

    public static string HumanReadableSize(this long length)
    {
        const int unit = 1024;
        var mu = new List<string> { "B", "KB", "MB", "GB", "PT" };
        while (length > unit)
        {
            mu.RemoveAt(0);
            length /= unit;
        }
        return $"{length}{mu[0]}";
    }


#pragma warning disable 8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
    /// <summary>
    /// Helper for <see cref="System.Collections.Generic.SortedList{TKey, TValue}"/>
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="sortedList"></param>
    /// <returns><see cref="Dictionary{TKey, TValue}"/></returns>
    public static Dictionary<TKey, TValue> ConvertToDictionary<TKey, TValue>(this System.Collections.Generic.SortedList<TKey, TValue> sortedList)
    {
        Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();
        foreach (KeyValuePair<TKey, TValue> pair in sortedList)
        {
            dictionary.Add(pair.Key, pair.Value);
        }
        return dictionary;
    }

    /// <summary>
    /// Helper for <see cref="System.Collections.SortedList"/>
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="sortedList"></param>
    /// <returns><see cref="Dictionary{TKey, TValue}"/></returns>
    public static Dictionary<TKey, TValue> ConvertToDictionary<TKey, TValue>(this System.Collections.SortedList sortedList)
    {
        Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();
        foreach (DictionaryEntry pair in sortedList)
        {
            dictionary.Add((TKey)pair.Key, (TValue)pair.Value);
        }
        return dictionary;
    }

    /// <summary>
    /// Helper for <see cref="System.Collections.Hashtable"/>
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="hashList"></param>
    /// <returns><see cref="Dictionary{TKey, TValue}"/></returns>
    public static Dictionary<TKey, TValue> ConvertToDictionary<TKey, TValue>(this System.Collections.Hashtable hashList)
    {
        Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();
        foreach (DictionaryEntry pair in hashList)
        {
            dictionary.Add((TKey)pair.Key, (TValue)pair.Value);
        }
        return dictionary;
    }
    #pragma warning restore 8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.


    #region [Duplicate Helpers]
    /// <summary>
    /// Returns a <see cref="Tuple{T1, T2}"/> representing the <paramref name="list"/>
    /// where <b>Item1</b> is the clean set and <b>Item2</b> is the duplicate set.
    /// </summary>
    public static (List<T>, List<T>) RemoveDuplicates<T>(this List<T> list)
    {
        HashSet<T> seen = new HashSet<T>();
        List<T> dupes = new List<T>();
        List<T> clean = new List<T>();
        foreach (T item in list)
        {
            if (seen.Contains(item))
                dupes.Add(item);
            else
            {
                seen.Add(item);
                clean.Add(item);
            }
        }
        return (clean, dupes);
    }

    /// <summary>
    /// Returns a <see cref="Tuple{T1, T2}"/> representing the <paramref name="enumerable"/>
    /// where <b>Item1</b> is the clean set and <b>Item2</b> is the duplicate set.
    /// </summary>
    public static (List<T>, List<T>) RemoveDuplicates<T>(this IEnumerable<T> enumerable)
    {
        HashSet<T> seen = new HashSet<T>();
        List<T> dupes = new List<T>();
        List<T> clean = new List<T>();
        foreach (T item in enumerable)
        {
            if (seen.Contains(item))
                dupes.Add(item);
            else
            {
                seen.Add(item);
                clean.Add(item);
            }
        }
        return (clean, dupes);
    }

    /// <summary>
    /// Returns a <see cref="Tuple{T1, T2}"/> representing the <paramref name="array"/>
    /// where <b>Item1</b> is the clean set and <b>Item2</b> is the duplicate set.
    /// </summary>
    public static (T[], T[]) RemoveDuplicates<T>(this T[] array)
    {
        HashSet<T> seen = new HashSet<T>();
        List<T> dupes = new List<T>();
        List<T> clean = new List<T>();
        foreach (T item in array)
        {
            if (seen.Contains(item))
                dupes.Add(item);
            else
            {
                seen.Add(item);
                clean.Add(item);
            }
        }
        return (clean.ToArray(), dupes.ToArray());
    }

    /// <summary>
    /// Returns a <see cref="IEnumerable{T}"/> representing the <paramref name="input"/> with duplicates removed.
    /// </summary>
    public static IEnumerable<T> DedupeUsingHashSet<T>(this IEnumerable<T> input)
    {
        if (input == null)
            yield return (T)Enumerable.Empty<T>();

        var values = new HashSet<T>();
        foreach (T item in input)
        {
            // The add function returns false if the item already exists.
            if (values.Add(item))
                yield return item;
        }
    }

    /// <summary>
    /// Returns a <see cref="List{T}"/> representing the <paramref name="input"/> with duplicates removed.
    /// </summary>
    public static List<T> DedupeUsingLINQ<T>(this List<T> input)
    {
        if (input == null)
            return new List<T>();

        return input.Distinct().ToList();
    }

    /// <summary>
    /// Returns a <see cref="List{T}"/> representing the <paramref name="input"/> with duplicates removed.
    /// </summary>
    public static List<T> DedupeUsingHashSet<T>(this List<T> input)
    {
        if (input == null)
            return new List<T>();

        return (new HashSet<T>(input)).ToList();
    }

    /// <summary>
    /// Returns a <see cref="List{T}"/> representing the <paramref name="input"/> with duplicates removed.
    /// </summary>
    public static List<T> DedupeUsingDictionary<T>(this List<T> input)
    {
        if (input == null)
            return new List<T>();

        Dictionary<T, bool> seen = new Dictionary<T, bool>();
        List<T> result = new List<T>();

        foreach (T item in input)
        {
            if (!seen.ContainsKey(item))
            {
                seen[item] = true;
                result.Add(item);
            }
        }

        return result;
    }

    /// <summary>
    /// Returns true if the <paramref name="input"/> contains duplicates, false otherwise.
    /// </summary>
    public static bool HasDuplicates<T>(this IEnumerable<T> input)
    {
        var knownKeys = new HashSet<T>();
        return input.Any(item => !knownKeys.Add(item));
    }

    /// <summary>
    /// Returns true if the <paramref name="input"/> contains duplicates, false otherwise.
    /// </summary>
    public static bool HasDuplicates<T>(this List<T> input)
    {
        var knownKeys = new HashSet<T>();
        return input.Any(item => !knownKeys.Add(item));
    }
    #endregion

    public static List<string> ExtractUrls(this string text)
    {
        List<string> urls = new List<string>();
        Regex urlRx = new Regex(@"((https?|ftp|file)\://|www\.)[A-Za-z0-9\.\-]+(/[A-Za-z0-9\?\&\=;\+!'\\(\)\*\-\._~%]*)*", RegexOptions.IgnoreCase);
        MatchCollection matches = urlRx.Matches(text);
        foreach (Match match in matches) { urls.Add(match.Value); }
        return urls;
    }

    public static string DumpContent<T>(this List<T> list)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("[ ");
        foreach (T item in list)
        {
            sb.Append(item);
            sb.Append(", ");
        }
        sb.Append(']');
        return sb.ToString();
    }

    public static int Remove<T>(this ObservableCollection<T> collection, Func<T, bool> predicate)
    {
        var itemsToRemove = collection.Where(predicate).ToList();
        foreach (T item in itemsToRemove)
        {
            collection.Remove(item);
        }
        return itemsToRemove.Count;
    }

    /// <summary>
    /// Helper method that takes a string as input and returns a DateTime object.
    /// This method can handle date formats such as "04/30", "0430", "04/2030", 
    /// "042030", "42030", "4/2030" and uses the current year as the year value
    /// for the returned DateTime object.
    /// </summary>
    /// <param name="dateString">the month and year string to parse</param>
    /// <returns><see cref="DateTime"/></returns>
    /// <example>
    /// CardData.CreditData.ExpirationDate = response.ExpiryDate.ExtractExpiration();
    /// </example>
    public static DateTime ExtractExpiration(this string dateString)
    {
        if (string.IsNullOrEmpty(dateString))
            return DateTime.Now;

        try
        {
            string yearPrefix = DateTime.Now.Year.ToString().Substring(0, 2);
            string yearSuffix = "00";

            if (dateString.Contains(@"\"))
                dateString = dateString.Replace(@"\", "/");

            if (dateString.Length == 5 && !dateString.Contains("/"))      // Myyyy
            {
                yearSuffix = dateString.Substring(dateString.Length - 2, 2);
                dateString = dateString.PadLeft(6, '0');
            }
            else if (dateString.Length == 4 && !dateString.Contains("/")) // MMyy
            {
                yearSuffix = dateString.Substring(dateString.Length - 2, 2);
                dateString = dateString.PadLeft(4, '0');
            }
            else if (dateString.Length == 3 && !dateString.Contains("/")) // Myy
            {
                yearSuffix = dateString.Substring(dateString.Length - 2, 2);
                dateString = dateString.PadLeft(4, '0');
            }
            else if (dateString.Length > 4)  // MM/yy
                yearSuffix = dateString.Substring(dateString.Length - 2, 2);
            else if (dateString.Length > 3)  // MMyy
                yearSuffix = dateString.Substring(dateString.Length - 2, 2);
            else if (dateString.Length > 2)  // Myy
                yearSuffix = dateString.Substring(dateString.Length - 2, 2);
            else if (dateString.Length > 1)  // should not happen
                yearSuffix = dateString;

            if (!int.TryParse($"{yearPrefix}{yearSuffix}", out int yearBase))
                yearBase = DateTime.Now.Year;

            DateTime result;
            if (DateTime.TryParseExact(dateString, "MM/yy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out result))
                return new DateTime(yearBase, result.Month, DateTime.DaysInMonth(yearBase, result.Month));
            else if (DateTime.TryParseExact(dateString, "MMyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out result))
                return new DateTime(yearBase, result.Month, DateTime.DaysInMonth(yearBase, result.Month));
            else if (DateTime.TryParseExact(dateString, "M/yy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out result))
                return new DateTime(yearBase, result.Month, DateTime.DaysInMonth(yearBase, result.Month));
            else if (DateTime.TryParseExact(dateString, "Myy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out result))
                return new DateTime(yearBase, result.Month, DateTime.DaysInMonth(yearBase, result.Month));
            else if (DateTime.TryParseExact(dateString, "MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out result))
                return new DateTime(result.Year, result.Month, DateTime.DaysInMonth(DateTime.Now.Year, result.Month));
            else if (DateTime.TryParseExact(dateString, "MMyyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out result))
                return new DateTime(result.Year, result.Month, DateTime.DaysInMonth(DateTime.Now.Year, result.Month));
            else if (DateTime.TryParseExact(dateString, "M/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out result))
                return new DateTime(result.Year, result.Month, DateTime.DaysInMonth(DateTime.Now.Year, result.Month));
            else if (DateTime.TryParseExact(dateString, "Myyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out result))
                return new DateTime(result.Year, result.Month, DateTime.DaysInMonth(DateTime.Now.Year, result.Month));
            else if (DateTime.TryParseExact(dateString, "yy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out result))
                return new DateTime(yearBase, 12, DateTime.DaysInMonth(yearBase, 12));
            else
                System.Diagnostics.Debug.WriteLine("[WARNING] ExtractExpiration: Invalid date format.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] ExtractExpiration: {ex.Message}");
        }

        return DateTime.Now;
    }

    public const double Epsilon = 0.000000000001;
    /// <summary>
    /// Determine if one number is greater than another.
    /// </summary>
    /// <param name="left">First <see cref="double"/></param>
    /// <param name="right">Second <see cref="double"/></param>
    /// <returns>
    /// True if the first number is greater than the second, false otherwise.
    /// </returns>
    public static bool IsGreaterThan(double left, double right)
    {
        return (left > right) && !AreClose(left, right);
    }

    /// <summary>
    /// Determine if one number is less than or close to another.
    /// </summary>
    /// <param name="left">First <see cref="double"/></param>
    /// <param name="right">Second <see cref="double"/></param>
    /// <returns>
    /// True if the first number is less than or close to the second, false otherwise.
    /// </returns>
    public static bool IsLessThanOrClose(double left, double right)
    {
        return (left < right) || AreClose(left, right);
    }

    /// <summary>
    /// Determine if two numbers are close in value.
    /// </summary>
    /// <param name="left">First <see cref="double"/></param>
    /// <param name="right">Second <see cref="double"/></param>
    /// <returns>
    /// True if the first number is close in value to the second, false otherwise.
    /// </returns>
    public static bool AreClose(double left, double right)
    {
        if (left == right)
        {
            return true;
        }

        double a = (Math.Abs(left) + Math.Abs(right) + 10.0) * Epsilon;
        double b = left - right;
        return (-a < b) && (a > b);
    }

    /// <summary>
    /// Consider anything within an order of magnitude of epsilon to be zero.
    /// </summary>
    /// <param name="value">The <see cref="double"/> to check</param>
    /// <returns>
    /// True if the number is zero, false otherwise.
    /// </returns>
    public static bool IsZero(this double value)
    {
        return Math.Abs(value) < Epsilon;
    }

    public static bool IsInvalid(this double value)
    {
        if (value == double.NaN || value == double.NegativeInfinity || value == double.PositiveInfinity)
            return true;

        return false;
    }

    public static double Mod(this double number, double divider)
    {
        var result = number % divider;
        if (double.IsNaN(result))
            return 0;
        result = result < 0 ? result + divider : result;
        return result;
    }

    /// <summary>
    /// Checks to see if a date is between two dates.
    /// </summary>
    public static bool Between(this DateTime dt, DateTime rangeBeg, DateTime rangeEnd) => dt.Ticks >= rangeBeg.Ticks && dt.Ticks <= rangeEnd.Ticks;


    /// <summary>
    /// Calculates the remaining months in the given <see cref="DateTime"/>.
    /// </summary>
    /// <param name="date"><see cref="DateTime"/></param>
    public static int MonthsTilEndOfInYear(this DateTime? date)
    {
        if (date is null)
            date = DateTime.Now;

        return 12 - date.Value.Month;
    }

    /// <summary>
    /// Calculates the remaining days in the given <see cref="DateTime"/>.
    /// </summary>
    /// <param name="date"><see cref="DateTime"/></param>
    public static int DaysTilEndOfMonth(this DateTime? date)
    {
        if (date is null)
            date = DateTime.Now;

        int daysInMonth = DateTime.DaysInMonth(date.Value.Year, date.Value.Month);
        return daysInMonth - date.Value.Day;
    }

    /// <summary>
    /// Returns a range of <see cref="DateTime"/> objects matching the criteria provided.
    /// </summary>
    /// <example>
    /// IEnumerable<DateTime> dateRange = DateTime.Now.GetDateRangeTo(DateTime.Now.AddDays(80));
    /// </example>
    /// <param name="self"><see cref="DateTime"/></param>
    /// <param name="toDate"><see cref="DateTime"/></param>
    /// <returns><see cref="IEnumerable{DateTime}"/></returns>
    public static IEnumerable<DateTime> GetDateRangeTo(this DateTime self, DateTime toDate)
    {
        // Query Syntax:
        //IEnumerable<int> range = Enumerable.Range(0, new TimeSpan(toDate.Ticks - self.Ticks).Days);
        //IEnumerable<DateTime> dates = from p in range select self.Date.AddDays(p);

        // Method Syntax:
        IEnumerable<DateTime> dates = Enumerable.Range(0, new TimeSpan(toDate.Ticks - self.Ticks).Days).Select(p => self.Date.AddDays(p));

        return dates;
    }

    /// <summary>
    /// Returns an <see cref="Int32"/> amount of days between two <see cref="DateTime"/> objects.
    /// </summary>
    /// <param name="self"><see cref="DateTime"/></param>
    /// <param name="toDate"><see cref="DateTime"/></param>
    public static int GetDaysBetween(this DateTime self, DateTime toDate)
    {
        return new TimeSpan(toDate.Ticks - self.Ticks).Days;
    }

    /// <summary>
    /// Returns a <see cref="TimeSpan"/> amount between two <see cref="DateTime"/> objects.
    /// </summary>
    /// <param name="self"><see cref="DateTime"/></param>
    /// <param name="toDate"><see cref="DateTime"/></param>
    public static TimeSpan GetTimeSpanBetween(this DateTime self, DateTime toDate)
    {
        return new TimeSpan(toDate.Ticks - self.Ticks);
    }

    /// <summary>
    /// Figure out how old something is.
    /// </summary>
    /// <returns>integer amount in years</returns>
    public static int CalculateYearAge(this DateTime dateTime)
    {
        int age = DateTime.Now.Year - dateTime.Year;
        if (DateTime.Now < dateTime.AddYears(age))
        {
            age--;
        }

        return age;
    }

    /// <summary>
    /// Figure out how old something is.
    /// </summary>
    /// <returns>integer amount in months</returns>
    public static int CalculateMonthAge(this DateTime dateTime)
    {
        int age = DateTime.Now.Year - dateTime.Year;
        if (DateTime.Now < dateTime.AddYears(age))
        {
            age--;
        }

        return age * 12;
    }

    /// <summary>
    /// Converts <see cref="TimeSpan"/> objects to a simple human-readable string.
    /// e.g. 420 milliseconds, 3.1 seconds, 2 minutes, 4.231 hours, etc.
    /// </summary>
    /// <param name="span"><see cref="TimeSpan"/></param>
    /// <param name="significantDigits">number of right side digits in output (precision)</param>
    /// <returns></returns>
    public static string ToTimeString(this TimeSpan span, int significantDigits = 3)
    {
        var format = $"G{significantDigits}";
        return span.TotalMilliseconds < 1000 ? span.TotalMilliseconds.ToString(format) + " milliseconds"
                : (span.TotalSeconds < 60 ? span.TotalSeconds.ToString(format) + " seconds"
                : (span.TotalMinutes < 60 ? span.TotalMinutes.ToString(format) + " minutes"
                : (span.TotalHours < 24 ? span.TotalHours.ToString(format) + " hours"
                : span.TotalDays.ToString(format) + " days")));
    }

    /// <summary>
    /// Converts <see cref="TimeSpan"/> objects to a simple human-readable string.
    /// e.g. 420 milliseconds, 3.1 seconds, 2 minutes, 4.231 hours, etc.
    /// </summary>
    /// <param name="span"><see cref="TimeSpan"/></param>
    /// <param name="significantDigits">number of right side digits in output (precision)</param>
    /// <returns></returns>
    public static string ToTimeString(this TimeSpan? span, int significantDigits = 3)
    {
        var format = $"G{significantDigits}";
        return span?.TotalMilliseconds < 1000 ? span?.TotalMilliseconds.ToString(format) + " milliseconds"
                : (span?.TotalSeconds < 60 ? span?.TotalSeconds.ToString(format) + " seconds"
                : (span?.TotalMinutes < 60 ? span?.TotalMinutes.ToString(format) + " minutes"
                : (span?.TotalHours < 24 ? span?.TotalHours.ToString(format) + " hours"
                : span?.TotalDays.ToString(format) + " days")));
    }

    /// <summary>
    /// Display a readable sentence as to when the time will happen.
    /// e.g. "in one second" or "in 2 days"
    /// </summary>
    /// <param name="value"><see cref="TimeSpan"/>the future time to compare from now</param>
    /// <returns>human friendly format</returns>
    public static string ToReadableTime(this TimeSpan value)
    {
        double delta = value.TotalSeconds;
        if (delta < 60) { return value.Seconds == 1 ? "one second" : value.Seconds + " seconds"; }
        if (delta < 120) { return "a minute"; }
        if (delta < 3000) { return value.Minutes + " minutes"; } // 50 * 60
        if (delta < 5400) { return "an hour"; } // 90 * 60
        if (delta < 86400) { return value.Hours + " hours"; } // 24 * 60 * 60
        if (delta < 172800) { return "one day"; } // 48 * 60 * 60
        if (delta < 2592000) { return value.Days + " days"; } // 30 * 24 * 60 * 60
        if (delta < 31104000) // 12 * 30 * 24 * 60 * 60
        {
            int months = Convert.ToInt32(Math.Floor((double)value.Days / 30));
            return months <= 1 ? "one month" : months + " months";
        }
        int years = Convert.ToInt32(Math.Floor((double)value.Days / 365));
        return years <= 1 ? "one year" : years + " years";
    }

    /// <summary>
    /// Display a readable sentence as to when that time happened.
    /// e.g. "5 minutes ago" or "in 2 days"
    /// </summary>
    /// <param name="value"><see cref="DateTime"/>the past/future time to compare from now</param>
    /// <returns>human friendly format</returns>
    public static string ToReadableTime(this DateTime value, bool useUTC = false)
    {
        TimeSpan ts;
        if (useUTC) { ts = new TimeSpan(DateTime.UtcNow.Ticks - value.Ticks); }
        else { ts = new TimeSpan(DateTime.Now.Ticks - value.Ticks); }

        double delta = ts.TotalSeconds;
        if (delta < 0) // in the future
        {
            delta = Math.Abs(delta);
            if (delta < 60) { return Math.Abs(ts.Seconds) == 1 ? "in one second" : "in " + Math.Abs(ts.Seconds) + " seconds"; }
            if (delta < 120) { return "in a minute"; }
            if (delta < 3000) { return "in " + Math.Abs(ts.Minutes) + " minutes"; } // 50 * 60
            if (delta < 5400) { return "in an hour"; } // 90 * 60
            if (delta < 86400) { return "in " + Math.Abs(ts.Hours) + " hours"; } // 24 * 60 * 60
            if (delta < 172800) { return "tomorrow"; } // 48 * 60 * 60
            if (delta < 2592000) { return "in " + Math.Abs(ts.Days) + " days"; } // 30 * 24 * 60 * 60
            if (delta < 31104000) // 12 * 30 * 24 * 60 * 60
            {
                int months = Convert.ToInt32(Math.Floor((double)Math.Abs(ts.Days) / 30));
                return months <= 1 ? "in one month" : "in " + months + " months";
            }
            int years = Convert.ToInt32(Math.Floor((double)Math.Abs(ts.Days) / 365));
            return years <= 1 ? "in one year" : "in " + years + " years";
        }
        else // in the past
        {
            if (delta < 60) { return ts.Seconds == 1 ? "one second ago" : ts.Seconds + " seconds ago"; }
            if (delta < 120) { return "a minute ago"; }
            if (delta < 3000) { return ts.Minutes + " minutes ago"; } // 50 * 60
            if (delta < 5400) { return "an hour ago"; } // 90 * 60
            if (delta < 86400) { return ts.Hours + " hours ago"; } // 24 * 60 * 60
            if (delta < 172800) { return "yesterday"; } // 48 * 60 * 60
            if (delta < 2592000) { return ts.Days + " days ago"; } // 30 * 24 * 60 * 60
            if (delta < 31104000) // 12 * 30 * 24 * 60 * 60
            {
                int months = Convert.ToInt32(Math.Floor((double)ts.Days / 30));
                return months <= 1 ? "one month ago" : months + " months ago";
            }
            int years = Convert.ToInt32(Math.Floor((double)ts.Days / 365));
            return years <= 1 ? "one year ago" : years + " years ago";
        }
    }

    /// <summary>
    /// Determines if the date is a working day, weekend, or determine the next workday coming up.
    /// </summary>
    /// <param name="date"><see cref="DateTime"/></param>
    public static bool WorkingDay(this DateTime date)
    {
        return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
    }

    /// <summary>
    /// Determines if the date is on a weekend (i.e. Saturday or Sunday)
    /// </summary>
    /// <param name="date"><see cref="DateTime"/></param>
    public static bool IsWeekend(this DateTime date)
    {
        return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
    }

    /// <summary>
    /// Gets the next date that is not a weekend.
    /// </summary>
    /// <param name="date"><see cref="DateTime"/></param>
    public static DateTime NextWorkday(this DateTime date)
    {
        DateTime nextDay = date.AddDays(1);
        while (!nextDay.WorkingDay())
        {
            nextDay = nextDay.AddDays(1);
        }
        return nextDay;
    }

    /// <summary>
    /// Determine the Next date by passing in a DayOfWeek (i.e. from this date, when is the next Tuesday?)
    /// </summary>
    public static DateTime Next(this DateTime current, DayOfWeek dayOfWeek)
    {
        int offsetDays = dayOfWeek - current.DayOfWeek;
        if (offsetDays <= 0)
        {
            offsetDays += 7;
        }
        DateTime result = current.AddDays(offsetDays);
        return result;
    }

    /// <summary>
    /// Converts a DateTime to a DateTimeOffset with the specified offset
    /// </summary>
    /// <param name="date">The DateTime to convert</param>
    /// <param name="offset">The offset to apply to the date field</param>
    /// <returns>The corresponding DateTimeOffset</returns>
    public static DateTimeOffset ToOffset(this DateTime date, TimeSpan offset) => new DateTimeOffset(date).ToOffset(offset);

    /// <summary>
    /// Accounts for once the <paramref name="date1"/> is past <paramref name="date2"/>
    /// or falls within the amount of <paramref name="days"/>.
    /// </summary>
    public static bool WithinDaysOrPast(this DateTime date1, DateTime date2, double days = 7.0)
    {
        if (date1 > date2) // Account for past-due amounts.
            return true;
        else
        {
            TimeSpan difference = date1 - date2;
            return Math.Abs(difference.TotalDays) <= days;
        }
    }

    /// <summary>
    /// Only accounts for date1 being within range of date2.
    /// </summary>
    public static bool WithinOneDay(this DateTime date1, DateTime date2)
    {
        TimeSpan difference = date1 - date2;
        return Math.Abs(difference.TotalDays) <= 1.0;
    }

    /// <summary>
    /// Only accounts for date1 being within range of date2 by some amount.
    /// </summary>
    public static bool WithinAmountOfDays(this DateTime date1, DateTime date2, double days)
    {
        TimeSpan difference = date1 - date2;
        return Math.Abs(difference.TotalDays) <= days;
    }

    public static DateTime ConvertToLastDayOfMonth(this DateTime date) => new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

    /// <summary>
    /// Multiplies the given <see cref="TimeSpan"/> by the scalar amount provided.
    /// </summary>
    public static TimeSpan Multiply(this TimeSpan timeSpan, double scalar) => new TimeSpan((long)(timeSpan.Ticks * scalar));

    /// <summary>
    /// Gets a <see cref="DateTime"/> object representing the time until midnight.
    /// <example><code>
    /// var hoursUntilMidnight = TimeUntilMidnight().TimeOfDay.TotalHours;
    /// </code></example>
    /// </summary>
    public static DateTime TimeUntilMidnight()
    {
        DateTime now = DateTime.Now;
        DateTime midnight = now.Date.AddDays(1);
        TimeSpan timeUntilMidnight = midnight - now;
        return new DateTime(timeUntilMidnight.Ticks);
    }

    /// <summary>
    /// Converts long file size into typical browser file size.
    /// </summary>
    public static string ToFileSize(this ulong size)
    {
        if (size < 1024) { return (size).ToString("F0") + " Bytes"; }
        if (size < Math.Pow(1024, 2)) { return (size / 1024).ToString("F0") + "KB"; }
        if (size < Math.Pow(1024, 3)) { return (size / Math.Pow(1024, 2)).ToString("F0") + "MB"; }
        if (size < Math.Pow(1024, 4)) { return (size / Math.Pow(1024, 3)).ToString("F0") + "GB"; }
        if (size < Math.Pow(1024, 5)) { return (size / Math.Pow(1024, 4)).ToString("F0") + "TB"; }
        if (size < Math.Pow(1024, 6)) { return (size / Math.Pow(1024, 5)).ToString("F0") + "PB"; }
        return (size / Math.Pow(1024, 6)).ToString("F0") + "EB";
    }

    /// <summary>
    /// Returns the AppData path including the <paramref name="moduleName"/>.
    /// e.g. "C:\Users\UserName\AppData\Local\MenuDemo\Settings"
    /// </summary>
    public static string LocalApplicationDataFolder(string moduleName = "Settings")
    {
        var result = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}\\{moduleName}");
        return result;
    }

    /// <summary>
    /// Offers two ways to determine the local app folder.
    /// </summary>
    /// <returns></returns>
    public static string LocalApplicationDataFolder()
    {
        WindowsIdentity? currentUser = WindowsIdentity.GetCurrent();
        SecurityIdentifier? currentUserSID = currentUser.User;
        SecurityIdentifier? localSystemSID = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        if (currentUserSID != null && currentUserSID.Equals(localSystemSID))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        }
        else
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
    }

    /// <summary>
    /// Check if a file can be created in the directory.
    /// </summary>
    /// <param name="directoryPath">the directory path to evaluate</param>
    /// <returns>true if the directory is writable, false otherwise</returns>
    public static bool CanWriteToDirectory(string directoryPath)
    {
        try
        {
            using (FileStream fs = File.Create(Path.Combine(directoryPath, "test.txt"), 1, FileOptions.DeleteOnClose)) { /* no-op */ }
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void PostWithComplete<T>(this SynchronizationContext context, Action<T> action, T state)
    {
        context.OperationStarted();
        context.Post(o => {
            try { action((T)o!); }
            finally { context.OperationCompleted(); }
        },
            state
        );
    }

    public static void PostWithComplete(this SynchronizationContext context, Action action)
    {
        context.OperationStarted();
        context.Post(_ => {
            try { action(); }
            finally { context.OperationCompleted(); }
        },
            null
        );
    }

    public static string SeparateCamelCase(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        StringBuilder result = new StringBuilder();
        result.Append(input[0]);

        for (int i = 1; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]))
                result.Append(' ');

            result.Append(input[i]);
        }

        return result.ToString();
    }

    public static int CompareName(this Object obj1, Object obj2)
    {
        if (obj1 is null && obj2 is null)
            return 0;

        PropertyInfo? pi1 = obj1 as PropertyInfo;
        if (pi1 is null)
            return -1;

        PropertyInfo? pi2 = obj2 as PropertyInfo;
        if (pi2 is null)
            return 1;

        return String.Compare(pi1.Name, pi2.Name);
    }

    /// <summary>
    /// Clamping function for any value of type <see cref="IComparable{T}"/>.
    /// </summary>
    /// <param name="val">initial value</param>
    /// <param name="min">lowest range</param>
    /// <param name="max">highest range</param>
    /// <returns>clamped value</returns>
    public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
    {
        return val.CompareTo(min) < 0 ? min : (val.CompareTo(max) > 0 ? max : val);
    }

    /// <summary>
    /// Linear interpolation for a range of floats.
    /// </summary>
    public static float Lerp(this float start, float end, float amount = 0.5F) => start + (end - start) * amount;
    /// <summary>
    /// Linear interpolation for a range of double.
    /// </summary>
    public static double Lerp(this double start, double end, double amount = 0.5F) => start + (end - start) * amount;

    public static float LogLerp(this float start, float end, float percent, float logBase = 1.2F) => start + (end - start) * MathF.Log(percent, logBase);

    public static double LogLerp(this double start, double end, double percent, double logBase = 1.2F) => start + (end - start) * Math.Log(percent, logBase);

    /// <summary>
    /// Scales a range of integers. [baseMin to baseMax] will become [limitMin to limitMax]
    /// </summary>
    public static int Scale(this int valueIn, int baseMin, int baseMax, int limitMin, int limitMax) => ((limitMax - limitMin) * (valueIn - baseMin) / (baseMax - baseMin)) + limitMin;
    /// <summary>
    /// Scales a range of floats. [baseMin to baseMax] will become [limitMin to limitMax]
    /// </summary>
    public static float Scale(this float valueIn, float baseMin, float baseMax, float limitMin, float limitMax) => ((limitMax - limitMin) * (valueIn - baseMin) / (baseMax - baseMin)) + limitMin;
    /// <summary>
    /// Scales a range of double. [baseMin to baseMax] will become [limitMin to limitMax]
    /// </summary>
    public static double Scale(this double valueIn, double baseMin, double baseMax, double limitMin, double limitMax) => ((limitMax - limitMin) * (valueIn - baseMin) / (baseMax - baseMin)) + limitMin;

    public static int MapValue(this int val, int inMin, int inMax, int outMin, int outMax) => (val - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;

    public static float MapValue(this float val, float inMin, float inMax, float outMin, float outMax) => (val - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;

    public static double MapValue(this double val, double inMin, double inMax, double outMin, double outMax) => (val - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;

    /// <summary>
    /// Used to gradually reduce the effect of certain changes over time.
    /// </summary>
    /// <param name="value">Some initial value, e.g. 40</param>
    /// <param name="target">Where we want the value to end up, e.g. 100</param>
    /// <param name="rate">How quickly we want to reach the target, e.g. 0.25</param>
    /// <returns></returns>
    public static float Dampen(this float value, float target, float rate)
    {
        float dampenedValue = value;
        if (value != target)
        {
            float dampeningFactor = MathF.Pow(1 - MathF.Abs((value - target) / rate), 2);
            dampenedValue = target + ((value - target) * dampeningFactor);
        }
        return dampenedValue;
    }

    public static float GetDecimalPortion(this float number)
    {
        // If the number is negative, make it positive.
        if (number < 0)
            number = -number;

        // Get the integer portion of the number.
        int integerPortion = (int)number;

        // Subtract the integer portion to get the decimal portion.
        float decimalPortion = number - integerPortion;

        return decimalPortion;
    }

    public static int GetDecimalPlacesCount(this string valueString) => valueString.SkipWhile(c => c.ToString(CultureInfo.CurrentCulture) != CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator).Skip(1).Count();

    /// <summary>
    /// Similar to <see cref="GetReadableTime(TimeSpan)"/>.
    /// </summary>
    /// <param name="timeSpan"><see cref="TimeSpan"/></param>
    /// <returns>formatted text</returns>
    public static string ToReadableString(this TimeSpan span)
    {
        var parts = new StringBuilder();
        if (span.Days > 0)
            parts.Append($"{span.Days} day{(span.Days == 1 ? string.Empty : "s")} ");
        if (span.Hours > 0)
            parts.Append($"{span.Hours} hour{(span.Hours == 1 ? string.Empty : "s")} ");
        if (span.Minutes > 0)
            parts.Append($"{span.Minutes} minute{(span.Minutes == 1 ? string.Empty : "s")} ");
        if (span.Seconds > 0)
            parts.Append($"{span.Seconds} second{(span.Seconds == 1 ? string.Empty : "s")} ");
        if (span.Milliseconds > 0)
            parts.Append($"{span.Milliseconds} millisecond{(span.Milliseconds == 1 ? string.Empty : "s")} ");

        if (parts.Length == 0) // result was less than 1 millisecond
            return $"{span.TotalMilliseconds:N4} milliseconds"; // similar to span.Ticks
        else
            return parts.ToString().Trim();
    }

    /// <summary>
    /// This should only be used on instantiated objects, not static objects.
    /// </summary>
    public static string ToStringDump<T>(this T obj)
    {
        const string Seperator = "\r\n";
        const System.Reflection.BindingFlags BindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;

        if (obj is null)
            return string.Empty;

        try
        {
            var objProperties =
                from property in obj?.GetType().GetProperties(BindingFlags)
                where property.CanRead
                select string.Format("{0} : {1}", property.Name, property.GetValue(obj, null));

            return string.Join(Seperator, objProperties);
        }
        catch (Exception ex)
        {
            return $"⇒ Probably a non-instanced object: {ex.Message}";
        }
    }

    /// <summary>
    /// var stack = GeneralExtensions.GetStackTrace(new StackTrace());
    /// </summary>
    public static string GetStackTrace(StackTrace st)
    {
        string result = string.Empty;
        for (int i = 0; i < st.FrameCount; i++)
        {
            StackFrame? sf = st.GetFrame(i);
            result += sf?.GetMethod() + " <== ";
        }
        return result;
    }

    public static string Flatten(this Exception? exception)
    {
        var sb = new StringBuilder();
        while (exception != null)
        {
            sb.AppendLine(exception.Message);
            sb.AppendLine(exception.StackTrace);
            exception = exception.InnerException;
        }
        return sb.ToString();
    }

    public static string DumpFrames(this Exception exception)
    {
        var sb = new StringBuilder();
        var st = new StackTrace(exception, true);
        var frames = st.GetFrames();
        foreach (var frame in frames)
        {
            if (frame != null)
            {
                if (frame.GetFileLineNumber() < 1)
                    continue;

                sb.Append($"File: {frame.GetFileName()}")
                  .Append($", Method: {frame.GetMethod()?.Name}")
                  .Append($", LineNumber: {frame.GetFileLineNumber()}")
                  .Append($"{Environment.NewLine}");
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Helper for parsing command line arguments.
    /// </summary>
    /// <param name="inputArray"></param>
    /// <returns>string array of args excluding the 1st arg</returns>
    public static string[] IgnoreFirstTakeRest(this string[] inputArray)
    {
        if (inputArray.Length > 1)
            return inputArray.Skip(1).ToArray();
        else
            return inputArray;
    }

    /// <summary>
    /// Helper for parsing command line arguments.
    /// </summary>
    /// <param name="inputArray"></param>
    /// <returns>string array of args excluding the 1st arg</returns>
    public static string[] IgnoreNthTakeRest(this string[] inputArray, int skip = 1)
    {
        if (inputArray.Length > skip)
            return inputArray.Skip(skip).ToArray();
        else
            return inputArray;
    }

    /// <summary>
    /// Returns the first element from a tokenized string, e.g.
    /// Input:"{tag}"  Output:"tag"
    /// </summary>
    /// <example>
    /// var clean = ExtractFirst("{tag}", '{', '}');
    /// </example>
    public static string ExtractFirst(this string text, char start, char end)
    {
        string pattern = @"\" + start + "(.*?)" + @"\" + end; //pattern = @"\{(.*?)\}"
        Match match = Regex.Match(text, pattern);
        if (match.Success)
            return match.Groups[1].Value;
        else
            return "";
    }

    /// <summary>
    /// Returns the last element from a tokenized string, e.g.
    /// Input:"{tag}"  Output:"tag"
    /// </summary>
    /// <example>
    /// var clean = ExtractLast("{tag}", '{', '}');
    /// </example>
    public static string ExtractLast(this string text, char start, char end)
    {
        string pattern = @"\" + start + @"(.*?)\" + end; //pattern = @"\{(.*?)\}"
        MatchCollection matches = Regex.Matches(text, pattern);
        if (matches.Count > 0)
        {
            Match lastMatch = matches[matches.Count - 1];
            return lastMatch.Groups[1].Value;
        }
        else
            return "";
    }

    /// <summary>
    /// Returns all the elements from a tokenized string, e.g.
    /// Input:"{tag}"  Output:"tag"
    /// </summary>
    public static string[] ExtractAll(this string text, char start, char end)
    {
        string pattern = @"\" + start + @"(.*?)\" + end; //pattern = @"\{(.*?)\}"
        MatchCollection matches = Regex.Matches(text, pattern);
        string[] results = new string[matches.Count];
        for (int i = 0; i < matches.Count; i++)
            results[i] = matches[i].Groups[1].Value;

        return results;
    }

    /// <summary>
    /// Returns the specified occurrence of a character in a string.
    /// </summary>
    /// <returns>
    /// Index of requested occurrence if successful, -1 otherwise.
    /// </returns>
    /// <example>
    /// If you wanted to find the second index of the percent character in a string:
    /// int index = "blah%blah%blah".IndexOfNth('%', 2);
    /// </example>
    public static int IndexOfNth(this string input, char character, int position)
    {
        int index = -1;

        if (string.IsNullOrEmpty(input))
            return index;

        for (int i = 0; i < position; i++)
        {
            index = input.IndexOf(character, index + 1);
            if (index == -1)
                break;
        }

        return index;
    }


    /// <summary>
    /// var collection = new[] { 10, 20, 30 };
    /// collection.ForEach(Debug.WriteLine);
    /// </summary>
    public static void ForEach<T>(this IEnumerable<T> ie, Action<T> action)
    {
        foreach (var i in ie)
        {
            try
            {
                action(i);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] ForEach: {ex.Message}");
            }
        }
    }

    public static IEnumerable<T> JoinLists<T>(this IEnumerable<T> list1, IEnumerable<T> list2)
    {
        var joined = new[] { list1, list2 }.Where(x => x != null).SelectMany(x => x);
        return joined ?? Enumerable.Empty<T>();
    }

    public static IEnumerable<T> JoinLists<T>(this IEnumerable<T> list1, IEnumerable<T> list2, IEnumerable<T> list3)
    {
        var joined = new[] { list1, list2, list3 }.Where(x => x != null).SelectMany(x => x);
        return joined ?? Enumerable.Empty<T>();
    }

    public static IEnumerable<T> JoinMany<T>(params IEnumerable<T>[] array)
    {
        var final = array.Where(x => x != null).SelectMany(x => x);
        return final ?? Enumerable.Empty<T>();
    }

    public static void AddRange<T>(this ICollection<T> target, IEnumerable<T> source)
    {
        if (target == null) { throw new ArgumentNullException(nameof(target)); }
        if (source == null) { throw new ArgumentNullException(nameof(source)); }
        foreach (var element in source) { target.Add(element); }
    }

    /// <summary>
    /// Merges the two input <see cref="IList{T}"/> instances and makes sure no duplicate items are present
    /// </summary>
    /// <typeparam name="T">The type of elements in the input collections</typeparam>
    /// <param name="a">The first <see cref="IList{T}"/> to merge</param>
    /// <param name="b">The second <see cref="IList{T}"/> to merge</param>
    /// <returns>An <see cref="IList{T}"/> instance with elements from both <paramref name="a"/> and <paramref name="b"/></returns>
    public static IList<T> Merge<T>(this IList<T> a, IList<T> b)
    {
        if (a.Any(b.Contains))
            Debug.WriteLine("[WARNING] The input collection has at least an item already present in the second collection");

        return a.Concat(b).ToArray();
    }

    /// <summary>
    /// Merges the two input <see cref="IEnumerable{T}"/> instances and makes sure no duplicate items are present
    /// </summary>
    /// <typeparam name="T">The type of elements in the input collections</typeparam>
    /// <param name="a">The first <see cref="IEnumerable{T}"/> to merge</param>
    /// <param name="b">The second <see cref="IEnumerable{T}"/> to merge</param>
    /// <returns>An <see cref="IEnumerable{T}"/> instance with elements from both <paramref name="a"/> and <paramref name="b"/></returns>
    public static IEnumerable<T> Merge<T>(this IEnumerable<T> a, IEnumerable<T> b)
    {
        if (a.Any(b.Contains))
            Debug.WriteLine("[WARNING] The input collection has at least an item already present in the second collection");

        return a.Concat(b).ToArray();
    }

    /// <summary>
    /// Merges the two input <see cref="IReadOnlyCollection{T}"/> instances and makes sure no duplicate items are present
    /// </summary>
    /// <typeparam name="T">The type of elements in the input collections</typeparam>
    /// <param name="a">The first <see cref="IReadOnlyCollection{T}"/> to merge</param>
    /// <param name="b">The second <see cref="IReadOnlyCollection{T}"/> to merge</param>
    /// <returns>An <see cref="IReadOnlyCollection{T}"/> instance with elements from both <paramref name="a"/> and <paramref name="b"/></returns>
    public static IReadOnlyCollection<T> Merge<T>(this IReadOnlyCollection<T> a, IReadOnlyCollection<T> b)
    {
        if (a.Any(b.Contains))
            Debug.WriteLine("[WARNING] The input collection has at least an item already present in the second collection");

        return a.Concat(b).ToArray();
    }

    /// <summary>
    /// Creates a new <see cref="Span{T}"/> over an input <see cref="List{T}"/> instance.
    /// </summary>
    /// <typeparam name="T">The type of elements in the input <see cref="List{T}"/> instance.</typeparam>
    /// <param name="list">The input <see cref="List{T}"/> instance.</param>
    /// <returns>A <see cref="Span{T}"/> instance with the values of <paramref name="list"/>.</returns>
    /// <remarks>
    /// Note that the returned <see cref="Span{T}"/> is only guaranteed to be valid as long as the items within
    /// <paramref name="list"/> are not modified. Doing so might cause the <see cref="List{T}"/> to swap its
    /// internal buffer, causing the returned <see cref="Span{T}"/> to become out of date. That means that in this
    /// scenario, the <see cref="Span{T}"/> would end up wrapping an array no longer in use. Always make sure to use
    /// the returned <see cref="Span{T}"/> while the target <see cref="List{T}"/> is not modified.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this List<T>? list)
    {
        return CollectionsMarshal.AsSpan(list);
    }

    /// <summary>
    /// Returns a simple string representation of an array.
    /// </summary>
    /// <typeparam name="T">The element type of the array.</typeparam>
    /// <param name="array">The source array.</param>
    /// <returns>The <see cref="string"/> representation of the array.</returns>
    public static string ToArrayString<T>(this T?[] array)
    {
        // The returned string will be in the following format: [1, 2, 3]
        StringBuilder builder = new StringBuilder();
        builder.Append('[');
        for (int i = 0; i < array.Length; i++)
        {
            if (i != 0)
                builder.Append(",\t");

            builder.Append(array[i]?.ToString());
        }
        builder.Append(']');
        return builder.ToString();
    }

    /// <summary>
    /// Helper for web images.
    /// </summary>
    /// <returns><see cref="Stream"/></returns>
    public static async Task<Stream> CopyStream(this HttpContent source)
    {
        var stream = new MemoryStream();
        await source.CopyToAsync(stream);
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    /// <summary>
    /// IEnumerable file reader.
    /// </summary>
    public static IEnumerable<string> ReadFileLines(string path)
    {
        string? line = string.Empty;

        if (!File.Exists(path))
            yield return line;
        else
        {
            using (TextReader reader = File.OpenText(path))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }
    }

    /// <summary>
    /// IAsyncEnumerable file reader.
    /// </summary>
    public static async IAsyncEnumerable<string> ReadFileLinesAsync(string path)
    {
        string? line = string.Empty;

        if (!File.Exists(path))
            yield return line;
        else
        {
            using (TextReader reader = File.OpenText(path))
            {
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    yield return line;
                }
            }
        }
    }

    /// <summary>
    /// File writer for <see cref="IEnumerable{T}"/> parameters.
    /// </summary>
    public static bool WriteFileLines(string path, IEnumerable<string> lines)
    {
        using (TextWriter writer = File.CreateText(path))
        {
            foreach (var line in lines)
            {
                writer.WriteLine(line);
            }
        }

        return true;
    }

    /// <summary>
    /// De-dupe file reader using a <see cref="HashSet{T}"/>.
    /// </summary>
    public static HashSet<string> ReadLines(string path)
    {
        if (!File.Exists(path))
            return new();

        return new HashSet<string>(File.ReadAllLines(path), StringComparer.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// De-dupe file writer using a <see cref="HashSet{T}"/>.
    /// </summary>
    public static bool WriteLines(string path, IEnumerable<string> lines)
    {
        var output = new HashSet<string>(lines, StringComparer.InvariantCultureIgnoreCase);

        using (TextWriter writer = File.CreateText(path))
        {
            foreach (var line in output)
            {
                writer.WriteLine(line);
            }
        }
        return true;
    }

    /// <summary>
    /// To populate parameters with a typical URI assigning format.
    /// This method assumes the format is like "mode=1,state=2,theme=dark"
    /// </summary>
    public static Dictionary<string, string> ParseAssignedValues(string inputString, string delimiter = ",")
    {
        Dictionary<string, string> parameters = new();

        try
        {
            var parts = inputString.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
            parameters = parts.Select(x => x.Split("=")).ToDictionary(x => x.First(), x => x.Last());
        }
        catch (Exception ex) { Debug.WriteLine($"[ERROR] ParseAssignedValues: {ex.Message}"); }

        return parameters;
    }

    /// <summary>
    /// <example><code>
    /// Dictionary<char, int> charCount = GetCharacterCount("some input text string here");
    /// foreach (var kvp in charCount) { Debug.WriteLine($"Character: {kvp.Key}, Count: {kvp.Value}"); }
    /// </code></example>
    /// </summary>
    /// <param name="input">the text string to analyze</param>
    /// <returns><see cref="Dictionary{TKey, TValue}"/></returns>
    public static Dictionary<char, int> GetCharacterCount(this string input)
    {
        Dictionary<char, int> charCount = new();

        if (string.IsNullOrEmpty(input))
            return charCount;

        foreach (var ch in input)
        {
            if (charCount.ContainsKey(ch))
                charCount[ch]++;
            else
                charCount[ch] = 1;
        }

        return charCount;
    }

    public static T? DeserializeFromFile<T>(string filePath, ref string error)
    {
        try
        {
            string jsonString = System.IO.File.ReadAllText(filePath);
            T? result = System.Text.Json.JsonSerializer.Deserialize<T>(jsonString);
            error = string.Empty;
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] {nameof(DeserializeFromFile)}: {ex.Message}");
            error = ex.Message;
            return default;
        }
    }

    public static bool SerializeToFile<T>(T obj, string filePath, ref string error)
    {
        if (obj == null || string.IsNullOrEmpty(filePath))
            return false;

        try
        {
            string jsonString = System.Text.Json.JsonSerializer.Serialize(obj);
            System.IO.File.WriteAllText(filePath, jsonString);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] {nameof(SerializeToFile)}: {ex.Message}");
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// This method will find all occurrences of a string pattern that starts with a double 
    /// quote, followed by any number of characters (non-greedy), and ends with a double 
    /// quote followed by zero or more spaces and a colon. This pattern matches the typical 
    /// format of keys in a JSON string.
    /// </summary>
    /// <param name="jsonString">JSON formatted text</param>
    /// <returns><see cref="List{T}"/> of each key</returns>
    public static List<string> ExtractKeys(string jsonString)
    {
        var keys = new List<string>();
        var matches = Regex.Matches(jsonString, "[,\\{]\"(.*?)\"\\s*:");
        foreach (Match match in matches) { keys.Add(match.Groups[1].Value); }
        return keys;
    }

    /// <summary>
    /// This method will find all occurrences of a string pattern that starts with a colon, 
    /// followed by zero or more spaces, followed by any number of characters (non-greedy), 
    /// and ends with a comma, closing brace, or closing bracket. This pattern matches the 
    /// typical format of values in a JSON string.
    /// </summary>
    /// <param name="jsonString">JSON formatted text</param>
    /// <returns><see cref="List{T}"/> of each value</returns>
    public static List<string> ExtractValues(string jsonString)
    {
        var values = new List<string>();
        var matches = Regex.Matches(jsonString, ":\\s*(.*?)(,|}|\\])");
        foreach (Match match in matches) { values.Add(match.Groups[1].Value.Trim()); }
        return values;
    }

    /// <summary>
    /// Convert a <see cref="DateTime"/> object into an ISO 8601 formatted string.
    /// </summary>
    /// <param name="dateTime"><see cref="DateTime"/></param>
    /// <returns>ISO 8601 formatted string</returns>
    public static string ToJsonFriendlyFormat(this DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue CastTo<TValue>(TValue value) where TValue : unmanaged
    {
        return (TValue)(object)value;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue? CastToNullable<TValue>(TValue? value) where TValue : unmanaged
    {
        if (value is null)
            return null;

        TValue validValue = value.GetValueOrDefault();
        return (TValue)(object)validValue;
    }

    /// <summary>
    /// Brute force alpha removal of <see cref="Version"/> text
    /// is not always the best approach, e.g. the following:
    /// "3.0.0-zmain.2211 (DCPP(199ff10ec000000)(cloudtest).160101.0800)"
    /// ...converts to: 
    /// "3.0.0.221119910000000.160101.0800" 
    /// ...which is not accurate.
    /// </summary>
    /// <param name="fullPath">the entire path to the file</param>
    /// <returns>sanitized <see cref="Version"/></returns>
    public static Version GetFileVersion(this string fullPath)
    {
        try
        {
            var ver = System.Diagnostics.FileVersionInfo.GetVersionInfo(fullPath).FileVersion;
            if (string.IsNullOrEmpty(ver)) { return new Version(); }
            if (ver.HasSpace())
            {   // Some assemblies contain versions such as "10.0.22622.1030 (WinBuild.160101.0800)"
                // This will cause the Version constructor to throw an exception, so just take the first piece.
                var chunk = ver.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var firstPiece = Regex.Replace(chunk[0].Replace(',', '.'), "[^.0-9]", "");
                return new Version(firstPiece);
            }
            string cleanVersion = Regex.Replace(ver, "[^.0-9]", "");
            return new Version(cleanVersion);
        }
        catch (Exception)
        {
            return new Version(); // 0.0
        }
    }

    public static bool HasAlpha(this string str)
    {
        if (string.IsNullOrEmpty(str)) { return false; }
        return str.Any(x => char.IsLetter(x));
    }
    public static bool HasAlphaRegex(this string str)
    {
        return Regex.IsMatch(str ?? "", @"[+a-zA-Z]+");
    }

    public static bool HasNumeric(this string str)
    {
        if (string.IsNullOrEmpty(str)) { return false; }
        return str.Any(x => char.IsNumber(x));
    }
    public static bool HasNumericRegex(this string str)
    {
        return Regex.IsMatch(str ?? "", @"[0-9]+"); // [^\D+]
    }

    public static bool HasSpace(this string str)
    {
        if (string.IsNullOrEmpty(str)) { return false; }
        return str.Any(x => char.IsSeparator(x));
    }
    public static bool HasSpaceRegex(this string str)
    {
        return Regex.IsMatch(str ?? "", @"[\s]+"); // [\s]
    }

    public static bool HasPunctuation(this string str)
    {
        if (string.IsNullOrEmpty(str)) { return false; }
        return str.Any(x => char.IsPunctuation(x));
    }

    public static bool HasAlphaNumeric(this string str)
    {
        if (string.IsNullOrEmpty(str)) { return false; }
        return str.Any(x => char.IsNumber(x)) && str.Any(x => char.IsLetter(x));
    }
    public static bool HasAlphaNumericRegex(this string str)
    {
        return Regex.IsMatch(str ?? "", "[a-zA-Z0-9]+");
    }

    public static string RemoveAlphas(this string str)
    {
        return string.Concat(str?.Where(c => char.IsNumber(c) || c == '.') ?? string.Empty);
    }

    public static string RemoveNumerics(this string str)
    {
        return string.Concat(str?.Where(c => char.IsLetter(c)) ?? string.Empty);
    }

    public static string RemoveDiacritics(this string strThis)
    {
        if (string.IsNullOrEmpty(strThis))
            return string.Empty;

        var sb = new StringBuilder();

        foreach (char c in strThis.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString();
    }

    #region [WinUI Specific]
    /// <summary>
    /// Can be useful if you only have a root (not merged) resource dictionary.
    /// var rdBrush = Extensions.GetResource{SolidColorBrush}("PrimaryBrush");
    /// </summary>
    public static T? GetResource<T>(string resourceName) where T : class
    {
        try
        {
            if (Application.Current.Resources.TryGetValue($"{resourceName}", out object value))
                return (T)value;
            else
                return default(T);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetResource: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Can be useful if you have merged theme resource dictionaries.
    /// var darkBrush = Extensions.GetThemeResource{SolidColorBrush}("PrimaryBrush", ElementTheme.Dark);
    /// var lightBrush = Extensions.GetThemeResource{SolidColorBrush}("PrimaryBrush", ElementTheme.Light);
    /// </summary>
    public static T? GetThemeResource<T>(string resourceName, ElementTheme? theme) where T : class
    {
        try
        {
            theme ??= ElementTheme.Default;

            var dictionaries = Application.Current.Resources.MergedDictionaries;
            foreach (var item in dictionaries)
            {
                // A typical IList<ResourceDictionary> will contain:
                //   - 'Default'
                //   - 'Light'
                //   - 'Dark'
                //   - 'HighContrast'
                foreach (var kv in item.ThemeDictionaries.Keys)
                {
                    // Examine the ICollection<T> for the key names.
                    Debug.WriteLine($"ThemeDictionary is named '{kv}'");
                }

                // Do we have any themes in this resource dictionary?
                if (item.ThemeDictionaries.Count > 0)
                {
                    if (theme == ElementTheme.Dark)
                    {
                        if (item.ThemeDictionaries.TryGetValue("Dark", out var drd))
                        {
                            ResourceDictionary? dark = drd as ResourceDictionary;
                            if (dark != null)
                            {
                                Debug.WriteLine($"Found dark theme resource dictionary");
                                if (dark.TryGetValue($"{resourceName}", out var tmp))
                                    return (T)tmp;
                                else
                                    Debug.WriteLine($"Could not find '{resourceName}'");
                            }
                        }
                        else { Debug.WriteLine($"{nameof(ElementTheme.Dark)} theme was not found"); }
                    }
                    else if (theme == ElementTheme.Light)
                    {
                        if (item.ThemeDictionaries.TryGetValue("Light", out var lrd))
                        {
                            ResourceDictionary? light = lrd as ResourceDictionary;
                            if (light != null)
                            {
                                Debug.WriteLine($"Found light theme resource dictionary");
                                if (light.TryGetValue($"{resourceName}", out var tmp))
                                    return (T)tmp;
                                else
                                    Debug.WriteLine($"Could not find '{resourceName}'");
                            }
                        }
                        else { Debug.WriteLine($"{nameof(ElementTheme.Light)} theme was not found"); }
                    }
                    else if (theme == ElementTheme.Default)
                    {
                        if (item.ThemeDictionaries.TryGetValue("Default", out var drd))
                        {
                            ResourceDictionary? dflt = drd as ResourceDictionary;
                            if (dflt != null)
                            {
                                Debug.WriteLine($"Found default theme resource dictionary");
                                if (dflt.TryGetValue($"{resourceName}", out var tmp))
                                    return (T)tmp;
                                else
                                    Debug.WriteLine($"Could not find '{resourceName}'");
                            }
                        }
                        else { Debug.WriteLine($"{nameof(ElementTheme.Default)} theme was not found"); }
                    }
                    else
                        Debug.WriteLine($"No theme to match");
                }
                else
                    Debug.WriteLine($"No theme dictionaries found");
            }

            return default(T);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetThemeResource: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Returns the selected item's content from a <see cref="ComboBox"/>.
    /// </summary>
    public static string GetSelectedText(this ComboBox comboBox)
    {
        var item = comboBox.SelectedItem as ComboBoxItem;
        if (item != null)
        {
            return (string)item.Content;
        }

        return "";
    }

    /// <summary>
    /// Enables or disables the Header.
    /// </summary>
    public static void IsLocked(this Expander expander, bool locked)
    {
        var ctrl = (expander.Header as FrameworkElement)?.Parent as Control;
        if (ctrl != null)
            ctrl.IsEnabled = locked;
    }

    /// <summary>
    /// Sets the desired Height for content when expanded.
    /// </summary>
    public static void SetContentHeight(this Expander expander, double contentHeight)
    {
        var ctrl = expander.Content as FrameworkElement;
        if (ctrl != null)
            ctrl.Height = contentHeight;
    }

    public static void SetOrientation(this VirtualizingLayout layout, Orientation orientation)
    {
        // Note:
        // The public properties of UniformGridLayout and FlowLayout interpret
        // orientation the opposite to how FlowLayoutAlgorithm interprets it. 
        // For simplicity, our validation code is written in terms that match
        // the implementation. For this reason, we need to switch the orientation
        // whenever we set UniformGridLayout.Orientation or StackLayout.Orientation.
        if (layout is StackLayout)
        {
            ((StackLayout)layout).Orientation = orientation;
        }
        else if (layout is UniformGridLayout)
        {
            ((UniformGridLayout)layout).Orientation = orientation;
        }
        else
        {
            throw new InvalidOperationException("layout unknown");
        }
    }

    public static void BindCenterPoint(this Microsoft.UI.Composition.Visual target)
    {
        var exp = target.Compositor.CreateExpressionAnimation("Vector3(this.Target.Size.X / 2, this.Target.Size.Y / 2, 0f)");
        target.StartAnimation("CenterPoint", exp);
    }

    public static void BindSize(this Microsoft.UI.Composition.Visual target, Microsoft.UI.Composition.Visual source)
    {
        var exp = target.Compositor.CreateExpressionAnimation("host.Size");
        exp.SetReferenceParameter("host", source);
        target.StartAnimation("Size", exp);
    }

    public static Microsoft.UI.Composition.ImplicitAnimationCollection CreateImplicitAnimation(this Microsoft.UI.Composition.ImplicitAnimationCollection source, string Target, TimeSpan? Duration = null)
    {
        Microsoft.UI.Composition.KeyFrameAnimation animation = null;
        switch (Target.ToLower())
        {
            case "offset":
            case "scale":
            case "centerPoint":
            case "rotationAxis":
                animation = source.Compositor.CreateVector3KeyFrameAnimation();
                break;

            case "size":
                animation = source.Compositor.CreateVector2KeyFrameAnimation();
                break;

            case "opacity":
            case "blueRadius":
            case "rotationAngle":
            case "rotationAngleInDegrees":
                animation = source.Compositor.CreateScalarKeyFrameAnimation();
                break;

            case "color":
                animation = source.Compositor.CreateColorKeyFrameAnimation();
                break;
        }

        if (animation == null) throw new ArgumentNullException("Unknown Target");
        if (!Duration.HasValue) Duration = TimeSpan.FromSeconds(0.2d);
        animation.InsertExpressionKeyFrame(1f, "this.FinalValue");
        animation.Duration = Duration.Value;
        animation.Target = Target;

        source[Target] = animation;
        return source;
    }

    /// <summary>
    /// Finds the contrast ratio.
    /// This is helpful for determining if one control's foreground and another control's background will be hard to distinguish.
    /// https://www.w3.org/WAI/GL/wiki/Contrast_ratio
    /// (L1 + 0.05) / (L2 + 0.05), where
    /// L1 is the relative luminance of the lighter of the colors, and
    /// L2 is the relative luminance of the darker of the colors.
    /// </summary>
    /// <param name="first"><see cref="Windows.UI.Color"/></param>
    /// <param name="second"><see cref="Windows.UI.Color"/></param>
    /// <returns>ratio between relative luminance</returns>
    public static double CalculateContrastRatio(Windows.UI.Color first, Windows.UI.Color second)
    {
        double relLuminanceOne = GetRelativeLuminance(first);
        double relLuminanceTwo = GetRelativeLuminance(second);
        return (Math.Max(relLuminanceOne, relLuminanceTwo) + 0.05) / (Math.Min(relLuminanceOne, relLuminanceTwo) + 0.05);
    }

    /// <summary>
    /// Gets the relative luminance.
    /// https://www.w3.org/WAI/GL/wiki/Relative_luminance
    /// For the sRGB colorspace, the relative luminance of a color is defined as L = 0.2126 * R + 0.7152 * G + 0.0722 * B
    /// </summary>
    /// <param name="c"><see cref="Windows.UI.Color"/></param>
    /// <remarks>This is mainly used by <see cref="Helpers.CalculateContrastRatio(Color, Color)"/></remarks>
    public static double GetRelativeLuminance(Windows.UI.Color c)
    {
        double rSRGB = c.R / 255.0;
        double gSRGB = c.G / 255.0;
        double bSRGB = c.B / 255.0;

        // WebContentAccessibilityGuideline 2.x definition was 0.03928 (incorrect)
        // WebContentAccessibilityGuideline 3.x definition is 0.04045 (correct)
        double r = rSRGB <= 0.04045 ? rSRGB / 12.92 : Math.Pow(((rSRGB + 0.055) / 1.055), 2.4);
        double g = gSRGB <= 0.04045 ? gSRGB / 12.92 : Math.Pow(((gSRGB + 0.055) / 1.055), 2.4);
        double b = bSRGB <= 0.04045 ? bSRGB / 12.92 : Math.Pow(((bSRGB + 0.055) / 1.055), 2.4);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    /// <summary>
    /// Calculates the linear interpolated Color based on the given Color values.
    /// </summary>
    /// <param name="colorFrom">Source Color.</param>
    /// <param name="colorTo">Target Color.</param>
    /// <param name="amount">Weight given to the target color.</param>
    /// <returns>Linear Interpolated Color.</returns>
    public static Windows.UI.Color Lerp(this Windows.UI.Color colorFrom, Windows.UI.Color colorTo, float amount)
    {
        // Convert colorFrom components to lerp-able floats
        float sa = colorFrom.A, sr = colorFrom.R, sg = colorFrom.G, sb = colorFrom.B;

        // Convert colorTo components to lerp-able floats
        float ea = colorTo.A, er = colorTo.R, eg = colorTo.G, eb = colorTo.B;

        // lerp the colors to get the difference
        byte a = (byte)Math.Max(0, Math.Min(255, sa.Lerp(ea, amount))),
             r = (byte)Math.Max(0, Math.Min(255, sr.Lerp(er, amount))),
             g = (byte)Math.Max(0, Math.Min(255, sg.Lerp(eg, amount))),
             b = (byte)Math.Max(0, Math.Min(255, sb.Lerp(eb, amount)));

        // return the new color
        return Windows.UI.Color.FromArgb(a, r, g, b);
    }

    /// <summary>
    /// Darkens the color by the given percentage using lerp.
    /// </summary>
    /// <param name="color">Source color.</param>
    /// <param name="amount">Percentage to darken. Value should be between 0 and 1.</param>
    /// <returns>Color</returns>
    public static Windows.UI.Color DarkerBy(this Windows.UI.Color color, float amount)
    {
        return color.Lerp(Colors.Black, amount);
    }

    /// <summary>
    /// Lightens the color by the given percentage using lerp.
    /// </summary>
    /// <param name="color">Source color.</param>
    /// <param name="amount">Percentage to lighten. Value should be between 0 and 1.</param>
    /// <returns>Color</returns>
    public static Windows.UI.Color LighterBy(this Windows.UI.Color color, float amount)
    {
        return color.Lerp(Colors.White, amount);
    }

    /// <summary>
    /// Creates a <see cref="LinearGradientBrush"/> from 3 input colors.
    /// </summary>
    /// <param name="c1">offset 0.0 color</param>
    /// <param name="c2">offset 0.5 color</param>
    /// <param name="c3">offset 1.0 color</param>
    /// <returns><see cref="LinearGradientBrush"/></returns>
    public static LinearGradientBrush CreateLinearGradientBrush(Windows.UI.Color c1, Windows.UI.Color c2, Windows.UI.Color c3)
    {
        var gs1 = new GradientStop(); gs1.Color = c1; gs1.Offset = 0.0;
        var gs2 = new GradientStop(); gs2.Color = c2; gs2.Offset = 0.5;
        var gs3 = new GradientStop(); gs3.Color = c3; gs3.Offset = 1.0;
        var gsc = new GradientStopCollection();
        gsc.Add(gs1); gsc.Add(gs2); gsc.Add(gs3);
        var lgb = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(0, 1),
            GradientStops = gsc
        };
        return lgb;
    }

    /// <summary>
    /// Creates a Color object from the hex color code and returns the result.
    /// </summary>
    /// <param name="hexColorCode">text representation of the color</param>
    /// <returns><see cref="Windows.UI.Color"/></returns>
    public static Windows.UI.Color? GetColorFromHexString(string hexColorCode)
    {
        if (string.IsNullOrEmpty(hexColorCode))
            return null;

        try
        {
            byte a = 255; byte r = 0; byte g = 0; byte b = 0;

            if (hexColorCode.Length == 9)
            {
                hexColorCode = hexColorCode.Substring(1, 8);
            }
            if (hexColorCode.Length == 8)
            {
                a = Convert.ToByte(hexColorCode.Substring(0, 2), 16);
                hexColorCode = hexColorCode.Substring(2, 6);
            }
            if (hexColorCode.Length == 6)
            {
                r = Convert.ToByte(hexColorCode.Substring(0, 2), 16);
                g = Convert.ToByte(hexColorCode.Substring(2, 2), 16);
                b = Convert.ToByte(hexColorCode.Substring(4, 2), 16);
            }

            return Windows.UI.Color.FromArgb(a, r, g, b);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Uses the <see cref="System.Reflection.PropertyInfo"/> of the 
    /// <see cref="Microsoft.UI.Colors"/> class to return the matching 
    /// <see cref="Windows.UI.Color"/> object.
    /// </summary>
    /// <param name="colorName">name of color, e.g. "Aquamarine"</param>
    /// <returns><see cref="Windows.UI.Color"/></returns>
    public static Windows.UI.Color? GetColorFromNameString(string colorName)
    {
        if (string.IsNullOrEmpty(colorName))
            return Windows.UI.Color.FromArgb(255, 128, 128, 128);

        try
        {
            var prop = typeof(Microsoft.UI.Colors).GetTypeInfo().GetDeclaredProperty(colorName);
            if (prop != null)
            {
                var tmp = prop.GetValue(null);
                if (tmp != null)
                    return (Windows.UI.Color)tmp;
            }
            else
            {
                Debug.WriteLine($"[WARNING] \"{colorName}\" could not be resolved as a {nameof(Windows.UI.Color)}.");
            }

            return Windows.UI.Color.FromArgb(255, 128, 128, 128);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] GetColorFromNameString: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Generates a completely random <see cref="Windows.UI.Color"/>.
    /// </summary>
    /// <returns><see cref="Windows.UI.Color"/></returns>
    public static Windows.UI.Color GetRandomWinUIColor()
    {
        byte[] buffer = new byte[3];
        Random.Shared.NextBytes(buffer);
        return Windows.UI.Color.FromArgb(255, buffer[0], buffer[1], buffer[2]);
    }

    public static Windows.UI.Color[] CreateColorScale(int start, int end)
    {
        var colors = new Windows.UI.Color[end - start + 1];
        for (int i = 0; i < colors.Length; i++)
        {
            float factor = ((float)i / (end - start)) * 255; // map the position to 0-255
            // Using red and green channels only.
            colors[i] = Windows.UI.Color.FromArgb(255, (byte)(200 * factor), (byte)(255 - 10 * factor), 0); // create a color gradient from light to dark
        }
        return colors;
    }

    /// <summary>
    /// Returns a random selection from <see cref="Microsoft.UI.Colors"/>.
    /// </summary>
    /// <returns><see cref="Windows.UI.Color"/></returns>
    public static Windows.UI.Color GetRandomMicrosoftUIColor()
    {
        try
        {
            var colorType = typeof(Microsoft.UI.Colors);
            var colors = colorType.GetProperties()
                .Where(p => p.PropertyType == typeof(Windows.UI.Color) && p.GetMethod.IsStatic && p.GetMethod.IsPublic)
                .Select(p => (Windows.UI.Color)p.GetValue(null))
                .ToList();

            if (colors.Count > 0)
            {
                var randomIndex = Random.Shared.Next(colors.Count);
                var randomColor = colors[randomIndex];
                return randomColor;
            }
            else
            {
                return Microsoft.UI.Colors.Gray;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] GetRandomColor: {ex.Message}");
            return Microsoft.UI.Colors.Red;
        }
    }


    /// <summary>
    /// Creates a Color from the hex color code and returns the result 
    /// as a <see cref="Microsoft.UI.Xaml.Media.SolidColorBrush"/>.
    /// </summary>
    /// <param name="hexColorCode">text representation of the color</param>
    /// <returns><see cref="Microsoft.UI.Xaml.Media.SolidColorBrush"/></returns>
    public static Microsoft.UI.Xaml.Media.SolidColorBrush? GetBrushFromHexString(string hexColorCode)
    {
        if (string.IsNullOrEmpty(hexColorCode))
            return null;

        try
        {
            byte a = 255; byte r = 0; byte g = 0; byte b = 0;

            if (hexColorCode.Length == 9)
                hexColorCode = hexColorCode.Substring(1, 8);

            if (hexColorCode.Length == 8)
            {
                a = Convert.ToByte(hexColorCode.Substring(0, 2), 16);
                hexColorCode = hexColorCode.Substring(2, 6);
            }

            if (hexColorCode.Length == 6)
            {
                r = Convert.ToByte(hexColorCode.Substring(0, 2), 16);
                g = Convert.ToByte(hexColorCode.Substring(2, 2), 16);
                b = Convert.ToByte(hexColorCode.Substring(4, 2), 16);
            }

            return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(a, r, g, b));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] GetBrushFromHexString: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Verifies if the given brush is a SolidColorBrush and its color does not include transparency.
    /// </summary>
    /// <param name="brush">Brush</param>
    /// <returns>true if yes, otherwise false</returns>
    public static bool IsOpaqueSolidColorBrush(this Microsoft.UI.Xaml.Media.Brush brush)
    {
        return (brush as Microsoft.UI.Xaml.Media.SolidColorBrush)?.Color.A == 0xff;
    }

    /// <summary>
    /// Generates a 7 digit color string including the # sign.
    /// If the <see cref="ElementTheme"/> is dark then 0, 1 & 2 options are 
    /// removed so dark colors such as 000000/111111/222222 are not possible.
    /// If the <see cref="ElementTheme"/> is light then D, E & F options are 
    /// removed so light colors such as DDDDDD/EEEEEE/FFFFFF are not possible.
    /// </summary>
    public static string GetRandomColorString(ElementTheme? theme)
    {
        StringBuilder sb = new StringBuilder();
        string chTable = "012346789ABCDEF";

        if (theme.HasValue && theme == ElementTheme.Dark)
            chTable = "346789ABCDEF";
        else if (theme.HasValue && theme == ElementTheme.Light)
            chTable = "012346789ABC";

        //char[] charArray = chTable.Distinct().ToArray();

        for (int x = 0; x < 6; x++)
            sb.Append(chTable[Random.Shared.Next() % chTable.Length]);

        return $"#{sb}";
    }

    /// <summary>
    /// Returns the given <see cref="Windows.UI.Color"/> as a hex string.
    /// </summary>
    /// <param name="color">color to convert</param>
    /// <returns>hex string (including pound sign)</returns>
    public static string ToHexString(this Windows.UI.Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    /// <summary>
    /// Returns a new <see cref="Windows.Foundation.Rect(double, double, double, double)"/> representing the size of the <see cref="Vector2"/>.
    /// </summary>
    /// <param name="vector"><see cref="System.Numerics.Vector2"/> vector representing object size for Rectangle.</param>
    /// <returns><see cref="Windows.Foundation.Rect(double, double, double, double)"/> value.</returns>
    public static Windows.Foundation.Rect ToRect(this System.Numerics.Vector2 vector)
    {
        return new Windows.Foundation.Rect(0, 0, vector.X, vector.Y);
    }

    /// <summary>
    /// Returns a new <see cref="System.Numerics.Vector2"/> representing the <see cref="Windows.Foundation.Size(double, double)"/>.
    /// </summary>
    /// <param name="size"><see cref="Windows.Foundation.Size(double, double)"/> value.</param>
    /// <returns><see cref="System.Numerics.Vector2"/> value.</returns>
    public static System.Numerics.Vector2 ToVector2(this Windows.Foundation.Size size)
    {
        return new System.Numerics.Vector2((float)size.Width, (float)size.Height);
    }

    /// <summary>
    /// Deflates rectangle by given thickness.
    /// </summary>
    /// <param name="rect">Rectangle</param>
    /// <param name="thick">Thickness</param>
    /// <returns>Deflated Rectangle</returns>
    public static Windows.Foundation.Rect Deflate(this Windows.Foundation.Rect rect, Microsoft.UI.Xaml.Thickness thick)
    {
        return new Windows.Foundation.Rect(
            rect.Left + thick.Left,
            rect.Top + thick.Top,
            Math.Max(0.0, rect.Width - thick.Left - thick.Right),
            Math.Max(0.0, rect.Height - thick.Top - thick.Bottom));
    }

    /// <summary>
    /// Inflates rectangle by given thickness.
    /// </summary>
    /// <param name="rect">Rectangle</param>
    /// <param name="thick">Thickness</param>
    /// <returns>Inflated Rectangle</returns>
    public static Windows.Foundation.Rect Inflate(this Windows.Foundation.Rect rect, Microsoft.UI.Xaml.Thickness thick)
    {
        return new Windows.Foundation.Rect(
            rect.Left - thick.Left,
            rect.Top - thick.Top,
            Math.Max(0.0, rect.Width + thick.Left + thick.Right),
            Math.Max(0.0, rect.Height + thick.Top + thick.Bottom));
    }

    /// <summary>
    /// Starts an <see cref="Microsoft.UI.Composition.ExpressionAnimation"/> to keep the size of the source <see cref="Microsoft.UI.Composition.CompositionObject"/> in sync with the target <see cref="UIElement"/>
    /// </summary>
    /// <param name="source">The <see cref="Microsoft.UI.Composition.CompositionObject"/> to start the animation on</param>
    /// <param name="target">The target <see cref="UIElement"/> to read the size updates from</param>
    public static void BindSize(this Microsoft.UI.Composition.CompositionObject source, UIElement target)
    {
        var visual = ElementCompositionPreview.GetElementVisual(target);
        var bindSizeAnimation = source.Compositor.CreateExpressionAnimation($"{nameof(visual)}.Size");
        bindSizeAnimation.SetReferenceParameter(nameof(visual), visual);
        // Start the animation
        source.StartAnimation("Size", bindSizeAnimation);
    }

    /// <summary>
    /// Starts an animation on the given property of a <see cref="Microsoft.UI.Composition.CompositionObject"/>
    /// </summary>
    /// <typeparam name="T">The type of the property to animate</typeparam>
    /// <param name="target">The target <see cref="Microsoft.UI.Composition.CompositionObject"/></param>
    /// <param name="property">The name of the property to animate</param>
    /// <param name="value">The final value of the property</param>
    /// <param name="duration">The animation duration</param>
    /// <returns>A <see cref="Task"/> that completes when the created animation completes</returns>
    public static Task StartAnimationAsync<T>(this Microsoft.UI.Composition.CompositionObject target, string property, T value, TimeSpan duration) where T : unmanaged
    {
        // Stop previous animations
        target.StopAnimation(property);

        // Setup the animation to run
        Microsoft.UI.Composition.KeyFrameAnimation animation;

        // Switch on the value to determine the necessary KeyFrameAnimation type
        switch (value)
        {
            case float f:
                var scalarAnimation = target.Compositor.CreateScalarKeyFrameAnimation();
                scalarAnimation.InsertKeyFrame(1f, f);
                animation = scalarAnimation;
                break;
            case Windows.UI.Color c:
                var colorAnimation = target.Compositor.CreateColorKeyFrameAnimation();
                colorAnimation.InsertKeyFrame(1f, c);
                animation = colorAnimation;
                break;
            case System.Numerics.Vector4 v4:
                var vector4Animation = target.Compositor.CreateVector4KeyFrameAnimation();
                vector4Animation.InsertKeyFrame(1f, v4);
                animation = vector4Animation;
                break;
            default: throw new ArgumentException($"Invalid animation type: {typeof(T)}", nameof(value));
        }

        animation.Duration = duration;

        // Get the batch and start the animations
        var batch = target.Compositor.CreateScopedBatch(Microsoft.UI.Composition.CompositionBatchTypes.Animation);

        // Create a TCS for the result
        var tcs = new TaskCompletionSource<object>();

        batch.Completed += (s, e) => tcs.SetResult(null);

        target.StartAnimation(property, animation);

        batch.End();

        return tcs.Task;
    }

    /// <summary>
    /// Creates a <see cref="Microsoft.UI.Composition.CompositionGeometricClip"/> from the specified <see cref="Windows.Graphics.IGeometrySource2D"/>.
    /// </summary>
    /// <param name="compositor"><see cref="Microsoft.UI.Composition.Compositor"/></param>
    /// <param name="geometry"><see cref="Windows.Graphics.IGeometrySource2D"/></param>
    /// <returns>CompositionGeometricClip</returns>
    public static Microsoft.UI.Composition.CompositionGeometricClip CreateGeometricClip(this Microsoft.UI.Composition.Compositor compositor, Windows.Graphics.IGeometrySource2D geometry)
    {
        // Create the CompositionPath
        var path = new Microsoft.UI.Composition.CompositionPath(geometry);
        // Create the CompositionPathGeometry
        var pathGeometry = compositor.CreatePathGeometry(path);
        // Create the CompositionGeometricClip
        return compositor.CreateGeometricClip(pathGeometry);
    }


    public static async Task LaunchUrlFromTextBox(Microsoft.UI.Xaml.Controls.TextBox textBox)
    {
        string text = "";
        textBox.DispatcherQueue.TryEnqueue(() => { text = textBox.Text; });
        Uri? uriResult;
        bool isValidUrl = Uri.TryCreate(text, UriKind.Absolute, out uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        if (isValidUrl)
            await Windows.System.Launcher.LaunchUriAsync(uriResult);
        else
            await Task.CompletedTask;
    }

    public static async Task LocateAndLaunchUrlFromTextBox(Microsoft.UI.Xaml.Controls.TextBox textBox)
    {
        string text = "";
        textBox.DispatcherQueue.TryEnqueue(() => { text = textBox.Text; });
        List<string> urls = text.ExtractUrls();
        if (urls.Count > 0)
        {
            Uri uriResult = new Uri(urls[0]);
            await Windows.System.Launcher.LaunchUriAsync(uriResult);
        }
        else
            await Task.CompletedTask;
    }

    public static async Task LocateAndLaunchUrlFromString(string text)
    {
        List<string> urls = text.ExtractUrls();
        if (urls.Count > 0)
        {
            Uri uriResult = new Uri(urls[0]);
            await Windows.System.Launcher.LaunchUriAsync(uriResult);
        }
        else
            await Task.CompletedTask;
    }

    /// <summary>
    /// Sets the element's Grid.Column property and sets the
    /// ColumnSpan attached property to the specified value.
    /// </summary>
    public static void SetColumnAndSpan(this FrameworkElement element, int column = 0, int columnSpan = 99)
    {
        Grid.SetColumn(element, column);
        Grid.SetColumnSpan(element, columnSpan);
    }

    /// <summary>
    /// Returns the <see cref="Microsoft.UI.Xaml.PropertyPath"/> based on the provided <see cref="Microsoft.UI.Xaml.Data.Binding"/>.
    /// </summary>
    public static string? GetBindingPropertyName(this Microsoft.UI.Xaml.Data.Binding binding)
    {
        return binding?.Path?.Path?.Split('.')?.LastOrDefault();
    }

    public static Windows.Foundation.Size GetTextSize(FontFamily font, double fontSize, string text)
    {
        var tb = new TextBlock { Text = text, FontFamily = font, FontSize = fontSize };
        tb.Measure(new Windows.Foundation.Size(Double.PositiveInfinity, Double.PositiveInfinity));
        return tb.DesiredSize;
    }

    public static bool IsMonospacedFont(FontFamily font)
    {
        var tb1 = new TextBlock { Text = "(!aiZ%#BIm,. ~`", FontFamily = font };
        tb1.Measure(new Windows.Foundation.Size(Double.PositiveInfinity, Double.PositiveInfinity));
        var tb2 = new TextBlock { Text = "...............", FontFamily = font };
        tb2.Measure(new Windows.Foundation.Size(Double.PositiveInfinity, Double.PositiveInfinity));
        var off = Math.Abs(tb1.DesiredSize.Width - tb2.DesiredSize.Width);
        return off < 0.01;
    }

    /// <summary>
    /// Gets a list of the specified FrameworkElement's DependencyProperties. This method will return all
    /// DependencyProperties of the element unless 'useBlockList' is true, in which case all bindings on elements
    /// that are typically not used as input controls will be ignored.
    /// </summary>
    /// <param name="element">FrameworkElement of interest</param>
    /// <param name="useBlockList">If true, ignores elements not typically used for input</param>
    /// <returns>List of DependencyProperties</returns>
    public static List<DependencyProperty> GetDependencyProperties(this FrameworkElement element, bool useBlockList)
    {
        List<DependencyProperty> dependencyProperties = new List<DependencyProperty>();

        bool isBlocklisted = useBlockList &&
            (element is Panel || element is Button || element is Image || element is ScrollViewer ||
             element is TextBlock || element is Border || element is Microsoft.UI.Xaml.Shapes.Shape || element is ContentPresenter);

        if (!isBlocklisted)
        {
            Type type = element.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            foreach (FieldInfo field in fields)
            {
                if (field.FieldType == typeof(DependencyProperty))
                {
                    var dp = (DependencyProperty)field.GetValue(null);
                    if (dp != null)
                        dependencyProperties.Add(dp);
                }
            }
        }

        return dependencyProperties;
    }

    public static bool IsXamlRootAvailable(bool UWP = false)
    {
        if (UWP)
            return Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "XamlRoot");
        else
            return Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent("Microsoft.UI.Xaml.UIElement", "XamlRoot");
    }

    /// <summary>
    /// Helper function to calculate an element's rectangle in root-relative coordinates.
    /// </summary>
    public static Windows.Foundation.Rect GetElementRect(this Microsoft.UI.Xaml.FrameworkElement element)
    {
        try
        {
            Microsoft.UI.Xaml.Media.GeneralTransform transform = element.TransformToVisual(null);
            Windows.Foundation.Point point = transform.TransformPoint(new Windows.Foundation.Point());
            return new Windows.Foundation.Rect(point, new Windows.Foundation.Size(element.ActualWidth, element.ActualHeight));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] GetElementRect: {ex.Message}");
            return new Windows.Foundation.Rect(0, 0, 0, 0);
        }
    }

    public static IconElement? GetIcon(string imagePath, string imageExt = ".png")
    {
        IconElement? result = null;

        try
        {
            result = imagePath.ToLowerInvariant().EndsWith(imageExt) ?
                        (IconElement)new BitmapIcon() { UriSource = new Uri(imagePath, UriKind.RelativeOrAbsolute), ShowAsMonochrome = false } :
                        (IconElement)new FontIcon() { Glyph = imagePath };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] GetIcon: {ex.Message}");
        }

        return result;
    }

    public static FontIcon GenerateFontIcon(Windows.UI.Color brush, string glyph = "\uF127", int width = 10, int height = 10)
    {
        return new FontIcon()
        {
            Glyph = glyph,
            FontSize = 1.5,
            Width = (double)width,
            Height = (double)height,
            Foreground = new SolidColorBrush(brush),
        };
    }

    public static async Task<byte[]> AsPng(this UIElement control)
    {
        // Get XAML Visual in BGRA8 format
        var rtb = new RenderTargetBitmap();
        await rtb.RenderAsync(control, (int)control.ActualSize.X, (int)control.ActualSize.Y);

        // Encode as PNG
        var pixelBuffer = (await rtb.GetPixelsAsync()).ToArray();
        IRandomAccessStream mraStream = new InMemoryRandomAccessStream();
        var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, mraStream);
        encoder.SetPixelData(
            Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
            Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
            (uint)rtb.PixelWidth,
            (uint)rtb.PixelHeight,
            184,
            184,
            pixelBuffer);
        await encoder.FlushAsync();

        // Transform to byte array
        var bytes = new byte[mraStream.Size];
        await mraStream.ReadAsync(bytes.AsBuffer(), (uint)mraStream.Size, InputStreamOptions.None);

        return bytes;
    }

    /// <summary>
    /// This redundant call can also be found in App.xaml.cs
    /// </summary>
    /// <param name="window"><see cref="Microsoft.UI.Xaml.Window"/></param>
    /// <returns><see cref="Microsoft.UI.Windowing.AppWindow"/></returns>
    public static Microsoft.UI.Windowing.AppWindow GetAppWindow(this Microsoft.UI.Xaml.Window window)
    {
        System.IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        Microsoft.UI.WindowId wndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(wndId);
    }

    /// <summary>
    /// This assumes your images reside in an "Assets" folder.
    /// </summary>
    /// <param name="assetName"></param>
    /// <returns><see cref="BitmapImage"/></returns>
    public static BitmapImage? GetImageFromAssets(this string assetName)
    {
        BitmapImage? img = null;

        try
        {
            Uri? uri = new Uri($"ms-appx:///Assets/" + assetName.Replace("./", ""));
            img = new BitmapImage(uri);
            Debug.WriteLine($"[INFO] Image resolved for '{assetName}'");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WARNING] GetImageFromAssets: {ex.Message}");
        }

        return img;
    }

    /// <summary>
    /// Gets a string value from a <see cref="StorageFile"/> located in the application local folder.
    /// </summary>
    /// <param name="fileName">
    /// The relative <see cref="string"/> file path.
    /// </param>
    /// <returns>
    /// The stored <see cref="string"/> value.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Exception thrown if the <paramref name="fileName"/> is null or empty.
    /// </exception>
    public static async Task<string> ReadLocalFileAsync(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            throw new ArgumentNullException(nameof(fileName));

        if (App.IsPackaged)
        {
            var folder = ApplicationData.Current.LocalFolder;
            var file = await folder.GetFileAsync(fileName);
            return await FileIO.ReadTextAsync(file, Windows.Storage.Streams.UnicodeEncoding.Utf8);
        }
        else
        {
            using (TextReader reader = File.OpenText(Path.Combine(AppContext.BaseDirectory, fileName)))
            {
                return await reader.ReadToEndAsync(); // uses UTF8 by default
            }
        }
    }

    /// <summary>
    /// Starts an animation and returns a <see cref="Task"/> that reports when it completes.
    /// </summary>
    /// <param name="storyboard">The target storyboard to start.</param>
    /// <returns>A <see cref="Task"/> that completes when <paramref name="storyboard"/> completes.</returns>
    public static Task BeginAsync(this Storyboard storyboard)
    {
        TaskCompletionSource<object?> taskCompletionSource = new TaskCompletionSource<object?>();

        void OnCompleted(object? sender, object e)
        {
            if (sender is Storyboard storyboard)
                storyboard.Completed -= OnCompleted;

            taskCompletionSource.SetResult(null);
        }

        storyboard.Completed += OnCompleted;
        storyboard.Begin();

        return taskCompletionSource.Task;
    }

    /// <summary>
    /// To get all buttons contained in a StackPanel:
    /// IEnumerable{Button} kids = GetChildren(rootStackPanel).Where(ctrl => ctrl is Button).Cast{Button}();
    /// </summary>
    /// <remarks>You must call this on a UI thread.</remarks>
    public static IEnumerable<UIElement> GetChildren(this UIElement parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            if (VisualTreeHelper.GetChild(parent, i) is UIElement child)
            {
                yield return child;
            }
        }
    }

    /// <summary>
    /// Walks the visual tree to determine if a particular child is contained within a parent DependencyObject.
    /// </summary>
    /// <param name="element">Parent DependencyObject</param>
    /// <param name="child">Child DependencyObject</param>
    /// <returns>True if the parent element contains the child</returns>
    public static bool ContainsChild(this DependencyObject element, DependencyObject child)
    {
        if (element != null)
        {
            while (child != null)
            {
                if (child == element)
                    return true;

                // Walk up the visual tree. If the root is hit, try using the framework element's
                // parent. This is done because Popups behave differently with respect to the visual tree,
                // and it could have a parent even if the VisualTreeHelper doesn't find it.
                DependencyObject parent = VisualTreeHelper.GetParent(child);
                if (parent == null)
                {
                    FrameworkElement? childElement = child as FrameworkElement;
                    if (childElement != null)
                    {
                        parent = childElement.Parent;
                    }
                }
                child = parent;
            }
        }
        return false;
    }

    /// <summary>
    /// Provides the distance in a <see cref="Point"/> from the passed in element to the element being called on.
    /// For instance, calling child.CoordinatesFrom(container) will return the position of the child within the container.
    /// Helper for <see cref="UIElement.TransformToVisual(UIElement)"/>.
    /// </summary>
    /// <param name="target">Element to measure distance.</param>
    /// <param name="parent">Starting parent element to provide coordinates from.</param>
    /// <returns><see cref="Windows.Foundation.Point"/> containing difference in position of elements.</returns>
    public static Windows.Foundation.Point CoordinatesFrom(this UIElement target, UIElement parent)
    {
        return target.TransformToVisual(parent).TransformPoint(default(Windows.Foundation.Point));
    }

    /// <summary>
    /// Provides the distance in a <see cref="Point"/> to the passed in element from the element being called on.
    /// For instance, calling container.CoordinatesTo(child) will return the position of the child within the container.
    /// Helper for <see cref="UIElement.TransformToVisual(UIElement)"/>.
    /// </summary>
    /// <param name="parent">Starting parent element to provide coordinates from.</param>
    /// <param name="target">Element to measure distance to.</param>
    /// <returns><see cref="Windows.Foundation.Point"/> containing difference in position of elements.</returns>
    public static Windows.Foundation.Point CoordinatesTo(this UIElement parent, UIElement target)
    {
        return target.TransformToVisual(parent).TransformPoint(default(Windows.Foundation.Point));
    }


    /// <summary>
    /// I created this to show what controls are members of <see cref="Microsoft.UI.Xaml.FrameworkElement"/>.
    /// </summary>
    public static void FindControlsInheritingFromFrameworkElement()
    {
        var controlAssembly = typeof(Microsoft.UI.Xaml.Controls.Control).GetTypeInfo().Assembly;
        var controlTypes = controlAssembly.GetTypes()
            .Where(type => type.Namespace == "Microsoft.UI.Xaml.Controls" &&
            typeof(Microsoft.UI.Xaml.FrameworkElement).IsAssignableFrom(type));

        foreach (var controlType in controlTypes)
        {
            Debug.WriteLine($"[FrameworkElement] {controlType.FullName}", $"ControlInheritingFrom");
        }
    }

    public static IEnumerable<Type?> GetHierarchyFromUIElement(this Type element)
    {
        if (element.GetTypeInfo().IsSubclassOf(typeof(UIElement)) != true)
        {
            yield break;
        }

        Type current = element;

        while (current != null && current != typeof(UIElement))
        {
            yield return current;
            current = current.GetTypeInfo().BaseType;
        }
    }

    public static void DisplayRoutedEventsForUIElement()
    {
        Type uiElementType = typeof(Microsoft.UI.Xaml.UIElement);
        var routedEvents = uiElementType.GetEvents();
        Debug.WriteLine($"[All RoutedEvents for {nameof(Microsoft.UI.Xaml.UIElement)}]");
        foreach (var routedEvent in routedEvents)
        {
            if (routedEvent.EventHandlerType == typeof(Microsoft.UI.Xaml.RoutedEventHandler) ||
                routedEvent.EventHandlerType == typeof(Microsoft.UI.Xaml.RoutedEvent) ||
                routedEvent.EventHandlerType == typeof(EventHandler))
            {
                Debug.WriteLine($" - '{routedEvent.Name}'");
            }
            else if (routedEvent.MemberType == System.Reflection.MemberTypes.Event)
            {
                Debug.WriteLine($" - '{routedEvent.Name}'");
            }
        }
    }

    public static void DisplayRoutedEventsForFrameworkElement()
    {
        Type fwElementType = typeof(Microsoft.UI.Xaml.FrameworkElement);
        var routedEvents = fwElementType.GetEvents();
        Debug.WriteLine($"[All RoutedEvents for {nameof(Microsoft.UI.Xaml.FrameworkElement)}]");
        foreach (var routedEvent in routedEvents)
        {
            if (routedEvent.EventHandlerType == typeof(Microsoft.UI.Xaml.RoutedEventHandler) ||
                routedEvent.EventHandlerType == typeof(Microsoft.UI.Xaml.RoutedEvent) ||
                routedEvent.EventHandlerType == typeof(EventHandler))
            {
                Debug.WriteLine($" - '{routedEvent.Name}'");
            }
            else if (routedEvent.MemberType == System.Reflection.MemberTypes.Event)
            {
                Debug.WriteLine($" - '{routedEvent.Name}'");
            }
        }
    }

    public static void DisplayRoutedEventsForControl()
    {
        Type ctlElementType = typeof(Microsoft.UI.Xaml.Controls.Control);
        var routedEvents = ctlElementType.GetEvents();
        Debug.WriteLine($"[All RoutedEvents for {nameof(Microsoft.UI.Xaml.Controls.Control)}]");
        foreach (var routedEvent in routedEvents)
        {
            if (routedEvent.EventHandlerType == typeof(Microsoft.UI.Xaml.RoutedEventHandler) ||
                routedEvent.EventHandlerType == typeof(Microsoft.UI.Xaml.RoutedEvent) ||
                routedEvent.EventHandlerType == typeof(EventHandler))
            {
                Debug.WriteLine($" - '{routedEvent.Name}'");
            }
            else if (routedEvent.MemberType == System.Reflection.MemberTypes.Event)
            {
                Debug.WriteLine($" - '{routedEvent.Name}'");
            }
        }
    }

#if COMMUNITY_TOOLKIT
    /// <summary>
    /// <para>
    /// Gets the image data from a Uri.
    /// </para>
    /// <para>
    /// The issue with many of the CommunityToolkit file access routines is that they do not
    /// handle unpackaged apps, so you will see I added logic switches for most of these methods.
    /// </para>
    /// </summary>
    /// <param name="uri">Image Uri</param>
    /// <returns>Image Stream as <see cref="Windows.Storage.Streams.IRandomAccessStream"/></returns>
    public static async Task<Windows.Storage.Streams.IRandomAccessStream?> GetImageStream(this Uri uri)
    {
        Windows.Storage.Streams.IRandomAccessStream? imageStream = null;
        string localPath = string.Empty;
        if (uri.LocalPath.StartsWith("\\\\"))
            localPath = $"{uri.LocalPath}".Replace("//", "/");
        else
            localPath = $"{uri.Host}/{uri.LocalPath}".Replace("//", "/");

        // If we don't have internet, then try to see if we have a packaged copy
        try
        {
            if (App.IsPackaged)
            {
                /*
                    "StreamHelper.GetPackagedFileStreamAsync" contains the following...
                    StorageFolder workingFolder = Package.Current.InstalledLocation;
                    return GetFileStreamAsync(fileName, accessMode, workingFolder);
                */
                imageStream = await CommunityToolkit.WinUI.Helpers.StreamHelper.GetPackagedFileStreamAsync(localPath);
            }
            else
            {
                /*
                    "StreamHelper.GetLocalFileStreamAsync" contains the following...
                    StorageFolder workingFolder = ApplicationData.Current.LocalFolder;
                    return GetFileStreamAsync(fileName, accessMode, workingFolder);
                */
                imageStream = await CommunityToolkit.WinUI.Helpers.StreamHelper.GetLocalFileStreamAsync(localPath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[INFO] {localPath}");
            Debug.WriteLine($"[WARNING] GetImageStream: {ex.Message}");
        }

        return imageStream;
    }
#endif

    #endregion
}

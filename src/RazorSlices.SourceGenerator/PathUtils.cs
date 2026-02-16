// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Adapted from https://github.com/dotnet/corefx/blob/master/src/Common/src/CoreLib/System/IO/Path.cs#L697
// by Anton Krouglov

// Further tweaked by Damian Edwards

using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Text;
using System.Runtime.InteropServices;

namespace System.IO;

// Provides methods for processing file system strings in a cross-platform manner.
// Most of the methods don't do a complete parsing (such as examining a UNC hostname), 
// but they will handle most string operations.
internal static class PathUtils
{
    /// <summary>
    /// Create a relative path from one path to another. Paths will be resolved before calculating the difference.
    /// Default path comparison for the active platform will be used (OrdinalIgnoreCase for Windows or Mac, Ordinal for Unix).
    /// </summary>
    /// <param name="relativeTo">The source path the output should be relative to. This path is always considered to be a directory.</param>
    /// <param name="path">The destination path.</param>
    /// <returns>The relative path or <paramref name="path"/> if the paths don't share the same root.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="relativeTo"/> or <paramref name="path"/> is <c>null</c> or an empty string.</exception>
    public static string GetRelativePath(string relativeTo, string path)
    {
        return GetRelativePath(relativeTo, path, StringComparison);
    }

    private static string GetRelativePath(string relativeTo, string path, StringComparison comparisonType)
    {
        if (string.IsNullOrEmpty(relativeTo))
        {
            throw new ArgumentNullException(nameof(relativeTo));
        }

        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentNullException(nameof(path));
        }

        Debug.Assert(comparisonType == StringComparison.Ordinal ||
                     comparisonType == StringComparison.OrdinalIgnoreCase);

        relativeTo = Path.GetFullPath(relativeTo);
        path = Path.GetFullPath(path);

        // Need to check if the roots are different- if they are we need to return the "to" path.
        if (!PathInternalNetCore.AreRootsEqual(relativeTo, path, comparisonType))
        {
            return path;
        }

        int commonLength = PathInternalNetCore.GetCommonPathLength(relativeTo, path,
            ignoreCase: comparisonType == StringComparison.OrdinalIgnoreCase);

        // If there is nothing in common they can't share the same root, return the "to" path as is.
        if (commonLength == 0)
        {
            return path;
        }

        // Trailing separators aren't significant for comparison
        int relativeToLength = relativeTo.Length;
        if (PathInternalNetCore.EndsInDirectorySeparator(relativeTo))
        {
            relativeToLength--;
        }

        bool pathEndsInSeparator = PathInternalNetCore.EndsInDirectorySeparator(path);
        int pathLength = path.Length;
        if (pathEndsInSeparator)
        {
            pathLength--;
        }

        // If we have effectively the same path, return "."
        if (relativeToLength == pathLength && commonLength >= relativeToLength)
        {
            return ".";
        }

        // We have the same root, we need to calculate the difference now using the
        // common Length and Segment count past the length.
        //
        // Some examples:
        //
        //  C:\Foo C:\Bar L3, S1 -> ..\Bar
        //  C:\Foo C:\Foo\Bar L6, S0 -> Bar
        //  C:\Foo\Bar C:\Bar\Bar L3, S2 -> ..\..\Bar\Bar
        //  C:\Foo\Foo C:\Foo\Bar L7, S1 -> ..\Bar

        StringBuilder sb = new(); //StringBuilderCache.Acquire(Math.Max(relativeTo.Length, path.Length));

        // Add parent segments for segments past the common on the "from" path
        if (commonLength < relativeToLength)
        {
            sb.Append("..");

            for (int i = commonLength + 1; i < relativeToLength; i++)
            {
                if (PathInternalNetCore.IsDirectorySeparator(relativeTo[i]))
                {
                    sb.Append(DirectorySeparatorChar);
                    sb.Append("..");
                }
            }
        }
        else if (PathInternalNetCore.IsDirectorySeparator(path[commonLength]))
        {
            // No parent segments and we need to eat the initial separator
            //  (C:\Foo C:\Foo\Bar case)
            commonLength++;
        }

        // Now add the rest of the "to" path, adding back the trailing separator
        int differenceLength = pathLength - commonLength;
        if (pathEndsInSeparator)
        {
            differenceLength++;
        }

        if (differenceLength > 0)
        {
            if (sb.Length > 0)
            {
                sb.Append(DirectorySeparatorChar);
            }

            sb.Append(path, commonLength, differenceLength);
        }

        return sb.ToString(); //StringBuilderCache.GetStringAndRelease(sb);
    }

    // Public static readonly variant of the separators. The Path implementation itself is using
    // internal const variant of the separators for better performance.
    public static readonly char DirectorySeparatorChar = PathInternalNetCore.DirectorySeparatorChar;
    public static readonly char AltDirectorySeparatorChar = PathInternalNetCore.AltDirectorySeparatorChar;
    public static readonly char VolumeSeparatorChar = PathInternalNetCore.VolumeSeparatorChar;
    public static readonly char PathSeparator = PathInternalNetCore.PathSeparator;

    /// <summary>Returns a comparison that can be used to compare file and directory names for equality.</summary>
    internal static StringComparison StringComparison => RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
}

/// <summary>Contains internal path helpers that are shared between many projects.</summary>
internal static class PathInternalNetCore
{
    internal const char DirectorySeparatorChar = '\\';
    internal const char AltDirectorySeparatorChar = '/';
    internal const char VolumeSeparatorChar = ':';
    internal const char PathSeparator = ';';

    internal const string ExtendedDevicePathPrefix = @"\\?\";
    internal const string UncPathPrefix = @"\\";
    internal const string UncDevicePrefixToInsert = @"?\UNC\";
    internal const string UncExtendedPathPrefix = @"\\?\UNC\";
    internal const string DevicePathPrefix = @"\\.\";

    //internal const int MaxShortPath = 260;

    // \\?\, \\.\, \??\
    internal const int DevicePrefixLength = 4;

    /// <summary>
    /// Returns true if the two paths have the same root
    /// </summary>
    internal static bool AreRootsEqual(string first, string second, StringComparison comparisonType)
    {
        int firstRootLength = GetRootLength(first);
        int secondRootLength = GetRootLength(second);

        return firstRootLength == secondRootLength
               && string.Compare(
                   strA: first,
                   indexA: 0,
                   strB: second,
                   indexB: 0,
                   length: firstRootLength,
                   comparisonType: comparisonType) == 0;
    }

    /// <summary>
    /// Gets the length of the root of the path (drive, share, etc.).
    /// </summary>
    internal static int GetRootLength(string path)
    {
        int i = 0;
        int volumeSeparatorLength = 2; // Length to the colon "C:"
        int uncRootLength = 2; // Length to the start of the server name "\\"

        bool extendedSyntax = path.StartsWith(ExtendedDevicePathPrefix);
        bool extendedUncSyntax = path.StartsWith(UncExtendedPathPrefix);
        if (extendedSyntax)
        {
            // Shift the position we look for the root from to account for the extended prefix
            if (extendedUncSyntax)
            {
                // "\\" -> "\\?\UNC\"
                uncRootLength = UncExtendedPathPrefix.Length;
            }
            else
            {
                // "C:" -> "\\?\C:"
                volumeSeparatorLength += ExtendedDevicePathPrefix.Length;
            }
        }

        if ((!extendedSyntax || extendedUncSyntax) && path.Length > 0 && IsDirectorySeparator(path[0]))
        {
            // UNC or simple rooted path (e.g. "\foo", NOT "\\?\C:\foo")

            i = 1; //  Drive rooted (\foo) is one character
            if (extendedUncSyntax || (path.Length > 1 && IsDirectorySeparator(path[1])))
            {
                // UNC (\\?\UNC\ or \\), scan past the next two directory separators at most
                // (e.g. to \\?\UNC\Server\Share or \\Server\Share\)
                i = uncRootLength;
                int n = 2; // Maximum separators to skip
                while (i < path.Length && (!IsDirectorySeparator(path[i]) || --n > 0))
                {
                    i++;
                }
            }
        }
        else if (path.Length >= volumeSeparatorLength &&
                 path[volumeSeparatorLength - 1] == PathUtils.VolumeSeparatorChar)
        {
            // Path is at least longer than where we expect a colon, and has a colon (\\?\A:, A:)
            // If the colon is followed by a directory separator, move past it
            i = volumeSeparatorLength;
            if (path.Length >= volumeSeparatorLength + 1 && IsDirectorySeparator(path[volumeSeparatorLength]))
            {
                i++;
            }
        }

        return i;
    }

    /// <summary>
    /// True if the given character is a directory separator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsDirectorySeparator(char c)
    {
        return c == PathUtils.DirectorySeparatorChar || c == PathUtils.AltDirectorySeparatorChar;
    }

    /// <summary>
    /// Get the common path length from the start of the string.
    /// </summary>
    internal static int GetCommonPathLength(string first, string second, bool ignoreCase)
    {
        int commonChars = EqualStartingCharacterCount(first, second, ignoreCase: ignoreCase);

        // If nothing matches
        if (commonChars == 0)
        {
            return commonChars;
        }

        // Or we're a full string and equal length or match to a separator
        if (commonChars == first.Length
            && (commonChars == second.Length || IsDirectorySeparator(second[commonChars])))
        {
            return commonChars;
        }

        if (commonChars == second.Length && IsDirectorySeparator(first[commonChars]))
        {
            return commonChars;
        }

        // It's possible we matched somewhere in the middle of a segment e.g. C:\Foodie and C:\Foobar.
        while (commonChars > 0 && !IsDirectorySeparator(first[commonChars - 1]))
        {
            commonChars--;
        }

        return commonChars;
    }

    /// <summary>
    /// Gets the count of common characters from the left optionally ignoring case
    /// </summary>
    internal static unsafe int EqualStartingCharacterCount(string first, string second, bool ignoreCase)
    {
        if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
        {
            return 0;
        }

        int commonChars = 0;

        fixed (char* f = first)
        fixed (char* s = second)
        {
            char* l = f;
            char* r = s;
            char* leftEnd = l + first.Length;
            char* rightEnd = r + second.Length;

            while (l != leftEnd && r != rightEnd
                                && (*l == *r || (ignoreCase &&
                                                 char.ToUpperInvariant((*l)) == char.ToUpperInvariant((*r)))))
            {
                commonChars++;
                l++;
                r++;
            }
        }

        return commonChars;
    }

    /// <summary>
    /// Returns true if the path ends in a directory separator.
    /// </summary>
    internal static bool EndsInDirectorySeparator(string path)
        => path.Length > 0 && IsDirectorySeparator(path[path.Length - 1]);
}

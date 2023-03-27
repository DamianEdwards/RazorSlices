﻿namespace RazorSlices;

public abstract partial class RazorSlice
{
    private AttributeInfo _attributeInfo;

    /// <summary>
    /// Begins writing out an attribute.
    /// </summary>
    /// <remarks>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all HTML attributes containing Razor expressions in your .cshtml file.
    /// </remarks>
    /// <param name="name">The name.</param>
    /// <param name="prefix">The prefix.</param>
    /// <param name="prefixOffset">The prefix offset.</param>
    /// <param name="suffix">The suffix.</param>
    /// <param name="suffixOffset">The suffix offset.</param>
    /// <param name="attributeValuesCount">The attribute values count.</param>
    protected virtual void BeginWriteAttribute(
        string name,
        string prefix,
        int prefixOffset,
        string suffix,
        int suffixOffset,
        int attributeValuesCount)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(suffix);

        _attributeInfo = new()
        {
            Name = name,
            Prefix = prefix,
            PrefixOffset = prefixOffset,
            Suffix = suffix,
            SuffixOffset = suffixOffset,
            AttributeValuesCount = attributeValuesCount
        };

        // Single valued attributes might be omitted in entirety if it the attribute value strictly evaluates to
        // null or false. Consequently defer the prefix generation until we encounter the attribute value.
        if (attributeValuesCount != 1)
        {
            WritePositionTaggedLiteral(prefix, prefixOffset);
        }
    }

    /// <summary>
    /// Writes out a <see cref="bool"/> attribute value.
    /// </summary>
    /// <remarks>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all HTML attributes containing Razor expressions in your .cshtml file.
    /// </remarks>
    /// <param name="prefix">The prefix.</param>
    /// <param name="prefixOffset">The prefix offset.</param>
    /// <param name="value">The value.</param>
    /// <param name="valueOffset">The value offset.</param>
    /// <param name="valueLength">The value length.</param>
    /// <param name="isLiteral">Whether the attribute is a literal.</param>
    protected void WriteAttributeValue(
        string prefix,
        int prefixOffset,
        bool? value,
#pragma warning disable IDE0060 // Remove unused parameter
        int valueOffset,
        int valueLength,
#pragma warning restore IDE0060 // Remove unused parameter
        bool isLiteral)
    {
        var valueIsAttributeName = false;

        if (_attributeInfo.AttributeValuesCount == 1)
        {
            if (IsBoolFalseOrNullValue(prefix, value))
            {
                // Value is either null or the bool 'false' with no prefix; don't render the attribute.
                _attributeInfo.Suppressed = true;
                return;
            }

            // We are not omitting the attribute. Write the prefix.
            WritePositionTaggedLiteral(_attributeInfo.Prefix, _attributeInfo.PrefixOffset);

            if (IsBoolTrueWithEmptyPrefixValue(prefix, value))
            {
                // The value is just the bool 'true', write the attribute name instead of the string 'True'.
                valueIsAttributeName = true;
            }
        }

        // This block handles two cases.
        // 1. Single value with prefix.
        // 2. Multiple values with or without prefix.
        if (value is not null || valueIsAttributeName)
        {
            if (!string.IsNullOrEmpty(prefix))
            {
                WritePositionTaggedLiteral(prefix, prefixOffset);
            }

            if (valueIsAttributeName)
            {
                if (isLiteral)
                {
                    WriteLiteral(_attributeInfo.Name);
                }
                else
                {
                    Write(_attributeInfo.Name);
                }
            }
            else
            {
                WriteBool(value);
            }
        }

        static bool IsBoolFalseOrNullValue(string? prefix, bool? value)
        {
            return string.IsNullOrEmpty(prefix) && value != true;
        }

        static bool IsBoolTrueWithEmptyPrefixValue(string? prefix, bool? value)
        {
            // If the value is just the bool 'true', use the attribute name as the value.
            return string.IsNullOrEmpty(prefix) && value == true;
        }
    }

    /// <summary>
    /// Writes out the <typeparamref name="TValue"/> attribute value.
    /// </summary>
    /// <remarks>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all HTML attributes containing Razor expressions in your .cshtml file.
    /// </remarks>
    /// <param name="prefix">The prefix.</param>
    /// <param name="prefixOffset">The prefix offset.</param>
    /// <param name="value">The value.</param>
    /// <param name="valueOffset">The value offset.</param>
    /// <param name="valueLength">The value length.</param>
    /// <param name="isLiteral">Whether the attribute is a literal.</param>
    /// <typeparam name="TValue">The type of the attribute value being written.</typeparam>
    protected void WriteAttributeValue<TValue>(
        string prefix,
        int prefixOffset,
        TValue? value,
#pragma warning disable IDE0060 // Remove unused parameter
        int valueOffset,
        int valueLength,
#pragma warning restore IDE0060 // Remove unused parameter
        bool isLiteral)
    {
        if (_attributeInfo.AttributeValuesCount == 1)
        {
            // NOTE: In some edge cases, 'value' could render an empty string, e.g. custom ISpanFormattable/IHtmlContent, and thus
            //       this IsNullValue() check will fail, but we want to avoid any allocation here so we only check if the value is
            //       null or is actually a string and is empty. In the edge case, the attribute will still be rendered with an
            //       empty value.
            if (IsNullValue(prefix, value))
            {
                // Value is null with no prefix; don't render the attribute.
                _attributeInfo.Suppressed = true;
                return;
            }

            // We are not omitting the attribute. Write the prefix.
            WritePositionTaggedLiteral(_attributeInfo.Prefix, _attributeInfo.PrefixOffset);
        }

        // This block handles two cases.
        // 1. Single value with prefix.
        // 2. Multiple values with or without prefix.
        if (value is not null)
        {
            if (!string.IsNullOrEmpty(prefix))
            {
                WritePositionTaggedLiteral(prefix, prefixOffset);
            }

            if (isLiteral)
            {
                WriteLiteral(value);
            }
            else
            {
                WriteValue(value);
            }
        }

        static bool IsNullValue(string? prefix, TValue? value)
        {
            return string.IsNullOrEmpty(prefix)
                && (value is null
                    || (value is string && string.IsNullOrEmpty((string)(object)value)));
        }
    }

    /// <summary>
    /// Ends writing an attribute.
    /// </summary>
    /// <remarks>
    /// You generally shouldn't call this method directly. The Razor compiler will emit the appropriate calls to this method for
    /// all HTML attributes containing Razor expressions in your .cshtml file.
    /// </remarks>
    protected virtual void EndWriteAttribute()
    {
        if (!_attributeInfo.Suppressed)
        {
            WritePositionTaggedLiteral(_attributeInfo.Suffix, _attributeInfo.SuffixOffset);
        }
    }

#pragma warning disable IDE0060 // Remove unused parameter
    private void WritePositionTaggedLiteral(string value, int position)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        WriteLiteral(value);
    }

    private struct AttributeInfo
    {
        public int AttributeValuesCount { get; init; }

        public string Name { get; init; }

        public string Prefix { get; init; }

        public int PrefixOffset { get; init; }

        public string Suffix { get; init; }

        public int SuffixOffset { get; init; }

        public bool Suppressed { get; set; }
    }
}

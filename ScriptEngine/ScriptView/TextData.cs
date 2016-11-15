// -----------------------------------------------------------------------
// <copyright file="TextData.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace System.Text
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Dynamic;
    using System.IO;
    using System.Collections;


    /// <summary>
    /// text data utilities.
    /// </summary>
    public static class TextData
    {

        /// <summary>
        /// breaks a string containing a single row from a CSV file into fields, accounting for the specified text-delimiter.
        /// </summary>
        /// <param name="row"></param>
        /// <param name="colSeperator"></param>
        /// <param name="textDelimiter"></param>
        /// <param name="trim">optional: if true, each value is trimmed for whitespace before being yeilded</param>
        /// <returns></returns>
        public static IEnumerable<string> EnumDelimitedText(this string row, char colSeperator, char textDelimiter, bool trim = false)
        {
            // holds the current value of the current field
            var fieldValue = new StringBuilder();

            // remembers if within a text delimiter
            var inText = false;

            foreach (var c in row)
            {
                // is the character a text delimiter?
                if (c == textDelimiter)
                {
                    // toggle the flag;
                    inText = !inText;
                }
                else if (inText)
                {
                    // append any character if inside text-delimiter
                    fieldValue.Append(c);
                }
                else if (c != colSeperator)
                {
                    // if not the column-seperator, append
                    fieldValue.Append(c);
                }
                else
                {

                    if (trim)
                        yield return fieldValue.ToString().Trim();
                    else
                        yield return fieldValue.ToString();

                    // clear the value:
                    fieldValue.Clear();
                }
            }

            // yield the final column 
            if (fieldValue.Length > 0)
            {
                if (trim)
                    yield return fieldValue.ToString().Trim();
                else
                    yield return fieldValue.ToString();
            }
        }

        /// <summary>
        /// write the enumeration to a text file with the given column, row and string delimeters.
        /// </summary>
        /// <param name="data">
        /// the enumeration of records to write to the text-file.
        /// </param>
        /// <param name="columnSep">
        /// the column-seperator to use, normally "," (comma)
        /// </param>
        /// <param name="rowSep">
        /// the row-seperator to use (normally cr/lf)
        /// </param>
        /// <param name="textDelim">
        /// when a column contains a row or column delimeter, it is wrapped using this value (normally ")
        /// </param>
        /// <param name="headings">
        /// should column-headings be written
        /// </param>
        /// <param name="encoding">
        /// the text-encoding used when converting text to binary
        /// </param> 
        /// <param name="ifNull">the string to use for null values. eg [null] or ""</param>
        /// <param name="target">the stream to write the data to</param>
        /// <param name="excludeColumns">any columns to exclude from the data</param>
        public static void WriteAsText<T>(this IEnumerable<T> data, Stream target, Encoding encoding, string columnSep, string rowSep, string textDelim, string ifNull, bool headings, bool alwayDelimitText, params string[] excludeColumns)
        {

            // select the properties that we want:
            var props = (from p in typeof(T).GetProperties() where p.CanRead && !p.GetGetMethod().IsStatic && !excludeColumns.Contains(p.Name, StringComparer.OrdinalIgnoreCase) select p).ToArray();

            // select the getters:
            var getters = (from p in props select p.CompileGetter()).ToArray();

            using (StreamWriter writer = new StreamWriter(target, encoding))
            {
                // set the new line character:
                writer.NewLine = rowSep;
                writer.AutoFlush = true;

                if (headings)
                {
                    StringBuilder line = new StringBuilder();
                    for (int i = 0; i < props.Length; i++)
                    {
                        if (line.Length > 0)
                            line.Append(columnSep);

                        // add the field name:
                        line.Append(props[i].Name);
                    }
                    // write the heading line into the stream:
                    writer.WriteLine(line.ToString());
                }

                // enumerate the rows of data:
                foreach (var item in data)
                {
                    // prepare the line:
                    StringBuilder line = new StringBuilder();
                    for (int i = 0; i < props.Length; i++)
                    {
                        if (line.Length > 0)
                            line.Append(columnSep);

                        // fetch the value from the current line using the current getter:
                        object value = getters[i].Invoke(item);

                        if (value != null)
                        {
                            // get the string representation:
                            string sv = value.ToString();

                            if (sv.Contains(rowSep))
                            {
                                sv = sv.Replace(rowSep, " ");
                            }
                            foreach (char rs in rowSep)
                            {
                                if (sv.Contains(rs))
                                    sv = sv.Replace(rs, ' ');
                            }

                            // does it contain a column delimeter?
                            if (sv.Contains(columnSep))
                            {
                                line.Append(textDelim).Append(sv).Append(textDelim);
                            }
                            else
                            {
                                line.Append(sv);
                            }
                        }
                        else
                        {
                            // append the null value:
                            line.Append(ifNull);
                        }
                    }

                    // now write the line to the output stream:
                    writer.WriteLine(line.ToString());
                }
            }
        }

        /// <summary>
        /// write the enumeration to a text file with the given column, row and string delimeters.
        /// </summary>
        /// <param name="data">
        /// the enumeration of records to write to the text-file.
        /// </param>
        /// <param name="columnSep">
        /// the column-seperator to use, normally "," (comma)
        /// </param>
        /// <param name="rowSep">
        /// the row-seperator to use (normally cr/lf)
        /// </param>
        /// <param name="textDelim">
        /// when a column contains a row or column delimeter, it is wrapped using this value (normally ")
        /// </param>
        /// <param name="headings">
        /// should column-headings be written
        /// </param>
        /// <param name="encoding">
        /// the text-encoding used when converting text to binary
        /// </param> 
        /// <param name="ifNull">the string to use for null values. eg [null] or ""</param>
        /// <param name="target">the stream to write the data to</param>
        /// <param name="excludeColumns">any columns to exclude from the data</param>
        public static void WriteAsText(this IEnumerable<string[]> data, Stream target, Encoding encoding, string columnSep, string rowSep, string textDelim, string ifNull, bool alwayDelimitText, params string[] excludeColumns)
        {


            using (StreamWriter writer = new StreamWriter(target, encoding))
            {
                // set the new line character:
                writer.NewLine = rowSep;
                writer.AutoFlush = true;


                // enumerate the rows of data:
                foreach (var item in data)
                {
                    // prepare the line:
                    StringBuilder line = new StringBuilder();
                    for (int i = 0; i < item.Length; i++)
                    {
                        if (line.Length > 0)
                            line.Append(columnSep);

                        // fetch the value from the current line
                        string sv = item[i];

                        if (!string.IsNullOrEmpty(sv))
                        { 
                            if (sv.Contains(rowSep))
                            {
                                sv = sv.Replace(rowSep, " ");
                            }
                            foreach (char rs in rowSep)
                            {
                                if (sv.Contains(rs))
                                    sv = sv.Replace(rs, ' ');
                            }

                            // does it contain a column delimeter?
                            if (sv.Contains(columnSep) || alwayDelimitText)
                            {
                                line.Append(textDelim).Append(sv).Append(textDelim);
                            }
                            else
                            {
                                line.Append(sv);
                            }
                        }
                        else
                        {
                            // append the null value:
                            line.Append(ifNull);
                        }
                    }

                    // now write the line to the output stream:
                    writer.WriteLine(line.ToString());
                }
            }
        }


        /// <summary>
        /// write the enumeration to a text file with the given column, row and string delimeters.
        /// </summary>
        /// <param name="data">
        /// the enumeration of records to write to the text-file.
        /// </param>
        /// <param name="dataType">the type of objects in data</param>
        /// <param name="columnSep">
        /// the column-seperator to use, normally "," (comma)
        /// </param>
        /// <param name="rowSep">
        /// the row-seperator to use (normally cr/lf)
        /// </param>
        /// <param name="textDelim">
        /// when a column contains a row or column delimeter, it is wrapped using this value (normally ")
        /// </param>
        /// <param name="headings">
        /// should column-headings be written
        /// </param>
        /// <param name="encoding">
        /// the text-encoding used when converting text to binary
        /// </param> 
        /// <param name="ifNull">the string to use for null values. eg [null] or ""</param>
        /// <param name="target">the stream to write the data to</param>
        /// <param name="excludeColumns">any columns to exclude from the data</param>
        public static void WriteAsText(this IEnumerable data, Type dataType, Stream target, Encoding encoding, string columnSep, string rowSep, string textDelim, string ifNull, bool headings, params string[] excludeColumns)
        {            
            // select the properties that we want:
            var props = (from p in dataType.GetProperties() where p.CanRead && !p.GetGetMethod().IsStatic && !excludeColumns.Contains(p.Name, StringComparer.OrdinalIgnoreCase) select p).ToArray();

            // select the getters:
            var getters = (from p in props select p.CompileGetter()).ToArray();

            // buider for this line:
            var line = new StringBuilder();

            // create a stream-writer to write to the target:
            using (StreamWriter writer = new StreamWriter(target, encoding))
            {
                // set the new line character:
                writer.NewLine = rowSep;
                writer.AutoFlush = true;

                if (headings)
                {
                    // clear the line:
                    line.Clear();

                    // iterate the properties:
                    for (int i = 0; i < props.Length; i++)
                    {
                        if (line.Length > 0)
                            line.Append(columnSep);

                        // add the field name:
                        line.Append(props[i].Name);
                    }

                    // write the heading line into the stream:
                    writer.WriteLine(line.ToString());
                }

                // enumerate the rows of data:
                foreach (var item in data)
                {
                    // prepare the line:
                    line.Clear();

                    // enumerate the properties:
                    for (int i = 0; i < props.Length; i++)
                    {
                        // prepend the seperator if data already on the line:
                        if (line.Length > 0)
                            line.Append(columnSep);

                        // fetch the value from the current line using the current getter:
                        object value = getters[i].Invoke(item);

                        if (value != null)
                        {
                            // get the string representation:
                            var sv = value.ToString();

                            // replace row-seperators with spaces:
                            if (sv.Contains(rowSep))
                            {
                                sv = sv.Replace(rowSep, " ");
                            }
                            foreach (char rs in rowSep)
                            {
                                if (sv.Contains(rs))
                                    sv = sv.Replace(rs, ' ');
                            }

                            // does it contain a column delimeter?
                            if (sv.Contains(columnSep))
                            {
                                line.Append(textDelim).Append(sv).Append(textDelim);
                            }
                            else
                            {
                                line.Append(sv);
                            }
                        }
                        else
                        {
                            // append the null value:
                            line.Append(ifNull);
                        }
                    }

                    // now write the line to the output stream:
                    writer.WriteLine(line.ToString());
                }
            }
        }


        /// <summary>
        /// returns this object as a row of delimited text.
        /// </summary>
        /// <param name="colDelimiter"></param>
        /// <returns></returns>
        public static string GetDelimitedTextRow(this object thisObject, char colDelimiter)
        {
            var props = thisObject.GetType().GetProperties();
            var sb = new StringBuilder();

            foreach (var prp in props)
            {
                if (sb.Length > 0)
                    sb.Append(colDelimiter);
                sb.Append(prp.GetValue(thisObject));
            }

            return sb.ToString();
        }

        /// <summary>
        /// returns a header row for a delimited text file 
        /// </summary>
        /// <param name="colDelimiter"></param>
        /// <returns></returns>
        public static string GetDelimitedHeadRow(this object thisObject, char colDelimiter)
        {
            var props = thisObject.GetType().GetProperties();
            var sb = new StringBuilder();
            
            foreach (var prp in props)
            {
                if (sb.Length > 0)
                    sb.Append(colDelimiter);
                sb.Append(prp.Name);
            }

            return sb.ToString();
        }
    }

}

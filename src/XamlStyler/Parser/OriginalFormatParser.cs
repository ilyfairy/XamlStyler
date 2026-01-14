// (c) Xavalon. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace Xavalon.XamlStyler.Parser
{
    /// <summary>
    /// Parses the original XAML source to extract format metadata that is lost during XmlReader processing.
    /// </summary>
    public class OriginalFormatParser
    {
        private readonly Dictionary<string, Queue<OriginalFormatInfo>> elementFormatInfos;

        public OriginalFormatParser(string xamlSource)
        {
            this.elementFormatInfos = new Dictionary<string, Queue<OriginalFormatInfo>>(StringComparer.Ordinal);
            this.ParseOriginalFormat(xamlSource);
        }

        /// <summary>
        /// Gets the next format info for the specified element name.
        /// </summary>
        public OriginalFormatInfo GetNextFormatInfo(string elementName)
        {
            if (this.elementFormatInfos.TryGetValue(elementName, out var queue) && queue.Count > 0)
            {
                return queue.Dequeue();
            }

            return null;
        }

        private void ParseOriginalFormat(string xamlSource)
        {
            try
            {
                using (var reader = new StringReader(xamlSource))
                {
                    var settings = new XmlReaderSettings
                    {
                        IgnoreComments = false,
                        IgnoreProcessingInstructions = false,
                        IgnoreWhitespace = false
                    };

                    using (var xmlReader = XmlReader.Create(reader, settings))
                    {
                        // We need to track positions in the original source
                        // XmlReader with IXmlLineInfo can give us line/column info
                        var lineInfo = xmlReader as IXmlLineInfo;

                        while (xmlReader.Read())
                        {
                            if (xmlReader.NodeType == XmlNodeType.Element)
                            {
                                var formatInfo = this.ParseElementFormatInfo(xmlReader, lineInfo, xamlSource);
                                this.AddFormatInfo(formatInfo);
                            }
                        }
                    }
                }
            }
            catch (XmlException)
            {
                // If parsing fails, we'll just have no format info
            }
        }

        private OriginalFormatInfo ParseElementFormatInfo(XmlReader xmlReader, IXmlLineInfo lineInfo, string xamlSource)
        {
            var formatInfo = new OriginalFormatInfo
            {
                ElementName = xmlReader.Name,
                IsSelfClosing = xmlReader.IsEmptyElement,
                AttributeCount = xmlReader.AttributeCount
            };

            if (lineInfo != null && lineInfo.HasLineInfo())
            {
                formatInfo.StartLine = lineInfo.LineNumber;
                formatInfo.StartColumn = lineInfo.LinePosition;
            }

            // Parse attribute line breaks from the original source
            if (xmlReader.HasAttributes)
            {
                this.ParseAttributeLineBreaks(xmlReader, lineInfo, formatInfo);
            }

            return formatInfo;
        }

        private void ParseAttributeLineBreaks(XmlReader xmlReader, IXmlLineInfo lineInfo, OriginalFormatInfo formatInfo)
        {
            if (lineInfo == null || !lineInfo.HasLineInfo())
            {
                return;
            }

            int elementLine = formatInfo.StartLine;
            int previousAttributeLine = elementLine;
            int attributeIndex = 0;

            while (xmlReader.MoveToNextAttribute())
            {
                int currentLine = lineInfo.LineNumber;

                // If this attribute is on a different line than the previous one (or the element for first attr)
                if (currentLine > previousAttributeLine)
                {
                    formatInfo.AttributeLineBreakIndices.Add(attributeIndex);
                }

                previousAttributeLine = currentLine;
                attributeIndex++;
            }

            // Move back to element
            xmlReader.MoveToElement();
        }

        private void AddFormatInfo(OriginalFormatInfo formatInfo)
        {
            if (!this.elementFormatInfos.TryGetValue(formatInfo.ElementName, out var queue))
            {
                queue = new Queue<OriginalFormatInfo>();
                this.elementFormatInfos[formatInfo.ElementName] = queue;
            }

            queue.Enqueue(formatInfo);
        }
    }
}

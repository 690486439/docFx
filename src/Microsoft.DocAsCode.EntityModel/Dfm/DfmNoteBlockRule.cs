﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.EntityModel
{
    using System.Text.RegularExpressions;

    using MarkdownLite;

    public class DfmNoteBlockRule : IMarkdownRule
    {
        public string Name => "DfmNoteBlcokRule";

        public Regex _dfmNoteRegex = new Regex(@"^(?<rawmarkdown>\s*\[\!(?<notetype>(NOTE|WARNING|TIP|IMPORTANT|CAUTION))\]\s*(\n|$))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public virtual IMarkdownToken TryMatch(MarkdownEngine engine, ref string source)
        {
            if (!engine.Context.Variables.ContainsKey(MarkdownBlockContext.IsBlockQuote) || !(bool)engine.Context.Variables[MarkdownBlockContext.IsBlockQuote])
            {
                return null;
            }
            var match = _dfmNoteRegex.Match(source);
            if (match.Length == 0)
            {
                return null;
            }
            source = source.Substring(match.Length);
            return new DfmNoteBlockToken(this, match.Groups["notetype"].Value, match.Groups["rawmarkdown"].Value);
        }
    }
}

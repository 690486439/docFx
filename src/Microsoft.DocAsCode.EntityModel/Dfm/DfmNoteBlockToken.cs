﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.EntityModel
{
    using MarkdownLite;

    public class DfmNoteBlockToken : MarkdownTextToken
    {

        public DfmNoteBlockToken(IMarkdownRule rule, string noteType, string content)
            : base(rule, content)
        {
            NoteType = noteType;
        }

        public string NoteType { get; }
    }
}

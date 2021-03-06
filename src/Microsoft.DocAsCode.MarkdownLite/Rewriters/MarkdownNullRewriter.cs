﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.MarkdownLite
{
    /// <summary>
    /// Null object.
    /// </summary>
    internal sealed class MarkdownNullRewriter : IMarkdownRewriter
    {
        public IMarkdownToken Rewrite(MarkdownEngine engine, IMarkdownToken token)
        {
            return null;
        }
    }
}

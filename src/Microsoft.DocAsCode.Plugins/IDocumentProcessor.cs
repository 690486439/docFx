﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.Plugins
{
    using System.Collections.Generic;
    using System.Collections.Immutable;

    public interface IDocumentProcessor
    {
        string Name { get; }
        ProcessingPriority GetProcessingPriority(FileAndType file);
        FileModel Load(FileAndType file, ImmutableDictionary<string, object> metadata);
        SaveResult Save(FileModel model);
        IEnumerable<FileModel> Prebuild(ImmutableList<FileModel> models, IHostService host);
        void Build(FileModel model, IHostService host);
        IEnumerable<FileModel> Postbuild(ImmutableList<FileModel> models, IHostService host);
    }
}

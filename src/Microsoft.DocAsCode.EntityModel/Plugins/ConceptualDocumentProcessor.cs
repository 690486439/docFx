﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.EntityModel.Plugins
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Composition;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;

    using Microsoft.DocAsCode.Plugins;

    [Export(typeof(IDocumentProcessor))]
    public class ConceptualDocumentProcessor : IDocumentProcessor
    {
        private const string ConceputalKey = "conceptual";
        private const string DocumentTypeKey = "documentType";

        public string Name => nameof(ConceptualDocumentProcessor);

        public ProcessingPriority GetProcessingPriority(FileAndType file)
        {
            if (file.Type != DocumentType.Article)
            {
                return ProcessingPriority.NotSupportted;
            }
            if (".md".Equals(Path.GetExtension(file.File), StringComparison.OrdinalIgnoreCase))
            {
                return ProcessingPriority.Normal;
            }
            return ProcessingPriority.NotSupportted;
        }

        public FileModel Load(FileAndType file, ImmutableDictionary<string, object> metadata)
        {
            if (file.Type != DocumentType.Article)
            {
                throw new NotSupportedException();
            }
            var content = MarkdownReader.ReadMarkdownAsConceptual(file.BaseDir, file.File);
            foreach (var item in metadata)
            {
                if (!content.ContainsKey(item.Key))
                {
                    content[item.Key] = item.Value;
                }
            }
            return new FileModel(
                file,
                content,
                serializer: new BinaryFormatter())
            {
                LocalPathFromRepoRoot = (content["source"] as SourceDetail)?.Remote?.RelativePath
            };
        }

        public SaveResult Save(FileModel model)
        {
            if (model.Type != DocumentType.Article)
            {
                throw new NotSupportedException();
            }

            string path = Path.Combine(model.BaseDir, model.File);
            try
            {
                JsonUtility.Serialize(path, model.Content);
            }
            catch (PathTooLongException e)
            {
                Logger.LogError($"Path \"{path}\": {e.Message}");
                throw;
            }

            return new SaveResult
            {
                DocumentType = model.DocumentType ?? "Conceptual",
                ModelFile = model.File,
                LinkToFiles = model.Properties.LinkToFiles,
                LinkToUids = model.Properties.LinkToUids,
            };
        }

        public IEnumerable<FileModel> Prebuild(ImmutableList<FileModel> models, IHostService host)
        {
            return models;
        }

        public void Build(FileModel model, IHostService host)
        {
            if (model.Type != DocumentType.Article)
            {
                return;
            }
            var content = (Dictionary<string, object>)model.Content;
            var markdown = (string)content[ConceputalKey];
            var result = host.Markup(markdown, model.FileAndType);
            content[ConceputalKey] = result.Html;
            content["title"] = result.Title;
            content["word_count"] = WordCounter.CountWord(result.Html);
            if (result.YamlHeader != null && result.YamlHeader.Count > 0)
            {
                foreach (var item in result.YamlHeader)
                {
                    if (item.Key == "uid")
                    {
                        var uid = item.Value as string;
                        if (!string.IsNullOrWhiteSpace(uid))
                        {
                            model.Uids = new[] { uid }.ToImmutableArray();
                        }
                    }
                    else
                    {
                        content[item.Key] = item.Value;
                        if (item.Key == DocumentTypeKey)
                        {
                            model.DocumentType = item.Value as string;
                        }
                    }
                }
            }
            model.Properties.LinkToFiles = result.LinkToFiles;
            model.Properties.LinkToUids = result.LinkToUids;
            model.File = Path.ChangeExtension(model.File, ".json");
        }

        public IEnumerable<FileModel> Postbuild(ImmutableList<FileModel> models, IHostService host)
        {
            return models;
        }
    }
}

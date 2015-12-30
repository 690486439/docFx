// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using Microsoft.DocAsCode.EntityModel;
    using Microsoft.DocAsCode.EntityModel.Builders;
    using Microsoft.DocAsCode.Plugins;
    using Microsoft.DocAsCode.Utility;

    using Newtonsoft.Json.Linq;

    class BuildCommand : ICommand
    {
        private readonly string _helpMessage;

        public BuildJsonConfig Config { get; }

        public BuildCommand(CommandContext context)
            : this(new BuildJsonConfig(), context)
        {
        }

        public BuildCommand(JToken value, CommandContext context)
            : this(CommandFactory.ConvertJTokenTo<BuildJsonConfig>(value), context)
        {
        }

        public BuildCommand(BuildJsonConfig config, CommandContext context)
        {
            Config = MergeConfig(config, context);
        }

        public BuildCommand(Options options, CommandContext context)
        {
            var buildCommandOptions = options.BuildCommand;
            if (buildCommandOptions.IsHelp)
            {
                _helpMessage = HelpTextGenerator.GetHelpMessage(options, "build");
            }
            else
            {
                Config = MergeConfig(GetConfigFromOptions(buildCommandOptions), context);
            }

            if (!string.IsNullOrWhiteSpace(buildCommandOptions.Log)) Logger.RegisterListener(new ReportLogListener(buildCommandOptions.Log));

            if (buildCommandOptions.LogLevel.HasValue) Logger.LogLevelThreshold = buildCommandOptions.LogLevel.Value;
        }

        public void Exec(RunningContext context)
        {
            if (_helpMessage != null)
            {
                Console.WriteLine(_helpMessage);
            }
            else
            {
                InternalExec(Config, context);
            }
        }

        private void InternalExec(BuildJsonConfig config, RunningContext context)
        {
            var parameters = ConfigToParameter(config);
            if (parameters.Files.Count == 0)
            {
                Logger.LogWarning("No files found, nothing is to be generated");
                return;
            }

            BuildDocument(config);

            var documentContext = DocumentBuildContext.DeserializeFrom(parameters.OutputBaseDir);
            var assembly = typeof(Program).Assembly;

            if (config.Templates == null || config.Templates.Count == 0)
            {
                config.Templates = new ListWithStringFallback { Constants.DefaultTemplateName };
            }

            // If RootOutput folder is specified from command line, use it instead of the base directory
            var outputFolder = Path.Combine(config.OutputFolder ?? config.BaseDirectory ?? string.Empty, config.Destination ?? string.Empty);
            using (var manager = new TemplateManager(assembly, "Template", config.Templates, config.Themes, config.BaseDirectory))
            {
                manager.ProcessTemplateAndTheme(documentContext, outputFolder, true);
            }

            // TODO: SEARCH DATA

            if (config?.Serve ?? false)
            {
                ServeCommand.Serve(outputFolder, config.Port);
            }
        }

        private BuildJsonConfig MergeConfig(BuildJsonConfig config, CommandContext context)
        {
            config.BaseDirectory = context?.BaseDirectory ?? config.BaseDirectory;
            if (context?.SharedOptions != null)
            {
                config.OutputFolder = context.SharedOptions.RootOutputFolder ?? config.OutputFolder;
                var templates = context.SharedOptions.Templates;
                if (templates != null) config.Templates = new ListWithStringFallback(templates);
                var themes = context.SharedOptions.Themes;
                if (themes != null) config.Themes = new ListWithStringFallback(themes);
                config.Force |= context.SharedOptions.ForceRebuild;
                config.Serve |= context.SharedOptions.Serve;
                config.Port = context.SharedOptions.Port?.ToString();
            }
            return config;
        }

        private static DocumentBuildParameters ConfigToParameter(BuildJsonConfig config)
        {
            var parameters = new DocumentBuildParameters();
            var baseDirectory = config.BaseDirectory ?? Environment.CurrentDirectory;

            parameters.OutputBaseDir = Path.Combine(baseDirectory, "obj");
            if (config.GlobalMetadata != null) parameters.Metadata = config.GlobalMetadata.ToImmutableDictionary();
            if (config.FileMetadata != null) parameters.FileMetadata = ConvertToFileMetadataItem(baseDirectory, config.FileMetadata);
            parameters.ExternalReferencePackages = GetFilesFromFileMapping(GlobUtility.ExpandFileMapping(baseDirectory, config.ExternalReference)).ToImmutableArray();
            parameters.Files = GetFileCollectionFromFileMapping(
                baseDirectory,
                Tuple.Create(DocumentType.Article, GlobUtility.ExpandFileMapping(baseDirectory, config.Content)),
                Tuple.Create(DocumentType.Override, GlobUtility.ExpandFileMapping(baseDirectory, config.Overwrite)),
                Tuple.Create(DocumentType.Resource, GlobUtility.ExpandFileMapping(baseDirectory, config.Resource)));
            return parameters;
        }

        private static FileMetadata ConvertToFileMetadataItem(string baseDirectory, Dictionary<string, FileMetadataPairs> fileMetadata)
        {
            var result = new Dictionary<string, ImmutableArray<FileMetadataItem>>();
            foreach (var item in fileMetadata)
            {
                var list = new List<FileMetadataItem>();
                foreach (var pair in item.Value.Items)
                {
                    list.Add(new FileMetadataItem(pair.Glob, item.Key, pair.Value));
                }
                result.Add(item.Key, list.ToImmutableArray());
            }

            return new FileMetadata(baseDirectory, result);
        }

        private static IEnumerable<string> GetFilesFromFileMapping(FileMapping mapping)
        {
            if (mapping == null) yield break;
            foreach (var file in mapping.Items)
            {
                foreach (var item in file.Files)
                {
                    yield return Path.Combine(file.SourceFolder ?? Environment.CurrentDirectory, item);
                }
            }
        }

        private static FileCollection GetFileCollectionFromFileMapping(string baseDirectory, params Tuple<DocumentType, FileMapping>[] files)
        {
            var fileCollection = new FileCollection(baseDirectory);
            foreach (var file in files)
            {
                if (file.Item2 != null)
                {
                    foreach (var mapping in file.Item2.Items)
                    {
                        fileCollection.Add(file.Item1, mapping.Files, s => ConvertToDestinationPath(Path.Combine(baseDirectory, s), mapping.SourceFolder, mapping.DestinationFolder));
                    }
                }
            }

            return fileCollection;
        }

        private static string ConvertToDestinationPath(string path, string src, string dest)
        {
            var relativePath = PathUtility.MakeRelativePath(src, path);
            return Path.Combine(dest ?? string.Empty, relativePath);
        }

        private static BuildJsonConfig GetConfigFromOptions(BuildCommandOptions options)
        {
            string configFile = options.ConfigFile;
            if (string.IsNullOrEmpty(configFile) && options.Content == null && options.Resource == null)
            {
                if (!File.Exists(Constants.ConfigFileName))
                {
                    throw new ArgumentException("Either provide config file or specify content files to start building documentation.");
                }
                else
                {
                    Logger.Log(LogLevel.Info, $"Config file {Constants.ConfigFileName} found, start building...");
                    configFile = Constants.ConfigFileName;
                }
            }

            BuildJsonConfig config;
            if (!string.IsNullOrEmpty(configFile))
            {
                var command = (BuildCommand)CommandFactory.ReadConfig(configFile, null).Commands.FirstOrDefault(s => s is BuildCommand);
                if (command == null) throw new ApplicationException($"Unable to find {CommandType.Build} subcommand config in file '{Constants.ConfigFileName}'.");
                config = command.Config;
                config.BaseDirectory = Path.GetDirectoryName(configFile);
            }
            else
            {
                config = new BuildJsonConfig();
            }

            config.OutputFolder = options.OutputFolder;
            string optionsBaseDirectory = Environment.CurrentDirectory;
            // Override config file with options from command line
            if (options.Templates != null && options.Templates.Count > 0) config.Templates = new ListWithStringFallback(options.Templates);

            if (options.Themes != null && options.Themes.Count > 0) config.Themes = new ListWithStringFallback(options.Themes);
            if (!string.IsNullOrEmpty(options.OutputFolder)) config.Destination = Path.GetFullPath(Path.Combine(options.OutputFolder, config.Destination ?? string.Empty));
            if (options.Content != null)
            {
                if (config.Content == null) config.Content = new FileMapping(new FileMappingItem());
                config.Content.Add(new FileMappingItem() { Files = new FileItems(options.Content), SourceFolder = optionsBaseDirectory });
            }
            if (options.Resource != null)
            {
                if (config.Resource == null) config.Resource = new FileMapping(new FileMappingItem());
                config.Resource.Add(new FileMappingItem() { Files = new FileItems(options.Resource), SourceFolder = optionsBaseDirectory });
            }
            if (options.Overwrite != null)
            {
                if (config.Overwrite == null) config.Overwrite = new FileMapping(new FileMappingItem());
                config.Overwrite.Add(new FileMappingItem() { Files = new FileItems(options.Overwrite), SourceFolder = optionsBaseDirectory });
            }
            if (options.ExternalReference != null)
            {
                if (config.ExternalReference == null) config.ExternalReference = new FileMapping(new FileMappingItem());
                config.ExternalReference.Add(new FileMappingItem() { Files = new FileItems(options.ExternalReference), SourceFolder = optionsBaseDirectory });
            }
            if (options.Serve) config.Serve = options.Serve;
            if (options.Port.HasValue) config.Port = options.Port.Value.ToString();
            return config;
        }

        private static void BuildDocument(BuildJsonConfig config)
        {
            AppDomain builderDomain = null;
            try
            {
                string applicationBaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string pluginDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
                string pluginConfig = Path.Combine(pluginDirectory, "docfx.plugins.config");

                Logger.LogInfo($"Plug-in directory: {pluginDirectory}, configuration file: {pluginConfig}");

                AppDomainSetup setup = new AppDomainSetup
                {
                    ApplicationBase = applicationBaseDirectory,
                    PrivateBinPath = string.Join(";", applicationBaseDirectory, pluginDirectory),
                    ConfigurationFile = pluginConfig
                };

                builderDomain = AppDomain.CreateDomain("document builder domain", null, setup);

                // TODO: redirect logged items into current domain
                builderDomain.DoCallBack(new DocumentBuilderWrapper(pluginDirectory, config).BuildDocument);
            }
            catch (DocumentException ex)
            {
                Logger.LogWarning("document error:" + ex.Message);
            }
            finally
            {
                if (builderDomain != null)
                {
                    AppDomain.Unload(builderDomain);
                }
            }
        }

        #region DocumentBuilderWrapper

        [Serializable]
        class DocumentBuilderWrapper
        {
            private readonly string _pluginDirectory;
            private readonly BuildJsonConfig _config;

            public DocumentBuilderWrapper(string pluginDirectory, BuildJsonConfig config)
            {
                if (string.IsNullOrEmpty(pluginDirectory))
                {
                    throw new ArgumentNullException(nameof(_pluginDirectory));
                }

                if (config == null)
                {
                    throw new ArgumentNullException(nameof(config));
                }

                _pluginDirectory = pluginDirectory;
                _config = config;
            }

            public void BuildDocument()
            {
                var builder = new DocumentBuilder(LoadPluginAssemblies(_pluginDirectory));

                var parameters = ConfigToParameter(_config);
                if (parameters.Files.Count == 0)
                {
                    Logger.LogWarning("No files found, nothing is to be generated");
                    return;
                }

                builder.Build(parameters);
            }

            private static IEnumerable<Assembly> LoadPluginAssemblies(string pluginDirectory)
            {
                if (!Directory.Exists(pluginDirectory))
                {
                    yield break;
                }

                Logger.LogInfo($"Searching custom plug-ins in directory {pluginDirectory}...");

                foreach (var assemblyFile in Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    Assembly assembly = null;

                    // assume assembly name is the same with file name without extension
                    string assemblyName = Path.GetFileNameWithoutExtension(assemblyFile);
                    if (!string.IsNullOrEmpty(assemblyName))
                    {
                        try
                        {
                            assembly = Assembly.Load(assemblyName);
                            Logger.LogInfo($"Scanning assembly file {assemblyFile}...");
                        }
                        catch (Exception ex) when (ex is BadImageFormatException || ex is FileLoadException || ex is FileNotFoundException)
                        {
                            Logger.LogWarning($"Skipping file {assemblyFile} due to load failure: {ex.Message}");
                        }

                        if (assembly != null)
                        {
                            yield return assembly;
                        }
                    }
                }
            }
        }

        #endregion DocumentBuilderWrapper
    }
}

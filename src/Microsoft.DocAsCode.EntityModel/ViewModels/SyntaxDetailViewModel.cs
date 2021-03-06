// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.EntityModel.ViewModels
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using YamlDotNet.Serialization;

    [Serializable]
    public class SyntaxDetailViewModel
    {
        [YamlMember(Alias = "content")]
        [JsonProperty("content")]
        public string Content { get; set; }

        [YamlMember(Alias = "content.csharp")]
        [JsonProperty("content.csharp")]
        public string ContentForCSharp { get; set; }

        [YamlMember(Alias = "content.vb")]
        [JsonProperty("content.vb")]
        public string ContentForVB { get; set; }

        [YamlMember(Alias = "parameters")]
        [JsonProperty("parameters")]
        public List<ApiParameter> Parameters { get; set; }

        [YamlMember(Alias = "typeParameters")]
        [JsonProperty("typeParameters")]
        public List<ApiParameter> TypeParameters { get; set; }

        [YamlMember(Alias = "return")]
        [JsonProperty("return")]
        public ApiParameter Return { get; set; }

        public static SyntaxDetailViewModel FromModel(SyntaxDetail model)
        {
            if (model == null)
            {
                return null;
            }
            var result = new SyntaxDetailViewModel
            {
                Parameters = model.Parameters,
                TypeParameters = model.TypeParameters,
                Return = model.Return,
            };
            if (model.Content != null && model.Content.Count > 0)
            {
                result.Content = model.Content.GetLanguageProperty(SyntaxLanguage.Default);
                var contentForCSharp = model.Content.GetLanguageProperty(SyntaxLanguage.CSharp);
                if (result.Content != contentForCSharp)
                {
                    result.ContentForCSharp = contentForCSharp;
                }
                var contentForVB = model.Content.GetLanguageProperty(SyntaxLanguage.VB);
                if (result.Content != contentForVB)
                {
                    result.ContentForVB = contentForVB;
                }
            }
            return result;
        }

    }
}

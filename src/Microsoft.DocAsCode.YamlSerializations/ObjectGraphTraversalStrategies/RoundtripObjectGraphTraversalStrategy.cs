// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.YamlSerializations.ObjectGraphTraversalStrategies
{
    using System;
    using System.Globalization;
    using System.Linq;

    using YamlDotNet.Serialization;

    using Microsoft.DocAsCode.YamlSerializations.Helpers;

    /// <summary>
    /// An implementation of <see cref="IObjectGraphTraversalStrategy"/> that traverses
    /// properties that are read/write, collections and dictionaries, while ensuring that
    /// the graph can be regenerated from the resulting document.
    /// </summary>
    public class RoundtripObjectGraphTraversalStrategy : FullObjectGraphTraversalStrategy
    {
        public RoundtripObjectGraphTraversalStrategy(YamlSerializer serializer, ITypeInspector typeDescriptor, ITypeResolver typeResolver, int maxRecursion)
            : base(serializer, typeDescriptor, typeResolver, maxRecursion, null)
        {
        }

        protected override void TraverseProperties(IObjectDescriptor value, IObjectGraphVisitor visitor, int currentDepth)
        {
            if (!value.Type.HasDefaultConstructor() && !Serializer.Converters.Any(c => c.Accepts(value.Type)))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Type '{0}' cannot be deserialized because it does not have a default constructor or a type converter.", value.Type));
            }

            base.TraverseProperties(value, visitor, currentDepth);
        }
    }
}
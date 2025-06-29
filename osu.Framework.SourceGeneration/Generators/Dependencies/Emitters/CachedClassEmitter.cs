// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using osu.Framework.SourceGeneration.Generators.Dependencies.Data;

namespace osu.Framework.SourceGeneration.Generators.Dependencies.Emitters
{
    /// <summary>
    /// Emits the statement for a [Cached] attribute on the class itself.
    /// </summary>
    public class CachedClassEmitter : IStatementEmitter
    {
        private readonly DependenciesFileEmitter fileEmitter;
        private readonly CachedAttributeData data;

        public CachedClassEmitter(DependenciesFileEmitter fileEmitter, CachedAttributeData data)
        {
            this.fileEmitter = fileEmitter;
            this.data = data;
        }

        public IEnumerable<StatementSyntax> Emit()
        {
            yield return SyntaxFactory.ExpressionStatement(
                SyntaxHelpers.CacheDependencyInvocation(
                    fileEmitter.Target.GlobalPrefixedTypeName,
                    SyntaxFactory.IdentifierName(DependenciesFileEmitter.TARGET_PARAMETER_NAME),
                    data.GlobalPrefixedTypeName,
                    data.Name,
                    null
                )
            );
        }
    }
}

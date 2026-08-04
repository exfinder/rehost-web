using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

[CollectionDefinition(nameof(CodegenSubstrateCollection))]
public sealed class CodegenSubstrateCollection : ICollectionFixture<CodegenSubstrateFixture>;

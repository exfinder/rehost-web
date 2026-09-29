using Xunit;

namespace Rehost.Web.Tests.Compatibility.Compilation;

[CollectionDefinition(nameof(CodegenSubstrateCollection))]
public sealed class CodegenSubstrateCollection : ICollectionFixture<CodegenSubstrateFixture>;

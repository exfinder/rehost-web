using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

[CollectionDefinition(nameof(PageCompilationCollection))]
public sealed class PageCompilationCollection : ICollectionFixture<PageCompilationFixture>;

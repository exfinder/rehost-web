using Xunit;

namespace Rehost.Web.Tests.Compatibility.Compilation;

[CollectionDefinition(nameof(PageCompilationCollection))]
public sealed class PageCompilationCollection : ICollectionFixture<PageCompilationFixture>;

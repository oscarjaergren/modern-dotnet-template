namespace Api.IntegrationTests;

/// <summary>
/// Binds <see cref="ApiFixture" /> to every test class marked
/// <c>[Collection(nameof(ApiCollection))]</c>, so the distributed application starts once rather
/// than per class.
/// </summary>
[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>;

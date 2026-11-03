using Omnidas.Core;

namespace Omnidas.Core.Tests;

public class OmnidasInfoTests
{
    [Fact]
    public void Name_IsOmnidas() => Assert.Equal("Omnidas", OmnidasInfo.Name);
}

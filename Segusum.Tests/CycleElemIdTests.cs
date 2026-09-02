using Seg;

namespace Segusum.Tests;

public sealed class CycleElemIdTests
{
    [Fact]
    public void StringIdsAreStableAndLegacyIdsRemainReferenceBased()
    {
        Assert.Equal(new CycleElemId("pippo"), new CycleElemId("pippo"));
        Assert.NotEqual(new CycleElemId("pippo"), new CycleElemId("pluto"));
        Assert.NotEqual(new CycleElemId(), new CycleElemId());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyStringIdsAreRejected(string id)
    {
        Assert.Throws<ArgumentException>(() => new CycleElemId(id));
    }
}

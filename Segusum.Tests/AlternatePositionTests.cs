using System.Xml.Linq;
using Seg;

namespace Segusum.Tests;

public sealed class AlternatePositionTests
{
    [Fact]
    public void ConstructorKeepsStableIdAndRejectsEmptyIds()
    {
        var position = new AlternatePosition("shavingScene");

        Assert.Equal("shavingScene", position.serId);
        Assert.Equal("shavingScene", position.ToString());
        Assert.Throws<ArgumentException>(() => new AlternatePosition(" "));
    }

    [Fact]
    public void LogicObjectSerializesAlternatePositionAndNullMeansDefault()
    {
        var obj = new LogicObj { loId = "camilla" };
        obj.AlternatePos = new AlternatePosition("shavingScene");
        var serialized = new XElement("logicObj");

        obj.serialize(serialized);

        Assert.Equal("shavingScene", serialized.Attribute("alternatePos")?.Value);

        obj.AlternatePos = null;
        var defaultSerialized = new XElement("logicObj");
        obj.serialize(defaultSerialized);

        Assert.Null(defaultSerialized.Attribute("alternatePos"));
    }
}

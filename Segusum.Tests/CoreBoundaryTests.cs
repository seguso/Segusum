using Seg;

namespace Segusum.Tests;

public class CoreBoundaryTests
{
    [Fact]
    public void CoreCanBeUsedWithoutWebOrPersistenceAssemblies()
    {
        var obj = new LogicObj();
        Assert.NotNull(obj);
        var references = typeof(LogicObj).Assembly.GetReferencedAssemblies()
            .Select(x => x.Name)
            .Where(x => x is not null)
            .ToArray();
        Assert.DoesNotContain(references, x => x!.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, x => x!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, x => x == "Segusum.Persistence");
    }
}

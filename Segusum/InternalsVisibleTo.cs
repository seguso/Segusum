using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Segusum.AspNetCore")]
[assembly: InternalsVisibleTo("Segusum.Persistence")]
[assembly: InternalsVisibleTo("Segusum.Tests")]
// Temporary compatibility bridge while the private Litgir consumer still
// uses the engine's historical internal gameplay/test seams. Remove these
// friends when Litgir no longer relies on the legacy internal surface.
[assembly: InternalsVisibleTo("WebApiLitGir")]
[assembly: InternalsVisibleTo("WebApiLitGir.Tests")]

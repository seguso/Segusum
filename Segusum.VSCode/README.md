# Segusum DSL VS Code extension

Development setup:

1. Build the host from the repository root:
   `dotnet build Segusum.Tooling.Host/Segusum.Tooling.Host.csproj`
2. In this directory run `npm install` and `npm run compile`.
3. Open the repository in VS Code and press F5 on the extension directory.

The extension looks for `Segusum.Tooling.Host/bin/Debug/net8.0/Segusum.Tooling.Host.dll`.
Set `segusum.toolingHostPath` when the host is elsewhere.

The host uses the first SDK project containing `.seg` files when no project is
provided. The extension chooses the project containing the active workspace's
`.seg` files; multiple-world targeting remains explicit through each file's
`world` directive.

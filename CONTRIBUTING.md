# Contributing to Azure Connectors .NET SDK Samples

This project welcomes contributions and suggestions. Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For details, visit <https://cla.opensource.microsoft.com>.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) for local storage emulation

### Building

```bash
dotnet restore
dotnet build
```

### Running

```bash
cd DirectConnector
cp local.settings.json.template local.settings.json
# Edit local.settings.json with your connection runtime URLs
func start
```

## How to Contribute

1. Fork the repository
2. Create a topic branch from `main` (`git checkout -b feature/my-change`)
3. Make your changes
4. Verify the build succeeds (`dotnet build`)
5. Commit your changes (`git commit -m "Add my change"`)
6. Push to your fork (`git push origin feature/my-change`)
7. Open a pull request against `main`

### Pull Request Guidelines

- Keep PRs focused on a single change
- Update documentation if behavior changes
- Follow the existing code style

### Automated PR Validation

PRs targeting `main` must pass the `lint`, `build (ubuntu-latest)`, and `build (windows-latest)` CI checks. This validation restores dependencies, builds the samples, and runs the tests. The Ubuntu run also generates and uploads the code coverage report artifact.

This validation does not publish packages, create a release, or change the current package version.

### Reporting Issues

- Use [GitHub Issues](https://github.com/Azure/Connectors-NET-Samples/issues) to report bugs or request features
- Search existing issues before creating a new one
- Use the provided issue templates when available

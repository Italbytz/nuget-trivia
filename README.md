# nuget-trivia

`nuget-trivia` bundles the reusable `Italbytz.Trivia.*` package family for question models and Open Trivia DB integration.

It is intended for developers who need trivia-style question contracts that can be reused across exam, quiz, demo, and teaching scenarios.

## Which package should I use?

- Use `Italbytz.Trivia.Abstractions` for shared contracts such as `IQuestion`, `IMultipleChoiceQuestion`, `IYesNoQuestion`, `Difficulty`, `Choices`, and `QuestionType`.
- Use `Italbytz.Trivia.OpenTriviaDb` for Open Trivia DB access, category and token handling, and mapping external payloads to the shared trivia model.

## Quality checks

This repository includes:

- a `GitHub Actions` workflow in `.github/workflows/ci.yml`
- automated `restore`, `build`, and `pack`
- package metadata and packed README support via `Directory.Build.props`

## Release model

- the current `nuget-trivia` line is published as a stable `1.0.0` package family
- a pushed tag such as `v1.0.0` triggers the release-ready pipeline in GitHub Actions
- if the repository secret `NUGET_API_KEY` is configured, the workflow also publishes `.nupkg` and `.snupkg` files to NuGet

## Local validation

```bash
dotnet restore nuget-trivia.sln
dotnet build nuget-trivia.sln -c Release
dotnet pack nuget-trivia.sln -c Release
```
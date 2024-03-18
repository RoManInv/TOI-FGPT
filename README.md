# TOI-FGPT

This solution is developed based on [FlashGPT3](https://doi.org/10.5281/zenodo.5504362)[1] proposed by Microsoft. OpenAI API token is required to run the experiments. `flashgpt3/cache/default.json` and `flashgpt3/cache/toi.json` are cache files for queries and TOIs, respectively. Delete `flashgpt3/cache/default.json` to obtain new query results. Delete `flashgpt3/cache/toi.json` and set TOILearning = true to obtain new TOIs.

## Run the experiments

The easiest way to get started is installing Visual Studio and loading the solution (`TOI-FGPT.sln`). All required dependencies can be easily [installed through NuGet](https://docs.microsoft.com/en-us/nuget/consume-packages/package-restore#restore-using-visual-studio). Code was tested in a [.NET 6.0](https://dotnet.microsoft.com/download/dotnet/6.0) environment.

## Related Links
* [FlashGPT3](https://doi.org/10.5281/zenodo.5504362)[1]
* [PROSE](https://www.microsoft.com/en-us/research/group/prose/)


[1] [Gust Verbruggen, Vu Le, and Sumit Gulwani. 2021. Semantic programming by example with pre-trained models. Proc. ACM Program. Lang. 5, OOPSLA, Article 100 (October 2021), 25 pages. https://doi.org/10.1145/3485477](https://doi.org/10.1145/3485477)

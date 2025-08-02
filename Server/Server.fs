open System
open System.Threading.Tasks

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting

open Orleans
open Orleans.Hosting

open Grains

[<EntryPoint>]
let main args =

    let builder = WebApplication.CreateBuilder(args)

    builder.Host.UseOrleans(fun (siloBuilder: ISiloBuilder) ->
        siloBuilder.UseLocalhostClustering().AddMemoryGrainStorageAsDefault().AddMemoryGrainStorage("urls")
        |> ignore)
    |> ignore

    let app = builder.Build()

    app.MapGet("/", Func<string>(fun () -> "Welcome to the URL shortener, powered by Orleans!"))
    |> ignore

    app.MapGet(
        "/shorten",
        Func<IGrainFactory, HttpRequest, string, Task<IResult>>(fun grains request url ->
            task {
                let host = $"{request.Scheme}://{request.Host.Value}"

                // Validate the URL query string
                let urlIsNullOfWhiteSpace = String.IsNullOrWhiteSpace url
                let urlIsWellFormed = Uri.IsWellFormedUriString(url, UriKind.Absolute)

                if urlIsNullOfWhiteSpace || not urlIsWellFormed then
                    let errorMsg =
                        $"""
                    The URL query string is required and needs to be well formed.
                    Consider, ${host}/shorten?url=https://www.microsoft.com.
                    """

                    return Results.BadRequest(errorMsg)
                else
                    // Create a unique, short ID
                    let shortenedRouteSegment = Guid.NewGuid().GetHashCode().ToString "X"

                    // Create and persist a grain with the shortened ID and full URL
                    let shortenerGrain = grains.GetGrain<IUrlShortenerGrain>(shortenedRouteSegment)

                    do! shortenerGrain.SetUrl(url)

                    // Return the shortened URL for later use
                    let resultBuilder = new UriBuilder(host)
                    resultBuilder.Path <- $"/go/{shortenedRouteSegment}"

                    return Results.Ok(resultBuilder.Uri)
            })
    )
    |> ignore

    app.MapGet(
        "/go/{shortenedRouteSegment:required}",
        Func<IGrainFactory, string, Task<IResult>>(fun grains shortenedRouteSegment ->
            task {
                // Retrieve the grain using the shortened ID and url to the original URL
                let shortenerGrain = grains.GetGrain<IUrlShortenerGrain>(shortenedRouteSegment)

                let! url = shortenerGrain.GetUrl()

                // Handles missing schemes, defaults to "http://".
                let redirectBuilder = new UriBuilder(url)

                return Results.Redirect(redirectBuilder.Uri.ToString())
            })
    )
    |> ignore

    app.Run()

    0 // exit code

namespace Grains

open System.Threading.Tasks

open Orleans
open Orleans.Runtime

// Define F# record types
[<GenerateSerializer>]
type UrlDetails =
    { [<Id(0u)>]
      FullUrl: string
      [<Id(1u)>]
      ShortenedRouteSegment: string }

// Define a grain interface
type IUrlShortenerGrain =
    inherit IGrainWithStringKey
    /// SetUrl method persists the original and their corresponding shortened URLs
    abstract member SetUrl: fullUrl: string -> Task
    /// GetUrl method retrieves the original URL given the shortened URL
    abstract member GetUrl: unit -> Task<string>

type UrlShortenerGrain([<PersistentState(stateName = "url", storageName = "urls")>] state: IPersistentState<UrlDetails>)
    =
    inherit Grain()

    interface IUrlShortenerGrain with
        member this.SetUrl(fullUrl: string) =
            state.State <-
                { ShortenedRouteSegment = this.GetPrimaryKeyString()
                  FullUrl = fullUrl }

            state.WriteStateAsync()

        member this.GetUrl() = state.State.FullUrl |> Task.FromResult

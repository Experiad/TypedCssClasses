//
// Modified for Zanaptak.TypedCssClasses by zanaptak.
//

/// Helper functions called from the generated code for working with files
module internal Zanaptak.TypedCssClasses.Internal.FSharp.Data.Runtime.IO

open System
open System.Collections.Generic
open System.IO
open System.Text
open Zanaptak.TypedCssClasses.Internal.FSharp.Data

#if LOGGING_ENABLED

let private logLock = obj()
let mutable private indentation = 0

let private appendToLogMultiple logFile lines = lock logLock <| fun () ->
    let path = logFile // Path.Combine( __SOURCE_DIRECTORY__ , logFile ) // "/../../" + logFile
    use stream = File.Open(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)
    use writer = new StreamWriter(stream)
    for (line:string) in lines do
        writer.WriteLine(line.Replace("\r", null).Replace("\n","\\n"))
    writer.Flush()

let private appendToLog logFile line =
    appendToLogMultiple logFile [line]

let internal log str =
#if TIMESTAMPS_IN_LOG
    "[" + DateTime.Now.TimeOfDay.ToString() + "] " + String(' ', indentation * 2) + str
#else
    String(' ', indentation * 2) + str
#endif
    |> appendToLog "Zanaptak.TypedCssClasses.log.txt"

let internal logWithStackTrace (str:string) =
    let stackTrace =
        Environment.StackTrace.Split '\n'
        |> Seq.skip 3
        |> Seq.truncate 5
        |> Seq.map (fun s -> s.TrimEnd())
        |> Seq.toList
    str::stackTrace |> appendToLogMultiple "Zanaptak.TypedCssClasses.log.txt"

open System.Diagnostics
open System.Threading

let internal logTime category (instance:string) =

    log (sprintf "%s %s" category instance)
    Interlocked.Increment &indentation |> ignore

    let s = Stopwatch()
    s.Start()

    { new IDisposable with
        member __.Dispose() =
            s.Stop()
            Interlocked.Decrement &indentation |> ignore
            log (sprintf "Finished %s [%dms]" category s.ElapsedMilliseconds)
            let instance = instance.Replace("\r", null).Replace("\n","\\n")
            sprintf "%s|%s|%d" category instance s.ElapsedMilliseconds
            |> appendToLog "Zanaptak.TypedCssClasses.log.csv" }

#else

let internal dummyDisposable = { new IDisposable with member __.Dispose() = () }
let inline internal log (_:string) = ()
let inline internal logWithStackTrace (_:string) = ()
let inline internal logTime (_:string) (_:string) = dummyDisposable

#endif

type private FileWatcher(path) =

    let subscriptions = Dictionary<string, unit -> unit>()

    let getLastWrite() = File.GetLastWriteTime path
    let mutable lastWrite = getLastWrite()

    let watcher =
        new FileSystemWatcher(
            Filter = Path.GetFileName path,
            Path = Path.GetDirectoryName path,
            EnableRaisingEvents = true)

    let checkForChanges action _ =
        let curr = getLastWrite()

        if lastWrite <> curr then
            log (sprintf "Watcher detected file %s: %s" action path)
            lastWrite <- curr
            // creating a copy since the handler can be unsubscribed during the iteration
            let handlers = subscriptions.Values |> Seq.toArray
            for handler in handlers do
                handler()

    do
        watcher.Changed.Add (checkForChanges "changed")
        watcher.Renamed.Add (checkForChanges "renamed")
        watcher.Deleted.Add (checkForChanges "deleted")

    member __.Subscribe(name, action) =
        subscriptions.Add(name, action)

    member __.Unsubscribe(name) =
        if subscriptions.Remove(name) then
            log (sprintf "Unsubscribed %s from %s watcher" name path)
            if subscriptions.Count = 0 then
                log (sprintf "Disposing %s watcher" path)
                watcher.Dispose()
                true
            else
                false
        else
            false

let private watchers = Dictionary<string, FileWatcher>()

// sets up a filesystem watcher that calls the invalidate function whenever the file changes
let watchForChanges path (owner, onChange) =

    let watcher =

        lock watchers <| fun () ->

            match watchers.TryGetValue(path) with
            | true, watcher ->

                log (sprintf "Reusing %s watcher" path)
                watcher.Subscribe(owner, onChange)
                watcher

            | false, _ ->

                log (sprintf "Setting up %s watcher" path)
                let watcher = FileWatcher path
                watcher.Subscribe(owner, onChange)
                watchers.Add(path, watcher)
                watcher

    { new IDisposable with
        member __.Dispose() =
            lock watchers <| fun () ->
                if watcher.Unsubscribe(owner) then
                    watchers.Remove(path) |> ignore
    }

type internal UriResolutionType =
    | DesignTime
    | Runtime
    | RuntimeInFSI

let internal isWeb (uri:Uri) = uri.IsAbsoluteUri && not uri.IsUnc && uri.Scheme <> "file"

type internal UriResolver =

    { ResolutionType : UriResolutionType
      DefaultResolutionFolder : string
      ResolutionFolder : string }

    static member Create(resolutionType, defaultResolutionFolder, resolutionFolder) =
      { ResolutionType = resolutionType
        DefaultResolutionFolder = defaultResolutionFolder
        ResolutionFolder = resolutionFolder }

    /// Resolve the absolute location of a file (or web URL) according to the rules
    /// used by standard F# type providers as described here:
    /// https://github.com/fsharp/fsharpx/issues/195#issuecomment-12141785
    ///
    ///  * if it is web resource, just return it
    ///  * if it is full path, just return it
    ///  * otherwise.
    ///
    ///    At design-time:
    ///      * if the user specified resolution folder, use that
    ///      * otherwise use the default resolution folder
    ///    At run-time:
    ///      * if the user specified resolution folder, use that
    ///      * if it is running in F# interactive (config.IsHostedExecution)
    ///        use the default resolution folder
    ///      * otherwise, use 'CurrentDomain.BaseDirectory'
    /// returns an absolute uri * isWeb flag
    member x.Resolve(uri:Uri) =
      let orCurrentDirIfEmpty dir =
        if String.IsNullOrWhiteSpace dir then
          log "Dir value is empty, using env curr dir"
          Environment.CurrentDirectory
        else dir

      if uri.IsAbsoluteUri then
        uri, isWeb uri
      else
        try
          log ( sprintf "Resolving path for: %s" uri.OriginalString )
          let root =
            match x.ResolutionType with
            | DesignTime ->
              log "ResolutionType = DesignTime"
              if String.IsNullOrWhiteSpace x.ResolutionFolder
              then
                log "No resolution folder param, using default config"
                x.DefaultResolutionFolder |> orCurrentDirIfEmpty
              else
                log "Using resolution folder param"
                x.ResolutionFolder
            | RuntimeInFSI ->
              log "ResolutionType = RuntimeInFSI, using default config"
              x.DefaultResolutionFolder |> orCurrentDirIfEmpty
            | Runtime ->
              log "ResolutionType = Runtime, using appdomain base"
              AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/')
          log ( sprintf "Root path = %s" root )
          let resolved = Uri( Path.GetFullPath( Path.Combine(root, uri.OriginalString) ) , UriKind.Absolute), false
          log ( sprintf "Resolved path = %s" ( fst resolved ).OriginalString )
          resolved
        with
        | ex ->
          log ( sprintf "%A" ex )
          reraise ()

/// Opens a stream to the uri using the uriResolver resolution rules
/// It the uri is a file, uses shared read, so it works when the file locked by Excel or similar tools,
/// and sets up a filesystem watcher that calls the invalidate function whenever the file changes
let internal asyncRead (uriResolver:UriResolver) formatName encodingStr (uri:Uri) =
  let uri, isWeb = uriResolver.Resolve uri
  if isWeb then
    async {
        let contentTypes =
            match formatName with
            | "CSV" -> [ HttpContentTypes.Csv ]
            | "HTML" -> [ HttpContentTypes.Html ]
            | "JSON" -> [ HttpContentTypes.Json ]
            | "XML" -> [ HttpContentTypes.Xml ]
            | "CSS" -> [ HttpContentTypes.Css ]
            | _ -> []
            @ [ HttpContentTypes.Any ]
        let headers = [ HttpRequestHeaders.UserAgent ("F# Data " + formatName + " Type Provider")
                        HttpRequestHeaders.Accept (String.concat ", " contentTypes) ]
        // Download the whole web resource at once, otherwise with some servers we won't get the full file
        let! text = Http.AsyncRequestString(uri.OriginalString, headers = headers, responseEncodingOverride = encodingStr)
        return new StringReader(text) :> TextReader
    }, None
  else
    let path = uri.OriginalString.Replace(Uri.UriSchemeFile + "://", "")
    async {
        let file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        let encoding = if encodingStr = "" then Encoding.UTF8 else HttpEncodings.getEncoding encodingStr
        return new StreamReader(file, encoding) :> TextReader
    }, Some path

let private withUri uri f =
  match Uri.TryCreate(uri, UriKind.RelativeOrAbsolute) with
  | false, _ -> failwithf "Invalid uri: %s" uri
  | true, uri -> f uri

/// Returns a TextReader for the uri using the runtime resolution rules
let asyncReadTextAtRuntime forFSI defaultResolutionFolder resolutionFolder formatName encodingStr uri =
  withUri uri <| fun uri ->
    let resolver = UriResolver.Create((if forFSI then RuntimeInFSI else Runtime),
                                      defaultResolutionFolder, resolutionFolder)
    asyncRead resolver formatName encodingStr uri |> fst

/// Returns a TextReader for the uri using the designtime resolution rules
let asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder formatName encodingStr uri =
  withUri uri <| fun uri ->
    let resolver = UriResolver.Create(DesignTime, defaultResolutionFolder, resolutionFolder)
    asyncRead resolver formatName encodingStr uri |> fst


namespace Zanaptak.TypedCssClasses

open Zanaptak.TypedCssClasses.Internal.FSharp.Data.Runtime
open Zanaptak.TypedCssClasses.Internal.ProviderImplementation
open Zanaptak.TypedCssClasses.Internal.ProviderImplementation.ProvidedTypes
open Zanaptak.TypedCssClasses.Internal.ProviderImplementation.ProviderHelpers
open FSharp.Core.CompilerServices
open System
open System.Reflection

module TypeProvider =

  [< TypeProvider >]
  type CssClassesTypeProvider ( config : TypeProviderConfig ) as this =
    inherit DisposableTypeProviderForNamespaces( config )

#if LOGGING_ENABLED
    do
      IO.log ( sprintf "TypeProviderConfig.IsHostedExecution = %b" config.IsHostedExecution )
      IO.log ( sprintf "TypeProviderConfig.IsInvalidationSupported = %b" config.IsInvalidationSupported )
      IO.log ( sprintf "TypeProviderConfig.TemporaryFolder = %s" config.TemporaryFolder )
      if config.ResolutionFolder = null then
        IO.log "TypeProviderConfig.ResolutionFolder is null"
      elif config.ResolutionFolder = "" then
        IO.log "TypeProviderConfig.ResolutionFolder is empty string"
      else
        IO.log ( sprintf "TypeProviderConfig.ResolutionFolder = %s" config.ResolutionFolder )
      IO.log ( sprintf "TypeProviderConfig.RuntimeAssembly = %s" config.RuntimeAssembly )

      IO.log ( sprintf "TypeProviderConfig.ReferencedAssemblies count = %i" ( Array.length config.ReferencedAssemblies ) )
      config.ReferencedAssemblies
      |> Array.truncate 5
      |> Array.iter ( fun s -> IO.log ( sprintf "    %s" s ) )
      if Array.length config.ReferencedAssemblies > 5 then IO.log "    ..."

      IO.log ( sprintf "Environment.CommandLine = %s" Environment.CommandLine )
      IO.log ( sprintf "Environment.CurrentDirectory = %s" Environment.CurrentDirectory )
#endif

    let ns = "Zanaptak.TypedCssClasses"
    let asm = Assembly.GetExecutingAssembly()

    let parentType = ProvidedTypeDefinition( asm , ns , "CssClasses" , Some ( typeof< obj > ) )

    let buildTypes ( typeName : string ) ( args : obj[] ) =

      let source = args.[ 0 ] :?> string
      let naming = args.[ 1 ] :?> Naming
      let resolutionFolder = args.[ 2 ] :?> string
      let getProperties = args.[ 3 ] :?> bool
      let nameCollisions = args.[ 4 ] :?> NameCollisions

      let getSpec _ value =

        let cssClasses = Utils.parseCss value naming nameCollisions

        //using ( IO.logTime "TypeGeneration" source ) <| fun _ ->

        let cssType = ProvidedTypeDefinition( asm , ns , typeName , Some ( typeof< obj > ) )

        cssClasses
        |> Seq.iter ( fun c ->
          let propName , propValue = c.Name , c.Value
          let prop = ProvidedProperty( propName , typeof< string > , isStatic = true , getterCode = ( fun _ -> <@@ propValue @@> ) )
          prop.AddXmlDoc( Utils.escapeHtml propValue )
          cssType.AddMember prop
        )

        if getProperties then
          let rowType = ProvidedTypeDefinition("Property", Some(typeof<string[]>), hideObjectMethods = true)
          let rowNameProp = ProvidedProperty("Name", typeof<string>, getterCode = fun (Singleton row) -> <@@ (%%row:string[]).[0] @@>)
          rowNameProp.AddXmlDoc "Generated property name using specified naming strategy."
          let rowValueProp = ProvidedProperty("Value", typeof<string>, getterCode = fun (Singleton row) -> <@@ (%%row:string[]).[1] @@>)
          rowValueProp.AddXmlDoc "The underlying CSS class value."

          rowType.AddMember rowNameProp
          rowType.AddMember rowValueProp
          cssType.AddMember rowType

          let propsArray = cssClasses |> Seq.map ( fun p -> [| p.Name ; p.Value |] ) |> Seq.toArray
          let usedNames = cssClasses |> Seq.map ( fun p -> p.Name ) |> Set.ofSeq
          let methodName =
            Seq.init 99 ( fun i -> "GetProperties" + if i = 0 then "" else "_" + string ( i + 1 ) )
            |> Seq.find ( fun s -> usedNames |> Set.contains s |> not )
          let staticMethod =
            ProvidedMethod(methodName, [], typedefof<seq<_>>.MakeGenericType(rowType), isStatic = true,
              invokeCode = fun _-> <@@ propsArray @@>)

          cssType.AddMember staticMethod

        {
          GeneratedType = cssType
          RepresentationType = cssType
          CreateFromTextReader = fun _ -> failwith "Not Applicable"
          CreateFromTextReaderForSampleList = fun _ -> failwith "Not Applicable"
        }

      generateType' "CSS" ( Sample source ) getSpec this config "" resolutionFolder "" typeName None

    let parameters = [
      ProvidedStaticParameter( "source" , typeof< string >, parameterDefaultValue = "" )
      ProvidedStaticParameter( "naming" , typeof< Naming >, parameterDefaultValue = Naming.Verbatim )
      ProvidedStaticParameter( "resolutionFolder" , typeof< string >, parameterDefaultValue = "" )
      ProvidedStaticParameter( "getProperties" , typeof< bool >, parameterDefaultValue = false )
      ProvidedStaticParameter( "nameCollisions" , typeof< NameCollisions >, parameterDefaultValue = NameCollisions.BasicSuffix )
    ]

    let helpText = """
      <summary>Typed CSS classes. Provides generated properties representing CSS classes from a stylesheet.</summary>
      <param name='source'>Location of a CSS stylesheet (file path or web URL), or a string containing CSS text.</param>
      <param name='naming'>Naming strategy for class name properties.
        One of: Naming.Verbatim (default), Naming.Underscores, Naming.CamelCase, Naming.PascalCase.</param>
      <param name='resolutionFolder'>A directory that is used when resolving relative file references.</param>
      <param name='getProperties'>Adds a GetProperties() method that returns a seq of all generated property name/value pairs.</param>
      <param name='nameCollisions'>Behavior of name collisions that arise from naming strategy.
        One of: NameCollisions.BasicSuffix (default), NameCollisions.ExtendedSuffix, NameCollisions.Omit. </param>
    """

    do parentType.AddXmlDoc helpText
    do parentType.DefineStaticParameters( parameters , buildTypes )

    do this.AddNamespace( ns, [ parentType ] )

[< TypeProviderAssembly >]
do ()

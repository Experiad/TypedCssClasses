# TypedCssClasses

A CSS class type provider for F# web development.

Bring external stylesheet classes into your F# code as design-time discoverable compiler-verified properties.

## Demo

Check out the [Fable Tailwind sample](https://github.com/zanaptak/TypedCssClasses/tree/master/sample/FableTailwind) to see it in action.

## Code examples

The following examples use [Fable.React](https://fable.io/blog/Announcing-Fable-React-5.html) view syntax, but any other web framework should work as well since the provided properties compile to strings.

### Bootstrap CSS

```fs
type Bootstrap = CssClasses<"https://stackpath.bootstrapcdn.com/bootstrap/4.3.1/css/bootstrap.min.css", Naming.PascalCase>

div [ ClassName Bootstrap.Card ] [
    div [ ClassName Bootstrap.CardBody ] [
        h5 [ ClassName Bootstrap.CardTitle ] [ str "A clever title" ]
        p [ ClassName Bootstrap.CardText ] [ str "Lorem ipsum dolor sit amet." ]
    ]
]
```

### Font Awesome CSS

```fs
type icon = CssClasses<"static/font-awesome/css/all.css", Naming.Underscores> // example using local CSS

i [ classList [
    icon.far, true
    icon.fa_grin, true ] ] []

i [ classList [
    icon.fas, true
    icon.fa_thumbs_up, true ] ] []
```

### Tailwind CSS

```fs
type tw = CssClasses<"https://unpkg.com/tailwindcss@^1.0/dist/tailwind.min.css", Naming.Verbatim>

div [ ClassName <| String.concat " " [
        tw.``bg-red-200``
        tw.``hover:bg-red-500``
        tw.``hover:font-bold``

        tw.``sm:text-2xl``
        tw.``sm:bg-green-200``
        tw.``sm:hover:bg-green-500``

        tw.``lg:text-4xl``
        tw.``lg:bg-blue-200``
        tw.``lg:hover:bg-blue-500`` ]
] [ str "Resize me! Hover me!" ]
```

#### CSS bundle size

When using a custom-generated Tailwind CSS file in your local build process, the [Tailwind documentation](https://tailwindcss.com/docs/controlling-file-size) recommends using PurgeCSS to reduce your CSS bundle size. With the `Naming.Verbatim` option, that technique will work with this library as well. Include your `*.fs` view files in the PurgeCSS content definition, and it will identify the classes you are actually using and remove unused classes from the final bundle.

Try it out with the [Fable Tailwind sample](https://github.com/zanaptak/TypedCssClasses/tree/master/sample/FableTailwind).

## Getting started

Add the [NuGet package](https://www.nuget.org/packages/Zanaptak.TypedCssClasses) to your project:
```
dotnet add package Zanaptak.TypedCssClasses
```

If the project was already open in an IDE, you might want to close and restart it to make sure the type provider loads into the process correctly.

Write some code:
```fs
open Zanaptak.TypedCssClasses

// Define a type for a CSS source.
// Can be file path, web URL, or CSS text.
type css = CssClasses<"https://stackpath.bootstrapcdn.com/bootstrap/4.3.1/css/bootstrap.min.css">

// Enjoy your typed properties!
let x = css.``display-1``
```

Works with Visual Studio Code (with Ionide-fsharp extension) on Windows and Linux, and Visual Studio 2019 on Windows. (Other environments/IDEs have not been tested by me.)

## Parameters

### source

The source CSS to process. Can be a file path, web URL, or CSS text.

### naming

* `Naming.Verbatim`: (default) use class names verbatim from source CSS, requiring backtick-quotes for names with special characters.
* `Naming.Underscores`: replace all non-alphanumeric characters with underscores.
* `Naming.CamelCase`: convert to camel case names with all non-alphanumeric characters removed.
* `Naming.PascalCase`: convert to Pascal case names with all non-alphanumeric characters removed.

Note that non-verbatim naming options can produce name collisions. See the [`nameCollisions`](#nameCollisions) parameter for details.

### resolutionFolder

A custom folder to use for resolving relative file paths. The default is the project root folder.

To have nested code files referencing CSS files in the same directory without having to specify the entire path from project root, you can use the built-in F# `__SOURCE_DIRECTORY__` value:

```fs
type css = CssClasses<"file-in-same-dir.css", resolutionFolder=__SOURCE_DIRECTORY__>
```

### getProperties

If true, the type will include a `GetProperties()` method that returns a sequence of the generated property name/value pairs. You could use this, for example, to produce a simple hard-coded standalone set of bindings by running the following `.fsx` script:

```fs
// NOTE: update this reference to a valid assembly location on your machine
#r "YOUR_USER_HOME_DIRECTORY/.nuget/packages/zanaptak.typedcssclasses/0.1.0/lib/netstandard2.0/Zanaptak.TypedCssClasses.dll"
open Zanaptak.TypedCssClasses
type css = CssClasses<"https://cdnjs.cloudflare.com/ajax/libs/materialize/1.0.0/css/materialize.min.css", Naming.CamelCase, getProperties=true>
css.GetProperties()
|> Seq.sortBy (fun p -> p.Name)
|> Seq.iter (fun p -> printfn "let %s = \"%s\"" p.Name p.Value)
```

Example output:
```
let accent1 = "accent-1"
let accent2 = "accent-2"
let accent3 = "accent-3"
let accent4 = "accent-4"
let activator = "activator"
...
```

### nameCollisions

If a naming option produces collisions, such as `card-text` and `card_text` CSS classes both mapping to a `CardText` property in Pascal case, then the duplicate names will be handled according to this option.

- `NameCollisions.BasicSuffix`: (default) The base property name will refer to the closest text match, and additional properties will receive `_2`, `_3`, etc. suffixes as needed. Note that if this option is used during ongoing CSS development, it can cause existing properties to silently change to refer to different classes if collisions are introduced that affect the base name and number determination.
- `NameCollisions.ExtendedSuffix`: All property names involved in a collision will receive an extended numbered suffix such as `__1_of_2`,  `__2_of_2`. This option is safer for ongoing development since any introduced collision will change all involved names and produce immediate compiler errors where the previous names were used.
- `NameCollisions.Omit`: All colliding properties will be omitted from the generated type. This option is safer for ongoing development since any introduced collision will remove the original property and produce immediate compiler errors wherever it was used.

## Notes

As with all type providers, the source CSS file must be available at both design time and build time.

This provider does not use formal CSS parsing, just identifying classes by typical selector patterns. It's working on the major CSS frameworks but may fail on more obscure CSS syntax.

Web URLs are expected to use static CDN or otherwise unchanging content and are cached on the local filesystem with a 90-day expiration.

If using Fable, update fable-compiler to version 2.3.17 or later to avoid an issue with the type provider failing to resolve relative file paths.

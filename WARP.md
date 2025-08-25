# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Commands

### Building

To build the `inklecate` compiler, run the following command from the root directory:

```bash
dotnet build -c Release inklecate/inklecate.csproj
```

To create a release build for all platforms, run:

```bash
./build_release.command
```

### Running Tests

The tests are written in C# using the NUnit framework. To run the tests, you can use the `dotnet test` command in the `tests` directory:

```bash
dotnet test tests/tests.csproj
```

## Architecture

The core of this repository is the **ink** scripting language, which is designed for writing interactive narrative. The main components are:

- **`compiler/`**: The C# source code for the ink compiler.
- **`ink-engine-runtime/`**: The C# runtime engine for ink.
- **`inklecate/`**: The command-line compiler for ink.
- **`tests/`**: NUnit tests for the ink compiler and runtime.

The general workflow for using ink is:

1. Write a story in a `.ink` file.
2. Compile the `.ink` file to a `.json` file using `inklecate`.
3. Load and run the `.json` file in a game engine using the C# or JavaScript runtime.


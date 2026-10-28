# Third-party binaries

These assemblies are the unmodified `netstandard2.0` builds from NuGet. `Google.Protobuf.dll` is the runtime used by
the generated `Game.cs`; the `System.*` assemblies are the ones it references and that Unity's Mono profile does not
always provide.

| Assembly | Version | Licence |
|---|---|---|
| Google.Protobuf.dll | 3.36.2 (matches `protoc` 4.36.2 used by the Java build) | BSD-3-Clause, Copyright Google Inc. |
| System.Memory.dll | 4.5.3 | MIT, .NET Foundation |
| System.Buffers.dll | 4.5.1 | MIT, .NET Foundation |
| System.Runtime.CompilerServices.Unsafe.dll | 4.5.2 | MIT, .NET Foundation |
| System.Numerics.Vectors.dll | 4.5.0 | MIT, .NET Foundation |

"Validate References" is switched off in their `.meta` files: Unity otherwise refuses to load a plugin whose
dependency it cannot resolve in its own profile. If Unity reports a duplicate assembly (for example because a package
such as Collections already ships `System.Runtime.CompilerServices.Unsafe.dll`), delete the duplicate from this folder.

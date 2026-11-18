# Bouncy Castle dependency

- Package: BouncyCastle.Cryptography 2.7.0, MIT (LICENSE.md).
- Source: https://www.nuget.org/packages/BouncyCastle.Cryptography/2.7.0
- Download: https://api.nuget.org/v3-flatcontainer/bouncycastle.cryptography/2.7.0/bouncycastle.cryptography.2.7.0.nupkg
- Selected assembly: lib/netstandard2.0/BouncyCastle.Cryptography.dll, no NuGet transitive dependencies.
- nupkg SHA-256: f091ffccab4d03993e660bace277659a79dee0972f54d7f1f4bd46d680966241
- DLL SHA-256: d61c1f2ba929a230a58e101ccd850e21f2675fa6b9814ec279633e8a089c3495
- Retrieved 2026-11-17. DLL is checked in for reproducible/offline Unity import.

Uses X25519 (RFC 7748), HKDF-SHA256 (RFC 5869), AES-256-GCM. The dependency is a general cryptography library, not a claim of certified/FIPS operation. Native Unity Mono execution is tested with the actual player build.

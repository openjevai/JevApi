# OpenJEV support

This fork adds **optional** support for [OpenJEV](https://openjev.sh), a free community gateway to the same Jev model built by [TypeSafe](https://typesafe.ai). **TypeSafe remains the default** — anyone with a TypeSafe key sees zero behaviour change.

Jev is TypeSafe's model; OpenJEV is a community gateway to it. OpenJEV never replaces or hides TypeSafe.

## What was added

- `TypeSafe.Client/OpenJev.cs` — `OpenJev.Resolve(...)` picks the endpoint/model/key per the selection rule; `OpenJevOptions` binds an `OpenJev` config section. The OpenJEV base address is derived from the configured TypeSafe base address by swapping the host (`typesafe.ai` -> `openjev.sh`), so no second endpoint is hard-coded.
- `TypeSafe.Client/TypeSafeClientOptions.cs` — added a `Provider` option (`"typesafe"` default or `"openjev"`) and an `OpenJev` model constant (`"openjev"`). The TypeSafe default `BaseAddress`/`Model` are unchanged.
- `TypeSafe.Client/TypeSafeClient.cs` — HTTP 503 is now retried alongside 429/529 (OpenJEV signals overload with 503).
- `TypeSafe.Client/TypeSafeApiException.cs` — `IsOverloaded` now also covers 503.
- `MCP/JevMcpRuntime.cs` — calls `OpenJev.Resolve` after binding config, so the MCP server honours the selection rule.
- `MCP/JevMcpTools.cs` — `health` and error messages mention the OpenJEV option.
- `MCP/appsettings.json` — documents an `OpenJev` section (all fields optional).
- `README.md`, `TypeSafe.Client/README.md`, `MCP/README.md` — short notes crediting TypeSafe first.

## Provider selection rule

1. An explicit provider wins — `JEV_PROVIDER=openjev` env var, or `OpenJev:Provider` / `TypeSafe:Provider` config.
2. Otherwise, **TypeSafe** when its key is set (the unchanged default).
3. Otherwise, **OpenJEV** when `OPENJEV_API_KEY` (env) or `OpenJev:ApiKey` (config) is set.

## How to configure (MCP server)

Set the OpenJEV key as an environment variable (the endpoint and model switch automatically):

```
export OPENJEV_API_KEY=oj-...
```

Or force OpenJEV even when a TypeSafe key is present:

```
export JEV_PROVIDER=openjev
export OPENJEV_API_KEY=oj-...
```

Or add an `OpenJev` section to `MCP/appsettings.json`:

```json
{ "OpenJev": { "Provider": "openjev", "ApiKey": "oj-..." } }
```

## How to configure (library)

Call `OpenJev.Resolve` on your `TypeSafeClientOptions`:

```csharp
var openjev = OpenJev.Resolve(options, new OpenJevOptions
{
    ApiKey = Environment.GetEnvironmentVariable("OPENJEV_API_KEY"),
});
```

## How it was verified

A live `POST` to the OpenJEV `systemone` endpoint (model `openjev`, state `ping`, one noul question) returned HTTP 200. The project's own build/tests were not run — third-party code is never executed by the porting process.

## Upstream

Original project: https://github.com/JawzoD3TH/JevApi by @JawzoD3TH.

# Route URL escaping compatibility

## Status

Follow-up required. `SYSLIB0013` is temporarily suppressed for the Runtime
project to preserve imported `System.Web.Routing` URL-generation behavior.

## Compatibility risk

`ParsedRoute.UrlEncode` uses `Uri.EscapeUriString`, then explicitly escapes a
selected set of reserved characters. The API is obsolete because it can
corrupt some URI strings. Replacing it directly with `Uri.EscapeDataString`
also changes observable routing behavior:

- `/` becomes `%2F`, breaking catch-all path generation unless restored;
- already escaped values such as `%2F` become `%252F`;
- additional URI syntax is treated as data and escaped;
- malformed and Unicode inputs can produce different output or exceptions.

Exact warning site: `Routing/ParsedRoute.cs:668`. The method is reached by
outbound generation of route literals, defaults, parameters, and catch-all
values.

## Required design

Compare .NET Framework and the portable candidate across:

- literal and parameter reserved characters;
- catch-all values containing path separators;
- existing percent escapes and literal percent characters;
- Unicode, invalid surrogate, and long inputs;
- application-relative and nested route patterns;
- generated URL round-tripping through inbound route matching.

Candidate direction: encode each value as data, then preserve only actual `/`
characters required by catch-all path semantics. Do not adopt a direct
`EscapeDataString` replacement without compatibility evidence.

## Completion criteria

- Define expected outbound encoding for every input category above.
- Add differential URL-generation coverage against .NET Framework.
- Replace the obsolete encoder without breaking catch-all routing, or retain a
  narrowly justified compatibility implementation.
- Remove the temporary `SYSLIB0013` suppression.

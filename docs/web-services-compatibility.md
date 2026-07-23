# Web Services compatibility

## Supported

`Rehost.WebForms.WebServices` supplies only the configuration surface currently
required by System.Web:

- `WebServicesSection`;
- `ProtocolElement` and `ProtocolElementCollection`;
- `WebServiceProtocols`;
- configuration deserialization and enabled-protocol aggregation.

This is not an ASMX/SOAP compatibility claim.

## Unsupported

- `.wsdl` build-provider proxy generation;
- `Application_WebReferences` discovery/proxy generation;
- general ASMX hosting, SOAP serialization, discovery, clients, and protocols;
- classic implicit protocol defaults.

An empty section currently enables no protocols. Unsupported build providers
throw actionable `PlatformNotSupportedException` and direct users to
pre-generated, separately compiled clients.

The full Framework implementation depends on unavailable XML serializer
importer/exporter internals and a binary cycle between System.Web and
System.Web.Services. Expanding support requires an explicit compatibility
profile and assembly-boundary design.

Implementation:
`src/Rehost.WebForms.WebServices`.
Tests:
`tests/Rehost.WebForms.WebServices.Tests/WebServicesSectionTests.cs`.
Remaining scope:
[Web Services](follow-ups/web-services.md).

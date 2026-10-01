# ResX compatibility

Add focused coverage for legacy BinaryFormatter/Soap payloads and drawing
values. Decide which platform-limited values are supported and ensure
untrusted `.resx` input is rejected or isolated by contract.

Done when payload trust boundaries, platform behavior, and deviations from the
`dotnet/winforms` `195f89a` baseline are tested and documented.

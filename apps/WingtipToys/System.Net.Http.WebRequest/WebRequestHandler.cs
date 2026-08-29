using System.Net.Security;

namespace System.Net.Http;

// Katana's Google middleware constructs this Framework facade type unconditionally,
// so the whole OWIN pipeline fails to build without an assembly of this identity.
public class WebRequestHandler : HttpClientHandler
{
    public RemoteCertificateValidationCallback ServerCertificateValidationCallback
    {
        get => _serverCertificateValidationCallback;
        set
        {
            _serverCertificateValidationCallback = value;
            ServerCertificateCustomValidationCallback = value == null
                ? null
                : (request, certificate, chain, errors) => value(request, certificate, chain, errors);
        }
    }

    private RemoteCertificateValidationCallback _serverCertificateValidationCallback;
}

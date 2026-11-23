using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Utilities.Encoders;
using SSMP.Logging;

namespace SSMP.Networking.Client;

/// <summary>
/// Client-side TLS client implementation that handles reporting supported cipher suites, provides client
/// authentication and checks server certificate.
/// </summary>
/// <param name="crypto">TlsCrypto instance for handling low-level cryptography.</param>
internal class ClientTlsClient(TlsCrypto crypto) : AbstractTlsClient(crypto) {
    /// <summary>
    /// List of supported cipher suites on the client-side.
    /// </summary>
    private static readonly int[] SupportedCipherSuites = [
        CipherSuite.TLS_AES_128_GCM_SHA256,
        CipherSuite.TLS_AES_256_GCM_SHA384,
        CipherSuite.TLS_CHACHA20_POLY1305_SHA256,
        CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384,
        CipherSuite.TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256
    ];

    /// <summary>
    /// (coop, optimization fix G) The same suites with ChaCha20-Poly1305 ahead of AES-GCM for DTLS 1.2, the only
    /// version offered. BouncyCastle's managed AES-GCM allocated about 2.3 KB for each record sent and 3.6 KB for each
    /// received, ChaCha20-Poly1305 about 0.8 and 2.0 (a .NET harness with this client and the server's TLS class,
    /// 64-byte records); the host sends and receives about 360 records a second. The server takes the client's order.
    /// </summary>
    private static readonly int[] ChaChaFirst = [
        CipherSuite.TLS_AES_128_GCM_SHA256,
        CipherSuite.TLS_AES_256_GCM_SHA384,
        CipherSuite.TLS_CHACHA20_POLY1305_SHA256,
        CipherSuite.TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256,
        CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384
    ];

    /// <summary>(coop) Not inlined: skipped, the client offers AES-GCM first, as before fix G.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool PrefersChaCha() => true;

    /// <inheritdoc />
    protected override ProtocolVersion[] GetSupportedVersions() {
        return ProtocolVersion.DTLSv12.Only();
    }

    /// <summary>
    /// Get the supported cipher suites for this TLS client.
    /// </summary>
    /// <returns>An int array representing the cipher suites.</returns>
    protected override int[] GetSupportedCipherSuites() {
        return PrefersChaCha() ? ChaChaFirst : SupportedCipherSuites;
    }

    /// <inheritdoc />
    public override void NotifySelectedCipherSuite(int selectedCipherSuite) {
        base.NotifySelectedCipherSuite(selectedCipherSuite);
        // (coop) which suite this connection uses, for fix G's probe
        Logger.Info($"DTLS cipher suite 0x{selectedCipherSuite:X4}");
    }

    /// <summary>
    /// The maximum time the handshake can take in milliseconds before timing out.
    /// </summary>
    /// <returns>The integer value of the timeout in milliseconds.</returns>
    public override int GetHandshakeTimeoutMillis() {
        return DtlsClient.DtlsHandshakeTimeoutMillis;
    }

    /// <inheritdoc />
    /// <summary>
    /// Get the authentication implementation for this TLS client that handles providing client credentials and
    /// checking server certificates.
    /// </summary>
    /// <returns>The TlsAuthentication instance for this TLS client.</returns>
    public override TlsAuthentication GetAuthentication() {
        return new TlsAuthenticationImpl();
    }

    /// <summary>
    /// Implementation for TLS authentication that handles providing client credentials and checking server
    /// certificates.
    /// </summary>
    private class TlsAuthenticationImpl : TlsAuthentication {
        /// <summary>
        /// Notify the TLS client of the server certificate that the server has sent. This method checks whether to
        /// trust the server based on this certificate or not. If not, the method will throw an exception which will
        /// subsequently abort the connection.
        /// In the current implementation, we only log the fingerprints of the certificates in the chain from the
        /// server.
        /// </summary>
        /// <inheritdoc />
        /// <param name="serverCertificate">The server certificate instance.</param>
        public void NotifyServerCertificate(TlsServerCertificate serverCertificate) {
            if (serverCertificate.Certificate == null || serverCertificate.Certificate.IsEmpty) {
                throw new TlsFatalAlert(AlertDescription.bad_certificate);
            }

            var chain = serverCertificate.Certificate.GetCertificateList();

            Logger.Info("Server certificate fingerprint(s):");
            foreach (var entry in chain.Select(t => X509CertificateStructure.GetInstance(t.GetEncoded()))) {
                Logger.Info($"  fingerprint:SHA256 {Fingerprint(entry)} ({entry.Subject})");
            }
        }

        /// <summary>
        /// Get the credentials of the client so the server can verify who we are. Currently, we have no way to
        /// provide client-side credentials, so we return null.
        /// </summary>
        /// <inheritdoc />
        public TlsCredentials? GetClientCredentials(CertificateRequest certificateRequest) {
            // TODO: provide means for a client to have certificate and return it in this method
            return null;
        }

        /// <summary>
        /// Return a fingerprint for the given X509 certificate.
        /// </summary>
        /// <param name="c">The 509 certificate to fingerprint.</param>
        /// <returns>The fingerprint as a string.</returns>
        private static string Fingerprint(X509CertificateStructure c) {
            var der = c.GetEncoded();
            var hash = DigestUtilities.CalculateDigest("SHA256", der);
            var hexBytes = Hex.Encode(hash);
            var hex = Encoding.ASCII.GetString(hexBytes).ToUpperInvariant();

            var fp = new StringBuilder();
            var i = 0;
            fp.Append(hex.Substring(i, 2));
            while ((i += 2) < hex.Length) {
                fp.Append(':');
                fp.Append(hex.Substring(i, 2));
            }

            return fp.ToString();
        }
    }
}

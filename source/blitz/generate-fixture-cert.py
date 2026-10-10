"""Create an ephemeral local HTTPS certificate for test-electron.cjs only."""
from datetime import datetime, timedelta, timezone
from pathlib import Path
import sys
from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.x509.oid import NameOID

destination = Path(sys.argv[1]).resolve()
destination.mkdir(parents=True, exist_ok=True)
key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
subject = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, "Patchwork local fixture")])
now = datetime.now(timezone.utc)
certificate = (
    x509.CertificateBuilder()
    .subject_name(subject).issuer_name(subject).public_key(key.public_key())
    .serial_number(x509.random_serial_number())
    .not_valid_before(now - timedelta(minutes=5)).not_valid_after(now + timedelta(days=1))
    .add_extension(x509.SubjectAlternativeName([x509.DNSName(name) for name in
        ("blitzapp.gg", "api.blitz.gg", "dn0qt3r0xannq.cloudfront.net", "live.primis.tech")]), critical=False)
    .sign(key, hashes.SHA256())
)
(destination / "fixture-key.pem").write_bytes(key.private_bytes(
    serialization.Encoding.PEM, serialization.PrivateFormat.PKCS8, serialization.NoEncryption()))
(destination / "fixture-cert.pem").write_bytes(certificate.public_bytes(serialization.Encoding.PEM))
print("Created local test certificate")

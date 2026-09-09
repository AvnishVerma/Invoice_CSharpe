# Password storage

New accounts and password changes use the .NET 8 built-in PBKDF2-HMAC-SHA256 implementation, 600,000 iterations, a 32-byte derived key, and a cryptographically random 16-byte salt encoded as hex. The stored hash format is `pbkdf2-sha256$v1$600000$<hex key>`; the existing Salt column contains the salt text whose UTF-8 bytes feed PBKDF2. No schema change is needed.

The iteration count follows [OWASP's PBKDF2 guidance](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html). PBKDF2 uses the runtime implementation without adding a cryptographic package. This is not a FIPS certification claim. Argon2id and performance on supported release hardware remain security-review considerations.

Verification uses fixed-time byte comparison. Unknown versions, invalid hex, incorrect key lengths, and unsupported iteration counts fail closed before deriving a key. Exact password whitespace and UTF-8 encoding are preserved.

Existing C# accounts containing SHA256(UTF8(salt + password)) authenticate with their current password. Successful verification writes a fresh salt and versioned hash before reporting success. Failed verification does not modify credentials. Hash upgrades do not change role or the mandatory password-change flag. Subsequent modern logins do not rewrite credentials. Restoring an older C# backup permits the same lazy upgrade. Older application binaries cannot verify upgraded hashes; rollback must account for this incompatibility.

The Flutter reference uses HMAC-SHA256 for salted passwords and plain SHA256 for older unsalted passwords. Neither is the existing C# format. Importing Flutter credentials requires an explicit, validated source-format migration; this change does not claim Flutter database compatibility.

Validation includes a PBKDF2 reference vector independently calculated with Python hashlib, migration persistence across model reload, failed-login non-mutation, preserved role/password-change state, malformed formats and password whitespace. One local upgrade measured 272 ms with runtime major roll-forward; this is a smoke measurement, not a release-hardware benchmark. Password derivation remains synchronous, so UI responsiveness, rate limiting, full service authorization, first-run owner setup, and recovery still require work.

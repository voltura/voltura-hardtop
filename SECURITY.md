# Security

Security fixes target the latest stable Voltura HardTop release.

Report vulnerabilities privately through [GitHub private vulnerability reporting](https://github.com/voltura/voltura-hardtop/security/advisories/new). If that is unavailable, email **joakim@voltura.se** with the subject **Voltura HardTop security**. Do not publish exploit details or private diagnostics in an issue before coordination.

Include the affected version, reproduction steps, impact and a minimal example. Do not include credentials, personal window titles or unrelated system data.

HardTop runs unelevated. Updates require a manifest signed by the embedded project key and a matching installer hash, version and architecture. Private update-signing and Authenticode keys must never enter Git, issues or release assets. A self-signed Authenticode signature does not create public publisher trust.

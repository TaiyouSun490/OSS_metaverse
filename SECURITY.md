# Security policy

Please report vulnerabilities privately to the repository maintainers rather than opening a public issue.

This 0.x package is a foundation, not a complete trust boundary. Before public deployment:

- authenticate users and authorize room/content operations on a server;
- rate-limit messages, voice, joins, and content uploads;
- validate all user content in an isolated build pipeline;
- serve catalogs and bundles over HTTPS with integrity and rollback controls;
- keep storage credentials out of clients and ScriptableObjects;
- add moderation audit logs, reporting, bans, and age/privacy controls;
- replace raw PCM voice with an encrypted production voice service or codec/transport.

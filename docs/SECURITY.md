# Credential handling

The PC tool uses authorization code with PKCE and a random state value. It listens only on 127.0.0.1:8000 during authorization, checks state and playback permissions, and exchanges the code directly with Spotify. No client secret, public website, or device web server is used.

The private JSON config, generated configuration source, and firmware contain credentials. Keep them private. Generated Unicode escapes are source escaping, not encryption.

Device settings retain the version-1 AES-CBC plus HMAC envelope, random IVs, separate derived keys, and alternating recovery files. This protects copied settings files, not full firmware extraction. Preserve DeviceSecrets.cs and its storage key across builds.

HTTPS verifies certificates using TLS 1.2. Connection reuse is disabled because it failed on the installed runtime. Uncertain playback commands are not automatically replayed; rate limits pause commands.

Logs contain statuses and timings, never credentials, tokens, response bodies, or authorization URLs.

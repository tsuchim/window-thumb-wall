### WindowThumbWall v0.2

#### Downloads

| Format | File | Notes |
|--------|------|-------|
| **ZIP** | `WindowThumbWall-v0.2-win-x64.zip` | Portable — extract and run |
| **MSI** | `WindowThumbWall-v0.2-win-x64.msi` | Traditional installer (Program Files + Start Menu) |
| **MSIX** | `WindowThumbWall-v0.2-win-x64.msix` | Modern package (may require sideloading / trusted cert) |

All packages are **self-contained x64** — no .NET runtime install needed.

#### Code signing / security note

These binaries are **not signed with a public (commercial) code signing certificate**.

- **ZIP / MSI**: typically **unsigned**.
- **MSIX**: may be **unsigned** or signed with a **self-signed/test certificate**.

Windows SmartScreen / Defender may show warnings depending on your environment.

#### Docs

- English: https://github.com/tsuchim/window-thumb-wall#readme
- 日本語: https://github.com/tsuchim/window-thumb-wall/blob/main/README.ja.md

#### Changes

- Version bump to 0.2
- Added MSI installer (WiX)
- Added portable ZIP distribution
- MSIX signing is now optional (graceful fallback)

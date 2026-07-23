# Changes

- Made the MSI installation smoke test reliable on GitHub-hosted Windows runners that do not provide an interactive main window.
- The test still proves installation, installed-file version, successful startup, and silent uninstall; it now cleans up only its own smoke-test process when a normal window-close request is unavailable.
- Kept ZIP, MSI, and MSIX release validation intact.

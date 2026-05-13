# Privacy Policy

WindowThumbWall does not transmit personal data to external servers and does not centrally collect user data.

## Data Collection

This application runs entirely on the local computer. To restore the previous layout, it records information about assigned windows, including process names and window titles. Depending on the content of those window titles, this local state may incidentally include personal or sensitive information.

## Network Communication

WindowThumbWall does not communicate with external servers and does not send any data over the internet.

## Local Data

Window information, window geometry, and fullscreen state are stored locally on the user's device in a state file under the user's profile directory:

`%LocalAppData%\\WindowThumbWall\\state.json`

This data is used only by the application to restore its state and is not transmitted outside the user's device.

## Deleting Local Data

To remove the stored state, close WindowThumbWall and delete the file above. The next time the application starts, it recreates the file as needed without the previous entries.

Uninstall behavior by package type:

- MSI: uninstall removes this local data folder for the user who runs the uninstall.
- MSIX (Store package): uninstall removes the packaged app data container.
- ZIP (portable): no uninstaller, so remove the file/folder manually if needed.

## Third-Party Services

This application does not use third-party analytics, tracking services, or advertising networks.

## Changes

If this policy changes in the future, updates will be published in this file.

## Contact

For questions about this policy, please contact the developer through the project's GitHub repository.

Project page:
https://github.com/tsuchim/window-thumb-wall

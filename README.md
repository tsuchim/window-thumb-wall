# WindowThumbWall

WPF (.NET 10) app that shows live DWM thumbnails of selected external windows in a 2x2 grid.

## How to run

- Open `WindowThumbWall.csproj` in Visual Studio 2026
- Start (F5)
- Use the left window list to assign windows to the grid (double-click)

## Notes

- Uses DWM Thumbnail API (`DwmRegisterThumbnail` / `DwmUpdateThumbnailProperties`)
- Each cell hosts a real HWND via `HwndHost`

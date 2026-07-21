# Use native presentation shells per operating system

Token HUD keeps WPF for Windows and plans SwiftUI for macOS rather than moving to a shared cross-platform visual framework. Platforms share terminology, contracts, schemas, and behavior fixtures, accepting duplicated presentation work in exchange for native window, tray or menu-bar, accessibility, and interaction behavior; the executable shared-core bridge is deferred until macOS work begins.

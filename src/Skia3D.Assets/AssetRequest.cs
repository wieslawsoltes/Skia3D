using System;

namespace Skia3D.Assets;

public sealed record AssetRequest(
    AssetId Id,
    Type AssetType,
    string? Source,
    object? Options,
    AssetManager Manager,
    AssetRegistry Registry,
    AssetCache Cache);

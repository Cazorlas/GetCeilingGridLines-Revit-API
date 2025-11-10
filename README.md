# GetCeilingGridLines (Revit API) — Reconstruct Ceiling Grids w/ Fallback

> Robust helper to extract ceiling grid lines on both **Revit 2025.3+ (.NET Core)** and **older .NET Framework** versions.

## ✨ What this does

- **Revit 2025.3+**: calls the native API `Ceiling.GetCeilingGridLines(includeBoundary)` and returns grid lines (optionally includes boundary). :contentReference[oaicite:0]{index=0}  
- **Older Revit/.NET Framework**: infers grid spacing & directions from stable references, generates long lines, and **clips them by the ceiling solid** to get final segments — optional boundary included. :contentReference[oaicite:1]{index=1}

## 🧠 How it works (fallback path)

1. Wrap the `Ceiling` to fetch geometry:
   - bottom planar face + solid + bottom boundary + stable reference string. :contentReference[oaicite:2]{index=2}
2. From the face’s **stable reference**, fetch **U** pair `{1,5}` and **V** pair `{2,6}` to anchor grid measurement. :contentReference[oaicite:3]{index=3}
3. Place a **temporary Dimension** across each pair to let Revit compute:
   - `Offset` (grid spacing), `DimDirection` (measure dir), and derive `GridDirection` (rotate 90° in face plane). :contentReference[oaicite:4]{index=4}
4. Generate many long, parallel lines in U/V using:
   - `Origin`, `Offset`, `DimDirection`, `GridDirection`, `Length` packed in `GridData`. :contentReference[oaicite:5]{index=5}
5. Intersect each line with the **ceiling solid** ⇒ collect clipped segments; optionally append bottom boundary curves. :contentReference[oaicite:6]{index=6}

> `GridData`: `{ Origin, Offset, DimDirection, GridDirection, Length }`. :contentReference[oaicite:7]{index=7}

## ✅ Compatibility

| Revit / Runtime | Behavior |
|---|---|
| 2025.3+ (.NET Core) | Uses native `Ceiling.GetCeilingGridLines(bool)` |
| Older (.NET Framework) | Geometric inference + solid intersection |

## 🚀 Quick start

### A) Call the utility directly
```csharp
// Returns ceiling grid segments as Curve objects; include boundary if needed
var curves = CeilingUtility.GetCeilingGridLines(ceiling, includeBoundary: true);

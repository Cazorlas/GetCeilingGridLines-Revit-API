# GetCeilingGridLines (Revit API) — Reconstruct Ceiling Grids w/ Fallback

> Robust helper to extract ceiling grid lines on both **Revit 2025.3+ (.NET Core)** and **older .NET Framework** versions.

## ✨ What this does

- **Revit 2025.3+**: calls the native API `Ceiling.GetCeilingGridLines(includeBoundary)` and returns grid lines (optionally includes boundary).  
- **Older Revit/.NET Framework**: infers grid spacing & directions from stable references, generates long lines, and **clips them by the ceiling solid** to get final segments — optional boundary included.

---

## 🎬 Demo video

[![Watch the demo](https://img.youtube.com/vi/m8xgFGUbxMM/0.jpg)](https://youtu.be/m8xgFGUbxMM)

---

## 🧠 How it works (fallback path)

1. Wrap the `Ceiling` to fetch geometry:
   - bottom planar face + solid + bottom boundary + stable reference string.
2. From the face’s **stable reference**, fetch **U** pair `{1,5}` and **V** pair `{2,6}` to anchor grid measurement.
3. Place a **temporary Dimension** across each pair to let Revit compute:
   - `Offset` (grid spacing), `DimDirection` (measure dir), and derive `GridDirection` (rotate 90° in face plane).
4. Generate many long, parallel lines in U/V using:
   - `Origin`, `Offset`, `DimDirection`, `GridDirection`, `Length` packed in `GridData`.
5. Intersect each line with the **ceiling solid** ⇒ collect clipped segments; optionally append bottom boundary curves.

> `GridData`: `{ Origin, Offset, DimDirection, GridDirection, Length }`

---

## 🧩 Inside `CeilingUtility` — Key methods

### 🔹 `GetCeilingGridLines(Ceiling ceiling, bool includeBoundary)`
Main entry point:
- Uses native API on Revit 2025.3+ (.NET Core)
- Uses geometric inference fallback on older versions:
  - Gets geometry from `CeilingData`
  - Extracts U/V refs
  - Calls `GetGridDataFromReferences()` to measure grid spacing
  - Generates infinite lines with `GenerateLines()`
  - Clips them using `Solid.IntersectWithCurve()`

### 🔹 `GetGridReferences(string stableRef, int[] indices)`
Builds a `ReferenceArray` from the ceiling face’s stable reference and the given sub-indices (U → `{1,5}`, V → `{2,6}`).

### 🔹 `GetGridDataFromReferences(ReferenceArray refs)`
Creates a temporary **Dimension** to let Revit compute the spacing:
- Reads `Offset` (spacing), `DimDirection` (measure dir)
- Rotates 90° to get `GridDirection`
- Nudges the dimension by 0.1ft and regenerates for accurate values
- Cleans up the dimension after reading

### 🔹 `GenerateLines(GridData data, double zLevel, double lineLength, int lineCount)`
Generates `2 * lineCount` long, parallel lines spaced by `(i + 0.5) * Offset` on both sides of the origin.  
These are later intersected with the ceiling’s solid.

---

## ✅ Compatibility

| Revit / Runtime | Behavior |
|---|---|
| 2025.3+ (.NET Core) | Uses native `Ceiling.GetCeilingGridLines(bool)` |
| Older (.NET Framework) | Geometric inference + solid intersection |

---

## 🚀 Quick start

### A) Call the utility directly
```csharp
// Returns ceiling grid segments as Curve objects; include boundary if needed
var curves = CeilingUtility.GetCeilingGridLines(ceiling, includeBoundary: true);
